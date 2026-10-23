// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Engine;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>Measures viewer file opening and first-page preparation with externally editable files.</summary>
public class ViewerFileOpenBenchmarks
{
    /// <summary>The viewer engine, reused across opens.</summary>
    private readonly HyperPdfEngine _engine = new();

    /// <summary>The generated document path.</summary>
    private string _path = string.Empty;

    /// <summary>Gets or sets the generated page count.</summary>
    [Params(1, 64)]
    public int Pages { get; set; }

    /// <summary>Writes a document before measurement.</summary>
    /// <returns>A task.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"hyperpdf-viewer-open-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(_path, TestPdf.Create(Pages), CancellationToken.None);
        using var document = _engine.Open(_path, null);
        await document.PreparePageAsync(0, CancellationToken.None);
    }

    /// <summary>Deletes the generated document.</summary>
    [GlobalCleanup]
    public void Cleanup() => File.Delete(_path);

    /// <summary>Opens through the viewer's synchronous adapter and prepares its first page.</summary>
    /// <returns>The page count.</returns>
    [Benchmark]
    public async ValueTask<int> OpenAndPrepareFirstPage()
    {
        using var document = _engine.Open(_path, null);
        await document.PreparePageAsync(0, CancellationToken.None).ConfigureAwait(false);
        return document.PageCount;
    }
}
