// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Reading;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures listing a document's sound and video, writing them out, describing a 3D model for assistive technology, and showing
/// and hiding a form field; allocations come from the EventPipe trace.
/// </summary>
public class DocumentMediaBenchmarks
{
    /// <summary>The destination stream, reused so only the library's allocations are measured.</summary>
    private readonly MemoryStream _destination = new();

    /// <summary>The document with a sound, a screen clip and a rich media asset.</summary>
    private BenchmarkPdfDocument _media = null!;

    /// <summary>The document with a 3D model.</summary>
    private BenchmarkPdfDocument _model = null!;

    /// <summary>The scripted form.</summary>
    private BenchmarkPdfDocument _form = null!;

    /// <summary>The media source.</summary>
    private IDocumentMediaSource _source = null!;

    /// <summary>The semantic source of the model document.</summary>
    private IPageSemanticSource _semantics = null!;

    /// <summary>The visibility feature of the form.</summary>
    private IFormVisibility _visibility = null!;

    /// <summary>Whether the field is hidden now, so each run toggles it.</summary>
    private bool _hidden;

    /// <summary>Opens the documents.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _media = new(TestPdf.CreateMediaDocument());
        _model = new(TestPdf.CreateModelDocument(U3dBuilder.SampleModel()));
        _form = new(TestPdf.CreateScriptedForm());
        _source = DocumentFeature<IDocumentMediaSource>.Cast(_media.Document)!;
        _semantics = DocumentFeature<IPageSemanticSource>.Cast(_model.Document)!;
        _visibility = DocumentFeature<IFormVisibility>.Cast(_form.Document)!;
        _ = _source.GetMedia();
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _media.Dispose();
        _model.Dispose();
        _form.Dispose();
        _destination.Dispose();
    }

    /// <summary>Lists the media.</summary>
    /// <returns>The count.</returns>
    [Benchmark]
    public int ListMedia() => _source.GetMedia().Count;

    /// <summary>Writes the sound as a WAVE file.</summary>
    /// <returns>Whether it was written.</returns>
    [Benchmark]
    public bool SaveSound()
    {
        _destination.Position = 0;
        return _source.SaveMedia(0, _destination);
    }

    /// <summary>Writes the screen clip.</summary>
    /// <returns>Whether it was written.</returns>
    [Benchmark]
    public bool SaveClip()
    {
        _destination.Position = 0;
        return _source.SaveMedia(1, _destination);
    }

    /// <summary>Lists what the model page offers assistive technology, which reads the model's structure.</summary>
    /// <returns>The item count.</returns>
    [Benchmark]
    public int DescribeModelPage() => _semantics.GetItems(0).Count;

    /// <summary>Hides a form field, or shows it again on the next run.</summary>
    /// <returns>Whether a widget changed.</returns>
    [Benchmark]
    public bool ToggleFieldVisibility()
    {
        _hidden = !_hidden;
        return _visibility.SetHidden("Spare", _hidden);
    }
}
