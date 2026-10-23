// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PdfUa;
using HyperPdfLibrary.Tests.Conformance.VeraPdf;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.PdfUa;

/// <summary>Checks the mandatory embedded-file names without turning the recommended description into a requirement.</summary>
public sealed class PdfUaEmbeddedFilenameTests
{
    /// <summary>The report code for the embedded-file filename rule.</summary>
    private const PdfAccessibilityCode FilenameCode = PdfAccessibilityCode.EmbeddedFileFilenameInvalid;

    /// <summary>The title after an edit requiring a save.</summary>
    private const string EditedTitle = "Edited attachments";

    /// <summary>The source attachment's name.</summary>
    private const string Filename = "file.txt";

    /// <summary>A file specification missing its Unicode filename.</summary>
    private const string MissingUnicodeName = "/F (file.txt)";

    /// <summary>Padding that puts indirect objects outside the opening cache.</summary>
    private const int PaddingBytes = 5 * 1024 * 1024;

    /// <summary>Spacing that puts related definitions on separate cache pages.</summary>
    private const int GapBytes = 128 * 1024;

    /// <summary>Malformed file names are reported without preventing the document from opening.</summary>
    /// <param name="entries">The file specification's filename entries.</param>
    /// <param name="expected">Whether one filename finding is required.</param>
    /// <param name="annotation">Whether the file is attached through an invisible annotation.</param>
    /// <param name="cancellationToken">Cancels opening and reporting.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", 1, false)]
    [Arguments("/F (file.txt)", 1, false)]
    [Arguments("/UF (file.txt)", 1, false)]
    [Arguments("/F null /UF (file.txt)", 1, false)]
    [Arguments("/F /file /UF (file.txt)", 1, false)]
    [Arguments("/F (file.txt) /UF 1", 1, false)]
    [Arguments("/F () /UF ()", 0, false)]
    [Arguments("/F (file.txt) /UF (file.txt)", 0, false)]
    [Arguments("", 1, true)]
    [Arguments("/F (file.txt)", 1, true)]
    [Arguments("/UF (file.txt)", 1, true)]
    [Arguments("/F null /UF (file.txt)", 1, true)]
    [Arguments("/F /file /UF (file.txt)", 1, true)]
    [Arguments("/F (file.txt) /UF 1", 1, true)]
    [Arguments("/F () /UF ()", 0, true)]
    [Arguments("/F (file.txt) /UF (file.txt)", 0, true)]
    public async Task ReadingReportsInvalidNames(string entries, int expected, bool annotation, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(entries, annotation), null, cancellationToken);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(report.GetCount(FilenameCode)).IsEqualTo(expected);
    }

    /// <summary>A retained PDF/UA-1 claim cannot be saved with missing file names.</summary>
    /// <param name="incremental">Whether to append an incremental revision.</param>
    /// <param name="annotation">Whether the embedded file is attached to a page.</param>
    /// <param name="cancellationToken">Cancels reading and saving.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task SaveRefusesMissingNames(bool incremental, bool annotation, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(MissingUnicodeName, annotation), null, cancellationToken);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });
        await using var output = new MemoryStream();
        var error = await Assert.That(() => SaveAsync(document, output, new(), incremental, cancellationToken)).Throws<PdfException>();
        await Assert.That(error!.Error).IsEqualTo(PdfError.ClaimUnmet);
        await Assert.That(error.Message).Contains("Clause 7.11");
        await Assert.That(output.Length).IsEqualTo(0);
    }

    /// <summary>The linearized writer also refuses an unmet filename claim before producing output.</summary>
    /// <param name="annotation">Whether the embedded file is attached to a page.</param>
    /// <param name="cancellationToken">Cancels reading and saving.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LinearizedSaveRefusesMissingNames(bool annotation, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(MissingUnicodeName, annotation), null, cancellationToken);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });
        await using var output = new MemoryStream();
        var error = await Assert.That(() => PdfLinearizedWriter.SaveAsync(document, output, new(), cancellationToken)).Throws<PdfException>();
        await Assert.That(error!.Error).IsEqualTo(PdfError.ClaimUnmet);
        await Assert.That(error.Message).Contains("Clause 7.11");
        await Assert.That(output.Length).IsEqualTo(0);
    }

    /// <summary>Names without a recommended Desc entry preserve the claim through either writing mode.</summary>
    /// <param name="incremental">Whether to append an incremental revision.</param>
    /// <param name="annotation">Whether the embedded file is attached to a page.</param>
    /// <param name="cancellationToken">Cancels reading and saving.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task SavePreservesValidNames(bool incremental, bool annotation, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build("/F (file.txt) /UF (file.txt)", annotation), null, cancellationToken);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });
        await using var output = new MemoryStream();
        await SaveAsync(document, output, new(), incremental, cancellationToken);
        using var saved = await PdfDocumentReader.OpenAsync(output.ToArray(), null, cancellationToken);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(saved, cancellationToken);
        await Assert.That(report.Claim.Part).IsEqualTo(PdfUaClaim.Part1);
        await Assert.That(report.GetCount(FilenameCode)).IsEqualTo(0);
        var specification = GetSpecification(saved, annotation);
        await Assert.That(specification.GetText(KnownName.F)).IsEqualTo(Filename);
        await Assert.That(specification.GetText(KnownName.UF)).IsEqualTo(Filename);
        await Assert.That(specification.Get(KnownName.Desc).IsNull).IsTrue();
    }

    /// <summary>Dropping an unmet claim does not invent a name or discard the embedded file.</summary>
    /// <param name="incremental">Whether to append an incremental revision.</param>
    /// <param name="annotation">Whether the file is attached to a page.</param>
    /// <param name="cancellationToken">Cancels reading and saving.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task RemovingClaimPreservesSourceNames(bool incremental, bool annotation, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(MissingUnicodeName, annotation), null, cancellationToken);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });
        var saveReport = new PdfSaveReport { UnmetUaClaim = PdfUaUnmetClaimAction.RemoveClaim };
        await using var output = new MemoryStream();
        await SaveAsync(document, output, saveReport, incremental, cancellationToken);
        await Assert.That(saveReport.UaClaim!.Removed).IsTrue();
        using var saved = await PdfDocumentReader.OpenAsync(output.ToArray(), null, cancellationToken);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(saved, cancellationToken);
        await Assert.That(report.Claim.Part).IsNotEqualTo(PdfUaClaim.Part1);
        var specification = GetSpecification(saved, annotation);
        await Assert.That(specification.GetText(KnownName.F)).IsEqualTo(Filename);
        await Assert.That(specification.Get(KnownName.UF).IsNull).IsTrue();
        await Assert.That(specification.GetDictionary(KnownName.EF)!.GetStream(KnownName.F)).IsNotNull();
    }

    /// <summary>The part 1 filename rule is not attributed to unclaimed files or other identification parts.</summary>
    /// <param name="part">The metadata's identification part.</param>
    /// <param name="expected">The expected filename finding count.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(0, 0)]
    [Arguments(1, 1)]
    [Arguments(2, 0)]
    [Arguments(3, 0)]
    public async Task ReadingScopesThePartOneRule(int part, int expected, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(MissingUnicodeName, false, part, "/Names [(file.txt) 6 0 R]"), null, cancellationToken);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(report.GetCount(FilenameCode)).IsEqualTo(expected);
    }

    /// <summary>A cyclic name tree retains readable leaf entries and terminates without duplicate findings.</summary>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadingRecoversCyclicNameTree(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(MissingUnicodeName, false, PdfUaClaim.Part1, "/Names [(file.txt) 6 0 R] /Kids [10 0 R]"), null, cancellationToken);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(report.GetCount(FilenameCode)).IsEqualTo(1);
    }

    /// <summary>All indirect filename definitions are prefetched before the synchronous reporting core reads them.</summary>
    /// <param name="cancellationToken">Cancels asynchronous reads.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadingLoadsIndirectNamesAsynchronously(CancellationToken cancellationToken)
    {
        var padding = MiniPdf.Stream(string.Empty, new(' ', PaddingBytes));
        var gap = MiniPdf.Stream(string.Empty, new(' ', GapBytes));
        var bytes = MiniPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R /Metadata 3 0 R /Names 5 0 R >>",
            "<< /Type /Pages /Count 1 /Kids [25 0 R] >>",
            MiniPdf.Stream("/Type /Metadata /Subtype /XML", Accessibility.AccessibilityPdfs.Packet(PdfUaClaim.Part1, true)),
            padding,
            "<< /EmbeddedFiles 7 0 R >>",
            gap,
            "<< /Kids 9 0 R >>",
            gap,
            "[11 0 R]",
            gap,
            "<< /Names 13 0 R >>",
            gap,
            "[(file.txt) 15 0 R]",
            gap,
            "<< /Type /Filespec /F 17 0 R /UF 19 0 R /EF 21 0 R >>",
            gap,
            "(file.txt)",
            gap,
            "/Invalid",
            gap,
            "<< /F 23 0 R >>",
            gap,
            MiniPdf.Stream("/Type /EmbeddedFile", "payload"),
            padding,
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
        ]);
        await using var stream = new ThrottledStream(bytes, TimeSpan.Zero);
        using var document = await PdfDocumentReader.OpenAsync(stream, null, cancellationToken);
        var syncBefore = stream.SyncReads;
        var asyncBefore = stream.AsyncReads;
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(report.GetCount(FilenameCode)).IsEqualTo(1);
        await Assert.That(stream.SyncReads).IsEqualTo(syncBefore);
        await Assert.That(stream.AsyncReads).IsGreaterThan(asyncBefore);
        var reads = stream.AsyncReads;
        await Assert.That(await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken)).IsSameReferenceAs(report);
        await Assert.That(stream.AsyncReads).IsEqualTo(reads);
    }

    /// <summary>Independent validation agrees on the filename keys without requiring the recommended Desc.</summary>
    /// <param name="entries">The source filename entries.</param>
    /// <param name="invalid">Whether clause 7.11 requires a finding.</param>
    /// <param name="cancellationToken">Cancels writing and validation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("/F (file.txt)", true)]
    [Arguments("/UF (file.txt)", true)]
    [Arguments("/F (file.txt) /UF (file.txt)", false)]
    [NotInParallel(nameof(VeraPdfValidator))]
    public async Task FilenameRuleAgreesWithVeraPdf(string entries, bool invalid, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("embedded-file-names");
        var path = Path.Combine(folder.Path, "filenames.pdf");
        var bytes = Build(entries, false);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(report.GetCount(FilenameCode) != 0).IsEqualTo(invalid);
        var jobs = await VeraPdfValidator.ValidateAsync(home, "ua1", [path], cancellationToken);
        var job = jobs[Path.GetFullPath(path)];
        await Assert.That(job.ProfileName).IsEqualTo("PDF/UA-1 validation profile");
        await Assert.That(job.Failures.Exists(static failure => failure.Clause == "7.11")).IsEqualTo(invalid).Because(job.FailureSummary);
    }

    /// <summary>Creates a named attachment or a hidden file attachment without unrelated accessibility faults.</summary>
    /// <param name="entries">The file specification's filename entries.</param>
    /// <param name="annotation">Whether the file is attached to a page.</param>
    /// <returns>The source bytes.</returns>
    private static byte[] Build(string entries, bool annotation) =>
        Build(entries, annotation, PdfUaClaim.Part1, "/Names [(file.txt) 6 0 R]");

    /// <summary>Creates an attachment with explicit identification and name-tree syntax.</summary>
    /// <param name="entries">The file specification's filename entries.</param>
    /// <param name="annotation">Whether the file is attached to a page.</param>
    /// <param name="part">The identification part.</param>
    /// <param name="treeEntries">The embedded-files tree root's entries.</param>
    /// <returns>The source bytes.</returns>
    private static byte[] Build(string entries, bool annotation, int part, string treeEntries)
    {
        var names = annotation ? string.Empty : "/Names << /EmbeddedFiles 10 0 R >>";
        var annotations = annotation ? "/Annots [9 0 R]" : string.Empty;
        return MiniPdf.Build(
        [
            $"<< /Type /Catalog /Pages 2 0 R /Metadata 3 0 R /StructTreeRoot 4 0 R /MarkInfo << /Marked true >> /Lang (en) /ViewerPreferences << /DisplayDocTitle true >> {names} >>",
            "<< /Type /Pages /Count 1 /Kids [8 0 R] >>",
            MiniPdf.Stream("/Type /Metadata /Subtype /XML", Accessibility.AccessibilityPdfs.Packet(part, true)),
            "<< /Type /StructTreeRoot /K 5 0 R >>",
            "<< /Type /StructElem /S /Document /P 4 0 R /K [] >>",
            $"<< /Type /Filespec {entries} /EF << /F 7 0 R >> >>",
            MiniPdf.Stream("/Type /EmbeddedFile", "payload"),
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] {annotations} >>",
            annotation ? "<< /Type /Annot /Subtype /FileAttachment /P 8 0 R /FS 6 0 R /F 2 /Rect [0 0 1 1] >>" : "<< >>",
            $"<< {treeEntries} >>",
        ]);
    }

    /// <summary>Reads the saved file specification without relying on preserved object numbers.</summary>
    /// <param name="document">The reopened document.</param>
    /// <param name="annotation">Whether the file is attached to a page.</param>
    /// <returns>The file specification.</returns>
    private static PdfDictionary GetSpecification(PdfDocument document, bool annotation) =>
        annotation
            ? PdfDocumentPages.GetPage(document, 0).Dictionary.GetArray(KnownName.Annots)!.GetDictionary(0)!.GetDictionary(document.Objects.Names.Intern("FS"u8))!
            : document.Catalog.GetDictionary(KnownName.Names)!.GetDictionary(KnownName.EmbeddedFiles)!.GetArray(KnownName.Names)!.GetDictionary(1)!;

    /// <summary>Saves through the selected writing entry point.</summary>
    /// <param name="document">The edited document.</param>
    /// <param name="output">Receives saved bytes.</param>
    /// <param name="report">Receives the claim decision.</param>
    /// <param name="incremental">Whether to append a revision.</param>
    /// <param name="cancellationToken">Cancels writing.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static Task SaveAsync(PdfDocument document, MemoryStream output, PdfSaveReport report, bool incremental, CancellationToken cancellationToken) =>
        incremental
            ? PdfIncrementalWriter.SaveAsync(document.Objects, output, report, cancellationToken)
            : PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, output, report, cancellationToken);
}
