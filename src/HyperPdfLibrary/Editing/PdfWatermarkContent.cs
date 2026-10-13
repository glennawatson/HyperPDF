// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>What a watermark draws, and what drawing it needs from the document.</summary>
internal abstract record PdfWatermarkContent
{
    /// <summary>Gets a value indicating whether the content has transparency of its own.</summary>
    internal abstract bool HasTransparency { get; }

    /// <summary>Gets a value indicating whether the content uses a font that is not embedded.</summary>
    internal abstract bool UsesUnembeddedFont { get; }

    /// <summary>Adds the content's form to the document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="group">The optional content group that controls the form, or an invalid id.</param>
    /// <returns>The form and its size in points.</returns>
    internal abstract PdfWatermarkForm Build(PdfDocument document, PdfEditTransaction transaction, PdfObjectId group);
}
