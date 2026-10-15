// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>Somewhere new indirect objects can be put: a new document being built or an open document being edited.</summary>
public interface IPdfObjectSink
{
    /// <summary>Gets the name table the document's names come from.</summary>
    PdfNameTable Names { get; }

    /// <summary>Gets the open document the objects belong to, or <see langword="null"/> for a document being built.</summary>
    PdfObjectStore? Owner { get; }

    /// <summary>Gets the embedding state shared by sinks writing the same document.</summary>
    PdfFontEmbeddingState FontEmbedding { get; }

    /// <summary>Gets existing embedding state, or null when no font has been embedded.</summary>
    PdfFontEmbeddingState? ExistingFontEmbedding { get; }

    /// <summary>Reserves an object number to fill in later with <see cref="Set"/>, so objects can refer to each other.</summary>
    /// <returns>The new object's id.</returns>
    PdfObjectId Reserve();

    /// <summary>Adds an object.</summary>
    /// <param name="value">The value; a stream is written with its data.</param>
    /// <returns>The new object's id.</returns>
    PdfObjectId Add(PdfValue value);

    /// <summary>Sets the value of an object.</summary>
    /// <param name="id">The object id, from <see cref="Reserve"/> or <see cref="Add"/>.</param>
    /// <param name="value">The value.</param>
    void Set(PdfObjectId id, PdfValue value);
}
