// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Writing;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Checks that optimising a fillable form preserves its fields.</summary>
[NotInParallel]
public sealed class FormOptimizationTests
{
    /// <summary>The text typed into the field.</summary>
    private const string Typed = "Bob";

    /// <summary>A fillable form keeps its widgets and can still be filled after optimising.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormsStillFill()
    {
        var source = TestPdf.CreateForm();
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller with { Cleanup = PdfCleanupItems.All });
        using var before = PdfDocumentReader.Open(source, null);
        using var after = PdfDocumentReader.Open(result.Bytes, null);
        var expected = Widgets(before);
        var widgets = Widgets(after);
        var text = widgets.FindIndex(static widget => widget.Type == PdfFieldType.Text);

        await Assert.That(widgets.Count).IsEqualTo(expected.Count);
        await Assert.That(HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(after), 0, text, Typed)).IsTrue();

        using var filled = PdfDocumentReader.Open(PdfIncrementalWriter.Save(after.Objects), null);
        await Assert.That(Widgets(filled)[text].Value).IsEqualTo(Typed);
    }

    /// <summary>Gets the first page's widgets.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The widgets.</returns>
    private static List<PdfFormWidget> Widgets(PdfDocument document)
    {
        var widgets = new List<PdfFormWidget>();
        HyperPdfLibrary.Forms.PdfFormReading.GetWidgets(PdfDocumentForms.GetForm(document), 0, widgets);
        return widgets;
    }
}
