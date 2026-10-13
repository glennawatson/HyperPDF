// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Carries the form fields behind copied widgets. Each widget keeps its <c>/Parent</c> chain: the fields above it are
/// copied with only the copied widgets and fields as kids, and the root fields join the target's <c>/AcroForm /Fields</c>.
/// A root field whose name the target uses is renamed, and the form's <c>/DR</c>, <c>/DA</c>, <c>/Q</c> and
/// <c>/NeedAppearances</c> are merged without changing the target's fields. The signature flags and calculation order
/// follow the carried fields, and the target's XFA data is removed when the carried fields make it stale.
/// </summary>
[DebuggerDisplay("PdfCarryForms: {_nodes.Count} fields")]
internal sealed class PdfCarryForms
{
    /// <summary>The signature flags that are defined: signatures exist (bit 1) and append only (bit 2).</summary>
    private const int SignatureFlagsMask = 3;

    /// <summary>What the report says when the target's XFA data was removed.</summary>
    private const string XfaRemovedMessage = "The document's XFA form data was removed because the copied pages added form fields it does not describe. The standard form fields are kept.";

    /// <summary>The context.</summary>
    private readonly PdfCarryContext _context;

    /// <summary>The fields above carried widgets, by source object number.</summary>
    private readonly Dictionary<int, PdfFieldNode> _nodes = [];

    /// <summary>The root fields, in the order they were met: source object numbers of the field nodes.</summary>
    private readonly List<int> _roots = [];

    /// <summary>The widgets that are root fields themselves, by source object number.</summary>
    private readonly List<int> _rootWidgets = [];

    /// <summary>The widgets carried, by source object number.</summary>
    private readonly HashSet<int> _widgets = [];

    /// <summary>Receives what the copy changed in the target's form, or <see langword="null"/>.</summary>
    private PdfPageEditReport? _report;

    /// <summary>Whether a carried widget belongs to a signature field.</summary>
    private bool _hasSignature;

    /// <summary>The names of the target's root fields, plus those carried; made on first use.</summary>
    private HashSet<string>? _rootNames;

    /// <summary>The form defaults, made when the first widget is met.</summary>
    private PdfFormDefaults? _defaults;

    /// <summary>Initializes a new instance of the <see cref="PdfCarryForms"/> class.</summary>
    /// <param name="context">The context.</param>
    internal PdfCarryForms(PdfCarryContext context) => _context = context;

    /// <summary>Gets a value indicating whether any field was met.</summary>
    internal bool HasFields => _nodes.Count > 0 || _rootWidgets.Count > 0;

    /// <summary>Prepares a widget on a copied page.</summary>
    /// <param name="widget">The widget in the source.</param>
    /// <param name="number">The widget's object number, which is not zero.</param>
    /// <returns>The dictionary to copy in its place, and whether its <c>/Parent</c> is kept.</returns>
    internal PreparedWidget Prepare(PdfDictionary widget, int number)
    {
        _defaults ??= new(_context);
        _ = _widgets.Add(number);
        _hasSignature |= IsSignature(widget);
        var copy = widget.Clone();
        RenameAppearance(copy);
        var parent = widget.GetRaw(KnownName.Parent).AsReference();
        if (parent.IsValid && RegisterChain(parent.Number))
        {
            _nodes[parent.Number].Kids.Add(number);
            return new(copy, true);
        }

        if (widget.ContainsKey(KnownName.FT) || widget.ContainsKey(KnownName.T))
        {
            RenameRoot(copy);
            _rootWidgets.Add(number);
        }

        return new(copy, false);
    }

    /// <summary>Copies the fields above the carried widgets and adds the root fields to the target's form.</summary>
    /// <param name="report">Receives what the copy changed in the target's form, or <see langword="null"/>.</param>
    internal void Finish(PdfPageEditReport? report)
    {
        if (!HasFields)
        {
            return;
        }

        _report = report;
        var sink = _context.Sink;
        var roots = new List<PdfValue>();
        foreach (var node in _nodes.Values)
        {
            sink.Set(new(sink.GetMapped(node.Number), 0), PdfValue.FromDictionary(BuildNode(node)));
        }

        foreach (var number in _roots)
        {
            roots.Add(PdfValue.FromReference(new(sink.GetMapped(number), 0)));
        }

        foreach (var number in _rootWidgets)
        {
            if (sink.GetMapped(number) > 0)
            {
                roots.Add(PdfValue.FromReference(new(sink.GetMapped(number), 0)));
            }
        }

        WriteForm(roots);
    }

    /// <summary>Determines whether a field is a signature field, reading <c>/FT</c> from the field or its nearest ancestor that has one.</summary>
    /// <param name="field">The field or widget in the source.</param>
    /// <returns><see langword="true"/> for a signature field.</returns>
    private static bool IsSignature(PdfDictionary field)
    {
        var node = field;
        for (var depth = 0; node is not null && depth < PdfLimits.MaxPageTreeDepth; depth++)
        {
            if (node.ContainsKey(KnownName.FT))
            {
                return node.IsName(KnownName.FT, KnownName.Sig);
            }

            node = node.GetDictionary(KnownName.Parent);
        }

        return false;
    }

    /// <summary>Registers a field and its ancestors that are not registered yet.</summary>
    /// <param name="number">The field's object number.</param>
    /// <returns><see langword="true"/> when the field is registered.</returns>
    private bool RegisterChain(int number)
    {
        var chain = new List<int>();
        var visited = new HashSet<int>();
        var next = number;
        while (next != 0 && chain.Count < PdfLimits.MaxPageTreeDepth && !_nodes.ContainsKey(next) && visited.Add(next))
        {
            if (StoreReading.GetDictionary(_context.Source, new(next, 0)) is not { } field)
            {
                break;
            }

            chain.Add(next);
            next = field.GetRaw(KnownName.Parent).AsReference().Number;
        }

        // The chain ends at a root, or at an ancestor registered earlier.
        var top = _nodes.ContainsKey(next) ? next : 0;
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            Register(chain[i], top);
            top = chain[i];
        }

        return _nodes.ContainsKey(number);
    }

    /// <summary>Registers one field: reserves its copy so widgets can name it as their parent.</summary>
    /// <param name="number">The field's object number.</param>
    /// <param name="parentNumber">The registered parent's object number, or 0 for a root.</param>
    private void Register(int number, int parentNumber)
    {
        var field = StoreReading.GetDictionary(_context.Source, new(number, 0))!;
        _context.Sink.MapObject(new(number, 0), _context.Sink.Reserve());
        if (parentNumber != 0)
        {
            _nodes[number] = new(number, field, parentNumber, null);
            _nodes[parentNumber].Kids.Add(number);
            return;
        }

        _roots.Add(number);
        _nodes[number] = new(number, field, 0, FindFreeName(field));
    }

    /// <summary>Gets the names the target's root fields use.</summary>
    /// <returns>The names, by <see cref="PdfCarryText.Key"/>; carried names are added as they are chosen.</returns>
    private HashSet<string> GetRootNames()
    {
        if (_rootNames is not null)
        {
            return _rootNames;
        }

        _rootNames = [with(StringComparer.Ordinal)];
        var fields = _context.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields);
        for (var i = 0; fields is not null && i < fields.Count; i++)
        {
            if (fields.GetDictionary(i) is { } field && field.GetStringBytes(KnownName.T) is { Length: > 0 } name)
            {
                _ = _rootNames.Add(PdfCarryText.Key(name));
            }
        }

        return _rootNames;
    }

    /// <summary>Picks a new name for a root field whose name the target uses.</summary>
    /// <param name="field">The root field in the source.</param>
    /// <returns>The new name's bytes, or <see langword="null"/> when the name is free or the field has none.</returns>
    private byte[]? FindFreeName(PdfDictionary field)
    {
        var name = field.GetStringBytes(KnownName.T).ToArray();
        if (name.Length == 0)
        {
            return null;
        }

        var unique = PdfCarryText.Unique(name, GetRootNames());
        return ReferenceEquals(unique, name) ? null : unique;
    }

    /// <summary>Renames a widget that is a root field itself when the target uses its name, and gives it the defaults it needs.</summary>
    /// <param name="copy">A copy of the widget, changed in place.</param>
    private void RenameRoot(PdfDictionary copy)
    {
        if (FindFreeName(copy) is { } name)
        {
            copy.Set(KnownName.T, PdfValue.FromString(name));
        }

        SetDefaults(copy);
    }

    /// <summary>Follows the font renames in a field's default appearance.</summary>
    /// <param name="field">A copy of the field, changed in place.</param>
    private void RenameAppearance(PdfDictionary field)
    {
        if (field.ContainsKey(KnownName.DA) && _defaults!.Renames.Count > 0)
        {
            field.Set(KnownName.DA, PdfValue.FromString(_defaults.RenameFonts(field.GetStringBytes(KnownName.DA))));
        }
    }

    /// <summary>Makes a root field carry the default appearance and quadding the target would not give it.</summary>
    /// <param name="field">The root field, changed in place.</param>
    private void SetDefaults(PdfDictionary field)
    {
        if (_defaults!.Appearance is { } appearance && _defaults.HasTargetForm && !field.ContainsKey(KnownName.DA))
        {
            field.Set(KnownName.DA, PdfValue.FromString(appearance));
        }

        if (_defaults.Quadding is { } quadding && _defaults.HasTargetForm && !field.ContainsKey(KnownName.Q))
        {
            field.Set(KnownName.Q, PdfValue.FromInteger(quadding));
        }
    }

    /// <summary>Builds the copy of a field above carried widgets.</summary>
    /// <param name="node">The field.</param>
    /// <returns>The copy, with the carried widgets and fields as its kids.</returns>
    private PdfDictionary BuildNode(PdfFieldNode node)
    {
        var sink = _context.Sink;
        var source = node.Source;
        var copy = _context.NewDictionary();
        for (var i = 0; i < source.Count; i++)
        {
            var key = source.GetKeyAt(i);
            if (key.Is(KnownName.Parent) || key.Is(KnownName.Kids))
            {
                continue;
            }

            var value = key.Is(KnownName.T) && node.NewName is { } name ? PdfValue.FromString(name) : sink.Import(source.GetValueAt(i));
            if (!value.IsNull)
            {
                copy.Add(sink.ImportName(key), value);
            }
        }

        RenameAppearance(copy);
        if (node.ParentNumber == 0)
        {
            SetDefaults(copy);
        }
        else
        {
            copy.Set(KnownName.Parent, PdfValue.FromReference(new(sink.GetMapped(node.ParentNumber), 0)));
        }

        copy.Set(KnownName.Kids, PdfValue.FromArray(BuildKids(node)));
        return copy;
    }

    /// <summary>Lists the carried kids of a field.</summary>
    /// <param name="node">The field.</param>
    /// <returns>References to the kids' copies.</returns>
    private PdfArray BuildKids(PdfFieldNode node)
    {
        var kids = new PdfArray(_context.TargetStore);
        foreach (var number in node.Kids)
        {
            if (_context.Sink.GetMapped(number) is var mapped and > 0)
            {
                kids.Add(PdfValue.FromReference(new(mapped, 0)));
            }
        }

        return kids;
    }

    /// <summary>Adds the root fields to the target's AcroForm, creating it when the target has none, and merges the form defaults.</summary>
    /// <param name="roots">References to the root fields' copies.</param>
    private void WriteForm(List<PdfValue> roots)
    {
        var catalog = _context.Catalog;
        var existing = catalog.GetDictionary(KnownName.AcroForm);
        var form = existing?.Clone() ?? _context.NewDictionary();
        var fields = existing?.GetArray(KnownName.Fields)?.Clone() ?? new PdfArray(_context.TargetStore);
        foreach (var root in roots)
        {
            fields.Add(root);
        }

        form.Set(KnownName.Fields, PdfValue.FromArray(fields));
        MergeSignatureFlags(form);
        MergeCalculationOrder(form);
        RemoveStaleXfa(form, existing);
        MergeResources(form);
        if (existing is null)
        {
            CopyFormDefaults(form);
        }

        if (_defaults!.SourceForm?.GetBoolean(KnownName.NeedAppearances) == true)
        {
            form.Set(KnownName.NeedAppearances, PdfValue.FromBoolean(true));
        }

        _context.SetEntry(catalog, KnownName.AcroForm, form);
        _context.CatalogChanged = true;
    }

    /// <summary>
    /// Keeps the signature flags (<c>/SigFlags</c>, ISO 32000-2 12.7.2, Table 224) the source set when a signature field was
    /// carried: the flags of the target and the source are combined.
    /// </summary>
    /// <param name="form">The target's form, changed in place.</param>
    private void MergeSignatureFlags(PdfDictionary form)
    {
        var carried = _hasSignature ? (_defaults!.SourceForm?.GetInt32(KnownName.SigFlags) ?? 0) & SignatureFlagsMask : 0;
        if (carried != 0)
        {
            form.Set(KnownName.SigFlags, PdfValue.FromInteger(form.GetInt32(KnownName.SigFlags) | carried));
        }
    }

    /// <summary>
    /// Appends the carried fields that the source's calculation order (<c>/CO</c>, ISO 32000-2 12.7.3.2, Table 225) lists to
    /// the target's, in the source's order. The target's fields calculate first.
    /// </summary>
    /// <param name="form">The target's form, changed in place.</param>
    private void MergeCalculationOrder(PdfDictionary form)
    {
        if (_defaults!.SourceForm?.GetArray(KnownName.CO) is not { } order)
        {
            return;
        }

        var merged = form.GetArray(KnownName.CO)?.Clone() ?? new PdfArray(_context.TargetStore);
        var before = merged.Count;
        for (var i = 0; i < order.Count; i++)
        {
            if (FindCarriedField(order.GetRaw(i)) is var mapped and > 0)
            {
                merged.Add(PdfValue.FromReference(new(mapped, 0)));
            }
        }

        if (merged.Count == before)
        {
            return;
        }

        form.Set(KnownName.CO, PdfValue.FromArray(merged));
    }

    /// <summary>Finds the copy of a field a calculation order lists.</summary>
    /// <param name="listed">The entry as stored in the source.</param>
    /// <returns>The copy's object number, or 0 when the field was not carried.</returns>
    private int FindCarriedField(PdfValue listed)
    {
        var field = listed.AsReference();
        return field.IsValid && (_nodes.ContainsKey(field.Number) || _widgets.Contains(field.Number)) ? Math.Max(0, _context.Sink.GetMapped(field.Number)) : 0;
    }

    /// <summary>
    /// Removes the target's XFA data (<c>/XFA</c>, ISO 32000-2 12.7.8) when the target also has standard fields and the copy
    /// added fields its template does not describe. XFA packets cannot be combined, and a form whose template misses fields
    /// would hide the copied ones; the standard fields stay, and the change is reported.
    /// </summary>
    /// <param name="form">The target's form, changed in place.</param>
    /// <param name="existing">The target's form before the copy, or <see langword="null"/>.</param>
    private void RemoveStaleXfa(PdfDictionary form, PdfDictionary? existing)
    {
        if (existing is null || !existing.ContainsKey(KnownName.XFA) || existing.GetArray(KnownName.Fields) is not { Count: > 0 })
        {
            return;
        }

        _ = form.Remove(KnownName.XFA);
        _report?.AddFormChange(XfaRemovedMessage);
    }

    /// <summary>Gives a new AcroForm the source form's default appearance and quadding.</summary>
    /// <param name="form">The new form.</param>
    private void CopyFormDefaults(PdfDictionary form)
    {
        if (_defaults!.Appearance is { } appearance)
        {
            form.Set(KnownName.DA, PdfValue.FromString(appearance));
        }

        if (_defaults.Quadding is { } quadding)
        {
            form.Set(KnownName.Q, PdfValue.FromInteger(quadding));
        }
    }

    /// <summary>Adds the source form's default resources that the target's form lacks.</summary>
    /// <param name="form">The target's form, changed in place.</param>
    private void MergeResources(PdfDictionary form)
    {
        if (_defaults!.SourceForm?.GetDictionary(KnownName.DR) is not { } sourceResources)
        {
            return;
        }

        var sink = _context.Sink;
        var resources = form.GetDictionary(KnownName.DR)?.Clone() ?? _context.NewDictionary();
        for (var i = 0; i < sourceResources.Count; i++)
        {
            var category = sink.ImportName(sourceResources.GetKeyAt(i));
            if (sourceResources.Get(sourceResources.GetKeyAt(i)).AsDictionary() is { } entries)
            {
                resources.Set(category, PdfValue.FromDictionary(MergeCategory(resources.GetDictionary(category), entries, category.Is(KnownName.Font))));
            }
            else if (!resources.ContainsKey(category))
            {
                resources.Set(category, sink.Import(sourceResources.GetValueAt(i)));
            }
        }

        form.Set(KnownName.DR, PdfValue.FromDictionary(resources));
    }

    /// <summary>Adds the resources of one category the target lacks.</summary>
    /// <param name="existing">The target's resources of the category, or <see langword="null"/>.</param>
    /// <param name="entries">The source's resources of the category.</param>
    /// <param name="isFont">Whether the category is the fonts, whose clashing names were renamed.</param>
    /// <returns>The merged resources.</returns>
    private PdfDictionary MergeCategory(PdfDictionary? existing, PdfDictionary entries, bool isFont)
    {
        var sink = _context.Sink;
        var merged = existing?.Clone() ?? _context.NewDictionary();
        var renames = _defaults!.Renames.GetAlternateLookup<ReadOnlySpan<byte>>();
        for (var i = 0; i < entries.Count; i++)
        {
            var spelling = _context.Source.Names.GetSpelling(entries.GetKeyAt(i));
            var name = isFont && renames.TryGetValue(spelling, out var renamed) ? sink.TargetNames.Intern(renamed) : sink.ImportName(entries.GetKeyAt(i));
            if (merged.ContainsKey(name))
            {
                continue;
            }

            var value = sink.Import(entries.GetValueAt(i));
            if (!value.IsNull)
            {
                merged.Add(name, value);
            }
        }

        return merged;
    }
}
