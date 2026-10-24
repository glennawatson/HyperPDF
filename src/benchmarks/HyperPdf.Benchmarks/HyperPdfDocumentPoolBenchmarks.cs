// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Engine;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>Takes an already open document from a pool of 40 tabs, which counts the open tabs and checks the pool's capacity each time.</summary>
public class HyperPdfDocumentPoolBenchmarks
{
    /// <summary>The tabs in the pool.</summary>
    private const int Tabs = 40;

    /// <summary>The folder holding the files.</summary>
    private string _folder = string.Empty;

    /// <summary>The pool.</summary>
    private DocumentPool _pool = null!;

    /// <summary>The tab taken from the pool.</summary>
    private DocumentSource _source = null!;

    /// <summary>Writes the files and opens every tab.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-pool-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_folder);
        var bytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        _pool = new(new HyperPdfEngine(), Tabs);
        for (var i = 0; i < Tabs; i++)
        {
            var path = Path.Combine(_folder, $"{i}.pdf");
            File.WriteAllBytes(path, bytes);
            _source = _pool.Create(path, null);
            _ = _source.Acquire();
        }
    }

    /// <summary>Closes the pool and removes the files.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _pool.Dispose();
        Directory.Delete(_folder, true);
    }

    /// <summary>Takes the open document of the last tab.</summary>
    /// <returns>Whether a document was returned.</returns>
    [Benchmark]
    public bool AcquireOpen() => _source.Acquire() is not null;
}
