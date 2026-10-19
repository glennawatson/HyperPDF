// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Edits the elements of a tagged document's structure tree (ISO 32000-2 14.7.2). An element comes from
/// <see cref="PdfDocumentTagged.GetStructureTree"/>. Each edit joins the open transaction or makes its own, so it can be
/// undone, and it drops the cached structure tree so the next read sees the change. A document certified with a signature
/// refuses the edit. Edits that would leave the tree unreadable are refused with a plain sentence.
/// </summary>
public static class PdfDocumentStructureEditing
{
    /// <summary>What an edit of the tags is called when the document's certification refuses it.</summary>
    private const string EditDescription = "change the tags";

    /// <summary>Sets an element's standard type.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element.</param>
    /// <param name="type">The standard type name, such as <c>P</c> or <c>H2</c>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The type is not standard for the element, or the element cannot be edited on its own.</exception>
    /// <exception cref="InvalidOperationException">The document is certified.</exception>
    public static void SetType(PdfDocument document, PdfStructureElement element, string type)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(type);
        Run(document, "Change tag type", new TypeState(element, type), static (edits, run) => PdfStructureEditor.SetType(edits, run.Element, run.Type));
    }

    /// <summary>Sets an element's standard type, loading what the edit needs first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element.</param>
    /// <param name="type">The standard type name.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the type is set.</returns>
    public static async ValueTask SetTypeAsync(PdfDocument document, PdfStructureElement element, string type, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        SetType(document, element, type);
    }

    /// <summary>
    /// Gets the standard types an element may take. They depend on its namespace and the file's version, and in a PDF 2.0
    /// tree only types that fit inside its parent and around its children are listed (ISO/TS 32005 clause 7.2).
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element.</param>
    /// <returns>The type names; the element's own type is always listed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static string[] GetTypeChoices(PdfDocument document, PdfStructureElement element)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);
        return PdfStructureEditor.GetChoices(document.Objects.Version, element);
    }

    /// <summary>Sets or clears one text entry of an element.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element.</param>
    /// <param name="field">The entry.</param>
    /// <param name="text">The new text; <see langword="null"/> or empty clears the entry.</param>
    /// <exception cref="ArgumentNullException">The document or element is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A language is not a valid tag, or the element cannot be edited on its own.</exception>
    /// <exception cref="InvalidOperationException">The document is certified.</exception>
    public static void SetText(PdfDocument document, PdfStructureElement element, PdfStructureTextField field, string? text)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);
        Run(document, "Change tag text", new TextState(element, field, text), static (edits, run) => PdfStructureEditor.SetText(edits, run.Element, run.Field, run.Text));
    }

    /// <summary>Sets or clears one text entry of an element, loading what the edit needs first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element.</param>
    /// <param name="field">The entry.</param>
    /// <param name="text">The new text; <see langword="null"/> or empty clears the entry.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the entry is set.</returns>
    public static async ValueTask SetTextAsync(PdfDocument document, PdfStructureElement element, PdfStructureTextField field, string? text, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        SetText(document, element, field, text);
    }

    /// <summary>Lists the entries of the structure tree's role map: custom tag types and what they stand for.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The entries, in file order; empty when the file has no tags or no role map.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static PdfRoleMapping[] GetRoleMap(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (PdfDocumentTagged.GetStructureTree(document)?.Root.GetDictionary(KnownName.RoleMap) is not { } map)
        {
            return [];
        }

        var names = document.Objects.Names;
        List<PdfRoleMapping> entries = [with(map.Count)];
        for (var i = 0; i < map.Count; i++)
        {
            var target = map.GetValueAt(i);
            if (target.Kind == PdfKind.Name)
            {
                entries.Add(new(Encoding.UTF8.GetString(names.GetSpelling(map.GetKeyAt(i))), Encoding.UTF8.GetString(names.GetSpelling(target.AsName()))));
            }
        }

        return [.. entries];
    }

    /// <summary>Maps a custom tag type to a standard type, so a reader knows what the custom tags mean. An existing mapping for the type is replaced.</summary>
    /// <param name="document">The document.</param>
    /// <param name="type">The custom type; it cannot be a standard type.</param>
    /// <param name="mapsTo">The standard type it stands for.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A name is not allowed.</exception>
    /// <exception cref="InvalidOperationException">The document is certified, has no tags, or the mapping would make a cycle.</exception>
    public static void SetRoleMapping(PdfDocument document, string type, string mapsTo)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(mapsTo);
        Run(document, "Map tag type", new RoleMapState(type, mapsTo), static (edits, run) => PdfStructureRoleMapEditor.Set(edits, run.Type, run.MapsTo));
    }

    /// <summary>Removes a custom tag type's mapping. A mapping that tags still use is kept, because they would be left without a meaning.</summary>
    /// <param name="document">The document.</param>
    /// <param name="type">The custom type.</param>
    /// <returns><see langword="true"/> when a mapping was removed; <see langword="false"/> when there was none.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The document is certified, or tags still use the type.</exception>
    public static bool RemoveRoleMapping(PdfDocument document, string type)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(type);
        if (PdfDocumentTagged.GetStructureTree(document) is { } tree && IsUsed(tree, type))
        {
            throw new InvalidOperationException($"Tags of the type '{type}' still use this mapping, so it was not removed.");
        }

        var removed = false;
        Run(document, "Remove tag type mapping", type, (edits, name) => removed = PdfStructureRoleMapEditor.Remove(edits, name));
        return removed;
    }

    /// <summary>Sets or clears the scope of a table header cell, which says which cells it labels (ISO 32000-2 14.8.5.7).</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The TH element.</param>
    /// <param name="scope">The scope; <see cref="PdfTableScope.None"/> clears it.</param>
    /// <exception cref="ArgumentNullException">The document or element is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The element is not a TH, or cannot be edited on its own.</exception>
    /// <exception cref="InvalidOperationException">The document is certified.</exception>
    public static void SetTableScope(PdfDocument document, PdfStructureElement element, PdfTableScope scope)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);
        Run(document, "Change header scope", new ScopeState(element, scope), static (edits, run) => PdfStructureAttributeEditor.SetTableScope(edits, run.Element, run.Scope));
    }

    /// <summary>Sets or clears how a list's items are labelled, such as <c>Decimal</c> or <c>Disc</c> (ISO 32000-2 14.8.5.6).</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The L element.</param>
    /// <param name="numbering">A name from ISO 32000-2 Table 383, such as <c>Decimal</c>, <c>Disc</c> or <c>UpperRoman</c>; <see langword="null"/> or empty clears it.</param>
    /// <exception cref="ArgumentNullException">The document or element is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The element is not a list, the name is not allowed, or the element cannot be edited on its own.</exception>
    /// <exception cref="InvalidOperationException">The document is certified.</exception>
    public static void SetListNumbering(PdfDocument document, PdfStructureElement element, string? numbering)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);
        Run(document, "Change list numbering", new NameState(element, numbering), static (edits, run) => PdfStructureAttributeEditor.SetListNumbering(edits, run.Element, run.Name));
    }

    /// <summary>Sets or clears how an element sits in the flow of content, such as <c>Block</c> or <c>Inline</c> (ISO 32000-2 14.8.5.4).</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element.</param>
    /// <param name="placement">A name from ISO 32000-2 Table 380: <c>Block</c>, <c>Inline</c>, <c>Before</c>, <c>Start</c> or <c>End</c>; <see langword="null"/> or empty clears it.</param>
    /// <exception cref="ArgumentNullException">The document or element is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The name is not allowed, or the element cannot be edited on its own.</exception>
    /// <exception cref="InvalidOperationException">The document is certified.</exception>
    public static void SetPlacement(PdfDocument document, PdfStructureElement element, string? placement)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);
        Run(document, "Change placement", new NameState(element, placement), static (edits, run) => PdfStructureAttributeEditor.SetPlacement(edits, run.Element, run.Name));
    }

    /// <summary>Sets how many rows and columns a table cell covers (ISO 32000-2 14.8.5.7). A span of 1 is the default and clears the entry.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The TH or TD element.</param>
    /// <param name="rows">How many rows the cell covers, at least 1.</param>
    /// <param name="columns">How many columns the cell covers, at least 1.</param>
    /// <exception cref="ArgumentNullException">The document or element is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The element is not a table cell, a span is below 1, or the element cannot be edited on its own.</exception>
    /// <exception cref="InvalidOperationException">The document is certified.</exception>
    public static void SetCellSpans(PdfDocument document, PdfStructureElement element, int rows, int columns)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);
        Run(document, "Change cell spans", new SpanState(element, rows, columns), static (edits, run) => PdfStructureAttributeEditor.SetCellSpans(edits, run.Element, run.Rows, run.Columns));
    }

    /// <summary>Sets or clears the scope of a table header cell, loading what the edit needs first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The TH element.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the scope is set.</returns>
    public static async ValueTask SetTableScopeAsync(PdfDocument document, PdfStructureElement element, PdfTableScope scope, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        SetTableScope(document, element, scope);
    }

    /// <summary>Moves an element among its siblings, which changes the reading order but not the page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element.</param>
    /// <param name="offset">How many places to move it; negative moves it earlier.</param>
    /// <returns><see langword="true"/> when the element moved.</returns>
    /// <exception cref="ArgumentNullException">The document or element is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The element cannot be edited on its own.</exception>
    /// <exception cref="InvalidOperationException">The document is certified.</exception>
    public static bool Move(PdfDocument document, PdfStructureElement element, int offset)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);
        var state = new MoveState(element, offset);
        Run(document, "Move tag", state, static (edits, run) => run.Moved = PdfStructureEditor.Move(edits, run.Element, run.Offset));
        return state.Moved;
    }

    /// <summary>Moves an element among its siblings, loading what the edit needs first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element.</param>
    /// <param name="offset">How many places to move it.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task whose result is <see langword="true"/> when the element moved.</returns>
    public static async ValueTask<bool> MoveAsync(PdfDocument document, PdfStructureElement element, int offset, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        return Move(document, element, offset);
    }

    /// <summary>Makes an element the last child of the element above it in its group, so a screen reader reads it as part of that element.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element.</param>
    /// <exception cref="ArgumentNullException">The document or element is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A tag involved is written directly inside its parent.</exception>
    /// <exception cref="InvalidOperationException">The document is certified, or there is no element above to go into.</exception>
    public static void Nest(PdfDocument document, PdfStructureElement element)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);
        var above = PreviousSibling(document, element) ?? throw new InvalidOperationException("There is no tag above this one to put it inside.");
        Run(document, "Nest tag", new NestState(element, above), static (edits, run) => PdfStructureEditor.Nest(edits, run.Element, run.Parent));
    }

    /// <summary>Makes an element the last child of any other element, so a tag can move to a different branch of the tree.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element to move.</param>
    /// <param name="newParent">The element that takes it in; it cannot be the element or one of its descendants.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A tag involved is written directly inside its parent.</exception>
    /// <exception cref="InvalidOperationException">The document is certified, the move would put a tag inside itself, or a PDF 2.0 hierarchy rule forbids it.</exception>
    public static void MoveInto(PdfDocument document, PdfStructureElement element, PdfStructureElement newParent)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(newParent);
        Run(document, "Move tag to another place", new NestState(element, newParent), static (edits, run) => PdfStructureEditor.Nest(edits, run.Element, run.Parent));
    }

    /// <summary>Moves an element into another element, loading what the edit needs first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element to move.</param>
    /// <param name="newParent">The element that takes it in.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the element has moved.</returns>
    public static async ValueTask MoveIntoAsync(PdfDocument document, PdfStructureElement element, PdfStructureElement newParent, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        MoveInto(document, element, newParent);
    }

    /// <summary>Nests an element, loading what the edit needs first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the element is nested.</returns>
    public static async ValueTask NestAsync(PdfDocument document, PdfStructureElement element, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        Nest(document, element);
    }

    /// <summary>Moves an element out of its parent, to just after the parent.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element.</param>
    /// <exception cref="ArgumentNullException">The document or element is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A tag involved is written directly inside its parent.</exception>
    /// <exception cref="InvalidOperationException">The document is certified, or the element is already at the top.</exception>
    public static void Unnest(PdfDocument document, PdfStructureElement element)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);
        Run(document, "Un-nest tag", element, PdfStructureEditor.Unnest);
    }

    /// <summary>Un-nests an element, loading what the edit needs first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the element is moved out.</returns>
    public static async ValueTask UnnestAsync(PdfDocument document, PdfStructureElement element, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        Unnest(document, element);
    }

    /// <summary>Removes an element that holds no content and lifts its child elements into its parent.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element.</param>
    /// <exception cref="ArgumentNullException">The document or element is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The element cannot be edited on its own.</exception>
    /// <exception cref="InvalidOperationException">The document is certified, or the element holds content or an ID.</exception>
    public static void Remove(PdfDocument document, PdfStructureElement element)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);
        Run(document, "Remove tag", element, PdfStructureEditor.Remove);
    }

    /// <summary>Removes an element, loading what the edit needs first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the element is removed.</returns>
    public static async ValueTask RemoveAsync(PdfDocument document, PdfStructureElement element, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        Remove(document, element);
    }

    /// <summary>Finds the element just above an element in its group.</summary>
    /// <param name="document">The document.</param>
    /// <param name="element">The element.</param>
    /// <returns>The element above, or <see langword="null"/> when it is first.</returns>
    private static PdfStructureElement? PreviousSibling(PdfDocument document, PdfStructureElement element)
    {
        PdfStructureElement? previous = null;
        if (element.Parent is { } parent)
        {
            foreach (var kid in parent.Kids)
            {
                if (kid.Element is not { } child)
                {
                    continue;
                }

                if (child.Id.Number == element.Id.Number)
                {
                    return previous;
                }

                previous = child;
            }

            return null;
        }

        if (PdfDocumentTagged.GetStructureTree(document) is not { } tree)
        {
            return null;
        }

        foreach (var root in tree.Roots)
        {
            if (root.Id.Number == element.Id.Number)
            {
                return previous;
            }

            previous = root;
        }

        return null;
    }

    /// <summary>Loads the structure tree root an edit reads.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the root is cached.</returns>
    private static async ValueTask PrefetchAsync(PdfDocument document, CancellationToken cancellationToken) =>
        await PdfEditPrefetch.CatalogEntryAsync(document, KnownName.StructTreeRoot, cancellationToken).ConfigureAwait(false);

    /// <summary>Runs a structure edit in a transaction.</summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="label">The undo label.</param>
    /// <param name="state">The arguments.</param>
    /// <param name="edit">The edit.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Run<TState>(PdfDocument document, string label, TState state, Action<PdfObjectEdits, TState> edit) =>
        PdfDocumentEditing.RunObjectEdit(document, label, PdfChangeKinds.Other, PdfCacheScopes.Structure, EditDescription, state, edit);

    /// <summary>Determines whether any element has a type.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="type">The type as the file spells it.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool IsUsed(PdfStructureTree tree, string type)
    {
        var pending = new Stack<PdfStructureElement>(tree.Roots);
        while (pending.TryPop(out var element))
        {
            if (string.Equals(element.RawType, type, StringComparison.Ordinal))
            {
                return true;
            }

            foreach (var kid in element.Kids)
            {
                if (kid.Element is { } child)
                {
                    pending.Push(child);
                }
            }
        }

        return false;
    }

    /// <summary>The arguments of a type change.</summary>
    /// <param name="Element">The element.</param>
    /// <param name="Type">The new type.</param>
    [DebuggerDisplay("TypeState: {Type}")]
    private readonly record struct TypeState(PdfStructureElement Element, string Type);

    /// <summary>The arguments of a nest.</summary>
    /// <param name="Element">The element.</param>
    /// <param name="Parent">The element above, which takes it in.</param>
    [DebuggerDisplay("NestState: {Element}")]
    private readonly record struct NestState(PdfStructureElement Element, PdfStructureElement Parent);

    /// <summary>The arguments of a role map change.</summary>
    /// <param name="Type">The custom type.</param>
    /// <param name="MapsTo">The type it stands for.</param>
    [DebuggerDisplay("RoleMapState: {Type}")]
    private readonly record struct RoleMapState(string Type, string MapsTo);

    /// <summary>The arguments of a span change.</summary>
    /// <param name="Element">The cell.</param>
    /// <param name="Rows">The rows it covers.</param>
    /// <param name="Columns">The columns it covers.</param>
    [DebuggerDisplay("SpanState: {Rows}x{Columns}")]
    private readonly record struct SpanState(PdfStructureElement Element, int Rows, int Columns);

    /// <summary>The arguments of a change to a named attribute, such as a list numbering or a placement.</summary>
    /// <param name="Element">The element.</param>
    /// <param name="Name">The new attribute name, or <see langword="null"/> to clear it.</param>
    [DebuggerDisplay("NameState: {Name}")]
    private readonly record struct NameState(PdfStructureElement Element, string? Name);

    /// <summary>The arguments of a scope change.</summary>
    /// <param name="Element">The header cell.</param>
    /// <param name="Scope">The new scope.</param>
    [DebuggerDisplay("ScopeState: {Scope}")]
    private readonly record struct ScopeState(PdfStructureElement Element, PdfTableScope Scope);

    /// <summary>The arguments of a text change.</summary>
    /// <param name="Element">The element.</param>
    /// <param name="Field">The entry.</param>
    /// <param name="Text">The new text.</param>
    [DebuggerDisplay("TextState: {Field}")]
    private readonly record struct TextState(PdfStructureElement Element, PdfStructureTextField Field, string? Text);

    /// <summary>The arguments of a move, and whether it happened.</summary>
    /// <param name="element">The element.</param>
    /// <param name="offset">The distance.</param>
    [DebuggerDisplay("MoveState: {Offset}")]
    private sealed class MoveState(PdfStructureElement element, int offset)
    {
        /// <summary>Gets the element.</summary>
        internal PdfStructureElement Element { get; } = element;

        /// <summary>Gets the distance.</summary>
        internal int Offset { get; } = offset;

        /// <summary>Gets or sets a value indicating whether the element moved.</summary>
        internal bool Moved { get; set; }
    }
}
