// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Redaction;

/// <summary>What applying redactions removes and cleans up.</summary>
/// <param name="Images">What happens to pictures a mark touches.</param>
/// <param name="LineArt">What happens to drawings a mark touches.</param>
/// <param name="RemoveHiddenText">Whether invisible text under a mark, such as a scan's text layer, is removed too.</param>
/// <param name="RemoveLinksAndComments">Whether links, comments and form fields under a mark are removed.</param>
/// <param name="ScrubMetadata">Whether the document information and metadata are removed.</param>
[DebuggerDisplay("RedactionSettings: images {Images}, line art {LineArt}")]
public sealed record RedactionSettings(
    RedactionImageChoice Images,
    RedactionLineArtChoice LineArt,
    bool RemoveHiddenText,
    bool RemoveLinksAndComments,
    bool ScrubMetadata)
{
    /// <summary>Gets the safe settings: blank pictures under a mark, remove covered drawings, hidden text, links and comments.</summary>
    public static RedactionSettings Default { get; } = new(RedactionImageChoice.BlankPart, RedactionLineArtChoice.RemoveCovered, true, true, false);
}
