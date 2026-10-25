// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests the appearance streams the form draws: parsed back, they hold the text, cells and fonts that were asked for.</summary>
public sealed partial class FormAppearanceTests
{
    /// <summary>The font setting of the default appearances.</summary>
    private const string TwelvePoint = "/Helv 12 Tf";

    /// <summary>The position of a rectangle's right edge, and of a matrix's third entry.</summary>
    private const int RightSlot = 2;

    /// <summary>The position of a rectangle's top edge.</summary>
    private const int TopSlot = 3;

    /// <summary>The position of a matrix's horizontal offset.</summary>
    private const int TranslateXSlot = 4;

    /// <summary>The characters typed into the comb form's code field.</summary>
    private const int CodeCharacters = 2;

    /// <summary>The options of the list box that are highlighted.</summary>
    private const int Highlights = 2;

    /// <summary>The font size of the street field.</summary>
    private const float StreetSize = 10;

    /// <summary>The width of the comb form's code field.</summary>
    private const float CodeWidth = 120;

    /// <summary>How close measurements must be.</summary>
    private const double Tolerance = 0.01;

    /// <summary>The digits typed into the zip field.</summary>
    private const int ZipDigits = 4;

    /// <summary>The boxes of the zip field.</summary>
    private const int ZipBoxes = 5;

    /// <summary>How many times the long text is repeated to overflow the field.</summary>
    private const int LongRepeats = 12;

    /// <summary>The number of options of the list box.</summary>
    private const int OptionCount = 4;

    /// <summary>The fields in the calculated form.</summary>
    private const int ScriptedFields = 6;

    /// <summary>The widget index of the simple form's text field.</summary>
    private const int NameIndex = 0;

    /// <summary>The widget index of the comb form's comb field.</summary>
    private const int CodeIndex = 1;

    /// <summary>The boxes of the comb field.</summary>
    private const int CombBoxes = 6;

    /// <summary>The option chosen in lists.</summary>
    private const int ChosenOption = 3;

    /// <summary>The number of rows in the list box.</summary>
    private const int ListRows = 4;

    /// <summary>The text typed into the simple form's text field.</summary>
    private const string TypedName = "Glenn Watson";

    /// <summary>The width of the simple form's text field.</summary>
    private const float NameWidth = 228;

    /// <summary>The height of the simple form's text field.</summary>
    private const float NameHeight = 24;

    /// <summary>The width of the rotated field's drawing area: the widget's height.</summary>
    private const float SidewaysLength = 120;

    /// <summary>The height of the rotated field's drawing area: the widget's width.</summary>
    private const float SidewaysThickness = 24;

    /// <summary>The most characters the multi-line field is filled with.</summary>
    private const string LongText = "One two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen";

    /// <summary>A text field's appearance holds the text, in the field's font and size, inside marked content, clipped to the field.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextFieldAppearanceHoldsText()
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateForm(), null);
        var changed = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, NameIndex, TypedName);
        var content = ReadAppearance(document, NameIndex);
        await Assert.That(changed).IsTrue();
        await Assert.That(content).Contains("/Tx BMC");
        await Assert.That(content).Contains($"({TypedName}) Tj");
        await Assert.That(content).Contains(TwelvePoint);
        await Assert.That(content).Contains("0 g");
        await Assert.That(content).Contains("W\nn");
        await Assert.That(content).Contains("EMC");
        var box = ReadStream(document, NameIndex).Dictionary.GetArray(KnownName.BBox)!;
        await Assert.That(box.GetSingle(RightSlot)).IsEqualTo(NameWidth);
        await Assert.That(box.GetSingle(TopSlot)).IsEqualTo(NameHeight);
    }

    /// <summary>The size an inherited default appearance sets is used, and fields that were not edited keep having no appearance.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InheritedSizeIsUsed()
    {
        using var document = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, FormSamples.StreetIndex, "Elm");
        var content = ReadAppearance(document, FormSamples.StreetIndex);
        await Assert.That(content).Contains("/Helv 10 Tf");
        await Assert.That(content).Contains("(Elm) Tj");
        await Assert.That(GetAnnotation(document, FormSamples.SecretIndex).GetDictionary(KnownName.AP)).IsNull();
    }

    /// <summary>An automatic size shrinks multi-line text until it fits the field's height.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AutoSizeShrinksToFit()
    {
        using var document = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, FormSamples.NotesIndex, string.Join(' ', Enumerable.Repeat(LongText, LongRepeats)));
        var content = ReadAppearance(document, FormSamples.NotesIndex);
        await Assert.That(content).Contains(" Tf");
        await Assert.That(content).DoesNotContain(TwelvePoint);
    }

    /// <summary>A multi-line field wraps its words over several lines with an automatic size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MultilineFieldWraps()
    {
        using var document = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, FormSamples.NotesIndex, LongText);
        var content = ReadAppearance(document, FormSamples.NotesIndex);
        await Assert.That(CountOccurrences(content, " Tj")).IsGreaterThan(1);
        await Assert.That(content).Contains("(One two three");
        await Assert.That(content).Contains("1 1 0.8 rg").Because("the background colour is painted first");
        await Assert.That(content).Contains("0 0 0 RG");
        await Assert.That(content).Contains(TwelvePoint);
    }

    /// <summary>A password field draws asterisks, in the colour of its default appearance.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PasswordFieldHidesText()
    {
        using var document = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, FormSamples.SecretIndex, "pw");
        var content = ReadAppearance(document, FormSamples.SecretIndex);
        await Assert.That(content).Contains("(**) Tj");
        await Assert.That(content).Contains("0 0 1 rg");
        await Assert.That(content).DoesNotContain("pw");
    }

    /// <summary>A comb field puts one character in each box and draws the lines between boxes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CombFieldDrawsCells()
    {
        using var document = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, FormSamples.ZipIndex, "9876");
        var content = ReadAppearance(document, FormSamples.ZipIndex);
        await Assert.That(content).Contains("(9) Tj");
        await Assert.That(content).Contains("(8) Tj");
        await Assert.That(content).Contains("(7) Tj");
        await Assert.That(content).Contains("(6) Tj");
        await Assert.That(CountOccurrences(content, " Tm")).IsEqualTo(ZipDigits);
        await Assert.That(CountOccurrences(content, " l\n")).IsEqualTo(ZipBoxes - 1);
    }

    /// <summary>A field longer than its limit is cut at the limit, and a comb cell is as wide as the field allows.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LimitCutsText()
    {
        using var document = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, FormSamples.LimitedIndex, "123456789");
        var content = ReadAppearance(document, FormSamples.LimitedIndex);
        var widgets = ReadWidgets(document);
        await Assert.That(content).Contains("(12345) Tj");
        await Assert.That(widgets[FormSamples.LimitedIndex].Value).IsEqualTo("12345");
    }

    /// <summary>The characters of a comb field are one box width apart.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CombCharactersAreOneBoxApart()
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateCombForm(), null);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, CodeIndex, "AA");
        var matches = CellPositions().Matches(ReadAppearance(document, CodeIndex));
        var gap = double.Parse(matches[1].Groups["x"].Value, CultureInfo.InvariantCulture) - double.Parse(matches[0].Groups["x"].Value, CultureInfo.InvariantCulture);
        await Assert.That(matches.Count).IsEqualTo(CodeCharacters);
        await Assert.That(gap).IsEqualTo(CodeWidth / CombBoxes).Within(Tolerance);
    }

    /// <summary>A combo box shows the label of the selected option and stores its export value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ComboBoxShowsLabel()
    {
        using var document = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SelectOption(PdfDocumentForms.GetForm(document), 0, FormSamples.ColourIndex, 0);
        var content = ReadAppearance(document, FormSamples.ColourIndex);
        var widget = ReadWidgets(document)[FormSamples.ColourIndex];
        await Assert.That(content).Contains("(Red) Tj");
        await Assert.That(widget.Value).IsEqualTo("r");
        await Assert.That(widget.SelectedOption).IsEqualTo(0);
        await Assert.That(widget.Options).IsEquivalentTo(["Red", "Green"]);
    }

    /// <summary>A list box draws every option and highlights the selected ones; a multi-select list keeps earlier choices.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListBoxHighlightsSelection()
    {
        using var document = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SelectOption(PdfDocumentForms.GetForm(document), 0, FormSamples.PickIndex, ChosenOption);
        var content = ReadAppearance(document, FormSamples.PickIndex);
        var widget = ReadWidgets(document)[FormSamples.PickIndex];
        await Assert.That(CountOccurrences(content, " Tj")).IsEqualTo(OptionCount);
        await Assert.That(CountOccurrences(content, "0.600006 0.756866 0.854904 rg")).IsEqualTo(Highlights);
        await Assert.That(content).Contains("(Four) Tj");
        await Assert.That(widget.SelectedOption).IsEqualTo(1);
        await Assert.That(widget.Value).IsEqualTo("Two");
    }

    /// <summary>A check box without appearances gets an on and an off appearance, and its state and value follow.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CheckBoxGetsAppearances()
    {
        using var document = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);
        var changed = HyperPdfLibrary.Forms.PdfFormEditing.SetChecked(PdfDocumentForms.GetForm(document), 0, FormSamples.AgreeIndex, true);
        var widget = GetAnnotation(document, FormSamples.AgreeIndex);
        var states = widget.GetDictionary(KnownName.AP)!.GetDictionary(KnownName.N)!;
        await Assert.That(changed).IsTrue();
        await Assert.That(states.ContainsKey(document.Objects.Names.Intern("Yes"))).IsTrue();
        await Assert.That(states.ContainsKey(document.Objects.Names.Intern("Off"))).IsTrue();
        await Assert.That(document.Objects.Names.GetString(widget.GetName(KnownName.AS))).IsEqualTo("Yes");
        await Assert.That(ReadWidgets(document)[FormSamples.AgreeIndex].IsChecked).IsTrue();
        await Assert.That(ReadWidgets(document)[FormSamples.AgreeIndex].Value).IsEqualTo("Yes");
    }

    /// <summary>Switching a radio button on switches the other off, and the group's value is the chosen button's.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RadioButtonsSwitchTogether()
    {
        using var document = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetChecked(PdfDocumentForms.GetForm(document), 0, FormSamples.PlanFirstIndex, true);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetChecked(PdfDocumentForms.GetForm(document), 0, FormSamples.PlanSecondIndex, true);
        var widgets = ReadWidgets(document);
        var switchedOff = HyperPdfLibrary.Forms.PdfFormEditing.SetChecked(PdfDocumentForms.GetForm(document), 0, FormSamples.PlanSecondIndex, false);
        await Assert.That(widgets[FormSamples.PlanFirstIndex].IsChecked).IsFalse();
        await Assert.That(widgets[FormSamples.PlanSecondIndex].IsChecked).IsTrue();
        await Assert.That(switchedOff).IsFalse();
    }

    /// <summary>A rotated field is drawn sideways with a matrix that turns it into its rectangle.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RotatedFieldGetsAMatrix()
    {
        using var document = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, FormSamples.SidewaysIndex, "Hello");
        var stream = ReadStream(document, FormSamples.SidewaysIndex);
        var matrix = stream.Dictionary.GetArray(KnownName.Matrix)!;
        var box = stream.Dictionary.GetArray(KnownName.BBox)!;
        await Assert.That(box.GetSingle(RightSlot)).IsEqualTo(SidewaysLength);
        await Assert.That(box.GetSingle(TopSlot)).IsEqualTo(SidewaysThickness);
        await Assert.That(matrix.GetSingle(1)).IsEqualTo(1);
        await Assert.That(matrix.GetSingle(RightSlot)).IsEqualTo(-1);
        await Assert.That(matrix.GetSingle(TranslateXSlot)).IsEqualTo(SidewaysThickness);
    }

    /// <summary>An edit leaves dictionaries read before it unchanged, and saving writes the new value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditsReplaceCopies()
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateForm(), null);
        var before = GetAnnotation(document, NameIndex);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, NameIndex, TypedName);
        var saved = HyperPdfLibrary.Writing.PdfIncrementalWriter.Save(document.Objects);
        using var reopened = PdfDocumentReader.Open(saved, null);
        await Assert.That(before.ContainsKey(KnownName.V)).IsFalse();
        await Assert.That(GetAnnotation(document, NameIndex)).IsNotSameReferenceAs(before);
        await Assert.That(StoreEditing.HasEdits(document.Objects)).IsTrue();
        await Assert.That(ReadWidgets(reopened)[NameIndex].Value).IsEqualTo(TypedName);
        await Assert.That(ReadAppearance(reopened, NameIndex)).Contains($"({TypedName}) Tj");
    }

    /// <summary>Repeated edits reuse the appearance object they drew.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatedEditsReuseTheAppearance()
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateForm(), null);
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, NameIndex, "one");
        var first = document.Objects.Size;
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, NameIndex, "two");
        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, NameIndex, "three");
        await Assert.That(document.Objects.Size).IsEqualTo(first);
        await Assert.That(ReadAppearance(document, NameIndex)).Contains("(three) Tj");
    }

    /// <summary>The form reports a missing form, and read-only and wrong-kind edits do nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesWhatItCannotDo()
    {
        using var plain = PdfDocumentReader.Open(TestPdf.Create(1), null);
        using var rich = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);
        using var simple = PdfDocumentReader.Open(TestPdf.CreateForm(), null);
        await Assert.That(HyperPdfLibrary.Forms.PdfFormReading.HasForm(PdfDocumentForms.GetForm(plain))).IsFalse();
        await Assert.That(HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(plain), 0, 0, "x")).IsFalse();
        await Assert.That(HyperPdfLibrary.Forms.PdfFormReading.HasForm(PdfDocumentForms.GetForm(rich))).IsTrue();
        await Assert.That(HyperPdfLibrary.Forms.PdfFormReading.NeedAppearances(PdfDocumentForms.GetForm(rich))).IsFalse();
        await Assert.That(HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(rich), 0, FormSamples.LockedIndex, "x")).IsFalse();
        await Assert.That(HyperPdfLibrary.Forms.PdfFormEditing.SetChecked(PdfDocumentForms.GetForm(rich), 0, FormSamples.NotesIndex, true)).IsFalse();
        await Assert.That(HyperPdfLibrary.Forms.PdfFormEditing.SelectOption(PdfDocumentForms.GetForm(rich), 0, FormSamples.PickIndex, OptionCount)).IsFalse();
        await Assert.That(HyperPdfLibrary.Forms.PdfFormEditing.SelectOption(PdfDocumentForms.GetForm(rich), 0, FormSamples.PickIndex, -1)).IsFalse();
        await Assert.That(StoreEditing.HasEdits(rich.Objects)).IsFalse();
        await Assert.That(HyperPdfLibrary.Forms.PdfFormReading.NeedAppearances(PdfDocumentForms.GetForm(simple))).IsTrue();
    }

    /// <summary>Fields inherit their type, value, limit and flags from their parents.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FieldsInheritFromParents()
    {
        using var document = PdfDocumentReader.Open(FormSamples.CreateRichForm(), null);
        var widgets = ReadWidgets(document);
        var street = widgets[FormSamples.StreetIndex];
        var zip = widgets[FormSamples.ZipIndex];
        await Assert.That(widgets.Count).IsEqualTo(FormSamples.WidgetCount);
        await Assert.That(street.Name).IsEqualTo("Address.Street");
        await Assert.That(street.Type).IsEqualTo(HyperPdfLibrary.Forms.PdfFieldType.Text);
        await Assert.That(street.Value).IsEqualTo("Main St");
        await Assert.That(street.FontSize).IsEqualTo(StreetSize);
        await Assert.That(zip.IsComb).IsTrue();
        await Assert.That(zip.MaxLength).IsEqualTo(ZipBoxes);
        await Assert.That(widgets[FormSamples.PlanFirstIndex].Name).IsEqualTo("Plan");
        await Assert.That(widgets[FormSamples.PlanFirstIndex].Value).IsEqualTo("Off");
    }

    /// <summary>The raw JavaScript of keystroke, format, validate and calculate actions is returned.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScriptsAreReadRaw()
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateCalculatedForm(), null);
        var scripts = new List<HyperPdfLibrary.Forms.PdfWidgetScripts>();
        HyperPdfLibrary.Forms.PdfFormReading.GetScripts(PdfDocumentForms.GetForm(document), 0, scripts);
        await Assert.That(scripts.Count).IsEqualTo(ScriptedFields);
        await Assert.That(scripts.Single(static s => s.Name == "Total").Calculate).Contains("AFSimple_Calculate");
        await Assert.That(scripts.Single(static s => s.Name == "Price").Keystroke).Contains("AFNumber_Keystroke");
    }

    /// <summary>Matches the position of each character of a comb field.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"1 0 0 1 (?<x>[-\d.]+) (?<y>[-\d.]+) Tm")]
    private static partial Regex CellPositions();

    /// <summary>Counts non-overlapping occurrences.</summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The value to count.</param>
    /// <returns>The count.</returns>
    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }

    /// <summary>Gets a page 1 widget's annotation dictionary.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The widget index.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary GetAnnotation(PdfDocument document, int index) => PdfDocumentPages.GetPage(document, 0).Dictionary.GetArray(KnownName.Annots)!.GetDictionary(index)!;

    /// <summary>Gets a widget's normal appearance stream.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The widget index.</param>
    /// <returns>The stream.</returns>
    private static PdfStream ReadStream(PdfDocument document, int index) => GetAnnotation(document, index).GetDictionary(KnownName.AP)?.GetStream(KnownName.N) ?? new(new(document.Objects), []);

    /// <summary>Decodes a widget's normal appearance stream.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The widget index.</param>
    /// <returns>The content as text.</returns>
    private static string ReadAppearance(PdfDocument document, int index) => Encoding.Latin1.GetString(ReadStream(document, index).DecodeToArray());

    /// <summary>Reads the widgets of page 1.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The widgets.</returns>
    private static List<HyperPdfLibrary.Forms.PdfFormWidget> ReadWidgets(PdfDocument document)
    {
        var widgets = new List<HyperPdfLibrary.Forms.PdfFormWidget>();
        HyperPdfLibrary.Forms.PdfFormReading.GetWidgets(PdfDocumentForms.GetForm(document), 0, widgets);
        return widgets;
    }
}
