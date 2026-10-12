// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>
/// The cost when I/O really waits, as on a network share or a cold disk. The documents are read through a stream that
/// delays each read by a fixed time: blocking reads for the synchronous API, awaited reads for the async API. The
/// four-tab benchmarks open four documents at once, as restoring a session does.
/// </summary>
public class HyperPdfAsyncSlowStreamBenchmarks
{
    /// <summary>The documents opened at once.</summary>
    private const int Tabs = 4;

    /// <summary>The pages whose text is read.</summary>
    private const int TextPages = 3;

    /// <summary>The bytes of the sample.</summary>
    private byte[] _bytes = [];

    /// <summary>Gets or sets the sample: Report or Scan.</summary>
    [Params("Report", "Scan")]
    public string Document { get; set; } = "Report";

    /// <summary>Gets or sets the delay of each read, in milliseconds.</summary>
    [Params(1, 5)]
    public int LatencyMilliseconds { get; set; }

    /// <summary>Reads the sample.</summary>
    /// <returns>A task.</returns>
    [GlobalSetup]
    public async Task Setup() => _bytes = await File.ReadAllBytesAsync(HyperPdfAsyncSamples.PathFor(Document)).ConfigureAwait(false);

    /// <summary>Opens a document through blocking reads and reads the text of its first pages.</summary>
    /// <returns>The characters.</returns>
    [Benchmark(Baseline = true)]
    public int OpenAndReadTextSync() => ReadTextSync();

    /// <summary>Opens a document through awaited reads and reads the text of its first pages.</summary>
    /// <returns>The characters.</returns>
    [Benchmark]
    public Task<int> OpenAndReadTextAsync() => ReadTextAsync();

    /// <summary>Opens four documents at once on pool threads that block on their reads.</summary>
    /// <returns>The characters.</returns>
    [Benchmark]
    public int FourTabsSync()
    {
        var characters = 0;
        _ = Parallel.For(0, Tabs, _ => Interlocked.Add(ref characters, ReadTextSync()));
        return characters;
    }

    /// <summary>Opens four documents at once with awaited reads, holding no thread while they wait.</summary>
    /// <returns>The characters.</returns>
    [Benchmark]
    public async Task<int> FourTabsAsync()
    {
        var tasks = new Task<int>[Tabs];
        for (var i = 0; i < Tabs; i++)
        {
            tasks[i] = ReadTextAsync();
        }

        var characters = 0;
        foreach (var count in await Task.WhenAll(tasks).ConfigureAwait(false))
        {
            characters += count;
        }

        return characters;
    }

    /// <summary>Opens the sample through blocking reads and reads the first pages' text.</summary>
    /// <returns>The characters.</returns>
    private int ReadTextSync()
    {
        using var stream = new ThrottledStream(_bytes, TimeSpan.FromMilliseconds(LatencyMilliseconds));
        using var document = PdfDocumentReader.Open(stream, null);
        var characters = 0;
        for (var i = 0; i < Math.Min(TextPages, document.PageCount); i++)
        {
            characters += PdfDocumentText.GetTextPage(document, i).CharCount;
        }

        return characters;
    }

    /// <summary>Opens the sample through awaited reads and reads the first pages' text.</summary>
    /// <returns>The characters.</returns>
    private async Task<int> ReadTextAsync()
    {
        await using var stream = new ThrottledStream(_bytes, TimeSpan.FromMilliseconds(LatencyMilliseconds));
        using var document = await PdfDocumentReader.OpenAsync(stream, null, CancellationToken.None).ConfigureAwait(false);
        var characters = 0;
        for (var i = 0; i < Math.Min(TextPages, document.PageCount); i++)
        {
            characters += (await PdfDocumentText.GetTextPageAsync(document, i, CancellationToken.None).ConfigureAwait(false)).CharCount;
        }

        return characters;
    }
}
