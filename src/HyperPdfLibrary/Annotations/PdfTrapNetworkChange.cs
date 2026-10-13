// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Annotations;

/// <summary>
/// What a save or page copy did with one page's trap network: the traps a prepress program added so colours still meet
/// when a press prints them slightly out of line. HyperPDF cannot make new traps, so a network that no longer matches its
/// page is removed; tell the user with <see cref="Message"/> when <see cref="Action"/> is not <see cref="PdfTrapNetworkAction.Repaired"/>.
/// </summary>
/// <param name="PageIndex">The zero based index of the page; for a page copy, the index of the source page.</param>
/// <param name="Action">What was done.</param>
/// <param name="Problems">What was wrong with the network.</param>
[DebuggerDisplay("PdfTrapNetworkChange: page {PageIndex}, {Action}, {Problems}")]
public sealed record PdfTrapNetworkChange(int PageIndex, PdfTrapNetworkAction Action, PdfTrapNetworkProblems Problems)
{
    /// <summary>Gets what was done and why, in plain words for the user.</summary>
    public string Message => PdfTrapNetworkText.Describe(PageIndex, Action, Problems);
}
