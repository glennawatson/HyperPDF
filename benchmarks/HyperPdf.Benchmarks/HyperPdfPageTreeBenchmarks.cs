// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Measures page tree traversal with tiny, wide and deep trees already loaded in memory.</summary>
public class HyperPdfPageTreeBenchmarks
{
    /// <summary>The pages in the wide tree.</summary>
    private const int WidePages = 50;

    /// <summary>The intermediate nodes in the deep tree.</summary>
    private const int DeepNodes = 32;

    /// <summary>The number of objects before flat-tree pages.</summary>
    private const int HeaderObjects = 2;

    /// <summary>The first page object number.</summary>
    private const int FirstPageObjectNumber = 3;

    /// <summary>The tiny document.</summary>
    private PdfDocument? _tiny;

    /// <summary>The wide document.</summary>
    private PdfDocument? _wide;

    /// <summary>The deep document.</summary>
    private PdfDocument? _deep;

    /// <summary>Opens each document before measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _tiny = PdfDocumentReader.Open(WideTree(1), null);
        _wide = PdfDocumentReader.Open(WideTree(WidePages), null);
        _deep = PdfDocumentReader.Open(DeepTree(DeepNodes), null);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _tiny?.Dispose();
        _wide?.Dispose();
        _deep?.Dispose();
    }

    /// <summary>Reads one leaf under a root node.</summary>
    /// <returns>The page count.</returns>
    [Benchmark(Baseline = true)]
    public int Tiny() => PageTreeReader.Read(_tiny!.Objects).Count;

    /// <summary>Reads many leaf siblings under one root node.</summary>
    /// <returns>The page count.</returns>
    [Benchmark]
    public int Wide() => PageTreeReader.Read(_wide!.Objects).Count;

    /// <summary>Reads one leaf under a chain of intermediate nodes.</summary>
    /// <returns>The page count.</returns>
    [Benchmark]
    public int Deep() => PageTreeReader.Read(_deep!.Objects).Count;

    /// <summary>Builds a tree with one root and the requested number of leaf siblings.</summary>
    /// <param name="count">The page count.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] WideTree(int count)
    {
        var objects = new string[count + HeaderObjects];
        objects[0] = "<< /Type /Catalog /Pages 2 0 R >>";
        var references = new string[count];
        for (var i = 0; i < count; i++)
        {
            references[i] = string.Create(CultureInfo.InvariantCulture, $"{i + FirstPageObjectNumber} 0 R");
            objects[i + HeaderObjects] = "<< /Type /Page /Parent 2 0 R >>";
        }

        objects[1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{string.Join(' ', references)}] /Count {count} >>");
        return MiniPdf.Build(objects);
    }

    /// <summary>Builds a chain of intermediate nodes ending in one leaf.</summary>
    /// <param name="count">The intermediate node count.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] DeepTree(int count)
    {
        var objects = new string[count + HeaderObjects];
        objects[0] = "<< /Type /Catalog /Pages 2 0 R >>";
        for (var i = 0; i < count; i++)
        {
            objects[i + 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{i + FirstPageObjectNumber} 0 R] /Count 1 >>");
        }

        objects[count + 1] = "<< /Type /Page >>";
        return MiniPdf.Build(objects);
    }
}
