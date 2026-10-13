// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures reading actions as data from a warm document: a three-action <c>/Next</c> chain, an embedded go-to action
/// with a two-step target path, and an annotation's <c>/AA</c> triggers. Allocations come from the EventPipe trace.
/// </summary>
public class HyperPdfActionBenchmarks
{
    /// <summary>The object number of the chain's first action.</summary>
    private const int ChainObject = 4;

    /// <summary>The object number of the embedded go-to action.</summary>
    private const int EmbeddedObject = 7;

    /// <summary>The object number of the widget annotation.</summary>
    private const int WidgetObject = 8;

    /// <summary>The document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The chain's first action.</summary>
    private PdfDictionary _chain = null!;

    /// <summary>The embedded go-to action.</summary>
    private PdfDictionary _embedded = null!;

    /// <summary>The widget annotation with additional actions.</summary>
    private PdfDictionary _widget = null!;

    /// <summary>Opens the document and reads each action once so its objects are parsed.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(
            MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Annots [8 0 R] >>",
            "<< /S /URI /URI (https://example.com/) /Next 5 0 R >>",
            "<< /S /Named /N /NextPage /Next 6 0 R >>",
            "<< /S /GoTo /D [3 0 R /Fit] >>",
            "<< /S /GoToE /D [0 /XYZ 0 792 0] /NewWindow true /T << /R /P /T << /R /C /N (Another embedded document) >> >> >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /Rect [0 0 10 10] "
            + "/AA << /E 4 0 R /X 5 0 R /D 6 0 R /U 6 0 R /Fo 5 0 R /Bl 5 0 R /K 6 0 R /F 6 0 R /V 6 0 R /C 6 0 R >> >>"),
            null);
        _chain = StoreReading.GetObject(_document.Objects, new(ChainObject, 0)).AsDictionary()!;
        _embedded = StoreReading.GetObject(_document.Objects, new(EmbeddedObject, 0)).AsDictionary()!;
        _widget = StoreReading.GetObject(_document.Objects, new(WidgetObject, 0)).AsDictionary()!;
        _ = ReadChain();
        _ = ReadEmbeddedGoTo();
        _ = ReadTriggers();
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Reads a URI action followed by a named action and a go-to action.</summary>
    /// <returns>The first action's next count.</returns>
    [Benchmark]
    public int ReadChain() => PdfDocumentActions.ReadActionNode(_document, _chain).Next.Length;

    /// <summary>Reads an embedded go-to action's target steps and destination.</summary>
    /// <returns>The target step count.</returns>
    [Benchmark]
    public int ReadEmbeddedGoTo() => ((EmbeddedGoToAction)PdfDocumentNavigation.ReadAction(_document, _embedded).Value!).Targets.Length;

    /// <summary>Reads a widget's ten additional actions and their chains.</summary>
    /// <returns>The trigger count.</returns>
    [Benchmark]
    public int ReadTriggers() => PdfDocumentActions.GetTriggers(_document, _widget).Length;
}
