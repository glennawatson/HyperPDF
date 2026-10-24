// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>Builds documents whose catalog asks the viewer to open a certain way.</summary>
public static class OpenHintSamples
{
    /// <summary>The pages of the generated documents.</summary>
    public static readonly int PageCount = 4;

    /// <summary>The title in the document's information dictionary.</summary>
    public static readonly string Title = "The Document Title";

    /// <summary>One page object, which draws the content stream that is object 7.</summary>
    private const string PageObject = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 7 0 R >>";

    /// <summary>
    /// Creates a four page document with the given catalog entries, such as <c>/PageMode /UseOutlines</c>. Page 1 is object 3,
    /// page 2 is object 4, page 3 is object 5 and page 4 is object 6. The information dictionary gives the title.
    /// </summary>
    /// <param name="catalogEntries">The entries added to the catalog.</param>
    /// <returns>The PDF bytes.</returns>
    public static byte[] Create(string catalogEntries) => MiniPdf.BuildWithTrailer(
        "/Info 8 0 R",
        $"<< /Type /Catalog /Pages 2 0 R {catalogEntries} >>",
        "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R 6 0 R] /Count 4 >>",
        PageObject,
        PageObject,
        PageObject,
        PageObject,
        MiniPdf.Stream(string.Empty, "0 g"),
        $"<< /Title ({Title}) >>");

    /// <summary>Creates a one page document with two bookmarks: a red bold one and a plain one.</summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateStyledOutline() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /Outlines 5 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>",
        "<< >>",
        "<< /Type /Outlines /First 6 0 R /Last 7 0 R /Count 2 >>",
        "<< /Title (Red) /Parent 5 0 R /Next 7 0 R /Dest [3 0 R /Fit] /C [1 0 0] /F 2 >>",
        "<< /Title (Plain) /Parent 5 0 R /Prev 6 0 R /Dest [3 0 R /Fit] >>");
}
