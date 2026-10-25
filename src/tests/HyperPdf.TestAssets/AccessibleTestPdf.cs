// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>
/// Writes a one page tagged PDF that carries what PDF/UA asks assistive technology to see: a language change, a figure with a
/// description, a table with an empty cell, a running header marked as an artifact, form fields with tooltips and rich text, a
/// field in a layer that is switched off, annotations with descriptions, and a full XMP packet.
/// </summary>
public static class AccessibleTestPdf
{
    /// <summary>The running header, marked as an artifact.</summary>
    public static readonly string ArtifactText = "Quarterly Report";

    /// <summary>The description of the figure.</summary>
    public static readonly string FigureDescription = "Bar chart of quarterly sales";

    /// <summary>The text of the paragraph written in French.</summary>
    public static readonly string FrenchText = "Bonjour le monde";

    /// <summary>The language of the French paragraph.</summary>
    public static readonly string FrenchLanguage = "fr-FR";

    /// <summary>The document's language.</summary>
    public static readonly string DocumentLanguage = "en-US";

    /// <summary>The tooltip of the check box.</summary>
    public static readonly string CheckBoxLabel = "Accept the terms";

    /// <summary>The tooltip of the rich text field.</summary>
    public static readonly string RichFieldLabel = "Notes";

    /// <summary>The tooltip of the field in the layer that is switched off.</summary>
    public static readonly string HiddenFieldLabel = "Hidden option";

    /// <summary>The description of the note annotation.</summary>
    public static readonly string NoteText = "Check this figure";

    /// <summary>The description of the link annotation.</summary>
    public static readonly string LinkText = "Open the project site";

    /// <summary>Creates the page.</summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateRich() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /MarkInfo << /Marked true >> /StructTreeRoot 6 0 R /Lang (en-US) /Metadata 22 0 R "
        + "/AcroForm << /Fields [16 0 R 17 0 R 18 0 R] /DA (/Helv 0 Tf 0 g) >> /OCProperties << /OCGs [21 0 R] /D << /OFF [21 0 R] /Order [21 0 R] >> >> >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R /StructParents 0 /Annots [16 0 R 17 0 R 18 0 R 19 0 R 20 0 R] >>",
        MiniPdf.Stream(
            string.Empty,
            """
            /Artifact BMC BT /F1 9 Tf 72 770 Td (Quarterly Report) Tj ET EMC
            /H1 << /MCID 0 >> BDC BT /F1 20 Tf 72 700 Td (Overview) Tj ET EMC
            /P << /MCID 1 >> BDC BT /F1 12 Tf 72 670 Td (This page mixes two languages) Tj ET EMC
            /Span << /MCID 2 >> BDC BT /F1 12 Tf 72 650 Td (Bonjour le monde) Tj ET EMC
            /Figure << /MCID 3 >> BDC 100 500 100 100 re f EMC
            /TD << /MCID 4 >> BDC BT /F1 12 Tf 72 450 Td (A1) Tj ET EMC
            """),
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        "<< /Type /StructTreeRoot /K 7 0 R /ParentTree << /Nums [0 [8 0 R 9 0 R 10 0 R 11 0 R 14 0 R]] >> /ParentTreeNextKey 1 >>",
        "<< /Type /StructElem /S /Document /P 6 0 R /K [8 0 R 9 0 R 10 0 R 11 0 R 12 0 R] >>",
        "<< /Type /StructElem /S /H1 /P 7 0 R /Pg 3 0 R /K 0 >>",
        "<< /Type /StructElem /S /P /P 7 0 R /Pg 3 0 R /K 1 >>",
        "<< /Type /StructElem /S /P /P 7 0 R /Pg 3 0 R /K 2 /Lang (fr-FR) >>",
        "<< /Type /StructElem /S /Figure /P 7 0 R /Pg 3 0 R /K 3 /Alt (Bar chart of quarterly sales) >>",
        "<< /Type /StructElem /S /Table /P 7 0 R /K [13 0 R] >>",
        "<< /Type /StructElem /S /TR /P 12 0 R /K [14 0 R 15 0 R] >>",
        "<< /Type /StructElem /S /TD /P 13 0 R /Pg 3 0 R /K 4 >>",
        "<< /Type /StructElem /S /TD /P 13 0 R /Pg 3 0 R >>",
        "<< /Type /Annot /Subtype /Widget /FT /Btn /T (agree) /TU (Accept the terms) /V /Yes /AS /Yes /Rect [72 400 90 418] /F 4 /P 3 0 R >>",
        "<< /Type /Annot /Subtype /Widget /FT /Tx /T (notes) /TU (Notes) /Ff 4096 /V (Plain) "
        + "/RV (<?xml version=\"1.0\"?><body xmlns=\"http://www.w3.org/1999/xhtml\"><p>Plain <b>bold</b> and <i>italic</i></p></body>) "
        + "/DS (font: Helvetica 12pt) /Rect [72 350 300 380] /F 4 /P 3 0 R >>",
        "<< /Type /Annot /Subtype /Widget /FT /Btn /T (secret) /TU (Hidden option) /OC 21 0 R /Rect [72 300 90 318] /F 4 /P 3 0 R >>",
        "<< /Type /Annot /Subtype /Text /Rect [400 700 420 720] /Contents (Check this figure) /F 4 /P 3 0 R >>",
        "<< /Type /Annot /Subtype /Link /Rect [72 650 200 665] /Contents (Open the project site) /A << /S /URI /URI (https://example.com/) >> /F 4 /P 3 0 R >>",
        "<< /Type /OCG /Name (Secret layer) >>",
        MiniPdf.Stream(
            "/Type /Metadata /Subtype /XML",
            """
            <?xpacket begin="" id="W5M0MpCehiHzreSzNTczkc9d"?>
            <x:xmpmeta xmlns:x="adobe:ns:meta/"><rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
            <rdf:Description rdf:about="" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:pdfuaid="http://www.aiim.org/pdfua/ns/id/"
              xmlns:xmp="http://ns.adobe.com/xap/1.0/" xmlns:custom="http://example.com/ns/custom/">
            <dc:title><rdf:Alt><rdf:li xml:lang="x-default">Quarterly Report</rdf:li></rdf:Alt></dc:title>
            <dc:creator><rdf:Seq><rdf:li>Ann</rdf:li><rdf:li>Bob</rdf:li></rdf:Seq></dc:creator>
            <pdfuaid:part>1</pdfuaid:part>
            <xmp:CreatorTool>Tester</xmp:CreatorTool>
            <custom:Department>Finance</custom:Department>
            </rdf:Description></rdf:RDF></x:xmpmeta>
            <?xpacket end="w"?>
            """));
}
