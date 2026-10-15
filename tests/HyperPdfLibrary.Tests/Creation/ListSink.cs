// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>An object sink that keeps its objects in a list, so tests can read back what a writer added.</summary>
[DebuggerDisplay("ListSink: {_values.Count} objects")]
internal sealed class ListSink : IPdfObjectSink
{
    /// <summary>The objects, where index <c>n - 1</c> holds object <c>n</c>.</summary>
    private readonly List<PdfValue> _values = [];

    /// <summary>The optional embedding state.</summary>
    private PdfFontEmbeddingState? _fontEmbedding;

    /// <inheritdoc/>
    public PdfNameTable Names { get; } = new();

    /// <inheritdoc/>
    public PdfObjectStore? Owner => null;

    /// <inheritdoc/>
    public PdfFontEmbeddingState FontEmbedding => _fontEmbedding ??= new();

    /// <inheritdoc/>
    public PdfFontEmbeddingState? ExistingFontEmbedding => _fontEmbedding;

    /// <inheritdoc/>
    public PdfObjectId Reserve()
    {
        _values.Add(default);
        return new(_values.Count, 0);
    }

    /// <inheritdoc/>
    public PdfObjectId Add(PdfValue value)
    {
        _values.Add(value);
        return new(_values.Count, 0);
    }

    /// <inheritdoc/>
    public void Set(PdfObjectId id, PdfValue value) => _values[id.Number - 1] = value;

    /// <summary>Gets an object's value.</summary>
    /// <param name="id">The object.</param>
    /// <returns>The value.</returns>
    internal PdfValue Get(PdfObjectId id) => _values[id.Number - 1];

    /// <summary>Gets an object that is a stream.</summary>
    /// <param name="id">The object.</param>
    /// <returns>The stream.</returns>
    internal PdfStream GetStream(PdfObjectId id) => _values[id.Number - 1].AsStream()!;

    /// <summary>Gets an object that is a dictionary.</summary>
    /// <param name="id">The object.</param>
    /// <returns>The dictionary.</returns>
    internal PdfDictionary GetDictionary(PdfObjectId id) => _values[id.Number - 1].AsDictionary()!;

    /// <summary>Follows a value that is a reference to its object.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The object's value, or the value itself when it is not a reference.</returns>
    internal PdfValue Resolve(PdfValue value) => value.IsReference ? Get(value.AsReference()) : value;
}
