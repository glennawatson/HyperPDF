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

    /// <summary>Japanese text written into the Shift-JIS field: one-byte and two-byte codes.</summary>
    private const string ShiftJisText = "日本語 ABC ｱｲｳ 日本語";

    /// <summary>The reusable PDF bytes.</summary>
    private byte[] _pdf = [];

    /// <summary>The form whose text and comb field appearances are regenerated.</summary>
    private PdfDocument _textDocument = null!;

    /// <summary>The form whose list appearance is regenerated.</summary>
    private PdfDocument _listDocument = null!;

    /// <summary>The reusable bytes of the form with rich text, a push button and round bevels.</summary>
    private byte[] _widgetsPdf = [];

    /// <summary>The form whose rich text and push button appearances are regenerated.</summary>
    private PdfDocument _widgetsDocument = null!;

    /// <summary>The reusable bytes of the form with a Shift-JIS field.</summary>
    private byte[] _shiftJisPdf = [];

    /// <summary>The form whose Shift-JIS field is regenerated.</summary>
    private PdfDocument _shiftJisDocument = null!;

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

        await PrepareWidgetsAsync();

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
        _shiftJisDocument.Dispose();
        _widgetsDocument.Dispose();
        _listDocument.Dispose();
        _textDocument.Dispose();
    }

    /// <summary>Regenerates a text field from its rich text value, with bold, italic, coloured, underlined and struck-through runs.</summary>
    /// <returns><see langword="true"/> when the appearance was regenerated.</returns>
    [Benchmark]
    public bool RegenerateRichText() =>
        HyperPdfLibrary.Forms.PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(_widgetsDocument), PageIndex, FormAppearanceSamples.RichTextIndex);

    /// <summary>Regenerates a push button with an icon above its caption.</summary>
    /// <returns><see langword="true"/> when the appearance was regenerated.</returns>
    [Benchmark]
    public bool RegeneratePushButton() =>
        HyperPdfLibrary.Forms.PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(_widgetsDocument), PageIndex, FormAppearanceSamples.PushButtonIndex);

    /// <summary>Regenerates a field written with the Shift-JIS CMap, whose codes are one or two bytes long.</summary>
    /// <returns><see langword="true"/> when the appearance was regenerated.</returns>
    [Benchmark]
    public bool RegenerateShiftJis() => HyperPdfLibrary.Forms.PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(_shiftJisDocument), PageIndex, 0);

    /// <summary>Sets the value of a Shift-JIS field on a fresh document, which builds the font's code map by reading every code its CMap defines.</summary>
    /// <returns><see langword="true"/> when the value was set.</returns>
    [Benchmark]
    public bool SetShiftJisOnFreshDocument()
    {
        using var document = PdfDocumentReader.Open(_shiftJisPdf, null);
        return HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), PageIndex, 0, ShiftJisText);
    }

    /// <summary>Generates the appearances of a radio button with a round bevel on a fresh form.</summary>
    /// <returns><see langword="true"/> when the button was switched on.</returns>
    [Benchmark]
    public bool GenerateBeveledRadio()
    {
        using var document = PdfDocumentReader.Open(_widgetsPdf, null);
        return HyperPdfLibrary.Forms.PdfFormEditing.SetChecked(PdfDocumentForms.GetForm(document), PageIndex, FormAppearanceSamples.BeveledRadioIndex, true);
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

    /// <summary>Opens the rich text, push button, round bevel and Shift-JIS forms, and checks that each generates its appearance.</summary>
    /// <returns>A task completing after the pages and fonts are prefetched.</returns>
    /// <exception cref="InvalidOperationException">A fixture widget cannot generate its appearance.</exception>
    private async Task PrepareWidgetsAsync()
    {
        _widgetsPdf = FormAppearanceSamples.CreateWidgetsForm();
        _widgetsDocument = PdfDocumentReader.Open(_widgetsPdf, null);
        _shiftJisPdf = FormAppearanceSamples.CreateShiftJisForm();
        _shiftJisDocument = PdfDocumentReader.Open(_shiftJisPdf, null);
        await PdfDocumentPages.PrefetchPageAsync(_widgetsDocument, PageIndex, CancellationToken.None);
        await PdfDocumentPages.PrefetchPageAsync(_shiftJisDocument, PageIndex, CancellationToken.None);
        if (!HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(_shiftJisDocument), PageIndex, 0, ShiftJisText)
            || !RegenerateRichText()
            || !RegeneratePushButton()
            || !RegenerateShiftJis()
            || !SetShiftJisOnFreshDocument()
            || !GenerateBeveledRadio())
        {
            throw new InvalidOperationException("A rich text, push button, Shift-JIS or beveled radio widget could not generate its appearance.");
        }
    }
}
