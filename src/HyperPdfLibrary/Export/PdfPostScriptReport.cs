// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Export;

/// <summary>What a PostScript export left out or drew differently, because PostScript cannot say it the way the page does.</summary>
/// <param name="PagesWritten">The pages written.</param>
/// <param name="Skipped">The things left out: tiling patterns, mesh and function shadings, patterns used as a stroke or on text, and images whose pixels could not be read.</param>
/// <param name="Approximated">The things drawn differently: translucent colours lightened towards white, and blend modes, soft masks, group opacity and text used as a clip not applied.</param>
[DebuggerDisplay("PdfPostScriptReport: {PagesWritten} pages, {Skipped} skipped, {Approximated} approximated")]
public readonly record struct PdfPostScriptReport(int PagesWritten, int Skipped, int Approximated)
{
    /// <summary>Gets a value indicating whether the output matches the page exactly.</summary>
    public bool IsExact => Skipped == 0 && Approximated == 0;
}
