// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Creation;

/// <summary>A new document to create from text, images and pages of other PDFs.</summary>
/// <param name="Blocks">The content, in reading order.</param>
[DebuggerDisplay("DocumentCreationRequest: {Title}, {Blocks.Count} blocks, accessible {Accessible}")]
public sealed record DocumentCreationRequest(IReadOnlyList<CreationBlock> Blocks)
{
    /// <summary>Gets the title written to the document information and shown by viewers.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the author.</summary>
    public string? Author { get; init; }

    /// <summary>Gets the subject.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the keywords.</summary>
    public string? Keywords { get; init; }

    /// <summary>Gets the document language as a BCP 47 tag such as <c>en-AU</c>.</summary>
    public string? Language { get; init; }

    /// <summary>
    /// Gets a value indicating whether to tag the document and claim PDF/UA-2. The claim is left out, with a note in the result,
    /// when the document cannot meet it, for example when no font file is given or an image has no alternative text.
    /// </summary>
    public bool Accessible { get; init; }

    /// <summary>Gets the font to embed for text, or <see langword="null"/> to use Helvetica, which is not embedded and limits text to Windows ANSI characters.</summary>
    public CreationFont? Font { get; init; }

    /// <summary>Gets the page width in points; the default is A4.</summary>
    public float PageWidth { get; init; } = 595.28F;

    /// <summary>Gets the page height in points; the default is A4.</summary>
    public float PageHeight { get; init; } = 841.89F;

    /// <summary>Gets the margin on every side in points.</summary>
    public float Margin { get; init; } = 72F;

    /// <summary>Gets the size of body text in points.</summary>
    public float BodySize { get; init; } = 11F;

    /// <summary>Gets a value indicating whether each page of text gets its number at the foot.</summary>
    public bool PageNumbers { get; init; }
}
