// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Pdf20;
using HyperPdfLibrary.Tests.Conformance;

namespace HyperPdfLibrary.Tests.Pdf20;

/// <summary>Checks that <see cref="Pdf20Converter"/> writes a PDF 2.0 copy, moves what it can, and refuses with reasons what it cannot keep.</summary>
public sealed class Pdf20ConverterTests
{
    /// <summary>The year of the XMP creation date in the disagreement sample.</summary>
    private const int XmpCreationYear = 2019;

    /// <summary>The fewest bytes a PDF 2.0 file identifier string has.</summary>
    private const int MinimumIdentifierLength = 16;

    /// <summary>The strings in a file identifier.</summary>
    private const int IdentifierParts = 2;

    /// <summary>The name in the information and form samples.</summary>
    private const string AuthorName = "Ada Lovelace";

    /// <summary>Gets the bytes a PDF 2.0 file starts with.</summary>
    private static ReadOnlySpan<byte> Header => "%PDF-2.0"u8;

    /// <summary>The copy has a PDF 2.0 header, the same pages and the same text, and the open file is not changed.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopyIsPdf20WithSamePagesAndText(CancellationToken cancellationToken)
    {
        var file = Pdf20TestPdf.InfoOnly();
        using var source = PdfDocumentReader.Open(file, null);
        var (result, output) = await ConvertAsync(source, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        await Assert.That(output.AsSpan(0, Header.Length).SequenceEqual(Header)).IsTrue();
        using var copy = PdfDocumentReader.Open(output, null);
        await Assert.That(copy.PageCount).IsEqualTo(source.PageCount);
        await Assert.That(PdfDocumentText.GetTextPage(copy, 0).Text).IsEqualTo(PdfDocumentText.GetTextPage(source, 0).Text);
        await Assert.That(PdfDocumentText.GetTextPage(copy, 0).Text).Contains(Pdf20TestPdf.SampleText);
        await Assert.That(source.Objects.Version).IsEqualTo("1.4");
        await Assert.That(Array.Exists(result.Changes, static note => note.Description.Contains("PDF 2.0", StringComparison.Ordinal) && note.Clause == "7.5.2")).IsTrue();
    }

    /// <summary>Every standard and custom information entry appears in the XMP, and the information keeps only the two dates, matching the XMP.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task InfoEntriesMoveToXmp(CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(Pdf20TestPdf.InfoOnly(), null);
        var (result, output) = await ConvertAsync(source, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        using var copy = PdfDocumentReader.Open(output, null);
        var xmp = PdfDocumentMetadata.GetXmp(copy)!;
        await Assert.That(xmp.Title).IsEqualTo("Quarterly report");
        await Assert.That(xmp.Creators).IsEquivalentTo([AuthorName]);
        await Assert.That(xmp.Description).IsEqualTo("Figures");
        await Assert.That(xmp.Keywords).IsEqualTo("alpha, beta");
        await Assert.That(xmp.CreatorTool).IsEqualTo("Writer 3");
        await Assert.That(xmp.Producer).IsEqualTo("Engine 9");
        await Assert.That(xmp.Trapped).IsEqualTo("False");
        await Assert.That(xmp.GetValue(XmpMetadata.PdfXNamespace, "Department")).IsEqualTo("Sales");
        await Assert.That(xmp.GetValue(XmpMetadata.PdfXNamespace, "Project_x0020_Code")).IsEqualTo("X-17");
        await Assert.That(xmp.GetValue(XmpMetadata.DublinCoreNamespace, "format")).IsEqualTo("application/pdf");

        var info = InfoOf(copy)!;
        var keys = Enumerable.Range(0, info.Count).Select(index => info.GetKeyAt(index).ToKnownName()).ToArray();
        await Assert.That(keys).IsEquivalentTo([KnownName.CreationDate, KnownName.ModDate]);
        await Assert.That(PdfDate.Parse(info.GetStringBytes(KnownName.CreationDate))).IsEqualTo(xmp.CreateDate);
        await Assert.That(PdfDate.Parse(info.GetStringBytes(KnownName.ModDate))).IsEqualTo(xmp.ModifyDate);
        await Assert.That(Array.Exists(result.Changes, static note => note.Description.Contains("Created XMP metadata", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>The document properties are the same for the source and the copy.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentPropertiesMatchTheSource(CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(Pdf20TestPdf.InfoOnly(), null);
        var (_, output) = await ConvertAsync(source, cancellationToken);

        using var copy = PdfDocumentReader.Open(output, null);
        var before = PdfDocumentMetadata.GetResolvedInfo(source);
        var after = PdfDocumentMetadata.GetResolvedInfo(copy);
        await Assert.That(after with { Version = null }).IsEqualTo(before with { Version = null });
        await Assert.That(after.Version).IsEqualTo("2.0");
    }

    /// <summary>When the XMP and the information disagree the XMP wins, and the summary names the value not carried over.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task XmpWinsWhenItDisagrees(CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(Pdf20TestPdf.InfoDisagreesWithXmp(), null);
        var (result, output) = await ConvertAsync(source, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        using var copy = PdfDocumentReader.Open(output, null);
        var xmp = PdfDocumentMetadata.GetXmp(copy)!;
        await Assert.That(xmp.Title).IsEqualTo("XMP title");
        await Assert.That(xmp.GetValue(XmpMetadata.PdfXNamespace, "Department")).IsEqualTo("Marketing");
        await Assert.That(xmp.GetValue(XmpMetadata.XmpNamespace, "Label")).IsEqualTo("Kept as it is");
        await Assert.That(xmp.Creators).IsEquivalentTo([AuthorName]);
        await Assert.That(xmp.CreateDate!.Value.Year).IsEqualTo(XmpCreationYear);
        await Assert.That(PdfDate.Parse(InfoOf(copy)!.GetStringBytes(KnownName.CreationDate))).IsEqualTo(xmp.CreateDate);
        await Assert.That(PdfDate.Parse(InfoOf(copy)!.GetStringBytes(KnownName.ModDate))).IsEqualTo(xmp.ModifyDate);
        var text = string.Join(' ', result.Changes.Select(static note => note.Description));
        await Assert.That(text).Contains("Info title");
        await Assert.That(text).Contains("Sales");
        await Assert.That(text).Contains("not carried over");
        await Assert.That(Encoding.UTF8.GetString(xmp.RawPacket)).Contains("Kept as it is");
    }

    /// <summary>A hybrid form keeps its fields, values and appearances, loses the XFA packet, and the summary says so.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task HybridFormKeepsFieldsAndLosesXfa(CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(Pdf20TestPdf.HybridForm(), null);
        var (result, output) = await ConvertAsync(source, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        using var copy = PdfDocumentReader.Open(output, null);
        var form = copy.Catalog.GetDictionary(KnownName.AcroForm)!;
        await Assert.That(form.ContainsKey(KnownName.XFA)).IsFalse();
        await Assert.That(form.ContainsKey(KnownName.NeedAppearances)).IsFalse();
        await Assert.That(form.GetArray(KnownName.Fields)!.Count).IsEqualTo(1);
        List<PdfFormWidget> widgets = [];
        PdfFormReading.GetWidgets(new(copy), 0, widgets);
        await Assert.That(widgets.Count).IsEqualTo(1);
        await Assert.That(widgets[0].Value).IsEqualTo(AuthorName);
        var widget = PdfDocumentPages.GetPage(copy, 0).Dictionary.GetArray(KnownName.Annots)!.GetDictionary(0)!;
        await Assert.That(widget.GetDictionary(KnownName.AP)!.ContainsKey(KnownName.N)).IsTrue();
        await Assert.That(Array.Exists(result.Changes, static note => note.Description.Contains("XFA", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>A false /NeedsRendering is removed and noted.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeedsRenderingFalseIsRemoved(CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(Pdf20TestPdf.NeedsRendering(false), null);
        var (result, output) = await ConvertAsync(source, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        using var copy = PdfDocumentReader.Open(output, null);
        await Assert.That(copy.Catalog.ContainsKey(KnownName.NeedsRendering)).IsFalse();
        await Assert.That(Array.Exists(result.Changes, static note => note.Clause == "7.7.2")).IsTrue();
    }

    /// <summary>Entries PDF 2.0 deprecates that carry no content are removed or moved, and each is noted with its clause.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeprecatedEntriesAreRemovedOrMoved(CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(Pdf20TestPdf.DeprecatedEntries(), null);
        var (result, output) = await ConvertAsync(source, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        using var copy = PdfDocumentReader.Open(output, null);
        var names = copy.Objects.Names;
        var page = PdfDocumentPages.GetPage(copy, 0);
        var resources = page.Resources!;
        await Assert.That(resources.ContainsKey(KnownName.ProcSet)).IsFalse();
        var font = resources.GetDictionary(KnownName.Font)!.GetDictionary(names.Intern("F1"u8))!;
        await Assert.That(font.ContainsKey(KnownName.Name)).IsFalse();
        var states = resources.GetDictionary(KnownName.ExtGState)!;
        var state = states.GetDictionary(names.Intern("GS1"u8))!;
        await Assert.That(state.GetName(KnownName.BM).ToKnownName()).IsEqualTo(KnownName.Multiply);
        await Assert.That(state.ContainsKey(KnownName.TR)).IsFalse();
        await Assert.That(state.ContainsKey(KnownName.TR2)).IsFalse();
        await Assert.That(states.GetDictionary(names.Intern("GS2"u8))!.GetName(KnownName.BM).ToKnownName()).IsEqualTo(KnownName.Normal);
        await Assert.That(page.Dictionary.ContainsKey(KnownName.ID)).IsFalse();

        var annotations = page.Dictionary.GetArray(KnownName.Annots)!;
        var specification = annotations.GetDictionary(0)!.GetDictionary(KnownName.A)!.GetDictionary(KnownName.F)!;
        await Assert.That(specification.GetText(KnownName.F)).IsEqualTo("A.TXT");
        await Assert.That(specification.ContainsKey(names.Intern("DOS"u8))).IsFalse();
        await Assert.That(specification.ContainsKey(KnownName.Mac)).IsFalse();
        await Assert.That(specification.ContainsKey(KnownName.Unix)).IsFalse();
        var launch = annotations.GetDictionary(1)!.GetDictionary(KnownName.A)!;
        await Assert.That(launch.GetText(KnownName.F)).IsEqualTo("notepad.exe");
        await Assert.That(launch.ContainsKey(KnownName.Win)).IsFalse();
        await Assert.That(launch.ContainsKey(KnownName.Unix)).IsFalse();

        var catalog = copy.Catalog;
        await Assert.That(catalog.ContainsKey(names.Intern("SpiderInfo"u8))).IsFalse();
        await Assert.That(catalog.ContainsKey(KnownName.Version)).IsFalse();
        await Assert.That(catalog.GetDictionary(KnownName.Names)!.ContainsKey(names.Intern("AlternatePresentations"u8))).IsFalse();
        var preferences = catalog.GetDictionary(KnownName.ViewerPreferences)!;
        await Assert.That(preferences.ContainsKey(names.Intern("ViewArea"u8))).IsFalse();
        await Assert.That(preferences.ContainsKey(names.Intern("PrintArea"u8))).IsFalse();
        await Assert.That(preferences.ContainsKey(names.Intern("Direction"u8))).IsTrue();
        var markInfo = catalog.GetDictionary(KnownName.MarkInfo)!;
        await Assert.That(markInfo.ContainsKey(names.Intern("Suspects"u8))).IsFalse();
        await Assert.That(markInfo.GetBoolean(names.Intern("Marked"u8))).IsTrue();
        await Assert.That(Array.TrueForAll(result.Changes, static note => note.Clause.Length > 0 && note.Specification == "ISO 32000-2:2020")).IsTrue();
        await Assert.That(Array.Exists(result.Changes, static note => note.Clause == "9.7.5.2" && note.Description.Contains("Adobe-Korea1", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>A lowercase print field check state is written in its current form.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task PrintFieldCheckStateIsRewritten(CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(Pdf20TestPdf.PrintFieldAttribute(), null);
        var (result, output) = await ConvertAsync(source, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        using var copy = PdfDocumentReader.Open(output, null);
        var element = copy.Catalog.GetDictionary(copy.Objects.Names.Intern("StructTreeRoot"u8))!.GetArray(KnownName.K)!.GetDictionary(0)!;
        var attribute = element.GetDictionary(KnownName.A)!;
        await Assert.That(copy.Objects.Names.GetString(attribute.GetName(copy.Objects.Names.Intern("Checked"u8)))).IsEqualTo("On");
    }

    /// <summary>The deprecated F operator is written as f in the page and in a form XObject.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task FillOperatorIsWrittenAsLowercase(CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(Pdf20TestPdf.DeprecatedFillOperator(), null);
        var (result, output) = await ConvertAsync(source, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        using var copy = PdfDocumentReader.Open(output, null);
        var page = PdfDocumentPages.GetPage(copy, 0);
        var content = Encoding.ASCII.GetString(page.Dictionary.GetStream(KnownName.Contents)!.DecodeToArray(cancellationToken));
        var form = page.Resources!.GetDictionary(KnownName.XObject)!.GetStream(copy.Objects.Names.Intern("Fx0"u8))!;
        var formContent = Encoding.ASCII.GetString(form.DecodeToArray(cancellationToken));
        await Assert.That(content).Contains("re f ").And.DoesNotContain("re F");
        await Assert.That(formContent).Contains("re f").And.DoesNotContain("re F");
        await Assert.That(Array.Exists(result.Changes, static note => note.Clause == "8.5.3.1")).IsTrue();
    }

    /// <summary>A short file identifier is replaced by one of the length PDF 2.0 needs.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShortIdentifierIsReplaced(CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(Pdf20TestPdf.ShortIdentifier(), null);
        var (result, output) = await ConvertAsync(source, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        using var copy = PdfDocumentReader.Open(output, null);
        var ids = copy.Objects.Trailer.GetArray(KnownName.ID)!;
        await Assert.That(ids.Count).IsEqualTo(IdentifierParts);
        await Assert.That(ids.Get(0).AsStringBytes().Length).IsGreaterThanOrEqualTo(MinimumIdentifierLength);
        await Assert.That(ids.Get(1).AsStringBytes().Length).IsGreaterThanOrEqualTo(MinimumIdentifierLength);
    }

    /// <summary>A file with nothing deprecated gets the new header and a file identifier, and nothing else is reported.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlainFileOnlyChangesTheHeader(CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(Pdf20TestPdf.Plain(), null);
        var (result, output) = await ConvertAsync(source, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        using var copy = PdfDocumentReader.Open(output, null);
        await Assert.That(copy.Objects.Trailer.GetArray(KnownName.ID)).IsNotNull();
        await Assert.That(result.Changes.Select(static note => note.Clause)).IsEquivalentTo(["7.5.2", "7.5.5"]);
    }

    /// <summary>Documents the copy cannot keep are refused with a plain reason, and nothing is written.</summary>
    /// <param name="kind">The sample.</param>
    /// <param name="expected">Words the reason must contain.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("movie", "movie")]
    [Arguments("sound", "sound")]
    [Arguments("trapnet", "trap network")]
    [Arguments("soundaction", "sounds or movies")]
    [Arguments("transfer", "transfer functions")]
    [Arguments("opi", "OPI")]
    [Arguments("separation", "preseparated")]
    public async Task UnkeepableFeaturesAreRefused(string kind, string expected, CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(Pdf20TestPdf.Unkeepable(kind), null);
        var (result, output) = await ConvertAsync(source, cancellationToken);

        await Assert.That(result.Converted).IsFalse();
        await Assert.That(output.Length).IsEqualTo(0);
        await Assert.That(string.Join(' ', result.Problems.Select(static note => note.Description))).Contains(expected);
    }

    /// <summary>An XFA-only form and /NeedsRendering true are refused, with the reason that the copy would have no form or pages.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task XfaOnlyAndNeedsRenderingAreRefused(CancellationToken cancellationToken)
    {
        using var xfaOnly = PdfDocumentReader.Open(Pdf20TestPdf.XfaOnly(), null);
        var (formResult, formOutput) = await ConvertAsync(xfaOnly, cancellationToken);
        using var needsRendering = PdfDocumentReader.Open(Pdf20TestPdf.NeedsRendering(true), null);
        var (pagesResult, pagesOutput) = await ConvertAsync(needsRendering, cancellationToken);

        await Assert.That(formResult.Converted).IsFalse();
        await Assert.That(formOutput.Length).IsEqualTo(0);
        await Assert.That(formResult.Problems[0].Description).Contains("no form");
        await Assert.That(pagesResult.Converted).IsFalse();
        await Assert.That(pagesOutput.Length).IsEqualTo(0);
        await Assert.That(pagesResult.Problems[0].Description).Contains("XFA");
    }

    /// <summary>A signed file is refused, as the rewrite would break the signature, and nothing is written.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignedFileIsRefused(CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(ConversionTestPdf.Signed(), null);
        var (result, output) = await ConvertAsync(source, cancellationToken);

        await Assert.That(result.Converted).IsFalse();
        await Assert.That(output.Length).IsEqualTo(0);
        await Assert.That(result.Problems.Single().Description).Contains("signed");
    }

    /// <summary>A usage rights signature is removed and noted, but a certification signature refuses the copy.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task UsageRightsAreRemovedButCertificationRefuses(CancellationToken cancellationToken)
    {
        using var rights = PdfDocumentReader.Open(Pdf20TestPdf.Permissions("UR3"), null);
        var kept = await ConvertAsync(rights, cancellationToken);
        using var certified = PdfDocumentReader.Open(Pdf20TestPdf.Permissions("DocMDP"), null);
        var refused = await ConvertAsync(certified, cancellationToken);

        await Assert.That(kept.Result.Converted).IsTrue().Because(Describe(kept.Result));
        using var copy = PdfDocumentReader.Open(kept.Output, null);
        await Assert.That(copy.Catalog.ContainsKey(KnownName.Perms)).IsFalse();
        await Assert.That(Array.Exists(kept.Result.Changes, static note => note.Clause == "12.8.2.3")).IsTrue();
        await Assert.That(refused.Result.Converted).IsFalse();
        await Assert.That(refused.Output.Length).IsEqualTo(0);
    }

    /// <summary>A file that claims PDF/A-1, 2 or 3 is refused, and the reason points to Save PDF/A Copy with PDF/A-4.</summary>
    /// <param name="part">The claimed part.</param>
    /// <param name="level">The claimed level.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, "B")]
    [Arguments(2, "B")]
    [Arguments(3, "U")]
    public async Task PdfAOneToThreeClaimsAreRefused(int part, string level, CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(ConversionTestPdf.ClaimedHelveticaText(part, level), null);
        var (result, output) = await ConvertAsync(source, cancellationToken);

        await Assert.That(result.Converted).IsFalse();
        await Assert.That(output.Length).IsEqualTo(0);
        await Assert.That(result.Problems.Single().Description).Contains("PDF/A-4").And.Contains($"PDF/A-{part}");
    }

    /// <summary>An encrypted file is refused, as the copy could not be protected again.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task EncryptedFileIsRefused(CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(EncryptedFixture.Build(), string.Empty);
        var (result, output) = await ConvertAsync(source, cancellationToken);

        await Assert.That(result.Converted).IsFalse();
        await Assert.That(output.Length).IsEqualTo(0);
        await Assert.That(result.Problems.Single().Description).Contains("protected");
    }

    /// <summary>The synchronous form writes the same copy as the asynchronous one, and a cancelled token stops the run.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SyncAndAsyncAgreeAndCancellationStops(CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(Pdf20TestPdf.InfoOnly(), null);
        var asyncOutcome = await ConvertAsync(source, cancellationToken);
        var syncOutcome = ConvertSync(source, cancellationToken);

        await Assert.That(asyncOutcome.Result.Changes.Select(static note => note.Description)).IsEquivalentTo(syncOutcome.Result.Changes.Select(static note => note.Description));
        await Assert.That(syncOutcome.Output.AsSpan(0, Header.Length).SequenceEqual(Header)).IsTrue();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.That(() => ConvertSync(source, cancelled.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Converting to PDF 2.0 again changes nothing but is accepted, and the output is a fixed point.</summary>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConvertingACopyAgainMakesNoMoves(CancellationToken cancellationToken)
    {
        using var source = PdfDocumentReader.Open(Pdf20TestPdf.InfoOnly(), null);
        var (_, first) = await ConvertAsync(source, cancellationToken);
        using var copy = PdfDocumentReader.Open(first, null);
        var (result, _) = await ConvertAsync(copy, cancellationToken);

        await Assert.That(result.Converted).IsTrue().Because(Describe(result));
        await Assert.That(result.Changes).IsEmpty();
    }

    /// <summary>Converts a document with the synchronous form.</summary>
    /// <param name="source">The document.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The outcome and the bytes written, empty when refused.</returns>
    private static Pdf20Outcome ConvertSync(PdfDocument source, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        var result = Pdf20Converter.SaveAsPdf20(source, stream, cancellationToken);
        return new(result, stream.ToArray());
    }

    /// <summary>Converts a document and returns the outcome and the written bytes.</summary>
    /// <param name="source">The document.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The outcome and the bytes written, empty when refused.</returns>
    private static async Task<Pdf20Outcome> ConvertAsync(PdfDocument source, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        var result = await Pdf20Converter.SaveAsPdf20Async(source, stream, cancellationToken);
        return new(result, stream.ToArray());
    }

    /// <summary>Reads the trailer's document information dictionary.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The dictionary, or <see langword="null"/>.</returns>
    private static PdfDictionary? InfoOf(PdfDocument document) =>
        StoreReading.Resolve(document.Objects, document.Objects.Trailer.GetRaw(KnownName.Info)).AsDictionary();

    /// <summary>Lists a refused conversion's problems, for assertion messages.</summary>
    /// <param name="result">The outcome.</param>
    /// <returns>The problems.</returns>
    private static string Describe(Pdf20ConversionResult result) => string.Join("; ", result.Problems.Select(static problem => $"{problem.Clause} {problem.Description}"));

    /// <summary>A conversion's outcome and output.</summary>
    /// <param name="Result">The outcome.</param>
    /// <param name="Output">The bytes written, empty when refused.</param>
    private sealed record Pdf20Outcome(Pdf20ConversionResult Result, byte[] Output);
}
