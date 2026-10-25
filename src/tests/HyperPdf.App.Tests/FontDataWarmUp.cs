// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;

namespace HyperPdf.App.Tests;

/// <summary>
/// Prepares the substitute faces of the standard 14 text fonts once per test run, before any test starts. The tests open
/// documents in Helvetica, Times and Courier without embedding them, and the synchronous text and render paths only read font
/// data that is already in the font data folder. Without this step a test passes only when an earlier test, or the app's
/// own asynchronous page load, filled the folder first. The step uses the library's public asynchronous text reading, which
/// prepares the fonts of a page before it reads them, and downloads the open font files from the pinned upstream revisions
/// the library uses when the folder does not hold them yet.
/// </summary>
internal static class FontDataWarmUp
{
    /// <summary>The width and height of the warm-up page in points.</summary>
    private const float PageSize = 612;

    /// <summary>The size of the text in points.</summary>
    private const float FontSize = 12;

    /// <summary>The distance between the lines of text in points.</summary>
    private const float LineHeight = 20;

    /// <summary>The distance of the first line from the page bottom in points.</summary>
    private const float FirstLine = 560;

    /// <summary>The text drawn in each font.</summary>
    private const string Sample = "Warm up";

    /// <summary>The standard 14 fonts that have an appearance font, covering each bundled substitute face.</summary>
    private static readonly AppearanceFont[] Fonts = Enum.GetValues<AppearanceFont>();

    /// <summary>Draws a line of text in each font on one page, then reads the page text the asynchronous way.</summary>
    /// <param name="cancellationToken">Cancels the preparation when the test session ends.</param>
    /// <returns>A task completing when the font data is in the font data folder.</returns>
    [Before(TestSession)]
    public static async Task PrepareAsync(CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var canvas = creator.BeginPage(PageSize, PageSize);
        try
        {
            var y = FirstLine;
            foreach (var font in Fonts)
            {
                canvas.DrawText(creator.AddBuiltInFont(font), FontSize, LineHeight, y, Sample);
                y -= LineHeight;
            }

            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        using var document = PdfDocumentReader.Open(creator.ToArray(), null);
        _ = await PdfDocumentText.GetTextPageAsync(document, 0, cancellationToken);
    }
}
