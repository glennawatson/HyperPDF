// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine.Tests;

/// <summary>Creates form fields with the managed library, fills one in and saves, then checks that PDFium reads and draws them as HyperPDF does.</summary>
public sealed class FormDesignParityTests
{
    /// <summary>The <c>FPDF_FORMFIELD_*</c> value of a push button.</summary>
    private const int PushButtonKind = 1;

    /// <summary>The <c>FPDF_FORMFIELD_*</c> value of a check box.</summary>
    private const int CheckBoxKind = 2;

    /// <summary>The <c>FPDF_FORMFIELD_*</c> value of a radio button.</summary>
    private const int RadioKind = 3;

    /// <summary>The <c>FPDF_FORMFIELD_*</c> value of a combo box.</summary>
    private const int ComboKind = 4;

    /// <summary>The <c>FPDF_FORMFIELD_*</c> value of a list box.</summary>
    private const int ListKind = 5;

    /// <summary>The <c>FPDF_FORMFIELD_*</c> value of a text field.</summary>
    private const int TextKind = 6;

    /// <summary>The <c>FPDF_FORMFIELD_*</c> value of a signature field.</summary>
    private const int SignatureKind = 7;

    /// <summary>The position of the right edge among the four numbers of a rectangle.</summary>
    private const int RightSlot = 2;

    /// <summary>The position of the top edge among the four numbers of a rectangle.</summary>
    private const int TopSlot = 3;

    /// <summary>The index of the combo box among the page's widgets.</summary>
    private const int ComboIndex = 4;

    /// <summary>The name of the text field.</summary>
    private const string TextName = "Name";

    /// <summary>The name of the radio group.</summary>
    private const string RadioName = "Colour";

    /// <summary>The name of the combo box.</summary>
    private const string ComboName = "Fruit";

    /// <summary>The label of the combo box's second option.</summary>
    private const string Banana = "Banana";

    /// <summary>The export value of the combo box's second option, which PDFium reports as the field's value.</summary>
    private const string BananaValue = "b";

    /// <summary>The name of the check box.</summary>
    private const string AgreeName = "Agree";

    /// <summary>The name of the list box.</summary>
    private const string ListName = "Fruits";

    /// <summary>The name of the push button.</summary>
    private const string SendName = "Send";

    /// <summary>The name of the signature field.</summary>
    private const string SignName = "Sign";

    /// <summary>The kind PDFium reports for each widget, in page order.</summary>
    private static readonly int[] WidgetKinds = [TextKind, CheckBoxKind, RadioKind, RadioKind, ComboKind, ListKind, PushButtonKind, SignatureKind];

    /// <summary>The full field name PDFium reports for each widget, in page order.</summary>
    private static readonly string[] WidgetNames = [TextName, AgreeName, RadioName, RadioName, ComboName, ListName, SendName, SignName];

    /// <summary>Fields the library created are read by PDFium with their kinds, names and values, after one is filled in and the file is saved.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CreatedFieldsAreReadByPdfium()
    {
        using var pair = await SaveAndPairAsync();

        var annotations = PdfiumReferenceAnnotations.GetAnnotations(pair.Pdfium, 0);
        await Assert.That(annotations.Count).IsEqualTo(WidgetKinds.Length);
        for (var i = 0; i < WidgetKinds.Length; i++)
        {
            await Assert.That(annotations[i].SubtypeName).IsEqualTo("Widget");
            await Assert.That(annotations[i].FieldType).IsEqualTo(WidgetKinds[i]);
            await Assert.That(annotations[i].FieldName).IsEqualTo(WidgetNames[i]);
        }

        await Assert.That(annotations[0].FieldValue).IsEqualTo("Ada");
        await Assert.That(annotations[ComboIndex].FieldValue).IsEqualTo(BananaValue);
    }

    /// <summary>Fields the library created render in both engines alike.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CreatedFieldsRenderLikePdfium()
    {
        using var pair = await SaveAndPairAsync();

        await Assert.That(FormAppearanceParityTests.Difference(pair, nameof(CreatedFieldsRenderLikePdfium))).IsLessThan(FormAppearanceParityTests.PageThreshold);
    }

    /// <summary>Makes a rectangle from four numbers in a string, so the test data holds no bare numbers.</summary>
    /// <param name="corners">The left, bottom, right and top edges, separated by spaces.</param>
    /// <returns>The rectangle.</returns>
    private static PdfRectangle Box(string corners)
    {
        var edges = Array.ConvertAll(corners.Split(' '), static edge => float.Parse(edge, CultureInfo.InvariantCulture));
        return new(edges[0], edges[1], edges[RightSlot], edges[TopSlot]);
    }

    /// <summary>Makes a placement on the first page.</summary>
    /// <param name="corners">The rectangle's edges.</param>
    /// <returns>The placement.</returns>
    private static PdfWidgetPlacement Place(string corners) => new(0, Box(corners));

    /// <summary>Makes a page and creates a text field, check box, radio group, combo box, list box, push button and signature field on it.</summary>
    /// <returns>The document.</returns>
    private static PdfDocument CreateDesignedForm()
    {
        var document = PdfDocumentReader.Open(
            MiniPdf.Build("<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] >>"),
            null);
        PdfFieldOption[] fruit = [new("a", "Apple"), new(BananaValue, Banana)];
        PdfFieldDefinition[] fields =
        [
            new(PdfFieldType.Text, TextName, [Place("20 260 140 285")]),
            new(PdfFieldType.CheckBox, AgreeName, [Place("20 225 40 245")]) { IsChecked = true },
            new(PdfFieldType.RadioButton, RadioName, [Place("60 225 80 245"), Place("100 225 120 245")]) { Value = "Choice2" },
            new(PdfFieldType.ComboBox, ComboName, [Place("20 190 140 212")]) { Options = fruit, Value = BananaValue },
            new(PdfFieldType.ListBox, ListName, [Place("160 150 280 212")]) { Options = fruit },
            new(PdfFieldType.PushButton, SendName, [Place("20 150 100 175")]) { Caption = SendName },
            new(PdfFieldType.Signature, SignName, [Place("20 90 140 130")]),
        ];
        _ = PdfFormFieldCreation.CreateFields(document, fields);
        return document;
    }

    /// <summary>Creates the fields, fills in the text field, saves and opens the file in both engines.</summary>
    /// <returns>The pair.</returns>
    private static async Task<EnginePair> SaveAndPairAsync()
    {
        using var document = CreateDesignedForm();
        _ = PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, 0, "Ada");
        await using var output = new MemoryStream();
        await PdfDocumentSaving.SaveAsync(document, output, CancellationToken.None);
        return new(output.ToArray());
    }
}
