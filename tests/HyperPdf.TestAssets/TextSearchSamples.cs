// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>Builds one page documents whose text exercises accent and look-alike character search.</summary>
public static class TextSearchSamples
{
    /// <summary>The text of the accented page: accents, curly quotes and an en dash.</summary>
    public static readonly string AccentedText = "résumé “quoted” café 2024–25";

    /// <summary>Creates a one page document of <see cref="AccentedText"/> in Helvetica with WinAnsi encoding.</summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateAccentedPage() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 200] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        MiniPdf.Stream(string.Empty, "BT /F1 14 Tf 20 100 Td (r\\351sum\\351 \\223quoted\\224 caf\\351 2024\\22625) Tj ET"));
}
