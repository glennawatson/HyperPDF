// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Layers;

/// <summary>
/// A document's layers and which are shown. Changing visibility only changes how pages are drawn; the file is not
/// touched. A lock keeps visibility expressions consistent while radio groups change several bits, and
/// <see cref="Version"/>, raised after the change, tells renderers when to redraw.
/// </summary>
[DebuggerDisplay("PdfOptionalContent: {Layers.Count} layers")]
public sealed class PdfOptionalContent
{
    /// <summary>The deepest visibility expression evaluated.</summary>
    private const int MaxExpressionDepth = 16;

    /// <summary>The objects references resolve against.</summary>
    private readonly PdfObjectStore _objects;

    /// <summary>The objects the layers were read from, which tell whether an edit changed them.</summary>
    private readonly object?[] _witness;

    /// <summary>Guards writers of <see cref="_set"/> and of its current visibility.</summary>
    private readonly Lock _gate = new();

    /// <summary>The document's reusable configuration reader, used under the visibility lock.</summary>
    private OptionalContentReader? _reader;

    /// <summary>The layers and their state under the configuration in use.</summary>
    private OptionalContentSet _set;

    /// <summary>The layers with their visibility as of <see cref="_snapshotVersion"/>.</summary>
    private PdfLayer[] _snapshot;

    /// <summary>The version <see cref="_snapshot"/> was made at.</summary>
    private int _snapshotVersion;

    /// <summary>The visibility version.</summary>
    private int _version;

    /// <summary>Initializes a new instance of the <see cref="PdfOptionalContent"/> class.</summary>
    /// <param name="objects">The document's objects.</param>
    public PdfOptionalContent(PdfObjectStore objects)
    {
        ArgumentNullException.ThrowIfNull(objects);
        _objects = objects;

        // Taken before the read, so an edit between the two makes the layers look changed, never unchanged.
        _witness = OptionalContentReader.Witness(objects);
        _set = _witness.Length == 0 ? OptionalContentSet.Empty : (_reader = new()).Read(objects);
        _snapshot = _set.Layers;
    }

    /// <summary>Gets a value indicating whether the document has layers.</summary>
    public bool HasLayers => Volatile.Read(ref _set).Layers.Length > 0;

    /// <summary>Gets a number that changes whenever a layer is shown or hidden, or the configuration changes.</summary>
    public int Version => Volatile.Read(ref _version);

    /// <summary>Gets the layers in document order, with their current visibility.</summary>
    public IReadOnlyList<PdfLayer> Layers
    {
        get
        {
            var version = Volatile.Read(ref _version);
            if (Volatile.Read(ref _snapshotVersion) == version)
            {
                return Volatile.Read(ref _snapshot);
            }

            lock (_gate)
            {
                version = Volatile.Read(ref _version);
                if (_snapshotVersion == version)
                {
                    return _snapshot;
                }

                // Visibility can change several bits together, so the snapshot shares the writers' lock.
                var set = _set;
                var layers = new PdfLayer[set.Layers.Length];
                for (var i = 0; i < layers.Length; i++)
                {
                    layers[i] = set.Layers[i] with
                    {
                        IsVisible = !OptionalContentSet.Test(set.Current, set.IndexOf(set.Layers[i].Id))
                    };
                }

                Volatile.Write(ref _snapshot, layers);
                Volatile.Write(ref _snapshotVersion, version);
                return layers;
            }
        }
    }

    /// <summary>
    /// Gets the layer order as a tree for a layer panel (ISO 32000-2, 8.11.4.3 /Order). A configuration without /Order lists
    /// every layer in turn. Layers the order leaves out are not presented, as the standard says.
    /// </summary>
    public IReadOnlyList<PdfLayerNode> Tree => Volatile.Read(ref _set).Tree;

    /// <summary>Gets which layers of <see cref="Tree"/> a panel lists (/ListMode).</summary>
    public PdfLayerListMode ListMode => Volatile.Read(ref _set).ListMode;

    /// <summary>Gets the default configuration and the alternate configurations, with the one in use marked.</summary>
    public IReadOnlyList<PdfLayerConfiguration> Configurations => Volatile.Read(ref _set).Configurations;

    /// <summary>Gets the <see cref="PdfLayerConfiguration.Index"/> of the configuration in use.</summary>
    public int ActiveConfiguration => Volatile.Read(ref _set).ActiveConfiguration;

    /// <summary>
    /// Shows or hides a layer. Showing a layer hides the other layers of its radio button groups, except locked ones.
    /// A locked layer cannot be changed this way.
    /// </summary>
    /// <param name="id">The layer's object number.</param>
    /// <param name="visible">Whether to show it.</param>
    /// <returns><see langword="true"/> when the document has the layer and it is not locked.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetVisible(int id, bool visible) => SetVisible(id, visible, false);

    /// <summary>Determines whether the configuration in use locks a layer, so a user interface must not change it.</summary>
    /// <param name="id">The layer's object number.</param>
    /// <returns><see langword="true"/> when locked.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsLocked(int id) => Volatile.Read(ref _set).IsLocked(id);

    /// <summary>Makes another configuration the one in use, which resets every layer to that configuration's state.</summary>
    /// <param name="index">The <see cref="PdfLayerConfiguration.Index"/>: a position in /Configs, or <see cref="PdfLayerConfiguration.DefaultIndex"/>.</param>
    /// <returns><see langword="false"/> when there is no such configuration or its /Intent does not include View.</returns>
    public bool SelectConfiguration(int index)
    {
        var configurations = Volatile.Read(ref _set).Configurations;
        var slot = index - PdfLayerConfiguration.DefaultIndex;
        if ((uint)slot >= (uint)configurations.Length || !configurations[slot].AppliesToView)
        {
            return false;
        }

        lock (_gate)
        {
            var set = (_reader ??= new()).Read(_objects, index);
            Volatile.Write(ref _set, set);
            _ = Interlocked.Increment(ref _version);
        }

        return true;
    }

    /// <summary>Undoes every change made with <see cref="SetVisible(int, bool)"/>, returning to the configuration's own states.</summary>
    /// <returns><see langword="true"/> when something changed.</returns>
    public bool ResetToDefault()
    {
        lock (_gate)
        {
            var set = Volatile.Read(ref _set);
            var changed = false;
            for (var i = 0; i < set.Hidden.Length; i++)
            {
                if (Volatile.Read(ref set.Current[i]) == set.Hidden[i])
                {
                    continue;
                }

                Volatile.Write(ref set.Current[i], set.Hidden[i]);
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            _ = Interlocked.Increment(ref _version);
            return true;
        }
    }

    /// <summary>
    /// Gets the object numbers of the layers a page refers to, for a layer panel whose <see cref="ListMode"/> is
    /// <see cref="PdfLayerListMode.VisiblePages"/>. The page's resources and annotations are read; its content is not.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>The layers' object numbers.</returns>
    public IReadOnlyCollection<int> GetLayerIdsOnPage(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return OptionalContentPages.Collect(_objects, page);
    }

    /// <summary>Gets the zoom class of a magnification, for <see cref="IsVisibleAt(PdfValue, double)"/> and for caching pictures.</summary>
    /// <param name="magnification">The magnification, where 1 is 100 percent.</param>
    /// <returns>A number that changes exactly when a zoom limit is crossed; 0 when no layer has a zoom limit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetZoomClass(double magnification) => OptionalContentZoom.ClassOf(Volatile.Read(ref _set).ZoomLimits, magnification);

    /// <summary>Determines whether content marked with an optional content group or membership dictionary is shown, ignoring zoom limits.</summary>
    /// <param name="value">The /OC value: a reference to an OCG or OCMD.</param>
    /// <returns><see langword="true"/> when the content is shown.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsVisible(PdfValue value) => IsVisible(value, OptionalContentZoom.NoZoom);

    /// <summary>
    /// Determines whether content marked with an optional content group or membership dictionary is shown at a
    /// magnification. A group an /AS usage application dictionary manages by zoom is shown from its /Zoom /min up to,
    /// not including, its /max (ISO 32000-2, 8.11.4.4, Table 100).
    /// </summary>
    /// <param name="value">The /OC value: a reference to an OCG or OCMD.</param>
    /// <param name="magnification">The magnification, where 1 is 100 percent.</param>
    /// <returns><see langword="true"/> when the content is shown.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsVisibleAt(PdfValue value, double magnification) => IsVisible(value, GetZoomClass(magnification));

    /// <summary>
    /// Determines whether content marked with an optional content group or membership dictionary prints, using each
    /// group's print usage (/Usage /Print /PrintState) and the configuration's Print event (ISO 32000-2, 8.11.4.4).
    /// </summary>
    /// <param name="value">The /OC value: a reference to an OCG or OCMD.</param>
    /// <returns><see langword="true"/> when the content prints.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPrinted(PdfValue value) => IsShownFor(value, PdfOptionalContentEvent.Print);

    /// <summary>
    /// Determines whether content marked with an optional content group or membership dictionary is kept when the
    /// document is saved to a format that has no optional content, using each group's export usage
    /// (/Usage /Export /ExportState) and the configuration's Export event.
    /// </summary>
    /// <param name="value">The /OC value: a reference to an OCG or OCMD.</param>
    /// <returns><see langword="true"/> when the content is exported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsExported(PdfValue value) => IsShownFor(value, PdfOptionalContentEvent.Export);

    /// <summary>Determines whether content is shown for an event, using the configuration's states and not the viewer's switches.</summary>
    /// <param name="value">The /OC value: a reference to an OCG or OCMD.</param>
    /// <param name="ev">The event.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    public bool IsShownFor(PdfValue value, PdfOptionalContentEvent ev)
    {
        if (ev == PdfOptionalContentEvent.View)
        {
            return IsVisible(value);
        }

        var set = Volatile.Read(ref _set);
        return set.Layers.Length == 0 || Evaluate(value, new(ev == PdfOptionalContentEvent.Print ? set.PrintHidden : set.ExportHidden, set, OptionalContentZoom.NoZoom), 0);
    }

    /// <summary>
    /// Determines whether a dictionary is a membership dictionary. A dictionary without a /Type is a group
    /// unless it carries /OCGs or /VE, so a damaged file that omits /Type still reads.
    /// </summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns><see langword="true"/> for an OCMD.</returns>
    internal static bool IsMembership(PdfDictionary dictionary)
    {
        if (dictionary.IsName(KnownName.Type, KnownName.OCG))
        {
            return false;
        }

        return dictionary.IsName(KnownName.Type, KnownName.OCMD) || dictionary.ContainsKey(KnownName.OCGs) || dictionary.ContainsKey(KnownName.VE);
    }

    /// <summary>Determines whether content is shown by the viewer's switches in a zoom class.</summary>
    /// <param name="value">The /OC value: a reference to an OCG or OCMD.</param>
    /// <param name="zoomClass">The zoom class from <see cref="GetZoomClass(double)"/>, or -1 to ignore zoom limits.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    internal bool IsVisible(PdfValue value, int zoomClass)
    {
        var set = Volatile.Read(ref _set);
        if (set.Layers.Length == 0)
        {
            return true;
        }

        // A radio group changes several bits together; an expression must see the whole change.
        lock (_gate)
        {
            set = _set;
            return Evaluate(value, new(set.Current, set, zoomClass), 0);
        }
    }

    /// <summary>Shows or hides a layer on behalf of an action, which may change a locked layer.</summary>
    /// <param name="id">The layer's object number.</param>
    /// <param name="visible">Whether to show it.</param>
    /// <returns><see langword="true"/> when the document has the layer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool SetVisibleByAction(int id, bool visible) => SetVisible(id, visible, true);

    /// <summary>
    /// Determines whether the objects the layers were read from are still the document's, so an edit elsewhere in the file
    /// leaves the layers, and which are shown, as they are.
    /// </summary>
    /// <returns><see langword="true"/> when no layer object changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsCurrent() => OptionalContentReader.IsWitnessCurrent(_objects, _witness);

    /// <summary>Tells renderers to redraw every page, after an edit changed what a page paints.</summary>
    internal void InvalidatePages() => _ = Interlocked.Increment(ref _version);

    /// <summary>Sets the first version, before the layer set is shared, so it starts above an earlier set's versions.</summary>
    /// <param name="version">The first version.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StartAt(int version)
    {
        Volatile.Write(ref _version, version);
        Volatile.Write(ref _snapshotVersion, version);
    }

    /// <summary>Determines whether a layer is within its zoom range.</summary>
    /// <param name="state">The state being evaluated.</param>
    /// <param name="bit">The layer's bit.</param>
    /// <returns><see langword="true"/> when no zoom limit hides the layer.</returns>
    private static bool InZoom(in Evaluation state, int bit) =>
        state.ZoomClass < 0 || state.Set.ZoomRanges.Length == 0 || state.Set.ZoomRanges[bit].Contains(state.ZoomClass);

    /// <summary>Determines whether one group is shown.</summary>
    /// <param name="group">The group reference.</param>
    /// <param name="state">The state being evaluated.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    private static bool IsGroupShown(PdfValue group, in Evaluation state)
    {
        var id = group.AsReference();
        if (!id.IsValid)
        {
            return true;
        }

        // A group the document does not list as a layer is never hidden, not for viewing and not limited by zoom.
        var bit = state.Set.IndexOf(id.Number);
        return bit < 0 || OptionalContentSet.Test(state.Set.NonView, bit) || (!OptionalContentSet.Test(state.Hidden, bit) && InZoom(state, bit));
    }

    /// <summary>Hides the other groups of every radio button set that holds a group.</summary>
    /// <param name="set">The layers.</param>
    /// <param name="id">The group just shown.</param>
    /// <param name="overrideLocks">Whether locked siblings may be hidden.</param>
    /// <returns><see langword="true"/> when a group was newly hidden.</returns>
    private static bool HideSiblings(OptionalContentSet set, int id, bool overrideLocks)
    {
        var changed = false;
        foreach (var group in set.RadioGroups)
        {
            if (Array.IndexOf(group, id) < 0)
            {
                continue;
            }

            foreach (var sibling in group)
            {
                var bit = set.IndexOf(sibling);
                changed |= sibling != id && bit >= 0 && (overrideLocks || !set.IsLocked(sibling)) && OptionalContentSet.Raise(set.Current, bit);
            }
        }

        return changed;
    }

    /// <summary>Shows or hides a layer, optionally ignoring locks.</summary>
    /// <param name="id">The layer's object number.</param>
    /// <param name="visible">Whether to show it.</param>
    /// <param name="overrideLocks">Whether a locked layer may change.</param>
    /// <returns><see langword="true"/> when the document has the layer and it may change.</returns>
    private bool SetVisible(int id, bool visible, bool overrideLocks)
    {
        var set = Volatile.Read(ref _set);
        if (set.IndexOf(id) < 0 || (!overrideLocks && set.IsLocked(id)))
        {
            return false;
        }

        lock (_gate)
        {
            // A configuration change replaces the set under this lock, so the set read here is the one that stays current.
            set = Volatile.Read(ref _set);
            var bit = set.IndexOf(id);
            if (bit < 0 || (!overrideLocks && set.IsLocked(id)))
            {
                return false;
            }

            var changed = visible ? OptionalContentSet.Lower(set.Current, bit) : OptionalContentSet.Raise(set.Current, bit);
            if (visible)
            {
                changed |= HideSiblings(set, id, overrideLocks);
            }

            if (changed)
            {
                _ = Interlocked.Increment(ref _version);
            }
        }

        return true;
    }

    /// <summary>Evaluates an OCG reference, an OCMD or a visibility expression.</summary>
    /// <param name="value">The value.</param>
    /// <param name="state">The state being evaluated.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    private bool Evaluate(PdfValue value, in Evaluation state, int depth)
    {
        if (depth > MaxExpressionDepth)
        {
            return true;
        }

        var dictionary = StoreReading.Resolve(_objects, value).AsDictionary();
        if (dictionary is null)
        {
            return true;
        }

        if (!IsMembership(dictionary))
        {
            return IsGroupShown(value, state);
        }

        var expression = dictionary.GetArray(KnownName.VE);
        return expression is not null ? EvaluateExpression(expression, state, depth + 1) : EvaluatePolicy(dictionary, state);
    }

    /// <summary>Applies an OCMD's /P policy to its /OCGs, counting only the groups that resolve.</summary>
    /// <param name="membership">The OCMD.</param>
    /// <param name="state">The state being evaluated.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    private bool EvaluatePolicy(PdfDictionary membership, in Evaluation state)
    {
        var groups = membership.GetRaw(KnownName.OCGs);
        var resolved = StoreReading.Resolve(_objects, groups);
        if (resolved.AsDictionary() is not null)
        {
            return IsGroupShown(groups, state);
        }

        if (resolved.AsArray() is not { } array)
        {
            return true;
        }

        var count = 0;
        var shown = 0;
        for (var i = 0; i < array.Count; i++)
        {
            var item = array.GetRaw(i);
            if (StoreReading.Resolve(_objects, item).AsDictionary() is null)
            {
                continue;
            }

            count++;
            shown += IsGroupShown(item, state) ? 1 : 0;
        }

        return count == 0 || membership.GetName(KnownName.P).ToKnownName() switch
        {
            KnownName.AllOn => shown == count,
            KnownName.AnyOff => shown < count,
            KnownName.AllOff => shown == 0,
            _ => shown > 0,
        };
    }

    /// <summary>Evaluates a /VE visibility expression: [/And ...], [/Or ...] or [/Not x].</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="state">The state being evaluated.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    private bool EvaluateExpression(PdfArray expression, in Evaluation state, int depth)
    {
        var op = expression.GetName(0).ToKnownName();
        if (op == KnownName.Not)
        {
            return !EvaluateOperand(expression.GetRaw(1), state, depth);
        }

        var isAnd = op == KnownName.And;
        for (var i = 1; i < expression.Count; i++)
        {
            var result = EvaluateOperand(expression.GetRaw(i), state, depth);
            if (result != isAnd)
            {
                return result;
            }
        }

        return isAnd;
    }

    /// <summary>Evaluates one operand of an expression: a group or a nested expression.</summary>
    /// <param name="operand">The operand.</param>
    /// <param name="state">The state being evaluated.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    private bool EvaluateOperand(PdfValue operand, in Evaluation state, int depth) =>
        StoreReading.Resolve(_objects, operand).AsArray() is { } nested && depth <= MaxExpressionDepth
            ? EvaluateExpression(nested, state, depth + 1)
            : Evaluate(operand, state, depth);

    /// <summary>What a visibility check reads: the hidden groups of the event, the layer set, and the zoom class.</summary>
    /// <param name="Hidden">The hidden groups.</param>
    /// <param name="Set">The layer set.</param>
    /// <param name="ZoomClass">The zoom class, or -1 to ignore zoom limits.</param>
    private readonly record struct Evaluation(int[] Hidden, OptionalContentSet Set, int ZoomClass);
}
