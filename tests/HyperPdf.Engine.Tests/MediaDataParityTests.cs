// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using TUnit.Assertions.Enums;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Checks that XFA packet names and bytes match PDFium's <c>FPDF_GetXFAPacket*</c>, and that document script names and text match
/// <c>FPDFDoc_GetJavaScriptAction*</c> (ISO 32000-2 §12.7.8, §12.6.4.17). Nothing runs. The single-stream XFA name difference is
/// recorded in <see cref="PdfiumReferenceParityTests"/>.
/// </summary>
public sealed class MediaDataParityTests
{
    /// <summary>The number of packets in the synthetic XFA array.</summary>
    private const int PacketCount = 4;

    /// <summary>The number of scripts in the synthetic name tree.</summary>
    private const int ScriptCount = 3;

    /// <summary>Gets a document with an XFA array of four packets, one of them with binary and non-ASCII bytes, and a name tree of three scripts.</summary>
    private static byte[] Synthetic => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /Names << /JavaScript 6 0 R >> "
        + "/AcroForm << /Fields [] /XFA [(preamble) 4 0 R (template) 5 0 R (datasets) 12 0 R (postamble) 13 0 R] >> >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
        MiniPdf.Stream(string.Empty, "<xdp:xdp xmlns:xdp=\"http://ns.adobe.com/xdp/\">"),
        MiniPdf.Stream(string.Empty, "<template>éÿ</template>"),
        "<< /Names [(alpha) 7 0 R (beta) 8 0 R (gamma) 9 0 R] >>",
        "<< /S /JavaScript /JS (this.alpha = 1;) >>",
        "<< /S /JavaScript /JS 10 0 R >>",
        "<< /S /JavaScript /JS 11 0 R >>",
        MiniPdf.Stream(string.Empty, "var beta = 2;\nvar gamma = 3;"),
        MiniPdf.Stream("/Filter /ASCIIHexDecode", "76617220673D313B>"),
        MiniPdf.Stream(string.Empty, "<xfa:datasets xmlns:xfa=\"http://www.xfa.org/schema/xfa-data/1.0/\"><xfa:data><a>1</a></xfa:data></xfa:datasets>"),
        MiniPdf.Stream(string.Empty, "</xdp:xdp>"));

    /// <summary>The synthetic XFA array gives the same packet names and bytes in HyperPDF as in PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SyntheticXfaPacketsMatch()
    {
        using var pair = new EnginePair(Synthetic);

        var expected = PdfiumReferenceXfa.GetPackets(pair.Pdfium);
        var actual = PdfDocumentXfa.GetXfa(pair.Library)!.Packets;

        await Assert.That(expected.Count).IsEqualTo(PacketCount);
        await Assert.That(actual.Select(static packet => packet.Name)).IsEquivalentTo(expected.Select(static packet => packet.Name), CollectionOrdering.Matching);
        for (var i = 0; i < expected.Count; i++)
        {
            await Assert.That(actual[i].Data).IsEquivalentTo(expected[i].Content, CollectionOrdering.Matching);
        }
    }

    /// <summary>The synthetic name tree gives the same script names and text in HyperPDF as in PDFium, whether the script is a string or a stream.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SyntheticScriptsMatch()
    {
        using var pair = new EnginePair(Synthetic);

        var expected = PdfiumReferenceScripts.GetDocumentScripts(pair.Pdfium);
        var actual = PdfDocumentActions.GetDocumentScripts(pair.Library);

        await Assert.That(PdfiumReferenceScripts.GetDocumentScriptCount(pair.Pdfium)).IsEqualTo(ScriptCount);
        await AssertScriptsMatchAsync(expected, actual);
    }

    /// <summary>The downloaded form samples give the same scripts and XFA packets in HyperPDF as in PDFium.</summary>
    /// <param name="id">The reference PDF id.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("pdfjs-textfields")]
    [Arguments("pdfjs-js-buttons")]
    public async Task ReferenceFormScriptsAndXfaMatch(string id)
    {
        using var pair = new EnginePair(ReferencePdfFile.Read(id));

        var expectedPackets = PdfiumReferenceXfa.GetPackets(pair.Pdfium);
        var actualPackets = PdfDocumentXfa.GetXfa(pair.Library)?.Packets ?? [];

        await AssertScriptsMatchAsync(PdfiumReferenceScripts.GetDocumentScripts(pair.Pdfium), PdfDocumentActions.GetDocumentScripts(pair.Library));
        await Assert.That(actualPackets.Count).IsEqualTo(expectedPackets.Count);
        for (var i = 0; i < expectedPackets.Count; i++)
        {
            await Assert.That(actualPackets[i].Data).IsEquivalentTo(expectedPackets[i].Content, CollectionOrdering.Matching);
        }
    }

    /// <summary>Compares script names and text.</summary>
    /// <param name="expected">PDFium's scripts.</param>
    /// <param name="actual">HyperPDF's scripts.</param>
    /// <returns>A task.</returns>
    private static async Task AssertScriptsMatchAsync(List<PdfiumNamedScript> expected, HyperPdfLibrary.Navigation.PdfNamedScript[] actual)
    {
        await Assert.That(actual.Select(static script => script.Name)).IsEquivalentTo(expected.Select(static script => script.Name), CollectionOrdering.Matching);
        await Assert.That(actual.Select(static script => script.Script)).IsEquivalentTo(expected.Select(static script => script.Script), CollectionOrdering.Matching);
    }
}
