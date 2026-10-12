// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Tests for <see cref="PdfOpenOptions"/> and <see cref="PdfDiagnostic"/>.</summary>
public sealed class DiagnosticsTests
{
    /// <summary>A page that points at a content stream in object 4.</summary>
    private const string ContentPage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 9 9] /Contents 4 0 R >>";

    /// <summary>The catalog.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The page tree node.</summary>
    private const string Pages = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";

    /// <summary>The object number of the content stream or other object 4 in a test file.</summary>
    private const int ContentNumber = 4;

    /// <summary>The object number of the first page.</summary>
    private const int PageNumber = 3;

    /// <summary>The object number of the stream in the mini document.</summary>
    private const int MiniStreamNumber = 5;

    /// <summary>The junk placed before the header.</summary>
    private const string Junk = "junk bytes before the header\n";

    /// <summary>The page tree depth that passes the limit.</summary>
    private const int TooDeep = 70;

    /// <summary>The number of bytes in a run-length pair.</summary>
    private const int PairLength = 2;

    /// <summary>The run-length control byte that repeats the next byte 128 times.</summary>
    private const byte RepeatMax = 129;

    /// <summary>The decoded size cap, 256 MiB.</summary>
    private const int DecodeCap = 0x1000_0000;

    /// <summary>The bytes one run-length pair expands to.</summary>
    private const int RunLength = 128;

    /// <summary>The pairs needed to pass the decoded size cap.</summary>
    private const int CapPairs = (DecodeCap / RunLength) + 1;

    /// <summary>The number of reader tasks.</summary>
    private const int Readers = 4;

    /// <summary>A clean file reports nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CleanFileReportsNothing()
    {
        var found = new ConcurrentQueue<PdfDiagnostic>();
        using var document = PdfDocumentReader.OpenWith(RobustnessSeeds.CreateMini(), new PdfOpenOptions { Diagnostics = found.Enqueue });
        _ = DocumentExerciser.Read(document);
        await Assert.That(found).IsEmpty();
    }

    /// <summary>A scrambled startxref makes the open rebuild the table and say so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScrambledStartXrefReportsRebuild()
    {
        var found = Open(Replace(RobustnessSeeds.CreateMini(), "startxref", "startxxxx"));
        await Assert.That(Codes(found)).Contains(PdfDiagnosticCode.XrefRebuilt);
    }

    /// <summary>Junk before the header is reported with its length.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JunkBeforeHeaderReportsHeaderOffset()
    {
        var found = Open(Encoding.Latin1.GetBytes(Junk + Encoding.Latin1.GetString(RobustnessSeeds.CreateMini())));
        var report = Find(found, PdfDiagnosticCode.HeaderOffset);
        await Assert.That(report.Offset).IsEqualTo(Junk.Length);
    }

    /// <summary>An object that is not where the table says is reported by number, along with the rebuild.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MisplacedObjectReportsBrokenObject()
    {
        var file = RobustnessSeeds.CreateMini();
        var text = Encoding.Latin1.GetString(file);
        var offset = text.IndexOf("\n3 0 obj", StringComparison.Ordinal) + 1;
        var row = string.Create(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n");
        var found = Open(Replace(file, row, "0000000001 00000 n"));
        await Assert.That(Find(found, PdfDiagnosticCode.BrokenObject).ObjectNumber).IsEqualTo(PageNumber);
        await Assert.That(Codes(found)).Contains(PdfDiagnosticCode.XrefRebuilt);
    }

    /// <summary>A wrong /Length is reported with the stream's object number.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrongLengthReportsBadStreamLength()
    {
        var file = MiniPdf.Build(Catalog, Pages, ContentPage, "<< /Length 3 >>\nstream\nabcdefgh\nendstream");
        var found = Open(file);
        await Assert.That(Find(found, PdfDiagnosticCode.BadStreamLength).ObjectNumber).IsEqualTo(ContentNumber);
    }

    /// <summary>A filter the library does not know is reported, and the data passes through.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownFilterIsReported()
    {
        var file = MiniPdf.Build(Catalog, Pages, ContentPage, MiniPdf.Stream("/Filter /NoSuchDecode", "abc"));
        var found = Open(file);
        await Assert.That(Find(found, PdfDiagnosticCode.UnknownFilter).ObjectNumber).IsEqualTo(ContentNumber);
    }

    /// <summary>A stream that decodes past the size cap is cut short and reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodePastTheCapIsReported()
    {
        var runs = new byte[CapPairs * PairLength];
        for (var i = 0; i < runs.Length; i += PairLength)
        {
            runs[i] = RepeatMax;
            runs[i + 1] = (byte)'a';
        }

        var file = MiniPdf.Build(Catalog, Pages, ContentPage, MiniPdf.Stream("/Filter /RunLengthDecode", Encoding.Latin1.GetString(runs)));
        var found = new ConcurrentQueue<PdfDiagnostic>();
        using var document = PdfDocumentReader.OpenWith(file, new PdfOpenOptions { Diagnostics = found.Enqueue });
        var stream = StoreReading.GetObject(document.Objects, new(ContentNumber, 0)).AsStream()!;
        var length = stream.DecodeToArray().Length;
        await Assert.That(Find(found, PdfDiagnosticCode.DecodeSizeCapped).ObjectNumber).IsEqualTo(ContentNumber);
        await Assert.That(length).IsGreaterThan(0);
    }

    /// <summary>A page tree deeper than the limit is cut off and reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeepPageTreeReportsRecursionLimit()
    {
        var objects = new List<string> { Catalog, };
        for (var i = 0; i < TooDeep; i++)
        {
            objects.Add(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{i + PageNumber} 0 R] /Count 1 >>"));
        }

        objects.Add("<< /Type /Page /MediaBox [0 0 9 9] >>");
        var found = Open(MiniPdf.Build([.. objects]));
        await Assert.That(Codes(found)).Contains(PdfDiagnosticCode.RecursionLimit);
    }

    /// <summary>A token that is already cancelled stops the open.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledTokenStopsOpen()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var file = RobustnessSeeds.CreateMini();
        var options = new PdfOpenOptions { CancellationToken = source.Token, };
        await Assert.That(() => PdfDocumentReader.OpenWith(file, options)).Throws<OperationCanceledException>();
    }

    /// <summary>A token that is already cancelled stops the repair scan.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledTokenStopsRepairScan()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var file = Replace(RobustnessSeeds.CreateMini(), "startxref", "startxxxx");
        var options = new PdfOpenOptions { CancellationToken = source.Token, };
        await Assert.That(() => PdfDocumentReader.OpenWith(file, options)).Throws<OperationCanceledException>();
    }

    /// <summary>Cancelling after the open stops later stream decoding.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellingAfterOpenStopsDecoding()
    {
        using var source = new CancellationTokenSource();
        using var document = PdfDocumentReader.OpenWith(RobustnessSeeds.CreateMini(), new PdfOpenOptions { CancellationToken = source.Token });
        var stream = StoreReading.GetObject(document.Objects, new(MiniStreamNumber, 0)).AsStream()!;
        var before = stream.DecodeToArray();
        await source.CancelAsync();
        await Assert.That(before.Length).IsGreaterThan(0);
        await Assert.That(() => stream.DecodeToArray()).Throws<OperationCanceledException>();
    }

    /// <summary>A sink called from several reader tasks at once sees every report without losing any.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SinkIsSafeFromManyThreads()
    {
        var file = MiniPdf.Build(Catalog, Pages, ContentPage, MiniPdf.Stream("/Filter /NoSuchDecode", "abc"));
        var found = new ConcurrentQueue<PdfDiagnostic>();
        using var document = PdfDocumentReader.OpenWith(file, new PdfOpenOptions { Diagnostics = found.Enqueue });
        var stream = StoreReading.GetObject(document.Objects, new(ContentNumber, 0)).AsStream()!;
        var tasks = new Task[Readers];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() => stream.DecodeToArray());
        }

        await Task.WhenAll(tasks);
        await Assert.That(found.Count).IsEqualTo(Readers);
    }

    /// <summary>Opens a file with a sink, reads everything, and returns the reports.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The reports.</returns>
    private static ConcurrentQueue<PdfDiagnostic> Open(byte[] file)
    {
        var found = new ConcurrentQueue<PdfDiagnostic>();
        using var document = PdfDocumentReader.OpenWith(file, new PdfOpenOptions { Diagnostics = found.Enqueue });
        _ = DocumentExerciser.Read(document);
        return found;
    }

    /// <summary>Gets the codes of the reports.</summary>
    /// <param name="found">The reports.</param>
    /// <returns>The codes.</returns>
    private static List<PdfDiagnosticCode> Codes(ConcurrentQueue<PdfDiagnostic> found)
    {
        var codes = new List<PdfDiagnosticCode>();
        foreach (var report in found)
        {
            codes.Add(report.Code);
        }

        return codes;
    }

    /// <summary>Finds the first report with a code.</summary>
    /// <param name="found">The reports.</param>
    /// <param name="code">The code.</param>
    /// <returns>The report, or a default one with code None when absent.</returns>
    private static PdfDiagnostic Find(ConcurrentQueue<PdfDiagnostic> found, PdfDiagnosticCode code)
    {
        foreach (var report in found)
        {
            if (report.Code == code)
            {
                return report;
            }
        }

        return default;
    }

    /// <summary>Replaces text in a file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="from">The text to find.</param>
    /// <param name="to">The replacement.</param>
    /// <returns>The new file.</returns>
    private static byte[] Replace(byte[] file, string from, string to) => Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(file).Replace(from, to, StringComparison.Ordinal));
}
