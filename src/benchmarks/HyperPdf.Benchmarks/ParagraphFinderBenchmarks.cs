// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;

namespace HyperPdf.Benchmarks;

/// <summary>Measures finding the lines of a paragraph in a page of forty-eight lines of text: left aligned lines, centred lines, and a list item whose bullet is a text of its own.</summary>
public class ParagraphFinderBenchmarks
{
    /// <summary>The lines on the page.</summary>
    private const int Lines = 48;

    /// <summary>The height of a line.</summary>
    private const float LineHeight = 12F;

    /// <summary>The distance from the top of one line to the top of the next.</summary>
    private const float Step = 14F;

    /// <summary>The left margin.</summary>
    private const float Margin = 72F;

    /// <summary>The width of the widest lines.</summary>
    private const float Widest = 450F;

    /// <summary>How much narrower each line of the centred page is than the one before, over three lines.</summary>
    private const float Narrowing = 30F;

    /// <summary>The number of widths the centred lines repeat over.</summary>
    private const int Widths = 3;

    /// <summary>How far in the words of a list item are from its bullet.</summary>
    private const float Hanging = 14F;

    /// <summary>The width of a bullet.</summary>
    private const float BulletWidth = 6F;

    /// <summary>The place of the line that is picked.</summary>
    private const int Middle = Lines / 2;

    /// <summary>The middle of the centred lines.</summary>
    private const float CentreLine = Margin + (Widest / 2);

    /// <summary>One half.</summary>
    private const float Half = 0.5F;

    /// <summary>The words of every line.</summary>
    private const string LineText = "The quick brown fox jumps over the lazy dog";

    /// <summary>The page of left aligned lines.</summary>
    private PageObjectInfo[] _left = [];

    /// <summary>The page of centred lines.</summary>
    private PageObjectInfo[] _centred = [];

    /// <summary>The page of list items with bullets of their own.</summary>
    private PageObjectInfo[] _list = [];

    /// <summary>Builds the pages.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _left = new PageObjectInfo[Lines];
        _centred = new PageObjectInfo[Lines];
        for (var i = 0; i < Lines; i++)
        {
            _left[i] = new(i, PageObjectKind.Text, new(Margin, i * Step, Widest, LineHeight), LineText);
            var width = Widest - ((i % Widths) * Narrowing);
            _centred[i] = new(i, PageObjectKind.Text, new(CentreLine - (width * Half), i * Step, width, LineHeight), LineText);
        }

        // Every item is a bullet and three lines, so the page has twelve items.
        var objects = new List<PageObjectInfo>(Lines + (Lines / Widths));
        for (var i = 0; i < Lines; i++)
        {
            if (i % Widths == 0)
            {
                objects.Add(new(objects.Count, PageObjectKind.Text, new(Margin, i * Step, BulletWidth, LineHeight), "•"));
            }

            objects.Add(new(objects.Count, PageObjectKind.Text, new(Margin + Hanging, i * Step, Widest, LineHeight), LineText));
        }

        _list = [.. objects];
    }

    /// <summary>Finds the lines of a paragraph of left aligned lines, which is every line of the page.</summary>
    /// <returns>The number of lines found, so the work is not optimised away.</returns>
    [Benchmark(Baseline = true)]
    public int FindLeftAligned() => ParagraphFinder.Find(_left, _left[Middle]).Length;

    /// <summary>Finds the lines of a paragraph of centred lines, which is every line of the page.</summary>
    /// <returns>The number of lines found.</returns>
    [Benchmark]
    public int FindCentred() => ParagraphFinder.Find(_centred, _centred[Middle]).Length;

    /// <summary>Finds the lines of a list item whose bullet is a text of its own.</summary>
    /// <returns>The number of lines found.</returns>
    [Benchmark]
    public int FindListItem() => ParagraphFinder.Find(_list, _list[Middle + 1]).Length;
}
