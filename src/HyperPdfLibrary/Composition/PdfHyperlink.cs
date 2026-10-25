// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>Text that jumps somewhere when it is clicked. The text takes the Hyperlink style unless it has its own formatting.</summary>
/// <param name="Target">Where the link goes.</param>
/// <param name="Content">The text that is the link.</param>
[DebuggerDisplay("PdfHyperlink: {Target}")]
public sealed record PdfHyperlink(PdfLinkTarget Target, IReadOnlyList<PdfInline> Content) : PdfInline
{
    /// <summary>Gets a description of the link for people who cannot see where it goes; it is the link's alternative text in a tagged document.</summary>
    public string? Description { get; init; }

    /// <summary>Makes a link whose text is one run.</summary>
    /// <param name="target">Where the link goes.</param>
    /// <param name="text">The link's text.</param>
    /// <returns>The link.</returns>
    public static PdfHyperlink To(PdfLinkTarget target, string text) => new(target, [new PdfRun(text)]);
}
