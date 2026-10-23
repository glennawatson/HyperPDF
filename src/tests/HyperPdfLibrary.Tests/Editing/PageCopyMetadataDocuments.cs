// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Builds and reads the documents for the tests of the metadata a page copy carries.</summary>
internal static class PageCopyMetadataDocuments
{
    /// <summary>The name of the first calculated field.</summary>
    internal const string FirstField = "First";

    /// <summary>The name of the second calculated field.</summary>
    internal const string SecondField = "Second";

    /// <summary>The name of the first layer.</summary>
    internal const string FirstLayer = "Alpha";

    /// <summary>The name of the second layer.</summary>
    internal const string SecondLayer = "Beta";

    /// <summary>The left edge of the heading text.</summary>
    private const int TextLeft = 72;

    /// <summary>The baseline of the heading text.</summary>
    private const int TextBaseline = 700;

    /// <summary>The page tree of a two page source.</summary>
    private const string TwoPageTree = "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>";

    /// <summary>The XFA template packet.</summary>
    private const string XfaPacket = "<xdp:xdp xmlns:xdp=\"http://ns.adobe.com/xdp/\"/>";

    /// <summary>Creates a form with two calculated text fields and a signature field on page 1, and one calculated field on page 2.</summary>
    /// <returns>The file bytes.</returns>
    internal static byte[] CreateCalculatingForm() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /AcroForm 5 0 R >>",
        TwoPageTree,
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [6 0 R 7 0 R 8 0 R] >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [9 0 R] >>",
        "<< /Fields [6 0 R 7 0 R 8 0 R 9 0 R] /SigFlags 3 /CO [7 0 R 6 0 R 9 0 R] /XFA [(template) 10 0 R] /DA (/Helv 0 Tf 0 g) >>",
        "<< /Type /Annot /Subtype /Widget /FT /Tx /T (First) /Rect [10 700 100 720] /F 4 /P 3 0 R /AA << /C << /S /JavaScript /JS (event.value = 1;) >> >> >>",
        "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Second) /Rect [10 660 100 680] /F 4 /P 3 0 R /AA << /C << /S /JavaScript /JS (event.value = 2;) >> >> >>",
        "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Signed) /Rect [10 620 100 640] /F 4 /P 3 0 R >>",
        "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Third) /Rect [10 700 100 720] /F 4 /P 4 0 R /AA << /C << /S /JavaScript /JS (event.value = 3;) >> >> >>",
        MiniPdf.Stream(string.Empty, XfaPacket));

    /// <summary>Creates a target with one calculated field, signatures and XFA data next to its standard field.</summary>
    /// <returns>The file bytes.</returns>
    internal static byte[] CreateXfaTarget() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /AcroForm 4 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [5 0 R] >>",
        "<< /Fields [5 0 R] /SigFlags 1 /CO [5 0 R] /XFA [(template) 6 0 R] >>",
        "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Own) /Rect [10 700 100 720] /F 4 /P 3 0 R /AA << /C << /S /JavaScript /JS (event.value = 0;) >> >> >>",
        MiniPdf.Stream(string.Empty, XfaPacket));

    /// <summary>Creates a target whose form is only XFA data, with no standard fields to fall back on.</summary>
    /// <returns>The file bytes.</returns>
    internal static byte[] CreatePureXfaTarget() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /AcroForm 4 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
        "<< /Fields [] /XFA [(template) 5 0 R] >>",
        MiniPdf.Stream(string.Empty, XfaPacket));

    /// <summary>Creates a source with three layers on two pages, one radio-button group of all three, one of two, and two automatic states.</summary>
    /// <returns>The file bytes.</returns>
    internal static byte[] CreateRadioLayers() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /OCProperties 7 0 R >>",
        TwoPageTree,
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 9 0 R /Resources << /Properties << /A 5 0 R /B 6 0 R >> >> >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 9 0 R /Resources << /Properties << /C 8 0 R >> >> >>",
        "<< /Type /OCG /Name (Alpha) >>",
        "<< /Type /OCG /Name (Beta) >>",
        "<< /OCGs [5 0 R 6 0 R 8 0 R] /D << /OFF [6 0 R 8 0 R] /RBGroups [[5 0 R 6 0 R 8 0 R] [5 0 R 8 0 R]] "
        + "/AS [<< /Event /View /Category [/View] /OCGs [5 0 R 6 0 R] >> << /Event /Print /Category [/Print] /OCGs [8 0 R] >>] >> >>",
        "<< /Type /OCG /Name (Gamma) >>",
        MiniPdf.Stream(string.Empty, "/OC /A BDC EMC /OC /B BDC EMC"));

    /// <summary>Creates a source with three links whose actions chain go-to actions, one of them for the page that is left out.</summary>
    /// <returns>The file bytes.</returns>
    internal static byte[] CreateChainedActions() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        TwoPageTree,
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [5 0 R 6 0 R 7 0 R] >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
        "<< /Type /Annot /Subtype /Link /Rect [10 600 100 620] /Border [0 0 0] /A << /S /URI /URI (http://example.com/one) /Next << /S /GoTo /D [4 0 R /Fit] >> >> >>",
        "<< /Type /Annot /Subtype /Link /Rect [10 560 100 580] /Border [0 0 0] /A << /S /GoTo /D [3 0 R /Fit] "
        + "/Next [<< /S /GoTo /D [4 0 R /Fit] >> << /S /URI /URI (http://example.com/two) >>] >> >>",
        "<< /Type /Annot /Subtype /Link /Rect [10 520 100 540] /Border [0 0 0] /A << /S /URI /URI (http://example.com/three) "
        + "/Next << /S /GoTo /D [3 0 R /Fit] /Next << /S /URI /URI (http://example.com/four) >> >> >> >>");

    /// <summary>Creates a tagged page whose outline has an entry for its heading and an entry that only names its figure's element.</summary>
    /// <returns>The file bytes.</returns>
    internal static byte[] CreateTaggedOutline()
    {
        var pdf = new TaggedPdfBuilder { Content = $"{TaggedPdfBuilder.Text("Heading1", 0, TextLeft, TextBaseline, "Title")}/Figure <</MCID 1>> BDC 0 0 0 rg 72 400 100 50 re f EMC\n" };
        var root = pdf.Reserve();
        var document = pdf.Reserve();
        var heading = pdf.Element("Heading1", document, "/K 0");
        var figure = pdf.Element("Figure", document, "/K 1 /Alt (A bar)");
        var parents = pdf.Add(Format($"<< /Nums [0 [{heading} 0 R {figure} 0 R]] >>"));
        pdf.Set(document, Format($"<< /Type /StructElem /S /Document /P {root} 0 R /K [{heading} 0 R {figure} 0 R] >>"));
        pdf.Set(root, Format($"<< /Type /StructTreeRoot /K {document} 0 R /ParentTree {parents} 0 R /RoleMap << /Heading1 /H1 >> >>"));
        var outline = pdf.Reserve();
        var first = pdf.Reserve();
        var second = pdf.Add(Format($"<< /Title (Figure) /Parent {outline} 0 R /Prev {first} 0 R /SE {figure} 0 R >>"));
        pdf.Set(first, Format($"<< /Title (Title) /Parent {outline} 0 R /Next {second} 0 R /Dest [{TaggedPdfBuilder.PageNumber} 0 R /Fit] /SE {heading} 0 R >>"));
        pdf.Set(outline, Format($"<< /Type /Outlines /First {first} 0 R /Last {second} 0 R /Count 2 >>"));
        pdf.Catalog = Format($"/MarkInfo << /Marked true >> /StructTreeRoot {root} 0 R /Outlines {outline} 0 R");
        return pdf.Build();
    }

    /// <summary>Reads the names of the fields a calculation order lists.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The field names, in calculation order.</returns>
    internal static List<string> ReadCalculationOrder(PdfDocument document)
    {
        var names = new List<string>();
        var order = document.Catalog.GetDictionary(KnownName.AcroForm)!.GetArray(KnownName.CO)!;
        for (var i = 0; i < order.Count; i++)
        {
            names.Add(order.GetDictionary(i)!.GetText(KnownName.T)!);
        }

        return names;
    }

    /// <summary>Reads the names of the layers in an array of group references.</summary>
    /// <param name="groups">The array.</param>
    /// <returns>The layer names, in order.</returns>
    internal static List<string> ReadLayerNames(PdfArray groups)
    {
        var names = new List<string>();
        for (var i = 0; i < groups.Count; i++)
        {
            names.Add(groups.GetDictionary(i)!.GetText(KnownName.Name)!);
        }

        return names;
    }

    /// <summary>Formats an interpolated PDF snippet with the invariant culture.</summary>
    /// <param name="text">The snippet.</param>
    /// <returns>The formatted snippet.</returns>
    private static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
