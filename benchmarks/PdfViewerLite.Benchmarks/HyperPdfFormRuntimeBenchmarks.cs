// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.Core.Forms.Scripting;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures the form runtime: tab and calculation order, reset, the time and date format scripts, and a page with the field tint.</summary>
public class HyperPdfFormRuntimeBenchmarks
{
    /// <summary>The tint colour.</summary>
    private const uint TintColor = 0xB4CCDCU;

    /// <summary>The tint opacity.</summary>
    private const byte TintAlpha = 72;

    /// <summary>The page width in points.</summary>
    private const int PageWidth = 612;

    /// <summary>The page height in points.</summary>
    private const int PageHeight = 792;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The time format script.</summary>
    private static readonly FormScript TimeFormat = FormScript.Parse("AFTime_FormatEx(\"h:MM tt\");");

    /// <summary>The reset action.</summary>
    private static readonly ResetFormAction Reset = new([], 0);

    /// <summary>The widget orders, reused.</summary>
    private readonly List<int> _order = [];

    /// <summary>The calculation order, reused.</summary>
    private readonly List<string> _names = [];

    /// <summary>The open form.</summary>
    private PdfDocument _document = null!;

    /// <summary>The renderer that draws the tint.</summary>
    private PdfPageRenderer _renderer = null!;

    /// <summary>The pixels the renderer draws into.</summary>
    private byte[] _pixels = [];

    /// <summary>Opens the form and draws it once so the page is recorded.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = BenchmarkDrawing.Open(FormRuntimeSamples.Create("/Tabs /R"), null);
        _renderer = new(_document, PdfRenderOptions.Default with { FormHighlight = new(TintColor, TintAlpha) });
        _pixels = new byte[PageWidth * PageHeight * PixelBytes];
        _ = RenderTinted();
    }

    /// <summary>Releases the form.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _renderer.Dispose();
        _document.Dispose();
    }

    /// <summary>Works out the tab order of the page by rows.</summary>
    /// <returns>The widget count.</returns>
    [Benchmark]
    public int TabSequence()
    {
        _order.Clear();
        HyperPdfLibrary.Forms.PdfFormOrder.GetTabSequence(PdfDocumentForms.GetForm(_document), 0, _order);
        return _order.Count;
    }

    /// <summary>Reads the calculation order.</summary>
    /// <returns>The field count.</returns>
    [Benchmark]
    public int CalculationOrder()
    {
        _names.Clear();
        HyperPdfLibrary.Forms.PdfFormOrder.GetCalculationOrder(PdfDocumentForms.GetForm(_document), _names);
        return _names.Count;
    }

    /// <summary>Resets every field.</summary>
    /// <returns>The number of fields reset.</returns>
    [Benchmark]
    public int ResetForm() => HyperPdfLibrary.Forms.PdfFormActions.Reset(PdfDocumentForms.GetForm(_document), Reset);

    /// <summary>Formats a time with a script.</summary>
    /// <returns>The text.</returns>
    [Benchmark]
    public string FormatTime() => FormScriptEngine.Format(TimeFormat, "13:45");

    /// <summary>Draws the recorded page with the tint over its fields.</summary>
    /// <returns>Whether the page drew.</returns>
    [Benchmark]
    public bool RenderTinted() =>
        _renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.Annotations), new(_pixels, PageWidth, PageHeight, PageWidth * PixelBytes));
}
