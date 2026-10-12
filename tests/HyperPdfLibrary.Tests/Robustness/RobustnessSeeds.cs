// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Writing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Documents the robustness tests start from.</summary>
internal static class RobustnessSeeds
{
    /// <summary>The page count of the multi-page seeds.</summary>
    private const int PageCount = 3;

    /// <summary>Gets the owner password of the AES-256 seed.</summary>
    internal static string OwnerPassword => "owner-secret";

    /// <summary>Builds the seed documents: plain, compressed, laid out, attached, encrypted, PDF 2.0, linearized and rewritten ones.</summary>
    /// <returns>The seeds.</returns>
    internal static List<Seed> Create()
    {
        var plain = TestPdf.Create(PageCount);
        var compressed = TestPdf.CreateCompressed();
        return [new(
        "basic",
        plain),
        new(
        "objstm",
        compressed),
        new(
        "layers",
        TestPdf.CreateWithLayers()),
        new(
        "attachment",
        TestPdf.CreateWithAttachment()),
        new(
        "links",
        TestPdf.CreateWithFileLinks()),
        new(
        "form",
        TestPdf.CreateForm()),
        new(
        "tagged",
        TestPdf.CreateTagged()),
        new(
        "viewport",
        TestPdf.CreateWithViewport()),
        new(
        "mini",
        CreateMini()),
        new(
        "encrypted",
        WritingTestDocuments.Encrypt(plain)),
        new(
        "aes256",
        Pdf2Documents.EncryptAes256(
        plain,
        string.Empty,
        OwnerPassword)),
        new(
        "pdf2",
        Pdf2Documents.CreateText()),
        new(
        "linearized",
        LinearizedDocuments.Create(false)),
        new(
        "linearized-streams",
        LinearizedDocuments.Create(true)),
        new(
        "compact",
        Compact(plain)),
        new(
        "compact-objstm",
        Compact(compressed)),
        ];
    }

    /// <summary>Builds a small document with labels, an outline, a link and an ASCIIHex stream.</summary>
    /// <returns>The file.</returns>
    internal static byte[] CreateMini() =>
        MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /Outlines 6 0 R /PageLabels << /Nums [0 << /S /r >> 1 << /S /D /P (A-) >>] >> >>",
        "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Resources << >> /Contents 5 0 R /Annots [7 0 R] >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 200] /Resources << >> /Rotate 90 >>",
        MiniPdf.Stream(
        "/Filter /ASCIIHexDecode",
        "48656C6C6F20776F726C64>"),
        "<< /Type /Outlines /First 8 0 R /Last 8 0 R /Count 1 >>",
        "<< /Type /Annot /Subtype /Link /Rect [0 0 100 50] /A << /S /URI /URI (http://example.com) >> >>",
        "<< /Title (Chapter) /Parent 6 0 R /Dest [3 0 R /Fit] >>");

    /// <summary>Rewrites a document with the compact writer.</summary>
    /// <param name="file">The document.</param>
    /// <returns>The rewritten file.</returns>
    private static byte[] Compact(byte[] file)
    {
        using var store = StoreOpening.Open(file, null);
        return PdfCompactWriter.Save(store, PdfCompactOptions.Default);
    }

    /// <summary>A named seed document.</summary>
    /// <param name="Name">The name, used in failure messages.</param>
    /// <param name="Bytes">The file.</param>
    internal sealed record Seed(string Name, byte[] Bytes);
}
