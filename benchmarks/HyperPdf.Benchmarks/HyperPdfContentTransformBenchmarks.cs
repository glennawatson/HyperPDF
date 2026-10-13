// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures transforming the content and annotations of every page, and copying viewer preferences, each followed by its undo.</summary>
public class HyperPdfContentTransformBenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int Pages = 100;

    /// <summary>The page edge in points.</summary>
    private const int Edge = 200;

    /// <summary>The scale of the transform.</summary>
    private const float Scale = 0.5F;

    /// <summary>The move of the transform in points.</summary>
    private const float Move = 50;

    /// <summary>The low edge of the clip.</summary>
    private const float ClipLow = 60;

    /// <summary>The high edge of the clip.</summary>
    private const float ClipHigh = 140;

    /// <summary>The transform applied to every page.</summary>
    private static readonly Matrix3x2 Shrink = Matrix3x2.CreateScale(Scale) * Matrix3x2.CreateTranslation(Move, Move);

    /// <summary>The pages every operation changes.</summary>
    private readonly int[] _pages = CreatePages();

    /// <summary>The document edited and restored by each operation.</summary>
    private PdfDocument _target = null!;

    /// <summary>A document with viewer preferences.</summary>
    private PdfDocument _source = null!;

    /// <summary>Opens the fixtures and warms each operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _target = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        _source = PdfDocumentReader.Open(new RenderTestPdf(Edge, Edge) { CatalogEntries = "/ViewerPreferences << /HideToolbar true /FitWindow true >>" }.ToBytes(), null);
        _ = TransformAndUndo();
        _ = CopyPreferencesAndUndo();
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _target.Dispose();
        _source.Dispose();
    }

    /// <summary>Shrinks the content of every page inside a clip, then undoes it.</summary>
    /// <returns>Whether the undo restored the document.</returns>
    [Benchmark]
    public bool TransformAndUndo()
    {
        PdfDocumentContentTransforms.TransformPages(_target, _pages, Shrink, new() { Clip = new PdfRectangle(ClipLow, ClipLow, ClipHigh, ClipHigh) });
        return PdfDocumentEditing.Undo(_target);
    }

    /// <summary>Copies the viewer preferences of another document, then undoes it.</summary>
    /// <returns>Whether the undo restored the document.</returns>
    [Benchmark]
    public bool CopyPreferencesAndUndo()
    {
        _ = PdfDocumentViewerPreferences.CopyViewerPreferences(_target, _source, CancellationToken.None);
        return PdfDocumentEditing.Undo(_target);
    }

    /// <summary>Lists every page of the fixture.</summary>
    /// <returns>The page indexes.</returns>
    private static int[] CreatePages()
    {
        var pages = new int[Pages];
        for (var i = 0; i < pages.Length; i++)
        {
            pages[i] = i;
        }

        return pages;
    }
}
