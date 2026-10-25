// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>Builds a configuration graph whose indirect objects lie outside the opening cache and on separate pages.</summary>
public static class OptionalContentPdf
{
    /// <summary>The padding at either end of the graph.</summary>
    private const int PaddingBytes = 5 * 1024 * 1024;

    /// <summary>The spacing between related indirect objects.</summary>
    private const int GapBytes = 128 * 1024;

    /// <summary>Creates named configurations with a prohibited indirect AS entry in the default configuration.</summary>
    /// <param name="metadata">The document's XMP packet.</param>
    /// <param name="indirectArray">Whether the Configs array is indirect.</param>
    /// <returns>The generated document bytes.</returns>
    public static byte[] Create(string metadata, bool indirectArray)
    {
        var padding = MiniPdf.Stream(string.Empty, new(' ', PaddingBytes));
        var gap = MiniPdf.Stream(string.Empty, new(' ', GapBytes));
        var configurations = indirectArray ? "9 0 R" : "[11 0 R]";
        return MiniPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R /Metadata 3 0 R /OCProperties 5 0 R >>",
            "<< /Type /Pages /Count 1 /Kids [17 0 R] >>",
            MiniPdf.Stream("/Type /Metadata /Subtype /XML", metadata),
            padding,
            $"<< /OCGs [] /D 7 0 R /Configs {configurations} >>",
            gap,
            "<< /Name 13 0 R /AS 15 0 R >>",
            gap,
            "[11 0 R]",
            gap,
            "<< /Name 13 0 R >>",
            gap,
            "(Configuration)",
            gap,
            "[]",
            padding,
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
        ]);
    }
}
