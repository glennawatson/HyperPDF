// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Engine;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>Measures adding and removing a caret and a freehand highlight; allocations come from the EventPipe trace.</summary>
public class CaretAndHighlighterBenchmarks
{
    /// <summary>The page the annotations go on.</summary>
    private const int Page = 0;

    /// <summary>The width of the highlighter pen in points.</summary>
    private const float PenWidth = 14;

    /// <summary>A pure yellow pen.</summary>
    private const uint Yellow = 0xFFFF00;

    /// <summary>The height of the stroke, in points from the top of the page.</summary>
    private const int StrokeY = 300;

    /// <summary>Where the caret's tip goes.</summary>
    private static readonly PagePoint CaretAt = new(200, 400);

    /// <summary>The stroke: one straight line across the page.</summary>
    private static readonly PagePoint[] Stroke = [new(100, StrokeY), new(220, StrokeY)];

    /// <summary>The length of the stroke.</summary>
    private static readonly int[] Strokes = [Stroke.Length];

    /// <summary>The document.</summary>
    private BenchmarkPdfDocument _document = null!;

    /// <summary>The native annotation state of the document.</summary>
    private HyperPdfAnnotations _state = null!;

    /// <summary>The document's editor.</summary>
    private IAnnotationEditor _editor = null!;

    /// <summary>Opens the document.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = new(TestPdf.Create(1));
        _state = HyperPdfAnnotationStateAccess.GetAnnotations((HyperPdfDocument)_document.Document);
        _editor = DocumentFeature<IAnnotationEditor>.Cast(_document.Document)!;
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Adds a caret and removes it again.</summary>
    /// <returns>Whether the caret was removed.</returns>
    [Benchmark]
    public bool AddAndRemoveCaret() => _editor.Remove(Page, HyperPdfAnnotationCarets.AddCaret(_state, Page, CaretAt, Yellow));

    /// <summary>Adds a freehand highlight and removes it again.</summary>
    /// <returns>Whether the highlight was removed.</returns>
    [Benchmark]
    public bool AddAndRemoveHighlight() =>
        _editor.Remove(Page, HyperPdfAnnotationShapes.AddInk(_state, Page, Stroke, Strokes, Yellow, PenWidth, AnnotationKind.FreehandHighlight));
}
