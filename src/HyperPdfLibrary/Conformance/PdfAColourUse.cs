// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>How a colour space is used, which some PDF/A rules read.</summary>
/// <param name="OverprintMode">The overprint mode in effect, for painting operators; zero otherwise.</param>
/// <param name="Overprint">Whether overprinting is on for the paint, for painting operators.</param>
/// <param name="Frame">The content stream position, so a pattern can be walked; null outside content.</param>
internal readonly record struct PdfAColourUse(int OverprintMode, bool Overprint, PdfAContentFrame? Frame);
