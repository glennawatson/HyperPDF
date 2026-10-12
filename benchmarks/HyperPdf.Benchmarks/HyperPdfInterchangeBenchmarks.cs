// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Interchange;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures exporting and importing 500 annotations as XFDF and FDF. The import benchmarks open a fresh document each
/// time, so <see cref="OpenOnly"/> is the cost to take off them. Allocations are checked from the EventPipe trace.
/// </summary>
public class HyperPdfInterchangeBenchmarks
{
    /// <summary>The annotations exported and imported.</summary>
    private const int Annotations = 500;

    /// <summary>The columns of the grid the annotations are laid out on.</summary>
    private const int Columns = 20;

    /// <summary>The width of one annotation.</summary>
    private const float Width = 20;

    /// <summary>The height of one annotation.</summary>
    private const float Height = 10;

    /// <summary>The gap between annotations.</summary>
    private const float Gap = 5;

    /// <summary>The page the annotations are on.</summary>
    private const int Page = 0;

    /// <summary>Yellow as 0xRRGGBB.</summary>
    private const uint Yellow = 0xFFFF00;

    /// <summary>Gets the repeating subtypes: text markup, shapes and notes.</summary>
    private static readonly string[] Subtypes = ["Highlight", "Underline", "Square", "Text", "StrikeOut"];

    /// <summary>The PDF the documents are opened from.</summary>
    private byte[] _pdf = [];

    /// <summary>The XFDF file of the annotations.</summary>
    private byte[] _xfdf = [];

    /// <summary>The FDF file of the annotations.</summary>
    private byte[] _fdf = [];

    /// <summary>The data both files were made from.</summary>
    private PdfInterchangeData _data = new();

    /// <summary>A document holding the annotations, for exporting.</summary>
    private PdfDocument _annotated = null!;

    /// <summary>Makes the files and the annotated document.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _pdf = TestPdf.Create(1);
        _data = CreateData();
        _xfdf = XfdfWriter.Write(_data);
        _fdf = FdfWriter.Write(_data);
        _annotated = PdfDocumentReader.Open(_pdf, null);
        _ = PdfInterchange.Import(_annotated, _data);
    }

    /// <summary>Disposes the annotated document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _annotated.Dispose();

    /// <summary>Opens the document and nothing else: the cost the import benchmarks include.</summary>
    /// <returns>The page count.</returns>
    [Benchmark(Baseline = true)]
    public int OpenOnly()
    {
        using var document = PdfDocumentReader.Open(_pdf, null);
        return document.PageCount;
    }

    /// <summary>Exports the annotations as XFDF.</summary>
    /// <returns>The file's length.</returns>
    [Benchmark]
    public int ExportXfdf() => PdfInterchange.ExportXfdf(_annotated).Length;

    /// <summary>Exports the annotations as FDF.</summary>
    /// <returns>The file's length.</returns>
    [Benchmark]
    public int ExportFdf() => PdfInterchange.ExportFdf(_annotated).Length;

    /// <summary>Opens a document and imports the XFDF file.</summary>
    /// <returns>The annotations added.</returns>
    [Benchmark]
    public int ImportXfdf()
    {
        using var document = PdfDocumentReader.Open(_pdf, null);
        return PdfInterchange.ImportXfdf(document, _xfdf).AnnotationsAdded;
    }

    /// <summary>Opens a document and imports the FDF file.</summary>
    /// <returns>The annotations added.</returns>
    [Benchmark]
    public int ImportFdf()
    {
        using var document = PdfDocumentReader.Open(_pdf, null);
        return PdfInterchange.ImportFdf(document, _fdf).AnnotationsAdded;
    }

    /// <summary>Parses the XFDF file without touching a document.</summary>
    /// <returns>The annotations read.</returns>
    [Benchmark]
    public int ReadXfdf() => XfdfReader.Read(_xfdf).Annotations.Count;

    /// <summary>Parses the FDF file without touching a document.</summary>
    /// <returns>The annotations read.</returns>
    [Benchmark]
    public int ReadFdf() => FdfReader.Read(_fdf).Annotations.Count;

    /// <summary>Makes the data both files are written from.</summary>
    /// <returns>The data.</returns>
    private static PdfInterchangeData CreateData()
    {
        var data = new PdfInterchangeData();
        for (var i = 0; i < Annotations; i++)
        {
            var row = Math.DivRem(i, Columns, out var column);
            var left = column * (Width + Gap);
            var bottom = row * (Height + Gap);
            var annotation = new PdfInterchangeAnnotation(Subtypes[i % Subtypes.Length]) { Page = Page };
            annotation.Rect = new PdfRectangle(left, bottom, left + Width, bottom + Height);
            annotation.Color = Yellow;
            annotation.Name = $"annotation-{i}";
            annotation.Title = "Glenn";
            annotation.Contents = $"Comment number {i}";
            data.Annotations.Add(annotation);
        }

        return data;
    }
}
