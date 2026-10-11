// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Writing;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Tests for form filling through <see cref="IFormFiller"/>, answered by the managed library.</summary>
public sealed class FormTests
{
    /// <summary>The number of fields in the test form.</summary>
    private const int FieldCount = 3;

    /// <summary>The index of the Blue option.</summary>
    private const int Blue = 2;

    /// <summary>The widget index of the combo box.</summary>
    private const int ComboIndex = 2;

    /// <summary>An option index the combo box does not have.</summary>
    private const int MissingOption = 9;

    /// <summary>The name typed into the text field.</summary>
    private const string TypedName = "Glenn Watson";

    /// <summary>The options of the combo box.</summary>
    private static readonly string[] Colours = ["Red", "Green", "Blue"];

    /// <summary>Verifies the fields, their kinds, values and options are read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsFields()
    {
        using var test = new FormDocument();
        var fields = Read(test.Filler);

        await Assert.That(test.Filler.HasForm).IsTrue();
        await Assert.That(fields.Count).IsEqualTo(FieldCount);
        await Assert.That(fields[0].Kind).IsEqualTo(FormFieldKind.Text);
        await Assert.That(fields[0].Name).IsEqualTo("Name");
        await Assert.That(fields[1].Kind).IsEqualTo(FormFieldKind.CheckBox);
        await Assert.That(fields[1].IsChecked).IsFalse();
        await Assert.That(fields[2].Kind).IsEqualTo(FormFieldKind.ComboBox);
        await Assert.That(fields[2].Options).IsEquivalentTo(Colours);
        await Assert.That(fields[2].SelectedOption).IsEqualTo(0);
    }

    /// <summary>Verifies each kind can be filled, and the edits survive saving through the managed library and through the PDFium copy.</summary>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <c>test.Document</c> is <see langword="null"/>.</exception>
    [Test]
    public async Task FillsAndSaves()
    {
        using var test = new FormDocument();
        var document = test.Document ?? throw new InvalidOperationException();
        var filler = test.Filler;
        var fields = Read(filler);

        var typed = filler.SetText(0, fields[0].Index, TypedName);
        var ticked = filler.SetChecked(0, fields[1].Index, true);
        var chosen = filler.SelectOption(0, fields[2].Index, Blue);
        var after = Read(filler);
        var unsaved = (test.Document.GetFeature(typeof(IAnnotationEditor)) as IAnnotationEditor) is { HasUnsavedChanges: true }
            && PdfViewerLite.HyperPdf.HyperPdfForms.GetHasManagedEdits(((HyperPdfDocument)test.Document));
        var managed = SaveManaged(document);
        var copy = SaveThroughCopy(document);
        try
        {
            using var reopened = new HyperPdfEngine().Open(managed, null);
            var viaManaged = Read((IFormFiller)DocumentFeatures.CastFeature(reopened, typeof(IFormFiller))!);
            using var viaCopy = new HyperPdfEngine().Open(copy, null);
            var readFromCopy = Read((IFormFiller)DocumentFeatures.CastFeature(viaCopy, typeof(IFormFiller))!);

            await Assert.That(typed).IsTrue();
            await Assert.That(ticked).IsTrue();
            await Assert.That(chosen).IsTrue();
            await Assert.That(unsaved).IsTrue();
            await AssertFilled(after);
            await AssertFilled(viaManaged);
            await AssertFilled(readFromCopy);
        }
        finally
        {
            File.Delete(managed);
            File.Delete(copy);
        }
    }

    /// <summary>Verifies a filled text field is drawn on the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawsFilledText()
    {
        const int bytesPerPixel = 4;
        const byte half = 0x80;
        using var test = new FormDocument();
        var fields = Read(test.Filler);
        _ = test.Filler.SetText(0, fields[0].Index, "WWWWWWWWWWWW");
        var bounds = fields[0].Bounds;
        var width = TestPdf.PortraitWidth;
        var height = TestPdf.PortraitHeight;
        var pixels = new byte[width * height * bytesPerPixel];

        _ = test.Document.Render(new(0, 1, PageRotation.None, 0, 0, RenderFlags.Annotations), new(pixels, width, height, width * bytesPerPixel));

        var dark = 0;
        for (var y = (int)bounds.Top; y < (int)bounds.Bottom; y++)
        {
            for (var x = (int)bounds.Left; x < (int)bounds.Right; x++)
            {
                dark += pixels[((y * width) + x) * bytesPerPixel] < half ? 1 : 0;
            }
        }

        await Assert.That(dark).IsGreaterThan(0);
    }

    /// <summary>Verifies a read-only edit, a wrong kind of widget and a missing widget are refused without changing anything.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesWrongEdits()
    {
        using var test = new FormDocument();
        var filler = test.Filler;

        await Assert.That(filler.SetText(0, 1, "x")).IsFalse();
        await Assert.That(filler.SetChecked(0, 0, true)).IsFalse();
        await Assert.That(filler.SelectOption(0, 0, 0)).IsFalse();
        await Assert.That(filler.SelectOption(0, ComboIndex, MissingOption)).IsFalse();
        await Assert.That(filler.SetText(0, FieldCount, "x")).IsFalse();
        await Assert.That(filler.SetText(1, 0, "x")).IsFalse();
        await Assert.That(((test.Document)?.GetFeature(typeof(IAnnotationEditor)) as IAnnotationEditor) is { HasUnsavedChanges: false }).IsTrue();
    }

    /// <summary>
    /// Verifies a read-only field keeps its value. PDFium's form filler overwrites it when asked, because it leaves that
    /// rule to the interface; the managed filler refuses.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadOnlyFieldIsNotChanged()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-readonly-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, FormSamples.CreateRichForm());
        try
        {
            using var document = new HyperPdfEngine().Open(path, null);
            var filler = (IFormFiller)DocumentFeatures.CastFeature(document, typeof(IFormFiller))!;
            var changed = filler.SetText(0, FormSamples.LockedIndex, "changed");

            await Assert.That(changed).IsFalse();
            await Assert.That(Read(filler)[FormSamples.LockedIndex].Value).IsEqualTo("Fixed");
            await Assert.That(Read(filler)[FormSamples.LockedIndex].IsReadOnly).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Checks the three filled fields.</summary>
    /// <param name="fields">The fields.</param>
    /// <returns>A task.</returns>
    private static async Task AssertFilled(List<FormField> fields)
    {
        await Assert.That(fields[0].Value).IsEqualTo(TypedName);
        await Assert.That(fields[1].IsChecked).IsTrue();
        await Assert.That(fields[2].Value).IsEqualTo("Blue");
        await Assert.That(fields[2].SelectedOption).IsEqualTo(Blue);
    }

    /// <summary>Reads the fields of page 1.</summary>
    /// <param name="filler">The filler.</param>
    /// <returns>The fields.</returns>
    private static List<FormField> Read(IFormFiller filler)
    {
        var fields = new List<FormField>();
        filler.GetFields(0, fields);
        return fields;
    }

    /// <summary>Saves the managed store's edits as an incremental update.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The file path.</returns>
    private static string SaveManaged(IDocument document)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-formsaved-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, PdfIncrementalWriter.Save(((HyperPdfDocument)document).Document.Objects));
        return path;
    }

    /// <summary>Saves through the PDFium copy.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The file path.</returns>
    private static string SaveThroughCopy(IDocument document)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-formcopy-{Guid.NewGuid():N}.pdf");
        using var stream = File.Create(path);
        _ = ((IAnnotationEditor)DocumentFeatures.CastFeature(document, typeof(IAnnotationEditor))!).Save(stream);
        return path;
    }

    /// <summary>The generated form, opened.</summary>
    private sealed class FormDocument : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="FormDocument"/> class.</summary>
        public FormDocument()
        {
            FilePath = Path.Combine(Path.GetTempPath(), $"hyperpdf-formsrc-{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(FilePath, TestPdf.CreateForm());
            Document = new HyperPdfEngine().Open(FilePath, null);
        }

        /// <summary>Gets the file path.</summary>
        public string FilePath { get; }

        /// <summary>Gets the document.</summary>
        public IDocument Document { get; }

        /// <summary>Gets the form filler.</summary>
        public IFormFiller Filler => (IFormFiller)DocumentFeatures.CastFeature(Document, typeof(IFormFiller))!;

        /// <inheritdoc/>
        public void Dispose()
        {
            Document.Dispose();
            File.Delete(FilePath);
        }
    }
}
