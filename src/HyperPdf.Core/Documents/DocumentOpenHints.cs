// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Printing;

namespace HyperPdf.Core.Documents;

/// <summary>
/// What a document asks of the viewer when it opens: where to start, which panel and page arrangement to show, and how to
/// print. These are hints; the user's own settings decide whether each one is followed.
/// </summary>
/// <param name="OpenPage">The zero-based page the document's <c>/OpenAction</c> goes to, or -1 when it names none.</param>
/// <param name="Panel">The side panel the document asks for.</param>
/// <param name="Layout">The page arrangement the document asks for.</param>
/// <param name="RightToLeft">Whether the document's spreads read right to left (<c>/Direction R2L</c>).</param>
/// <param name="DisplayDocTitle">Whether the document asks for its title, rather than its file name, in the window title.</param>
/// <param name="Print">The print defaults the document asks for.</param>
/// <param name="IsTagged">Whether the document is marked as a tagged PDF.</param>
[DebuggerDisplay("DocumentOpenHints: page {OpenPage} {Panel} {Layout}")]
public sealed record DocumentOpenHints(
    int OpenPage,
    OpenPanel Panel,
    PageLayoutMode Layout,
    bool RightToLeft,
    bool DisplayDocTitle,
    PrintDefaults Print,
    bool IsTagged)
{
    /// <summary>Gets hints that ask for nothing.</summary>
    public static DocumentOpenHints None { get; } = new(-1, OpenPanel.None, PageLayoutMode.Single, false, false, PrintDefaults.None, false);
}
