// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.Services;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.HyperPdf;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.App.Tests;

/// <summary>
/// Checks the form runtime in the app: the Tab key follows the page's tab order on HyperPDF and annotation order on
/// PDFium, buttons run reset and hide actions, form data is never sent, and the form field tint comes from the settings.
/// </summary>
public sealed class FormRuntimeUiTests
{
    /// <summary>The x of a point inside the empty, fillable field "Limited" of the rich form.</summary>
    private const int FillableX = 172;

    /// <summary>The y, from the top, of a point inside "Limited".</summary>
    private const int FillableY = 460;

    /// <summary>The tint colour the settings give.</summary>
    private const uint TintColor = 0x336699U;

    /// <summary>The tint opacity the settings give.</summary>
    private const byte TintAlpha = 128;

    /// <summary>How far a rendered channel may differ from the blend worked out here, in levels of 255.</summary>
    private const int Tolerance = 3;

    /// <summary>The largest value of a channel.</summary>
    private const double Channel = 255;

    /// <summary>The bits to shift the red of a colour down by.</summary>
    private const int RedShift = 16;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The position of the blue channel in a BGRA pixel.</summary>
    private const int BlueSlot = 0;

    /// <summary>The position of the red channel in a BGRA pixel.</summary>
    private const int RedSlot = 2;

    /// <summary>The field names in annotation order, which PDFium follows.</summary>
    private static readonly string[] AnnotationOrder = ["Total", "Price", "Qty", "Clear", "Hider", "Layer", "Send", "Script"];

    /// <summary>The field names in column order, which a page with <c>/Tabs /C</c> asks for.</summary>
    private static readonly string[] ColumnOrder = ["Price", "Qty", "Clear", "Layer", "Script", "Total", "Hider", "Send"];

    /// <summary>The field names in row order, which a page with <c>/Tabs /R</c> asks for.</summary>
    private static readonly string[] RowOrder = ["Price", "Total", "Qty", "Clear", "Hider", "Layer", "Send", "Script"];

    /// <summary>HyperPDF follows the page's tab order; PDFium visits the fields in annotation order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TabKeyFollowsTheTabOrder()
    {
        var hyperColumns = await TabNamesAsync(new HyperPdfEngine(), "/Tabs /C");
        var hyperRows = await TabNamesAsync(new HyperPdfEngine(), "/Tabs /R");
        var hyperNone = await TabNamesAsync(new HyperPdfEngine(), string.Empty);
        var pdfium = await TabNamesAsync(new PdfiumEngine(), "/Tabs /C");

        await Assert.That(hyperColumns.SequenceEqual(ColumnOrder)).IsTrue();
        await Assert.That(hyperRows.SequenceEqual(RowOrder)).IsTrue();
        await Assert.That(hyperNone.SequenceEqual(AnnotationOrder)).IsTrue();
        await Assert.That(pdfium.SequenceEqual(AnnotationOrder)).IsTrue();
    }

    /// <summary>Buttons reset the form and hide a field, and a submit button sends nothing and says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ButtonsRunResetAndHideButSendNothing()
    {
        using var test = new TestServices(new HyperPdfEngine());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("runtime.pdf", FormRuntimeSamples.Create(string.Empty))]);
        var tab = main.SelectedTab!;
        var filler = (IFormFiller)DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(IFormFiller))!;
        _ = filler.SetText(0, FormRuntimeSamples.PriceIndex, "9");

        var reset = await tab.Forms.RunButtonAsync(Field(tab, "Clear"));
        var priceAfterReset = Field(tab, "Price").Value;
        var hide = await tab.Forms.RunButtonAsync(Field(tab, "Hider"));
        var send = await tab.Forms.RunButtonAsync(Field(tab, "Send"));

        await Assert.That(reset).IsEqualTo(FormActionResult.Ran | FormActionResult.Changed);
        await Assert.That(priceAfterReset).IsEqualTo("1");
        await Assert.That(hide).IsEqualTo(FormActionResult.Ran | FormActionResult.Changed);
        await Assert.That(send).IsEqualTo(FormActionResult.Declined);
        await Assert.That(tab.Notice).Contains("does not send form data");
    }

    /// <summary>The tint the settings name is applied to a document when it opens, and drawn over fillable fields.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpenedDocumentsGetTheSettingsTint()
    {
        var engine = new HighlightedHyperPdfEngine(static () => new(TintColor, TintAlpha));
        var path = Path.Combine(Path.GetTempPath(), $"tint-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, FormSamples.CreateRichForm());
        try
        {
            using var document = engine.Open(path, null);
            var size = document.GetPageSizes()[0];
            var width = (int)Math.Ceiling(size.Width);
            var height = (int)Math.Ceiling(size.Height);
            var pixels = new byte[width * height * PixelBytes];
            var rendered = document.Render(new(0, 1, PageRotation.None, 0, 0, RenderFlags.Annotations), new(pixels, width, height, width * PixelBytes));
            var offset = ((FillableY * width) + FillableX) * PixelBytes;

            await Assert.That(rendered).IsTrue();
            FormHighlight expected = new(TintColor, TintAlpha);
            await Assert.That(((IFormHighlight)DocumentFeatures.CastFeature(document, typeof(IFormHighlight))!).Highlight).IsEqualTo(expected);
            await Assert.That(Math.Abs(pixels[offset + RedSlot] - Blend(TintColor >> RedShift))).IsLessThanOrEqualTo(Tolerance);
            await Assert.That(Math.Abs(pixels[offset + BlueSlot] - Blend(TintColor))).IsLessThanOrEqualTo(Tolerance);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Works out the channel a colour channel gives over white at the tint opacity.</summary>
    /// <param name="channel">The colour's channel, in the low byte.</param>
    /// <returns>The blended value.</returns>
    private static int Blend(uint channel) => (int)Math.Round(Channel - ((Channel - (channel & byte.MaxValue)) * (TintAlpha / Channel)));

    /// <summary>Opens the runtime form and visits every fillable field with Tab, in the order the form asks for.</summary>
    /// <param name="engine">The engine that opens the form.</param>
    /// <param name="pageEntries">Extra entries of the page dictionary, such as <c>/Tabs /C</c>.</param>
    /// <returns>The field names in the order they were visited.</returns>
    private static async Task<List<string>> TabNamesAsync(IDocumentEngine engine, string pageEntries)
    {
        using var test = new TestServices(engine);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tabs.pdf", FormRuntimeSamples.Create(pageEntries))]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        var forms = tab.Forms;
        var names = new List<string>();
        for (var next = forms.CommitAndMove(false); next is not null && names.Count < FormRuntimeSamples.WidgetCount; next = forms.CommitAndMove(false))
        {
            names.Add(next.Name);
        }

        return names;
    }

    /// <summary>Finds a field by name on the first page.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="name">The field's name.</param>
    /// <returns>The field.</returns>
    private static FormField Field(DocumentTabViewModel tab, string name)
    {
        var fields = new List<FormField>();
        ((IFormFiller)DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(IFormFiller))!).GetFields(0, fields);
        return fields.Single(field => field.Name == name);
    }
}
