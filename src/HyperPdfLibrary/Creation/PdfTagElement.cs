// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Creation;

/// <summary>A structure element of a tagged document being created (ISO 32000-2 14.7.2). Make one with <see cref="PdfTagTree.AddElement(PdfTagElement, string)"/>.</summary>
[DebuggerDisplay("PdfTagElement: {Type}, {_kids.Count} kids")]
public sealed class PdfTagElement
{
    /// <summary>The element's children and content, in reading order.</summary>
    private readonly List<Kid> _kids = [];

    /// <summary>Initializes a new instance of the <see cref="PdfTagElement"/> class.</summary>
    /// <param name="id">The element's object.</param>
    /// <param name="type">The structure type.</param>
    /// <param name="parent">The parent element, or <see langword="null"/> for the root.</param>
    /// <param name="options">What the element says besides its type.</param>
    internal PdfTagElement(PdfObjectId id, string type, PdfTagElement? parent, PdfTagOptions options)
    {
        Id = id;
        Type = type;
        TypeUtf8 = Encoding.ASCII.GetBytes(type);
        Parent = parent;
        Options = options;
    }

    /// <summary>Gets the element's object.</summary>
    public PdfObjectId Id { get; }

    /// <summary>Gets the structure type, such as <c>P</c>, <c>H1</c> or <c>Figure</c>.</summary>
    public string Type { get; }

    /// <summary>Gets the parent element, or <see langword="null"/> for the document element.</summary>
    public PdfTagElement? Parent { get; }

    /// <summary>Gets what the element says besides its type.</summary>
    public PdfTagOptions Options { get; }

    /// <summary>Gets the page of the element's first marked content, or an invalid id when it has none yet.</summary>
    internal PdfObjectId Page { get; private set; }

    /// <summary>Gets the structure type as ASCII bytes, the tag its marked content is written with.</summary>
    internal byte[] TypeUtf8 { get; }

    /// <summary>Gets the element's children and content, in reading order.</summary>
    internal List<Kid> Kids => _kids;

    /// <summary>Adds a child element.</summary>
    /// <param name="child">The child.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddChild(PdfTagElement child) => _kids.Add(new(child, 0, default, default));

    /// <summary>Adds a piece of marked content.</summary>
    /// <param name="page">The page the content is on.</param>
    /// <param name="mcid">The marked content id, unique on the page.</param>
    internal void AddContent(PdfObjectId page, int mcid)
    {
        if (!Page.IsValid)
        {
            Page = page;
        }

        _kids.Add(new(null, mcid, page, default));
    }

    /// <summary>Adds a reference to an annotation, such as a link, that belongs to the element.</summary>
    /// <param name="page">The page the annotation is on.</param>
    /// <param name="annotation">The annotation's object.</param>
    internal void AddAnnotation(PdfObjectId page, PdfObjectId annotation)
    {
        if (!Page.IsValid)
        {
            Page = page;
        }

        _kids.Add(new(null, 0, page, annotation));
    }

    /// <summary>A child element, a piece of marked content or a reference to an annotation.</summary>
    /// <param name="Element">The child element, or <see langword="null"/> for marked content or an annotation.</param>
    /// <param name="Mcid">The marked content id, when <paramref name="Element"/> is <see langword="null"/> and there is no annotation.</param>
    /// <param name="Page">The page of the marked content or annotation.</param>
    /// <param name="Annotation">The annotation's object, or an invalid id when the kid is not an annotation.</param>
    internal readonly record struct Kid(PdfTagElement? Element, int Mcid, PdfObjectId Page, PdfObjectId Annotation);
}
