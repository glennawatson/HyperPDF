// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Walks a structure tree from its root, building every element once. Each object is visited once and the walk stops
/// at <see cref="PdfLimits.MaxNesting"/> levels and <see cref="MaxElements"/> elements, so loops and huge trees in
/// damaged files end.
/// </summary>
[DebuggerDisplay("StructureTreeBuilder: {_count} elements")]
internal sealed class StructureTreeBuilder
{
    /// <summary>The most elements built for one document.</summary>
    internal const int MaxElements = 1 << 20;

    /// <summary>The most kids read from one element.</summary>
    private const int MaxKids = 1 << 16;

    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The interned structure names.</summary>
    private readonly TaggedNames _names;

    /// <summary>Resolves types through the role maps.</summary>
    private readonly StructureRoleMapper _roles;

    /// <summary>Reads attributes.</summary>
    private readonly StructureAttributeReader _attributes;

    /// <summary>The object numbers already visited.</summary>
    private readonly HashSet<int> _visitedObjects = [];

    /// <summary>The direct dictionaries already visited.</summary>
    private readonly HashSet<PdfDictionary> _visitedDirect = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>The raw type names already decoded, by name id.</summary>
    private readonly Dictionary<int, string> _typeNames = [];

    /// <summary>The tree's indexes, filled as elements are built.</summary>
    private readonly StructureIndex _index;

    /// <summary>The elements built so far.</summary>
    private int _count;

    /// <summary>Initializes a new instance of the <see cref="StructureTreeBuilder"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="root">The structure tree root.</param>
    /// <param name="names">The interned structure names.</param>
    /// <param name="index">Receives the elements by dictionary and by id.</param>
    internal StructureTreeBuilder(PdfDocument document, PdfDictionary root, TaggedNames names, StructureIndex index)
    {
        _document = document;
        _names = names;
        _index = index;
        var table = document.Objects.Names;
        _roles = new(names, table, root.GetDictionary(KnownName.RoleMap));
        _attributes = new(names, table, root.GetDictionary(KnownName.ClassMap));
    }

    /// <summary>Builds the top-level elements.</summary>
    /// <param name="root">The structure tree root.</param>
    /// <param name="language">The document's language, inherited by every element.</param>
    /// <param name="output">Receives the top-level elements, in logical order.</param>
    internal void Build(PdfDictionary root, string? language, List<PdfStructureElement> output)
    {
        var kids = root.GetRaw(KnownName.K);
        var parent = new Inherited(null, -1, language, 0);
        if (StoreReading.Resolve(_document.Objects, kids).AsArray() is { } array)
        {
            var count = Math.Min(array.Count, MaxKids);
            for (var i = 0; i < count; i++)
            {
                AddTopLevel(array.GetRaw(i), parent, output);
            }

            return;
        }

        AddTopLevel(kids, parent, output);
    }

    /// <summary>Gets a non-empty text entry.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The text, or <see langword="null"/> when missing or empty.</returns>
    private static string? ReadText(PdfDictionary dictionary, PdfName key) => dictionary.GetText(key) is { Length: > 0 } text ? text : null;

    /// <summary>Adds a top-level element.</summary>
    /// <param name="raw">The kid as stored.</param>
    /// <param name="parent">What the root passes down.</param>
    /// <param name="output">Receives the element.</param>
    private void AddTopLevel(PdfValue raw, in Inherited parent, List<PdfStructureElement> output)
    {
        if (AddElement(raw, parent) is { } element)
        {
            output.Add(element);
        }
    }

    /// <summary>Builds an element and, recursively, its kids.</summary>
    /// <param name="raw">The element as stored: a reference or a direct dictionary.</param>
    /// <param name="parent">What the parent passes down.</param>
    /// <returns>The element, or <see langword="null"/> when it is not one, was seen before or is past a limit.</returns>
    private PdfStructureElement? AddElement(PdfValue raw, in Inherited parent)
    {
        if (parent.Depth > PdfLimits.MaxNesting || _count >= MaxElements)
        {
            return null;
        }

        var id = raw.AsReference();
        if (StoreReading.Resolve(_document.Objects, raw).AsDictionary() is not { } dictionary || !MarkVisited(id, dictionary))
        {
            return null;
        }

        _count++;
        var element = new PdfStructureElement(dictionary, id, parent.Element);
        Describe(element, parent);
        _index.Add(element);
        ReadKids(element);
        return element;
    }

    /// <summary>Records an object as visited.</summary>
    /// <param name="id">The object id; not valid for a direct dictionary.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns><see langword="true"/> on the first visit.</returns>
    private bool MarkVisited(PdfObjectId id, PdfDictionary dictionary) => id.IsValid ? _visitedObjects.Add(id.Number) : _visitedDirect.Add(dictionary);

    /// <summary>Reads an element's own entries and what it inherits.</summary>
    /// <param name="element">The element.</param>
    /// <param name="parent">What the parent passes down.</param>
    private void Describe(PdfStructureElement element, in Inherited parent)
    {
        var dictionary = element.Dictionary;
        var type = dictionary.GetName(KnownName.S);
        var space = dictionary.GetDictionary(_names.NS);
        var resolved = _roles.Resolve(type, space);
        element.RawType = GetTypeName(type);
        element.MappedType = resolved.MappedType;
        element.Type = resolved.Type;
        element.ResolvesToMathMl = resolved.IsMathMl;
        element.Namespace = space is null ? null : _roles.GetUri(space);
        element.Depth = parent.Depth;
        element.PageIndex = ReadPage(dictionary, parent.PageIndex);
        element.AlternateText = ReadText(dictionary, KnownName.Alt);
        element.ActualText = ReadText(dictionary, KnownName.ActualText);
        element.Expansion = ReadText(dictionary, KnownName.E);
        element.Title = ReadText(dictionary, KnownName.T);
        element.Language = ReadText(dictionary, KnownName.Lang) ?? parent.Language;
        element.ElementId = ReadText(dictionary, KnownName.ID);
        element.Attributes = _attributes.Read(dictionary);
        element.SetNoteType(_attributes.NoteType);
    }

    /// <summary>Gets a type name's text, decoding each name once.</summary>
    /// <param name="type">The name.</param>
    /// <returns>The text.</returns>
    private string GetTypeName(PdfName type)
    {
        ref var text = ref CollectionsMarshal.GetValueRefOrAddDefault(_typeNames, type.Id, out var exists);
        if (!exists)
        {
            text = _document.Objects.Names.GetString(type);
        }

        return text!;
    }

    /// <summary>Reads a dictionary's <c>/Pg</c>.</summary>
    /// <param name="dictionary">The element or kid dictionary.</param>
    /// <param name="inherited">The page to use when it has none.</param>
    /// <returns>The page index, or the inherited one.</returns>
    private int ReadPage(PdfDictionary dictionary, int inherited)
    {
        var page = dictionary.GetRaw(KnownName.Pg);
        if (!page.IsReference)
        {
            return inherited;
        }

        var index = PdfDocumentPages.GetPageIndex(_document, page.AsReference());
        return index >= 0 ? index : inherited;
    }

    /// <summary>Reads an element's <c>/K</c>: one kid or an array of them.</summary>
    /// <param name="element">The element.</param>
    private void ReadKids(PdfStructureElement element)
    {
        var kids = element.Dictionary.GetRaw(KnownName.K);
        var passed = new Inherited(element, element.PageIndex, element.Language, element.Depth + 1);
        if (StoreReading.Resolve(_document.Objects, kids).AsArray() is { } array)
        {
            var count = Math.Min(array.Count, MaxKids);
            for (var i = 0; i < count; i++)
            {
                ReadKid(element, array.GetRaw(i), passed);
            }

            return;
        }

        ReadKid(element, kids, passed);
    }

    /// <summary>Reads one kid: a marked content id, a marked content reference, an object reference or an element.</summary>
    /// <param name="element">The parent element.</param>
    /// <param name="raw">The kid as stored.</param>
    /// <param name="passed">What the element passes down.</param>
    private void ReadKid(PdfStructureElement element, PdfValue raw, in Inherited passed)
    {
        var kid = StoreReading.Resolve(_document.Objects, raw);
        if (kid.IsNumber)
        {
            element.AddKid(new(PdfStructureKidKind.MarkedContent, null, element.PageIndex, kid.AsInt32(), default, default));
            return;
        }

        if (kid.AsDictionary() is not { } dictionary)
        {
            return;
        }

        switch (dictionary.GetName(KnownName.Type).ToKnownName())
        {
            case KnownName.MCR:
                {
                    var stream = dictionary.GetRaw(_names.Stm).AsReference();
                    element.AddKid(new(PdfStructureKidKind.MarkedContent, null, ReadPage(dictionary, element.PageIndex), dictionary.GetInt32(KnownName.MCID, -1), default, stream));
                    break;
                }

            case KnownName.OBJR:
                {
                    var target = dictionary.GetRaw(KnownName.Obj).AsReference();
                    element.AddKid(new(PdfStructureKidKind.Object, null, ReadPage(dictionary, element.PageIndex), -1, target, default));
                    break;
                }

            default:
                {
                    if (AddElement(raw, passed) is { } child)
                    {
                        element.AddKid(new(PdfStructureKidKind.Element, child, child.PageIndex, -1, default, default));
                    }

                    break;
                }
        }
    }

    /// <summary>What an element passes down to its kids.</summary>
    /// <param name="Element">The element, or <see langword="null"/> for the root.</param>
    /// <param name="PageIndex">Its page, or -1.</param>
    /// <param name="Language">Its language, or <see langword="null"/>.</param>
    /// <param name="Depth">The kids' depth.</param>
    [DebuggerDisplay("Inherited: page {PageIndex} depth {Depth}")]
    private readonly record struct Inherited(PdfStructureElement? Element, int PageIndex, string? Language, int Depth);
}
