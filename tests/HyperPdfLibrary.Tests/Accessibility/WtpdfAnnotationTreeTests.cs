// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks the Well-Tagged PDF rules for annotations that sit in the structure tree.</summary>
public sealed class WtpdfAnnotationTreeTests
{
    /// <summary>A highlight is enclosed in an Annot tag, a popup is not in the tree, and a widget has a label or a Contents.</summary>
    /// <param name="holder">The tag type that holds the annotation.</param>
    /// <param name="holderExtra">Extra entries on the holding tag.</param>
    /// <param name="annotation">The annotation's subtype and extra entries.</param>
    /// <param name="clause">The clause that is tested.</param>
    /// <param name="fails">Whether the file breaks the clause.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("P", "", "/Subtype /Highlight", "8.9.2.3", true)]
    [Arguments("Annot", "", "/Subtype /Highlight", "8.9.2.3", false)]
    [Arguments("P", "", "/Subtype /Popup", "8.9.2.4.9", true)]
    [Arguments("Annot", "/Alt (Note)", "/Subtype /Square /Contents (Other)", "8.9.4.2", true)]
    [Arguments("Annot", "/Alt (Note)", "/Subtype /Square /Contents (Note)", "8.9.4.2", false)]
    [Arguments("Form", "", "/Subtype /Widget /FT /Btn /T (b)", "8.10.2.3", true)]
    [Arguments("Form", "", "/Subtype /Widget /FT /Btn /T (b) /Contents (Send)", "8.10.2.3", false)]
    [Arguments("Form", "", "/Subtype /Widget /FT /Btn /T (b) /AA << >>", "8.10.2.3", true)]
    [Arguments("P", "/Alt <FEFFE000>", "/Subtype /Link /Contents (x)", "8.6", true)]
    [Arguments("P", "/Alt (A plain note)", "/Subtype /Link /Contents (x)", "8.6", false)]
    public async Task AnnotationRulesNameTheirClause(string holder, string holderExtra, string annotation, string clause, bool fails, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(holder, holderExtra, annotation), null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        await Assert.That(report.Findings.Any(f => f.Clause == clause)).IsEqualTo(fails);
    }

    /// <summary>Builds a PDF 2.0 file whose Document holds one tag that holds one annotation, which is also on the page.</summary>
    /// <param name="holder">The tag type that holds the annotation.</param>
    /// <param name="holderExtra">Extra entries on the holding tag.</param>
    /// <param name="annotation">The annotation's subtype and extra entries.</param>
    /// <returns>The file.</returns>
    internal static byte[] Build(string holder, string holderExtra, string annotation) => MiniPdf.Build(
    [
        "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> /Version /2.0 /Metadata 8 0 R /Lang (en-US) >>",
        "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Tabs /S /Annots [9 0 R] >>",
        "<< /Type /StructElem /S /Document /NS 7 0 R /P 5 0 R /K [6 0 R] >>",
        "<< /Type /StructTreeRoot /K 4 0 R /Namespaces [7 0 R] /ParentTree << /Nums [0 6 0 R] >> >>",
        $"<< /Type /StructElem /S /{holder} /NS 7 0 R /P 4 0 R /Pg 3 0 R /K [<< /Type /OBJR /Obj 9 0 R /Pg 3 0 R >>] {holderExtra} >>",
        "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>",
        MiniPdf.Stream("/Type /Metadata /Subtype /XML", AccessibilityPdfs.Packet(PdfUaClaim.Part2, true, "2024")),
        $"<< /Type /Annot /Rect [0 0 10 10] /StructParent 0 {annotation} >>",
    ]);
}
