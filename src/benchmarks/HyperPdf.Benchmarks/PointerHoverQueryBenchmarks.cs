// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Engine;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the document reads one pointer move over a page makes: the form field list and the annotation list.
/// The app project is not referenced here, so these time the Engine calls the hover path makes, not the canvas itself.
/// </summary>
public class PointerHoverQueryBenchmarks
{
    /// <summary>The page the queries read.</summary>
    private const int Page = 0;

    /// <summary>The yellow of the carets.</summary>
    private const uint Yellow = 0xFFFF00;

    /// <summary>The columns the carets are laid out in.</summary>
    private const int Columns = 20;

    /// <summary>The space between carets, in points.</summary>
    private const int Spacing = 25;

    /// <summary>The space from the page edge to the first caret, in points.</summary>
    private const int Margin = 20;

    /// <summary>The form document.</summary>
    private BenchmarkPdfDocument _form = null!;

    /// <summary>The form's filler.</summary>
    private IFormFiller _filler = null!;

    /// <summary>The document the annotations go on.</summary>
    private BenchmarkPdfDocument _annotated = null!;

    /// <summary>The annotation editor.</summary>
    private IAnnotationEditor _editor = null!;

    /// <summary>Receives the fields read.</summary>
    private List<FormField> _fields = null!;

    /// <summary>Receives the annotations read.</summary>
    private List<PageAnnotation> _annotations = null!;

    /// <summary>Gets or sets how many annotations the page holds.</summary>
    [Params(0, 20, 200)]
    public int AnnotationCount { get; set; }

    /// <summary>Opens the documents and adds the annotations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _form = new(TestPdf.CreateForm());
        _filler = DocumentFeature<IFormFiller>.Cast(_form.Document)!;
        _annotated = new(TestPdf.Create(1));
        _editor = DocumentFeature<IAnnotationEditor>.Cast(_annotated.Document)!;
        var state = HyperPdfAnnotationStateAccess.GetAnnotations((HyperPdfDocument)_annotated.Document);
        for (var i = 0; i < AnnotationCount; i++)
        {
            _ = HyperPdfAnnotationCarets.AddCaret(state, Page, new(Margin + (i % Columns * Spacing), Margin + (i / Columns * Spacing)), Yellow);
        }

        _fields = [];
        _annotations = [];
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _form.Dispose();
        _annotated.Dispose();
    }

    /// <summary>Reads the form fields on the page, as one uncached hover over a form did.</summary>
    /// <returns>The number of fields read.</returns>
    [Benchmark]
    public int ReadFormFieldsOnPage()
    {
        _fields.Clear();
        _filler.GetFields(Page, _fields);
        return _fields.Count;
    }

    /// <summary>Reads the annotations on the page once, as the comment hover test does.</summary>
    /// <returns>The number of annotations read.</returns>
    [Benchmark]
    public int ReadAnnotationsOnPageOnce()
    {
        _annotations.Clear();
        _editor.GetAnnotations(Page, _annotations);
        return _annotations.Count;
    }

    /// <summary>Reads the annotations on the page twice, as the comment hover test did before it read them once.</summary>
    /// <returns>The number of annotations read.</returns>
    [Benchmark]
    public int ReadAnnotationsOnPageTwice()
    {
        _annotations.Clear();
        _editor.GetAnnotations(Page, _annotations);
        var first = _annotations.Count;
        _annotations.Clear();
        _editor.GetAnnotations(Page, _annotations);
        return first + _annotations.Count;
    }
}
