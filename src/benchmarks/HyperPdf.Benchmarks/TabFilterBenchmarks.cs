// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Tabs;

namespace HyperPdf.Benchmarks;

/// <summary>Measures filtering a thousand open tabs as the user types in the tab finder; allocations should be zero.</summary>
public class TabFilterBenchmarks
{
    /// <summary>The number of open tabs.</summary>
    private const int TabCount = 1000;

    /// <summary>How often a tab is a "report".</summary>
    private const int ReportEvery = 7;

    /// <summary>The tabs.</summary>
    private readonly TabSummary[] _tabs = new TabSummary[TabCount];

    /// <summary>The matching indexes.</summary>
    private readonly int[] _matches = new int[TabCount];

    /// <summary>Creates the tabs.</summary>
    [GlobalSetup]
    public void Setup()
    {
        for (var i = 0; i < TabCount; i++)
        {
            var kind = i % ReportEvery == 0 ? "report" : "notes";
            _tabs[i] = new(
                string.Create(CultureInfo.InvariantCulture, $"{kind}-{i}.pdf"),
                string.Create(CultureInfo.InvariantCulture, $"Quarterly {kind} {i}"),
                string.Create(CultureInfo.InvariantCulture, $"/home/user/Documents/Project {i % ReportEvery}"));
        }
    }

    /// <summary>Filters the tabs by a two word query.</summary>
    /// <returns>The number of matches.</returns>
    [Benchmark]
    public int FilterThousandTabs() => TabFilter.Filter("quarterly report", _tabs, _matches);
}
