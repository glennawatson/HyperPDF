// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Forms;
using HyperPdf.Engine;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the engine's link, form field and annotation reads before and after edits. Each method uses only calls that
/// exist on both sides of a change so the baseline ref and the change run the same definition; allocations come from the EventPipe trace.
/// </summary>
public class EngineReadCacheBenchmarks
{
    /// <summary>The pages in the link document.</summary>
    private const int Pages = 50;

    /// <summary>The note's position.</summary>
    private const float NoteAt = 40;

    /// <summary>The note's colour.</summary>
    private const uint NoteColor = 0xFFEE88;

    /// <summary>The reads each reader thread makes.</summary>
    private const int ReadsPerReader = 1000;

    /// <summary>The notes the calling thread adds and removes while the readers run.</summary>
    private const int EditRounds = 20;

    /// <summary>The reused list the reads fill.</summary>
    private readonly List<FormField> _fields = [];

    /// <summary>The reused list the annotation reads fill.</summary>
    private readonly List<PageAnnotation> _annotations = [];

    /// <summary>The document with links on every page.</summary>
    private BenchmarkPdfDocument _linkSource = null!;

    /// <summary>The document with a form.</summary>
    private BenchmarkPdfDocument _formSource = null!;

    /// <summary>The link document's engine type.</summary>
    private HyperPdfDocument _linkDocument = null!;

    /// <summary>The form document's engine type.</summary>
    private HyperPdfDocument _formDocument = null!;

    /// <summary>The form document's first text field's widget index.</summary>
    private int _fieldIndex;

    /// <summary>Opens the documents and warms the caches.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _linkSource = new(TestPdf.Create(Pages));
        _linkDocument = (HyperPdfDocument)_linkSource.Document;
        _formSource = new(TestPdf.CreateForm());
        _formDocument = (HyperPdfDocument)_formSource.Document;
        HyperPdfDocumentFormFilling.GetFields(_formDocument, 0, _fields);
        _fieldIndex = _fields[0].Index;
        _fields.Clear();
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _linkSource.Dispose();
        _formSource.Dispose();
    }

    /// <summary>Adds and removes a note on the first page, then reads every page's links, as a hover after an edit does.</summary>
    /// <returns>The number of links read.</returns>
    [Benchmark]
    public int EditNoteThenGetLinksOnEveryPage()
    {
        var index = HyperPdfDocumentAnnotationEditing.AddNote(_linkDocument, 0, new(NoteAt, NoteAt), "Note", NoteColor);
        _ = HyperPdfDocumentAnnotationEditing.Remove(_linkDocument, 0, index);
        var count = 0;
        for (var page = 0; page < Pages; page++)
        {
            count += HyperPdfNavigation.GetLinks(_linkDocument, page).Count;
        }

        return count;
    }

    /// <summary>Reads the first page's links with the cache warm.</summary>
    /// <returns>The number of links.</returns>
    [Benchmark]
    public int GetLinksWarm() => HyperPdfNavigation.GetLinks(_linkDocument, 0).Count;

    /// <summary>Reads the form page's fields with nothing edited between reads.</summary>
    /// <returns>The number of fields.</returns>
    [Benchmark]
    public int GetFieldsWarm()
    {
        _fields.Clear();
        HyperPdfDocumentFormFilling.GetFields(_formDocument, 0, _fields);
        return _fields.Count;
    }

    /// <summary>Sets a field's text and reads the page's fields again.</summary>
    /// <returns>The number of fields.</returns>
    [Benchmark]
    public int SetTextThenGetFields()
    {
        _ = HyperPdfDocumentFormFilling.SetText(_formDocument, 0, _fieldIndex, "typed");
        _fields.Clear();
        HyperPdfDocumentFormFilling.GetFields(_formDocument, 0, _fields);
        return _fields.Count;
    }

    /// <summary>Reads a page's annotations with the cache warm.</summary>
    /// <returns>The number of annotations.</returns>
    [Benchmark]
    public int GetAnnotationsWarm()
    {
        _annotations.Clear();
        HyperPdfAnnotationReading.GetAnnotations(HyperPdfAnnotationStateAccess.GetAnnotations(_linkDocument), 0, _annotations);
        return _annotations.Count;
    }

    /// <summary>Reads a page's annotations from four threads while the calling thread adds and removes a note.</summary>
    /// <returns>The number of annotations the readers saw.</returns>
    [Benchmark]
    public async Task<int> GetAnnotationsFourReadersWhileEditing()
    {
        var state = HyperPdfAnnotationStateAccess.GetAnnotations(_linkDocument);
        var readers = new Task<int>[4];
        for (var reader = 0; reader < readers.Length; reader++)
        {
            readers[reader] = Task.Run(() =>
            {
                List<PageAnnotation> seen = [];
                var total = 0;
                for (var i = 0; i < ReadsPerReader; i++)
                {
                    seen.Clear();
                    HyperPdfAnnotationReading.GetAnnotations(state, 0, seen);
                    total += seen.Count;
                }

                return total;
            });
        }

        for (var i = 0; i < EditRounds; i++)
        {
            var index = HyperPdfDocumentAnnotationEditing.AddNote(_linkDocument, 0, new(NoteAt, NoteAt), "Note", NoteColor);
            _ = HyperPdfDocumentAnnotationEditing.Remove(_linkDocument, 0, index);
        }

        return (await Task.WhenAll(readers).ConfigureAwait(false)).Sum();
    }
}
