// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Measures regenerated text, list and button widget appearances through the form API.</summary>
public class HyperPdfFormAppearanceBenchmarks
{
    /// <summary>The first page of the form.</summary>
    private const int PageIndex = 0;

    /// <summary>Text long enough to wrap over several lines in the Notes widget.</summary>
    private const string NotesText = "One two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen";

    /// <summary>Characters written into the Zip widget's comb cells.</summary>
    private const string ZipText = "9876";

    /// <summary>The reusable PDF bytes.</summary>
    private byte[] _pdf = [];

    /// <summary>The form whose text and comb field appearances are regenerated.</summary>
    private PdfDocument _textDocument = null!;

    /// <summary>The form whose list appearance is regenerated.</summary>
    private PdfDocument _listDocument = null!;

    /// <summary>Opens and prepares reusable forms, and checks every measured path.</summary>
    /// <returns>A task completing after the pages and fonts are prefetched.</returns>
    /// <exception cref="InvalidOperationException">A fixture widget cannot generate its appearance.</exception>
    [GlobalSetup]
    public async Task Setup()
    {
        _pdf = FormSamples.CreateRichForm();
        _textDocument = PdfDocumentReader.Open(_pdf, null);
        _listDocument = PdfDocumentReader.Open(_pdf, null);
        await PdfDocumentPages.PrefetchPageAsync(_textDocument, PageIndex, CancellationToken.None);
        await PdfDocumentPages.PrefetchPageAsync(_listDocument, PageIndex, CancellationToken.None);

        if (!HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(_textDocument), PageIndex, FormSamples.NotesIndex, NotesText)
            || !HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(_textDocument), PageIndex, FormSamples.ZipIndex, ZipText)
            || !RegenerateText()
            || !HyperPdfLibrary.Forms.PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(_listDocument), PageIndex, FormSamples.PickIndex))
        {
            throw new InvalidOperationException("A text, comb or list widget could not regenerate its appearance.");
        }

        using var buttonDocument = PdfDocumentReader.Open(_pdf, null);
        await PdfDocumentPages.PrefetchPageAsync(buttonDocument, PageIndex, CancellationToken.None);
        if (!HyperPdfLibrary.Forms.PdfFormEditing.SetChecked(PdfDocumentForms.GetForm(buttonDocument), PageIndex, FormSamples.AgreeIndex, true)
            || !HyperPdfLibrary.Forms.PdfFormEditing.SetChecked(PdfDocumentForms.GetForm(buttonDocument), PageIndex, FormSamples.PlanFirstIndex, true))
        {
            throw new InvalidOperationException("A check box or radio button could not generate its appearance.");
        }
    }

    /// <summary>Releases the reusable documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _listDocument.Dispose();
        _textDocument.Dispose();
    }

    /// <summary>Regenerates a multiline field and a comb field.</summary>
    /// <returns><see langword="true"/> when both appearances were regenerated.</returns>
    [Benchmark]
    public bool RegenerateText()
    {
        var notes = HyperPdfLibrary.Forms.PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(_textDocument), PageIndex, FormSamples.NotesIndex);
        var zip = HyperPdfLibrary.Forms.PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(_textDocument), PageIndex, FormSamples.ZipIndex);
        return notes && zip;
    }

    /// <summary>Generates check box and radio button appearances on a fresh form.</summary>
    /// <returns>The number of buttons updated.</returns>
    [Benchmark]
    public int GenerateButtons()
    {
        using var document = PdfDocumentReader.Open(_pdf, null);
        var count = HyperPdfLibrary.Forms.PdfFormEditing.SetChecked(PdfDocumentForms.GetForm(document), PageIndex, FormSamples.AgreeIndex, true) ? 1 : 0;
        count += HyperPdfLibrary.Forms.PdfFormEditing.SetChecked(PdfDocumentForms.GetForm(document), PageIndex, FormSamples.PlanFirstIndex, true) ? 1 : 0;
        return count;
    }

    /// <summary>Regenerates a selected list box appearance.</summary>
    /// <returns><see langword="true"/> when the appearance was regenerated.</returns>
    [Benchmark]
    public bool RegenerateList() => HyperPdfLibrary.Forms.PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(_listDocument), PageIndex, FormSamples.PickIndex);
}
