// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A named set of paragraph and character formatting. A style can be based on another and then only names what it changes.</summary>
/// <param name="Name">The style's name, which paragraphs and runs use to refer to it.</param>
[DebuggerDisplay("PdfStyle: {Name} based on {BasedOn}")]
public sealed record PdfStyle(string Name)
{
    /// <summary>Gets the name of the style this one is based on, or <see langword="null"/> when it is based on none.</summary>
    public string? BasedOn { get; init; }

    /// <summary>Gets the paragraph formatting.</summary>
    public PdfParagraphFormat? Paragraph { get; init; }

    /// <summary>Gets the character formatting.</summary>
    public PdfCharacterFormat? Character { get; init; }
}
