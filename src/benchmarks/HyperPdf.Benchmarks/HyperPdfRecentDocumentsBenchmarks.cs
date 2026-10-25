// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using BenchmarkDotNet.Attributes;
using HyperPdf.Platform.Linux.Recent;

namespace HyperPdf.Benchmarks;

/// <summary>Reads the recent documents from, and adds one to, a recently-used.xbel file that holds the 500 bookmarks the store keeps, 100 of them for files that exist.</summary>
public class HyperPdfRecentDocumentsBenchmarks
{
    /// <summary>The bookmarks in the file.</summary>
    private const int Bookmarks = 500;

    /// <summary>The bookmarks whose files exist.</summary>
    private const int ExistingFiles = 100;

    /// <summary>The recent documents asked for.</summary>
    private const int Recent = 10;

    /// <summary>The folder the files and the XBEL file are in.</summary>
    private string _folder = string.Empty;

    /// <summary>The store.</summary>
    private XbelRecentDocumentStore _store = null!;

    /// <summary>A file the store already holds.</summary>
    private string _existing = string.Empty;

    /// <summary>Writes the files and fills the store outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-recent-bench-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_folder);
        _store = new(Path.Combine(_folder, "recently-used.xbel"), TimeProvider.System);
        for (var i = 0; i < Bookmarks; i++)
        {
            var path = Path.Combine(_folder, string.Create(CultureInfo.InvariantCulture, $"document-{i}.pdf"));
            if (i < ExistingFiles)
            {
                File.WriteAllText(path, "%PDF-1.7");
                _existing = path;
            }

            _store.Add(path);
        }
    }

    /// <summary>Deletes the files.</summary>
    [GlobalCleanup]
    public void Cleanup() => Directory.Delete(_folder, true);

    /// <summary>Lists the ten most recent documents.</summary>
    /// <returns>The documents listed.</returns>
    [Benchmark]
    public int GetRecentDocuments() => _store.GetRecent(Recent).Count;

    /// <summary>Records that a document the file already holds was opened again.</summary>
    /// <returns>The documents listed afterwards.</returns>
    [Benchmark]
    public int AddOpenedDocument()
    {
        _store.Add(_existing);
        return Recent;
    }
}
