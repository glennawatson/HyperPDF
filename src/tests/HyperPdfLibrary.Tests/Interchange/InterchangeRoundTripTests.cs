// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Interchange;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

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

    /// <summary>The fields of the unknown-names file that the rich form has.</summary>
    private const int KnownFields = 2;

    /// <summary>The fields of the unknown-names file that the rich form does not have.</summary>
    private const int UnknownFields = 2;

    /// <summary>The rectangle of every sample annotation, as <see cref="InterchangeSamples"/> writes it.</summary>
    private const string SampleBox = "72,600,172,650";

    /// <summary>The option of the list box selected in the sample.</summary>
    private const int ListOption = 3;

    /// <summary>The reference PDFs with form fields.</summary>
    private static readonly string[] ReferenceForms = ["pdfjs-textfields", "pypdf-latex-forms", "pypdf-libreoffice-form"];

    /// <summary>The annotation names the tests read back and the text each one holds.</summary>
    private static readonly string[] KeptAnnotations = [InterchangeSamples.HighlightName, "note-1"];

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

    /// <summary>The highlight and the text note keep their page, rectangle and contents through XFDF and FDF.</summary>
    /// <param name="useFdf">Whether to go through FDF instead of XFDF.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task HighlightAndTextKeepPageRectangleAndContents(bool useFdf)
    {
        using var original = Annotated();
        using var copy = PdfDocumentReader.Open(TestPdf.Create(SamplePages), null);
        _ = useFdf ? PdfInterchange.ImportFdf(copy, PdfInterchange.ExportFdf(original)) : PdfInterchange.ImportXfdf(copy, PdfInterchange.ExportXfdf(original));

        foreach (var name in KeptAnnotations)
        {
            var annotation = Annotations(copy, 0).Single(annotation => annotation.GetText(KnownName.NM) == name);
            var expected = InterchangeSamples.Rect(SampleBox);

            await Assert.That(annotation.GetText(KnownName.Contents)).IsEqualTo($"Contents of {name}\nsecond line");
            await Assert.That(annotation.TryGetRectangle(KnownName.Rect, out var rect)).IsTrue();
            await Assert.That(PdfRectangle.FromCorners(rect.Left, rect.Bottom, rect.Right, rect.Top)).IsEqualTo(expected);
            await Assert.That(Annotations(copy, 1).Exists(other => other.GetText(KnownName.NM) == name)).IsFalse();
        }
    }

    /// <summary>XFDF names that are not in the form are ignored, including a missing child of a field that exists.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task XfdfIgnoresUnknownFieldNames()
    {
        var xfdf = """
            <?xml version="1.0" encoding="UTF-8"?>
            <xfdf xmlns="http://ns.adobe.com/xfdf/"><fields>
              <field name="Notes"><value>hello</value></field>
              <field name="NoSuchField"><value>x</value></field>
              <field name="Address"><field name="Street"><value>Elm Street</value></field><field name="Missing"><value>y</value></field></field>
            </fields></xfdf>
            """u8.ToArray();
        using var document = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);

        var result = PdfInterchange.ImportXfdf(document, xfdf);

        await Assert.That(result.FieldsApplied).IsEqualTo(KnownFields);
        await Assert.That(result.FieldsSkipped).IsEqualTo(UnknownFields);
        await Assert.That(Describe(document)).Contains("Notes=hello").And.Contains("Address.Street=Elm Street");
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

    /// <summary>
    /// The form files of the reference PDFs (<c>pdfjs-textfields</c>, <c>pypdf-latex-forms</c> and <c>pypdf-libreoffice-form</c>)
    /// export, import again and export the same file in both formats, with every field keeping its value. Skips when a file is
    /// not fetched, and fails when <c>HYPERPDF_REQUIRE_REFERENCE_PDFS</c> is <c>1</c>; it never passes with no files.
    /// </summary>
    /// <param name="useFdf">Whether to go through FDF instead of XFDF.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ReferencePdfFormsRoundTrip(bool useFdf)
    {
        var filledFields = 0;
        foreach (var path in ReferencePdfFiles.RequirePaths(ReferenceForms))
        {
            var name = Path.GetFileName(path);

            // As shipped: whatever the form exports imports into a fresh copy without changing it.
            using var shipped = PdfDocumentReader.Open(path, null);
            using var shippedCopy = PdfDocumentReader.Open(path, null);
            var shippedResult = RoundTrip(shipped, shippedCopy, useFdf);
            await Assert.That(shippedResult.FieldsSkipped).IsEqualTo(0).Because($"{name} exports only fields it can import");
            await Assert.That(ExportedFields(shippedCopy)).IsEqualTo(ExportedFields(shipped)).Because(name);
            await Assert.That(Describe(shippedCopy)).IsEqualTo(Describe(shipped)).Because(name);

            // With every text field and check box set: the values travel through the file.
            using var filled = PdfDocumentReader.Open(path, null);
            var set = Fill(filled);
            using var filledCopy = PdfDocumentReader.Open(path, null);
            var filledResult = RoundTrip(filled, filledCopy, useFdf);
            filledFields += set;
            await Assert.That(set).IsGreaterThan(0).Because($"{name} has a text field or check box to fill");
            await Assert.That(filledResult.FieldsApplied).IsGreaterThanOrEqualTo(set).Because(name);
            await Assert.That(filledResult.FieldsSkipped).IsEqualTo(0).Because(name);
            await Assert.That(ExportedFields(filledCopy)).IsEqualTo(ExportedFields(filled)).Because(name);
            await Assert.That(Describe(filledCopy)).IsEqualTo(Describe(filled)).Because(name);
        }

        await Assert.That(filledFields).IsGreaterThan(0);
    }

    /// <summary>Exports a document's fields and imports the file into another copy.</summary>
    /// <param name="source">The document that is exported.</param>
    /// <param name="target">The document that receives the file.</param>
    /// <param name="useFdf">Whether to go through FDF instead of XFDF.</param>
    /// <returns>What the import did.</returns>
    private static PdfInterchangeImportResult RoundTrip(PdfDocument source, PdfDocument target, bool useFdf) => useFdf
        ? PdfInterchange.ImportFdf(target, PdfInterchange.ExportFdf(source, PdfInterchangeContent.Fields))
        : PdfInterchange.ImportXfdf(target, PdfInterchange.ExportXfdf(source, PdfInterchangeContent.Fields));

    /// <summary>Exports a document's fields as XFDF without the file identifiers.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The XFDF text.</returns>
    private static string ExportedFields(PdfDocument document)
    {
        var data = PdfInterchange.Export(document, PdfInterchangeContent.Fields);
        data.OriginalId = null;
        data.ModifiedId = null;
        return Encoding.UTF8.GetString(XfdfWriter.Write(data));
    }

    /// <summary>Sets every editable text field and check box on every page.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The number of fields set.</returns>
    private static int Fill(PdfDocument document)
    {
        var form = PdfDocumentForms.GetForm(document);
        var set = 0;
        for (var page = 0; page < document.PageCount; page++)
        {
            var widgets = new List<PdfFormWidget>();
            HyperPdfLibrary.Forms.PdfFormReading.GetWidgets(form, page, widgets);
            foreach (var widget in widgets.Where(static widget => !widget.IsReadOnly))
            {
                var changed = widget.Type switch
                {
                    PdfFieldType.Text => HyperPdfLibrary.Forms.PdfFormEditing.SetText(form, page, widget.Index, $"value {set} é"),
                    PdfFieldType.CheckBox => HyperPdfLibrary.Forms.PdfFormEditing.SetChecked(form, page, widget.Index, true),
                    _ => false,
                };
                set += changed ? 1 : 0;
            }
        }

        return set;
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
