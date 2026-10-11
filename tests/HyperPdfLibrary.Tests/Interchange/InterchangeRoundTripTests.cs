// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Interchange;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Interchange;

/// <summary>Exports a document, imports the file into a fresh copy and compares what the copy holds.</summary>
public sealed class InterchangeRoundTripTests
{
    /// <summary>The pages of the annotated sample.</summary>
    private const int SamplePages = 2;

    /// <summary>The markup annotations that each get a pop-up.</summary>
    private const int Popups = 4;

    /// <summary>A small rectangle.</summary>
    private const string Corner = "0,0,10,10";

    /// <summary>The option of the list box selected in the sample.</summary>
    private const int ListOption = 3;

    /// <summary>Every kind of annotation survives XFDF: the copy exports the same file, and the entries are in the document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationsRoundTripThroughXfdf()
    {
        using var original = Annotated();
        var xfdf = PdfInterchange.ExportXfdf(original);
        using var copy = PdfDocumentReader.Open(TestPdf.Create(SamplePages), null);

        var result = PdfInterchange.ImportXfdf(copy, xfdf);

        await Assert.That(result.AnnotationsAdded).IsEqualTo(InterchangeSamples.AnnotationCount);
        await Assert.That(result.AnnotationsSkipped).IsEqualTo(0);
        await Assert.That(Encoding.UTF8.GetString(PdfInterchange.ExportXfdf(copy))).IsEqualTo(Encoding.UTF8.GetString(xfdf));
        await Assert.That(Subtypes(copy, 0)).Contains("Highlight");
        await Assert.That(Subtypes(copy, 0).Count(static name => name == "Popup")).IsEqualTo(Popups);
        await Assert.That(Subtypes(copy, 1)).Contains("FileAttachment");
    }

    /// <summary>The same annotations survive FDF, and FDF and XFDF carry the same data.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationsRoundTripThroughFdf()
    {
        using var original = Annotated();
        var xfdf = PdfInterchange.ExportXfdf(original);
        var fdf = PdfInterchange.ExportFdf(original);
        using var copy = PdfDocumentReader.Open(TestPdf.Create(SamplePages), null);

        var result = PdfInterchange.ImportFdf(copy, fdf);

        await Assert.That(result.AnnotationsAdded).IsEqualTo(InterchangeSamples.AnnotationCount);
        await Assert.That(Encoding.UTF8.GetString(PdfInterchange.ExportXfdf(copy))).IsEqualTo(Encoding.UTF8.GetString(xfdf));
        await Assert.That(Encoding.UTF8.GetString(XfdfWriter.Write(FdfReader.Read(fdf)))).IsEqualTo(Encoding.UTF8.GetString(xfdf));
    }

    /// <summary>A reply is linked to the annotation it answers, a pop-up to its parent, and an attachment keeps its bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImportLinksRepliesPopupsAndAttachments()
    {
        using var original = Annotated();
        using var copy = PdfDocumentReader.Open(TestPdf.Create(SamplePages), null);
        _ = PdfInterchange.ImportXfdf(copy, PdfInterchange.ExportXfdf(original));

        var annotations = Annotations(copy, 0);
        var reply = annotations.Single(static annotation => annotation.GetText(KnownName.NM) == "reply-1");
        var highlight = annotations.Single(static annotation => annotation.GetText(KnownName.NM) == InterchangeSamples.HighlightName);
        var popup = highlight.GetDictionary(KnownName.Popup)!;
        var attachment = Annotations(copy, 1).Single(static annotation => annotation.GetText(KnownName.NM) == "attachment-1");
        var bytes = attachment.GetDictionary(copy.Objects.Names.Intern("FS"))!.GetDictionary(KnownName.EF)!.GetStream(KnownName.F)!.DecodeToArray();

        await Assert.That(PdfAnnotations.GetInReplyTo(reply)).IsSameReferenceAs(highlight);
        await Assert.That(reply.GetName(KnownName.RT).Is(KnownName.R)).IsTrue();
        await Assert.That(popup.GetDictionary(KnownName.Parent)).IsSameReferenceAs(highlight);
        await Assert.That(popup.GetBoolean(KnownName.Open)).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(bytes)).IsEqualTo(InterchangeSamples.AttachedText);
    }

    /// <summary>Importing the same file twice leaves one copy of each annotation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImportingTwiceReplacesByName()
    {
        using var original = Annotated();
        var xfdf = PdfInterchange.ExportXfdf(original);
        using var copy = PdfDocumentReader.Open(TestPdf.Create(SamplePages), null);
        _ = PdfInterchange.ImportXfdf(copy, xfdf);
        var before = Annotations(copy, 0).Count;

        var second = PdfInterchange.ImportXfdf(copy, xfdf);

        await Assert.That(second.AnnotationsReplaced).IsEqualTo(InterchangeSamples.AnnotationCount);
        await Assert.That(second.AnnotationsAdded).IsEqualTo(0);
        await Assert.That(Annotations(copy, 0).Count).IsGreaterThanOrEqualTo(before);
        await Assert.That(Encoding.UTF8.GetString(PdfInterchange.ExportXfdf(copy))).IsEqualTo(Encoding.UTF8.GetString(xfdf));
    }

    /// <summary>The imported annotations are saved by the incremental writer and read back from the saved file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImportedAnnotationsSurviveSaving()
    {
        using var original = Annotated();
        using var copy = PdfDocumentReader.Open(TestPdf.Create(SamplePages), null);
        _ = PdfInterchange.ImportXfdf(copy, PdfInterchange.ExportXfdf(original));

        using var reopened = PdfDocumentReader.Open(PdfIncrementalWriter.Save(copy.Objects), null);

        // Saving adds a file identifier, which the comparison leaves out.
        await Assert.That(AnnotationsXml(reopened)).IsEqualTo(AnnotationsXml(original));
    }

    /// <summary>Every kind of form value survives XFDF and FDF.</summary>
    /// <param name="useFdf">Whether to go through FDF instead of XFDF.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FormValuesRoundTrip(bool useFdf)
    {
        using var original = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);
        FillRichForm(original);
        var file = useFdf ? PdfInterchange.ExportFdf(original) : PdfInterchange.ExportXfdf(original);
        using var copy = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);

        var result = useFdf ? PdfInterchange.ImportFdf(copy, file) : PdfInterchange.ImportXfdf(copy, file);

        await Assert.That(result.FieldsSkipped).IsEqualTo(0);
        await Assert.That(Describe(copy)).IsEqualTo(Describe(original));
        await Assert.That(Describe(copy)).Contains("Notes=first\nsecond");
        await Assert.That(Describe(copy)).Contains("Address.Street=Elm Street");
        await Assert.That(Describe(copy)).Contains("Colour=r");
    }

    /// <summary>An export lists fields by their fully qualified names, and nests them in XFDF.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExportNamesNestedFields()
    {
        using var document = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);
        var data = PdfInterchange.Export(document, PdfInterchangeContent.Fields);
        var xml = Encoding.UTF8.GetString(XfdfWriter.Write(data));

        await Assert.That(data.Fields.Select(static field => field.Name)).Contains("Address.Street");
        await Assert.That(xml).Contains("<field name=\"Address\">");
        await Assert.That(xml).Contains("<field name=\"Street\">");
        await Assert.That(data.Annotations).IsEmpty();
    }

    /// <summary>Markup text with characters XML cannot carry is written without them, and rich text that is not well formed is left out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsafeTextIsCleaned()
    {
        var data = new PdfInterchangeData();
        var note = new PdfInterchangeAnnotation("Text") { Rect = InterchangeSamples.Rect(Corner) };
        note.Contents = "a\u0001b\uD800c";
        note.RichContents = "<body><p>unclosed</body>";
        data.Annotations.Add(note);
        data.Fields.Add(new("Field", ["x\u0002y"]));

        var read = XfdfReader.Read(XfdfWriter.Write(data));

        await Assert.That(read.Annotations[0].Contents).IsEqualTo("abc");
        await Assert.That(read.Annotations[0].RichContents).IsNull();
        await Assert.That(read.Fields[0].Value).IsEqualTo("xy");
    }

    /// <summary>Rich text with an XML declaration is written as a fragment.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RichTextWithDeclarationIsKept()
    {
        var data = new PdfInterchangeData();
        var text = new PdfInterchangeAnnotation("FreeText") { Rect = InterchangeSamples.Rect(Corner) };
        text.RichContents = "<?xml version=\"1.0\"?><body xmlns=\"http://www.w3.org/1999/xhtml\"><p>kept</p></body>";
        data.Annotations.Add(text);

        var read = XfdfReader.Read(XfdfWriter.Write(data));

        await Assert.That(read.Annotations[0].RichContents).Contains("<p>kept</p>");
    }

    /// <summary>Rich-text indentation stays stable while explicit carriage returns remain part of plain text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RichTextAndCarriageReturnsSurviveRoundTrip()
    {
        const string contents = "first\r\nsecond\rlast";
        var data = new PdfInterchangeData();
        var text = new PdfInterchangeAnnotation("FreeText") { Rect = InterchangeSamples.Rect(Corner), Contents = contents };
        text.RichContents = "<body xmlns=\"http://www.w3.org/1999/xhtml\"><p>Rich <b>text</b></p></body>";
        data.Annotations.Add(text);
        data.Fields.Add(new("Field", [contents]) { RichText = text.RichContents });

        var first = XfdfWriter.Write(data);
        var read = XfdfReader.Read(first);

        await Assert.That(read.Annotations[0].Contents).IsEqualTo(contents);
        await Assert.That(read.Fields[0].Value).IsEqualTo(contents);
        await Assert.That(Encoding.UTF8.GetString(first)).DoesNotContain("\r");
        await Assert.That(Encoding.UTF8.GetString(XfdfWriter.Write(read))).IsEqualTo(Encoding.UTF8.GetString(first));
    }

    /// <summary>Forms in the pdf.js reference files export and import again without change, when available.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfJsFormsRoundTrip()
    {
        foreach (var path in PdfJsFormSamples())
        {
            using var original = PdfDocumentReader.Open(path, null);
            var xfdf = PdfInterchange.ExportXfdf(original);
            using var copy = PdfDocumentReader.Open(path, null);
            _ = PdfInterchange.ImportXfdf(copy, xfdf);

            await Assert.That(Encoding.UTF8.GetString(PdfInterchange.ExportXfdf(copy))).IsEqualTo(Encoding.UTF8.GetString(xfdf));
            await Assert.That(Describe(copy)).IsEqualTo(Describe(original));
        }
    }

    /// <summary>Finds pdf.js form files on this machine.</summary>
    /// <returns>The paths that exist.</returns>
    private static IEnumerable<string> PdfJsFormSamples()
    {
        var root = Environment.GetEnvironmentVariable("PDFVIEWERLITE_PDFJS_PDF_DIR");
        string[] candidates =
        [
            "/home/glennwatson/source/pdf/pdf.js/test/pdfs/textfields.pdf",
            root is null ? string.Empty : Path.Combine(root, "textfields.pdf"),
            root is null ? string.Empty : Path.Combine(root, "pdflatex-forms.pdf"),
            root is null ? string.Empty : Path.Combine(root, "libreoffice-form.pdf"),
        ];
        return candidates.Where(static path => path.Length > 0 && File.Exists(path));
    }

    /// <summary>Exports a document's annotations as XFDF without the file identifiers.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The XFDF text.</returns>
    private static string AnnotationsXml(PdfDocument document)
    {
        var data = PdfInterchange.Export(document, PdfInterchangeContent.Annotations);
        data.OriginalId = null;
        data.ModifiedId = null;
        return Encoding.UTF8.GetString(XfdfWriter.Write(data));
    }

    /// <summary>Opens the annotated two page sample.</summary>
    /// <returns>The document.</returns>
    private static PdfDocument Annotated()
    {
        var document = PdfDocumentReader.Open(TestPdf.Create(SamplePages), null);
        InterchangeSamples.AddAnnotations(document);
        return document;
    }

    /// <summary>Sets a value in each kind of field of the rich form.</summary>
    /// <param name="document">The document.</param>
    private static void FillRichForm(PdfDocument document)
    {
        var form = PdfDocumentForms.GetForm(document);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(form, 0, FormSamples.NotesIndex, "first\nsecond");
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(form, 0, FormSamples.SecretIndex, "pw");
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SelectOption(form, 0, FormSamples.PickIndex, ListOption);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SelectOption(form, 0, FormSamples.ColourIndex, 0);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetChecked(form, 0, FormSamples.AgreeIndex, true);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(form, 0, FormSamples.StreetIndex, "Elm Street");
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(form, 0, FormSamples.ZipIndex, "98765");
    }

    /// <summary>Describes every widget of the first page: name, value, state and selection.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The description, a line per widget.</returns>
    private static string Describe(PdfDocument document)
    {
        var widgets = new List<PdfFormWidget>();
        HyperPdfLibrary.Forms.PdfFormReading.GetWidgets(PdfDocumentForms.GetForm(document), 0, widgets);
        var text = new StringBuilder();
        foreach (var widget in widgets)
        {
            _ = text.Append(widget.Name).Append('=').Append(widget.Value).Append(" checked=").Append(widget.IsChecked)
                .Append(" selected=").Append(widget.SelectedOption).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Lists the subtype names of a page's annotations.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page index.</param>
    /// <returns>The subtype names.</returns>
    private static List<string> Subtypes(PdfDocument document, int page) =>
        [.. Annotations(document, page).Select(annotation => document.Objects.Names.GetString(annotation.GetName(KnownName.Subtype)))];

    /// <summary>Lists a page's annotation dictionaries.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page index.</param>
    /// <returns>The dictionaries.</returns>
    private static List<PdfDictionary> Annotations(PdfDocument document, int page)
    {
        var array = PdfPageAnnotations.GetArray(document.Objects, PdfDocumentPages.GetPage(document, page));
        var list = new List<PdfDictionary>();
        for (var i = 0; i < (array?.Count ?? 0); i++)
        {
            if (array!.GetDictionary(i) is { } annotation)
            {
                list.Add(annotation);
            }
        }

        return list;
    }
}
