// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Annotations;
using HyperPdf.ViewModels.Markup;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the sidebar annotation filter reading each item's search text for a typed query.</summary>
public class AnnotationItemFilterTextBenchmarks
{
    /// <summary>The number of annotations in the list.</summary>
    private const int ItemCount = 5000;

    /// <summary>Every this many items has a note that matches the query.</summary>
    private const int MatchEvery = 50;

    /// <summary>The number of annotations on each page.</summary>
    private const int PerPage = 10;

    /// <summary>The query typed into the filter box.</summary>
    private const string Query = "budget";

    /// <summary>The items the filter looks through.</summary>
    private AnnotationItemViewModel[] _items = [];

    /// <summary>Creates the items, with notes that match the query once in every fifty.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _items = new AnnotationItemViewModel[ItemCount];
        for (var i = 0; i < ItemCount; i++)
        {
            var note = i % MatchEvery == 0 ? $"Check the budget line {i}" : $"Reword paragraph {i}";
            _items[i] = new(new PageAnnotation(i / PerPage, i % PerPage, AnnotationKind.Note, default, 0, note, "Reviewer"), "1", []);
        }
    }

    /// <summary>Filters every item by the typed query, as one keystroke does.</summary>
    /// <returns>The number of items that match.</returns>
    [Benchmark]
    public int FilterByTypedQuery()
    {
        var shown = 0;
        foreach (var item in _items)
        {
            if (item.SearchText.Contains(Query, StringComparison.CurrentCultureIgnoreCase))
            {
                shown++;
            }
        }

        return shown;
    }
}
