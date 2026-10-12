// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Tests for the whole-document check and for saves that write conforming structures.</summary>
public sealed class CheckAndSaveRepairTests
{
    /// <summary>The catalog.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The page tree node.</summary>
    private const string Pages = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";

    /// <summary>A page with a content stream in object 4.</summary>
    private const string ContentPage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Contents 4 0 R >>";

    /// <summary>Valid page content.</summary>
    private const string GoodContent = "q 0 0 10 10 re f Q";

    /// <summary>An operator no PDF defines.</summary>
    private const string BadOperator = "frobnicate";

    /// <summary>One hundred, for percentages.</summary>
    private const int Percent = 100;

    /// <summary>The faults on the page that has no /Type and no /Parent.</summary>
    private const int PageFaults = 2;

    /// <summary>The object number of the page tree node.</summary>
    private const int PagesNumber = 2;

    /// <summary>The object number of the page.</summary>
    private const int PageNumber = 3;

    /// <summary>The object number of the content stream.</summary>
    private const int ContentNumber = 4;

    /// <summary>The objects in the clean document.</summary>
    private const int CleanObjects = 4;

    /// <summary>The repeats that give the compressed content some length.</summary>
    private const int Repeats = 200;

    /// <summary>The share of the compressed bytes kept when truncating, in percent.</summary>
    private const int KeepPercent = 60;

    /// <summary>The width of the corrected page.</summary>
    private const float PageWidth = 200;

    /// <summary>The rotation the page ends with after the repeated key is resolved.</summary>
    private const int Rotation = 90;

    /// <summary>A clean document checks clean, and the check counts what it looked at.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CleanDocumentChecksClean()
    {
        using var document = PdfDocumentReader.Open(Clean(GoodContent), null);
        var report = PdfDocumentCheck.Check(document, PdfCheckOptions.Default);
        await Assert.That(report.IsClean).IsTrue();
        await Assert.That(report.ObjectsChecked).IsEqualTo(CleanObjects);
        await Assert.That(report.StreamsChecked).IsEqualTo(1);
        await Assert.That(report.PagesChecked).IsEqualTo(1);
    }

    /// <summary>A truncated stream is found, with its object number.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TruncatedStreamIsFound()
    {
        var packed = Compress(Encoding.Latin1.GetBytes(string.Join('\n', Enumerable.Repeat(GoodContent, Repeats))));
        var kept = Encoding.Latin1.GetString(packed.AsSpan(0, packed.Length * KeepPercent / Percent));
        using var document = PdfDocumentReader.Open(MiniPdf.Build(Catalog, Pages, ContentPage, MiniPdf.Stream("/Filter /FlateDecode", kept)), null);
        var report = PdfDocumentCheck.Check(document, PdfCheckOptions.Default);
        await Assert.That(Codes(report, ContentNumber)).Contains(PdfDiagnosticCode.TruncatedStream);
    }

    /// <summary>Content with an undefined operator, a restore with nothing saved and an open text object is reported.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="message">A word the fault's description contains.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments($"q 0 0 10 10 re f Q {BadOperator}", "not defined")]
    [Arguments("Q q 0 0 10 10 re f Q", "never saved")]
    [Arguments("BT /F1 12 Tf", "never closed")]
    public async Task BadContentIsFound(string content, string message)
    {
        using var document = PdfDocumentReader.Open(Clean(content), null);
        var report = PdfDocumentCheck.Check(document, PdfCheckOptions.Default);
        var faults = report.Faults.Where(static fault => fault.Code == PdfDiagnosticCode.BadContentStream).ToList();
        await Assert.That(faults.Count).IsEqualTo(1);
        await Assert.That(faults[0].Message).Contains(message);
        await Assert.That(faults[0].ObjectNumber).IsEqualTo(PageNumber);
    }

    /// <summary>Turning content parsing off leaves content faults alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContentParsingCanBeSwitchedOff()
    {
        using var document = PdfDocumentReader.Open(Clean(BadOperator), null);
        await Assert.That(PdfDocumentCheck.Check(document, new PdfCheckOptions { ParseContent = false }).IsClean).IsTrue();
    }

    /// <summary>A page tree whose count, type and parent are wrong is reported node by node.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageTreeFaultsAreFound()
    {
        var file = MiniPdf.Build(Catalog, "<< /Type /Pages /Kids [3 0 R] /Count 5 >>", "<< /MediaBox [0 0 200 100] >>");
        using var document = PdfDocumentReader.Open(file, null);
        var report = PdfDocumentCheck.Check(document, PdfCheckOptions.Default);
        await Assert.That(Codes(report, PagesNumber)).Contains(PdfDiagnosticCode.BadStructure);
        var onPage = report.Faults.Where(static fault => fault.ObjectNumber == PageNumber).ToList();
        await Assert.That(onPage.Count).IsEqualTo(PageFaults);
    }

    /// <summary>The check without recovery gives one fault for a file that needs its table rebuilt, and with recovery it repairs and lists the repair.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrictCheckDoesNotRecover()
    {
        var damaged = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(RobustnessSeeds.CreateMini()).Replace("startxref", "startxxxx", StringComparison.Ordinal));
        var strict = PdfDocumentCheck.Check(damaged, new PdfCheckOptions { Recovery = false });
        var lenient = PdfDocumentCheck.Check(damaged, PdfCheckOptions.Default);
        await Assert.That(strict.Faults.Count).IsEqualTo(1);
        await Assert.That(strict.Faults[0].Code).IsEqualTo(PdfDiagnosticCode.BadStructure);
        await Assert.That(lenient.Faults.Select(static fault => fault.Code)).Contains(PdfDiagnosticCode.XrefRebuilt);
    }

    /// <summary>Ignoring cross-reference streams makes a file that needs them read as damaged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IgnoringCrossReferenceStreamsRebuildsTheTable()
    {
        var seed = RobustnessSeeds.Create().First(static s => s.Name == "compact-objstm").Bytes;
        var normal = PdfDocumentCheck.Check(seed, PdfCheckOptions.Default);
        var ignored = PdfDocumentCheck.Check(seed, new PdfCheckOptions { IgnoreXrefStreams = true });
        var strict = PdfDocumentCheck.Check(seed, new PdfCheckOptions { IgnoreXrefStreams = true, Recovery = false });
        await Assert.That(normal.Faults.Select(static fault => fault.Code)).DoesNotContain(PdfDiagnosticCode.XrefRebuilt);
        await Assert.That(ignored.Faults.Select(static fault => fault.Code)).Contains(PdfDiagnosticCode.XrefRebuilt);
        await Assert.That(strict.Faults.Count).IsEqualTo(1);
    }

    /// <summary>The asynchronous check finds what the synchronous one finds, and stops when cancelled.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncCheckMatchesAndCancels()
    {
        using var document = PdfDocumentReader.Open(Clean(BadOperator), null);
        var sync = PdfDocumentCheck.Check(document, PdfCheckOptions.Default);
        var async = await PdfDocumentCheck.CheckAsync(document, PdfCheckOptions.Default, CancellationToken.None);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.That(async.Faults.Select(static fault => fault.Code)).IsEquivalentTo(sync.Faults.Select(static fault => fault.Code));
        await Assert.That(async () => await PdfDocumentCheck.CheckAsync(document, PdfCheckOptions.Default, cancelled.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>The asynchronous check of a path reads the file and reports.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncPathCheckReadsTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"check-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, Clean(BadOperator));
        try
        {
            var report = await PdfDocumentCheck.CheckAsync(path, PdfCheckOptions.Default, CancellationToken.None);
            await Assert.That(report.Faults.Select(static fault => fault.Code)).Contains(PdfDiagnosticCode.BadContentStream);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A compact save writes the required types, parents, counts and boxes and drops repeated keys, so the saved file checks clean.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompactSaveWritesConformingStructures()
    {
        var file = MiniPdf.Build("<< /Pages 2 0 R >>", "<< /Kids [3 0 R] /Count 5 >>", "<< /MediaBox [200 100 0 0] /Contents 4 0 R /Rotate 0 /Rotate 90 >>", MiniPdf.Stream(string.Empty, GoodContent));
        using var damaged = PdfDocumentReader.Open(file, null);
        var saved = PdfCompactWriter.Save(damaged.Objects, PdfCompactOptions.Classic);
        var text = Encoding.Latin1.GetString(saved);
        using var reopened = PdfDocumentReader.Open(saved, null);
        await Assert.That(PdfDocumentCheck.WasRepaired(reopened)).IsFalse();
        await Assert.That(PdfDocumentCheck.Check(reopened, PdfCheckOptions.Default).IsClean).IsTrue();
        await Assert.That(reopened.Catalog.IsName(KnownName.Type, KnownName.Catalog)).IsTrue();
        await Assert.That(PdfDocumentPages.GetPage(reopened, 0).Height).IsEqualTo(PageWidth);
        await Assert.That(PdfDocumentPages.GetPage(reopened, 0).Rotation).IsEqualTo(Rotation);
        await Assert.That(text).DoesNotContain("/Rotate 0");
        await Assert.That(StoreRepairs.GetDiagnostics(damaged.Objects).Select(static fault => fault.Code)).Contains(PdfDiagnosticCode.FixedOnSave);
    }

    /// <summary>A file that needed its cross-reference table rebuilt saves to one that needs no repair, both ways of saving.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RebuiltFileSavesToACleanFile()
    {
        var damaged = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(RobustnessSeeds.CreateMini()).Replace("startxref", "startxxxx", StringComparison.Ordinal));
        using var document = PdfDocumentReader.Open(damaged, null);
        var compact = PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);
        var incremental = PdfIncrementalWriter.Save(document.Objects);
        using var fromCompact = PdfDocumentReader.Open(compact, null);
        using var fromIncremental = PdfDocumentReader.Open(incremental, null);
        await Assert.That(PdfDocumentCheck.WasRepaired(document)).IsTrue();
        await Assert.That(PdfDocumentCheck.WasRepaired(fromCompact)).IsFalse();
        await Assert.That(fromIncremental.Objects.WasRepaired).IsFalse();
        await Assert.That(fromCompact.PageCount).IsEqualTo(document.PageCount);
        await Assert.That(fromIncremental.PageCount).IsEqualTo(document.PageCount);
    }

    /// <summary>A compact save writes a truncated Flate stream again from the part that decoded, so the saved stream is whole.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompactSaveRewritesTruncatedStreams()
    {
        var packed = Compress(Encoding.Latin1.GetBytes(string.Join('\n', Enumerable.Repeat(GoodContent, Repeats))));
        var kept = Encoding.Latin1.GetString(packed.AsSpan(0, packed.Length * KeepPercent / Percent));
        using var damaged = PdfDocumentReader.Open(MiniPdf.Build(Catalog, Pages, ContentPage, MiniPdf.Stream("/Filter /FlateDecode", kept)), null);
        var expected = StoreReading.GetObject(damaged.Objects, new(ContentNumber, 0)).AsStream()!.DecodeToArray();
        var saved = PdfCompactWriter.Save(damaged.Objects, PdfCompactOptions.Classic);
        using var reopened = PdfDocumentReader.Open(saved, null);
        var report = PdfDocumentCheck.Check(reopened, PdfCheckOptions.Default);
        var content = PdfDocumentPages.GetPage(reopened, 0).Dictionary.Get(KnownName.Contents).AsStream()!.DecodeToArray();
        await Assert.That(report.Faults.Select(static fault => fault.Code)).DoesNotContain(PdfDiagnosticCode.TruncatedStream);
        await Assert.That(content.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    /// <summary>Builds a one-page document with the given content.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The file.</returns>
    private static byte[] Clean(string content) => MiniPdf.Build(Catalog, Pages, ContentPage, MiniPdf.Stream(string.Empty, content));

    /// <summary>Gets the codes of the faults of one object.</summary>
    /// <param name="report">The report.</param>
    /// <param name="number">The object number.</param>
    /// <returns>The codes.</returns>
    private static List<PdfDiagnosticCode> Codes(PdfCheckReport report, int number) => [.. report.Faults.Where(fault => fault.ObjectNumber == number).Select(static fault => fault.Code)];

    /// <summary>Compresses bytes as zlib.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The zlib data.</returns>
    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }
}
