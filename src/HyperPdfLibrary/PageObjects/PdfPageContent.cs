// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Owns one page or form content stream and its pending edits.</summary>
[DebuggerDisplay("PdfPageContent: {Objects.Count} objects")]
public sealed class PdfPageContent
{
    /// <summary>The content as it was read.</summary>
    private readonly byte[] _source;

    /// <summary>The objects, in painting order.</summary>
    private readonly List<PdfPageObject> _objects = [];

    /// <summary>How deep this content is nested in forms.</summary>
    private readonly int _depth;

    /// <summary>The restores that had no matching save.</summary>
    private int _underflow;

    /// <summary>The saves left open at the end.</summary>
    private int _unclosed;

    /// <summary>How <see cref = "PdfPageContentApplication.Apply(PdfPageContent, PdfRegenerateMode)"/> writes the objects that were not changed.</summary>
    private PdfRegenerateMode _applyMode;

    /// <summary>Initializes a new instance of the <see cref = "PdfPageContent"/> class.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "page">The page, or <see langword="null"/> for a form's content.</param>
    /// <param name = "form">The form the content belongs to, or <see langword="null"/> for a page's.</param>
    /// <param name = "resources">The resources the content names.</param>
    /// <param name = "baseMatrix">The matrix from the content's space to user space.</param>
    /// <param name = "source">The decoded content.</param>
    /// <param name = "depth">How deep the content is nested in forms.</param>
    internal PdfPageContent(PdfDocument document, PdfPage? page, PdfFormObject? form, PdfDictionary? resources, Matrix3x2 baseMatrix, byte[] source, int depth)
    {
        Document = document;
        Page = page;
        FormObject = form;
        Resources = resources;
        BaseMatrix = baseMatrix;
        _source = source;
        _depth = depth;
    }

    /// <summary>Gets the document.</summary>
    public PdfDocument Document { get; }

    /// <summary>Gets the page, or <see langword="null"/> for the content of a form.</summary>
    public PdfPage? Page { get; }

    /// <summary>Gets the resources the content names; those a form inherits from the content that paints it when it has none.</summary>
    public PdfDictionary? Resources { get; }

    /// <summary>Gets the matrix from the content's space to user space: the identity for a page.</summary>
    public Matrix3x2 BaseMatrix { get; }

    /// <summary>Gets the objects, in painting order.</summary>
    public IReadOnlyList<PdfPageObject> Objects => _objects;

    /// <summary>Gets the decoded content as it was read.</summary>
    public ReadOnlySpan<byte> Source => _source;

    /// <summary>Gets the SourceBytes state.</summary>
    internal byte[] SourceBytes => _source;

    /// <summary>Gets the ObjectItems state.</summary>
    internal List<PdfPageObject> ObjectItems => _objects;

    /// <summary>Gets the Appended state.</summary>
    internal List<byte> Appended { get; } = [];

    /// <summary>Gets the bytes written before the page's own content, so what they paint lies behind it.</summary>
    internal List<byte> Prepended { get; } = [];

    /// <summary>Gets the Pending state.</summary>
    internal List<PendingResource> Pending { get; } = [];

    /// <summary>Gets the Depth state.</summary>
    internal int Depth => _depth;

    /// <summary>Gets the Underflow state.</summary>
    internal ref int Underflow => ref _underflow;

    /// <summary>Gets the Unclosed state.</summary>
    internal ref int Unclosed => ref _unclosed;

    /// <summary>Gets the form object this content belongs to, or <see langword="null"/> for a page.</summary>
    internal PdfFormObject? FormObject { get; }

    /// <summary>Gets the marked-content sequences, in the order their opening operators appear.</summary>
    internal List<PdfMark> Begins { get; } = [];

    /// <summary>Gets the ApplyMode state.</summary>
    internal ref PdfRegenerateMode ApplyMode => ref _applyMode;

    /// <summary>Gets the Generated state.</summary>
    internal List<PendingResource> Generated { get; } = [];
}
