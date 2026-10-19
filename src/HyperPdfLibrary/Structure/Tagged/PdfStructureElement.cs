// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// One element of a document's structure tree. The tree builds every element once, then never changes it, so elements
/// are safe to read from any thread.
/// </summary>
[DebuggerDisplay("PdfStructureElement: {RawType} as {Type} on page {PageIndex}")]
public sealed class PdfStructureElement
{
    /// <summary>The kids, in logical order.</summary>
    private readonly List<PdfStructureKid> _kids = [];

    /// <summary>The recognised subtype stored compactly beside the element's flags.</summary>
    private byte _noteType;

    /// <summary>Initializes a new instance of the <see cref="PdfStructureElement"/> class.</summary>
    /// <param name="dictionary">The element's dictionary.</param>
    /// <param name="id">The element's object id; not valid for a direct dictionary.</param>
    /// <param name="parent">The parent element, or <see langword="null"/> for a top-level element.</param>
    internal PdfStructureElement(PdfDictionary dictionary, PdfObjectId id, PdfStructureElement? parent)
    {
        Dictionary = dictionary;
        Id = id;
        Parent = parent;
    }

    /// <summary>Gets the element's dictionary.</summary>
    public PdfDictionary Dictionary { get; }

    /// <summary>Gets the element's object id; not valid for an element written directly in its parent.</summary>
    public PdfObjectId Id { get; }

    /// <summary>Gets the parent element, or <see langword="null"/> for a top-level element.</summary>
    public PdfStructureElement? Parent { get; }

    /// <summary>Gets the kids, in logical order.</summary>
    public IReadOnlyList<PdfStructureKid> Kids => _kids;

    /// <summary>Gets the element's own type, its <c>/S</c>.</summary>
    public string RawType { get; internal set; } = string.Empty;

    /// <summary>Gets the type after the role maps: a standard type, or the last type the maps lead to.</summary>
    public string MappedType { get; internal set; } = string.Empty;

    /// <summary>Gets the standard type the element resolves to.</summary>
    public PdfStructureType Type { get; internal set; }

    /// <summary>Gets the URI of the namespace the element's type belongs to, or <see langword="null"/> for the default.</summary>
    public string? Namespace { get; internal set; }

    /// <summary>Gets a value indicating whether the role maps lead the element's type into the MathML namespace, whose types need no further mapping.</summary>
    public bool ResolvesToMathMl { get; internal set; }

    /// <summary>Gets the page the element's content is on, from its <c>/Pg</c> or its parent's; -1 when unknown.</summary>
    public int PageIndex { get; internal set; } = -1;

    /// <summary>Gets the <c>/Alt</c> description, or <see langword="null"/>.</summary>
    public string? AlternateText { get; internal set; }

    /// <summary>Gets the <c>/ActualText</c> that replaces the content, or <see langword="null"/>.</summary>
    public string? ActualText { get; internal set; }

    /// <summary>Gets the <c>/E</c> expansion of an abbreviation, or <see langword="null"/>.</summary>
    public string? Expansion { get; internal set; }

    /// <summary>Gets the <c>/T</c> title, or <see langword="null"/>.</summary>
    public string? Title { get; internal set; }

    /// <summary>Gets the language: the element's <c>/Lang</c>, else its parent's, else the document's; <see langword="null"/> when none.</summary>
    public string? Language { get; internal set; }

    /// <summary>Gets the element id, its <c>/ID</c>, which table <c>/Headers</c> and the ID tree refer to; <see langword="null"/> when none.</summary>
    public string? ElementId { get; internal set; }

    /// <summary>Gets the reading attributes.</summary>
    public PdfStructureAttributes Attributes { get; internal set; } = PdfStructureAttributes.None;

    /// <summary>Gets the FENote subtype; None when absent or unsupported.</summary>
    public PdfNoteType NoteType => (PdfNoteType)_noteType;

    /// <summary>Gets how deep the element is: 0 for a top-level element.</summary>
    public int Depth { get; internal set; }

    /// <summary>Gets the heading level, saturated at Int32.MaxValue; zero otherwise. HeadingLevelDigits retains the exact numbered level.</summary>
    public int HeadingLevel => NumberedHeadings.GetLevel(this);

    /// <summary>Gets the exact decimal heading level without a numeric-size limit; empty for an unnumbered heading or non-heading.</summary>
    public ReadOnlySpan<char> HeadingLevelDigits
    {
        get
        {
            var name = NumberedHeadings.GetName(this);
            return name.Length == 0 ? [] : name.AsSpan(1);
        }
    }

    /// <summary>Gets what the element means to a reader.</summary>
    public PdfSemanticRole Role => PdfStructureTypes.GetRole(Type);

    /// <summary>Gets the kids, in logical order, as a span that can be walked without allocating an enumerator. The tree never changes after it is built.</summary>
    internal ReadOnlySpan<PdfStructureKid> KidSpan => CollectionsMarshal.AsSpan(_kids);

    /// <summary>Adds a kid.</summary>
    /// <param name="kid">The kid.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddKid(in PdfStructureKid kid) => _kids.Add(kid);

    /// <summary>Records a recognised subtype during tree construction.</summary>
    /// <param name="type">The parsed subtype.</param>
    internal void SetNoteType(PdfNoteType type) => _noteType = (byte)type;
}
