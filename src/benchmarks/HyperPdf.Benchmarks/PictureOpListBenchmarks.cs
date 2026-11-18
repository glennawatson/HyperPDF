// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Render.Managed.Pictures;

namespace HyperPdf.Benchmarks;

/// <summary>Measures recording the steps of a page into a list and giving the list up, which every page render does once.</summary>
public class PictureOpListBenchmarks
{
    /// <summary>Gets or sets the steps of a page, which are a little over the size at which the list's array is a power of two for the middle size.</summary>
    [Params(300, 1100, 5000)]
    public int Steps { get; set; }

    /// <summary>Records the steps and disposes the list.</summary>
    /// <returns>The number of steps recorded.</returns>
    [Benchmark]
    public int RecordAndDispose()
    {
        using PictureOpList list = new();
        for (var i = 0; i < Steps; i++)
        {
            list.Add(default);
        }

        return list.Count;
    }
}
