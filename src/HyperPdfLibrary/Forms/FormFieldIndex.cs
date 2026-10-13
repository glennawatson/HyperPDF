// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// The form's field hierarchy read once: every field by fully qualified name and by object number, so name clashes, parents
/// and descendants are found without walking the tree again. Reading is one pass over the fields.
/// </summary>
[DebuggerDisplay("FormFieldIndex: {All.Count} fields")]
internal sealed class FormFieldIndex
{
    /// <summary>The names reserved by fields that are about to be created.</summary>
    private readonly Dictionary<string, NameUse> _planned = [with(StringComparer.Ordinal)];

    /// <summary>The document's objects, which resolve the references in the field arrays.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>Initializes a new instance of the <see cref="FormFieldIndex"/> class.</summary>
    /// <param name="store">The document's objects.</param>
    private FormFieldIndex(PdfObjectStore store) => _store = store;

    /// <summary>What a name is used for.</summary>
    internal enum NameUse
    {
        /// <summary>No field has the name.</summary>
        Free = 0,

        /// <summary>A field with that name only groups other fields.</summary>
        Group = 1,

        /// <summary>A field with that name shows widgets and holds a value.</summary>
        Terminal = 2,
    }

    /// <summary>Gets every field, parents before children.</summary>
    internal List<FormFieldNode> All { get; } = [];

    /// <summary>Gets the fields of the form's <c>/Fields</c> array.</summary>
    internal List<FormFieldNode> Roots { get; } = [];

    /// <summary>Gets the fields by fully qualified name; the first of two fields with one name wins.</summary>
    internal Dictionary<string, FormFieldNode> ByName { get; } = [with(StringComparer.Ordinal)];

    /// <summary>Gets the fields by the object number of their dictionary.</summary>
    internal Dictionary<int, FormFieldNode> ByNumber { get; } = [];

    /// <summary>Reads the form's fields.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The index; empty when the document has no form.</returns>
    internal static FormFieldIndex Build(PdfDocument document, CancellationToken cancellationToken)
    {
        var index = new FormFieldIndex(document.Objects);
        if (document.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields) is not { } fields)
        {
            return index;
        }

        var visited = new HashSet<int>();
        for (var i = 0; i < fields.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index.Read(fields.GetRaw(i), null, visited, 0, cancellationToken);
        }

        return index;
    }

    /// <summary>Lists a field and the fields below it, parents first.</summary>
    /// <param name="node">The field.</param>
    /// <param name="output">The list receiving the fields.</param>
    internal static void CollectSubtree(FormFieldNode node, List<FormFieldNode> output)
    {
        output.Add(node);
        for (var i = 0; i < node.Children.Count; i++)
        {
            CollectSubtree(node.Children[i], output);
        }
    }

    /// <summary>Finds a field by fully qualified name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The field; <see langword="null"/> when none has it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal FormFieldNode? Find(string name) => ByName.GetValueOrDefault(name);

    /// <summary>Tells how a name is used by the fields read and by those reserved.</summary>
    /// <param name="name">The fully qualified name.</param>
    /// <returns>The use.</returns>
    internal NameUse GetUse(string name)
    {
        if (_planned.TryGetValue(name, out var planned))
        {
            return planned;
        }

        if (!ByName.TryGetValue(name, out var node))
        {
            return NameUse.Free;
        }

        return node.IsTerminal ? NameUse.Terminal : NameUse.Group;
    }

    /// <summary>Reserves a name for a field that is about to be created, so later checks see it.</summary>
    /// <param name="name">The fully qualified name.</param>
    /// <param name="use">How the name will be used.</param>
    internal void Reserve(string name, NameUse use) => _planned[name] = use;

    /// <summary>Reads one entry of a <c>/Fields</c> or <c>/Kids</c> array and the fields below it.</summary>
    /// <param name="raw">The array entry as stored.</param>
    /// <param name="parent">The parent field, or <see langword="null"/>.</param>
    /// <param name="visited">The object numbers read, so a loop ends.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    private void Read(PdfValue raw, FormFieldNode? parent, HashSet<int> visited, int depth, CancellationToken cancellationToken)
    {
        var id = raw.AsReference();
        if (depth >= PdfLimits.MaxNesting || (id.IsValid && !visited.Add(id.Number)))
        {
            return;
        }

        var dictionary = StoreReading.Resolve(_store, raw).AsDictionary();
        if (dictionary is null)
        {
            return;
        }

        var partial = dictionary.GetText(KnownName.T) ?? string.Empty;
        var name = FormFieldNames.Join(parent?.Name ?? string.Empty, partial);
        var node = new FormFieldNode(name, id, dictionary, parent);
        All.Add(node);
        if (parent is null)
        {
            Roots.Add(node);
        }
        else
        {
            parent.Children.Add(node);
        }

        if (id.IsValid)
        {
            ByNumber[id.Number] = node;
        }

        _ = ByName.TryAdd(name, node);
        ReadKids(node, visited, depth, cancellationToken);
    }

    /// <summary>Reads the kids of a field: its widgets and the fields below it.</summary>
    /// <param name="node">The field.</param>
    /// <param name="visited">The object numbers read.</param>
    /// <param name="depth">The field's nesting depth.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    private void ReadKids(FormFieldNode node, HashSet<int> visited, int depth, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (node.Dictionary.GetArray(KnownName.Kids) is not { } kids)
        {
            if (node.IsMerged)
            {
                node.Widgets.Add(new(node.Dictionary, node.Id));
            }

            return;
        }

        for (var i = 0; i < kids.Count; i++)
        {
            var kid = kids.GetDictionary(i);
            if (kid is null)
            {
                continue;
            }

            if (kid.ContainsKey(KnownName.T) || kid.ContainsKey(KnownName.Kids))
            {
                Read(kids.GetRaw(i), node, visited, depth + 1, cancellationToken);
            }
            else
            {
                node.Widgets.Add(new(kid, kids.GetRaw(i).AsReference()));
            }
        }
    }
}
