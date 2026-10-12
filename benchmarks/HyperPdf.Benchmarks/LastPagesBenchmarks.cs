// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Settings;

namespace HyperPdf.Benchmarks;

/// <summary>Measures finding and recording where a document was closed, with the most documents remembered.</summary>
public class LastPagesBenchmarks
{
    /// <summary>The documents remembered.</summary>
    private const int DocumentCount = 200;

    /// <summary>The remembered pages.</summary>
    private readonly List<LastViewedPage> _pages = [with(DocumentCount + 1)];

    /// <summary>The oldest document, found last.</summary>
    private string _oldest = string.Empty;

    /// <summary>Fills the remembered pages.</summary>
    [GlobalSetup]
    public void Setup()
    {
        for (var i = 0; i < DocumentCount; i++)
        {
            LastPages.Remember(_pages, string.Create(CultureInfo.InvariantCulture, $"/home/user/Documents/report-{i}.pdf"), i);
        }

        _oldest = _pages[0].FilePath;
    }

    /// <summary>Finds the oldest remembered document, the longest search.</summary>
    /// <returns>The page.</returns>
    [Benchmark]
    public int FindOldest() => LastPages.Find(_pages, _oldest);

    /// <summary>Records the newest document again, as closing its tab does.</summary>
    /// <returns>The remembered count.</returns>
    [Benchmark]
    public int RememberNewest()
    {
        LastPages.Remember(_pages, _pages[^1].FilePath, 1);
        return _pages.Count;
    }
}
