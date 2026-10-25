// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests creating form fields: every kind, their widgets, inheritance, names, appearances and the form dictionary.</summary>
public sealed class FormFieldCreationTests
{
    /// <summary>The pages in the test document.</summary>
    private const int Pages = 3;

    /// <summary>The second page's index.</summary>
    private const int SecondPage = 1;

    /// <summary>The third page's index.</summary>
    private const int ThirdPage = 2;

    /// <summary>The third row.</summary>
    private const int ThirdRow = 2;

    /// <summary>The fourth row.</summary>
    private const int FourthRow = 3;

    /// <summary>The name of the first test field.</summary>
    private const string FirstName = "Name";

    /// <summary>The most characters the text field takes.</summary>
    private const int Limit = 20;

    /// <summary>The number of fields created in the bulk test.</summary>
    private const int Bulk = 1000;

    /// <summary>The rows in the bulk test before the pattern repeats.</summary>
    private const int BulkRows = 15;

    /// <summary>The export value of the check box.</summary>
    private const string Accepted = "Accepted";

    /// <summary>The name of a field under Address.</summary>
    private const string CityName = "Address.City";

    /// <summary>The option labelled Banana.</summary>
    private const string Banana = "Banana";

    /// <summary>The option labelled Apple.</summary>
    private const string Apple = "Apple";

    /// <summary>The option that shows its own value.</summary>
    private const string Cherry = "Cherry";

    /// <summary>The radio export value Green.</summary>
    private const string Green = "Green";

    /// <summary>The radio export value Small.</summary>
    private const string Small = "Small";

    /// <summary>The pages of the radio group.</summary>
    private static readonly int[] RadioPages = [0, SecondPage, ThirdPage];

    /// <summary>The labels of the sorted combo box.</summary>
    private static readonly string[] SortedLabels = [Apple, Banana, Cherry];

    /// <summary>The labels in the order given.</summary>
    private static readonly string[] GivenLabels = [Banana, Apple, Cherry];

    /// <summary>A text field with a value is created, shows on its page with its text, fills in and saves.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextFieldIsCreatedAndFilled()
    {
        using var document = FormDesignDocuments.OpenPlain(Pages);
        var created = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(FirstName, 0) with { Value = "Ada", MaxLength = Limit, Quadding = 1 });

        var widgets = FormDesignDocuments.Widgets(document, 0);
        await Assert.That(created.Widgets.Length).IsEqualTo(1);
        await Assert.That(widgets.Count).IsEqualTo(1);
        await Assert.That(widgets[0].Name).IsEqualTo(FirstName);
        await Assert.That(widgets[0].Type).IsEqualTo(PdfFieldType.Text);
        await Assert.That(widgets[0].Value).IsEqualTo("Ada");
        await Assert.That(widgets[0].MaxLength).IsEqualTo(Limit);

        var form = PdfDocumentForms.GetForm(document);
        await Assert.That(PdfFormEditing.SetText(form, 0, widgets[0].Index, "Grace")).IsTrue();
        var saved = await FormDesignDocuments.SaveAsync(document);
        using var reopened = PdfDocumentReader.Open(saved, null);
        await Assert.That(FormDesignDocuments.Widgets(reopened, 0)[0].Value).IsEqualTo("Grace");
        await Assert.That(await FormDesignDocuments.PassesQpdfAsync("text", saved)).IsTrue();
    }

    /// <summary>A form is made when the document has none, with default appearance, default resources and no request for readers to redraw.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormDictionaryIsMadeWithDefaults()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(FirstName, 0));

        var form = document.Catalog.GetDictionary(KnownName.AcroForm)!;
        await Assert.That(form.GetArray(KnownName.Fields)!.Count).IsEqualTo(1);
        await Assert.That(form.GetStringBytes(KnownName.DA).Length).IsGreaterThan(0);
        await Assert.That(form.GetDictionary(KnownName.DR)!.GetDictionary(KnownName.Font)!.ContainsKey(document.Objects.Names.Intern("Helv"))).IsTrue();
        await Assert.That(form.ContainsKey(KnownName.NeedAppearances)).IsFalse();
    }

    /// <summary>A field with one widget is one merged dictionary; a field with two widgets has a parent with two kids that hold no name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OneWidgetIsMergedAndSeveralAreKids()
    {
        using var document = FormDesignDocuments.OpenPlain(Pages);
        var single = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text("One", 0));
        var several = PdfFormFieldCreation.CreateField(document, new(PdfFieldType.Text, "Two", [FormDesignDocuments.Place(0, 1), FormDesignDocuments.Place(SecondPage, 1)]));

        var merged = FormDesignDocuments.Dictionary(document, single.FieldId);
        var parent = FormDesignDocuments.Dictionary(document, several.FieldId);
        var kid = parent.GetArray(KnownName.Kids)!.GetDictionary(0)!;
        await Assert.That(merged.IsName(KnownName.Subtype, KnownName.Widget)).IsTrue();
        await Assert.That(merged.ContainsKey(KnownName.Kids)).IsFalse();
        await Assert.That(parent.IsName(KnownName.Subtype, KnownName.Widget)).IsFalse();
        await Assert.That(parent.GetArray(KnownName.Kids)!.Count).IsEqualTo(several.Widgets.Length);
        await Assert.That(kid.ContainsKey(KnownName.T)).IsFalse();
        await Assert.That(kid.ContainsKey(KnownName.FT)).IsFalse();
        await Assert.That(kid.GetRaw(KnownName.Parent).AsReference()).IsEqualTo(several.FieldId);
        await Assert.That(several.Widgets[1].PageIndex).IsEqualTo(SecondPage);
        await Assert.That(FormDesignDocuments.Widgets(document, SecondPage)[0].Name).IsEqualTo("Two");
    }

    /// <summary>A check box has on and off appearances, starts in the state asked for, and a user can switch it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CheckBoxHasOnAndOffStates()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        var created = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Field(PdfFieldType.CheckBox, "Agree", 0) with { IsChecked = true, ExportValue = Accepted });

        var widget = FormDesignDocuments.Dictionary(document, created.FieldId);
        var states = widget.GetDictionary(KnownName.AP)!.GetDictionary(KnownName.N)!;
        await Assert.That(states.ContainsKey(document.Objects.Names.Intern(Accepted))).IsTrue();
        await Assert.That(states.ContainsKey(document.Objects.Names.Intern("Off"))).IsTrue();
        await Assert.That(document.Objects.Names.GetString(widget.GetName(KnownName.AS))).IsEqualTo(Accepted);
        await Assert.That(FormDesignDocuments.Widgets(document, 0)[0].IsChecked).IsTrue();

        var form = PdfDocumentForms.GetForm(document);
        await Assert.That(PdfFormEditing.SetChecked(form, 0, 0, false)).IsTrue();
        await Assert.That(FormDesignDocuments.Widgets(document, 0)[0].IsChecked).IsFalse();
    }

    /// <summary>A radio group across pages has one export value per button, one selected, and switching one switches the others off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RadioGroupSpansPagesWithExportValues()
    {
        using var document = FormDesignDocuments.OpenPlain(Pages);
        PdfWidgetPlacement[] buttons =
        [
            FormDesignDocuments.Place(0, 0) with { ExportValue = "Red" },
            FormDesignDocuments.Place(SecondPage, 0) with { ExportValue = Green },
            FormDesignDocuments.Place(ThirdPage, 0) with { ExportValue = "Blue" },
        ];
        var created = PdfFormFieldCreation.CreateField(document, new(PdfFieldType.RadioButton, "Colour", buttons) { Value = Green, Flags = PdfFieldFlags.NoToggleToOff });

        await Assert.That(created.Widgets.Select(static w => w.PageIndex).ToArray()).IsEquivalentTo(RadioPages);
        await Assert.That(FormDesignDocuments.Widgets(document, 0)[0].IsChecked).IsFalse();
        await Assert.That(FormDesignDocuments.Widgets(document, SecondPage)[0].IsChecked).IsTrue();
        await Assert.That(FormDesignDocuments.Widgets(document, SecondPage)[0].Value).IsEqualTo(Green);

        var form = PdfDocumentForms.GetForm(document);
        await Assert.That(PdfFormEditing.SetChecked(form, ThirdPage, 0, true)).IsTrue();
        await Assert.That(FormDesignDocuments.Widgets(document, SecondPage)[0].IsChecked).IsFalse();
        await Assert.That(FormDesignDocuments.Widgets(document, ThirdPage)[0].Value).IsEqualTo("Blue");
    }

    /// <summary>Export values that cannot be names are kept in <c>/Opt</c> and the buttons are named by position.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnusualExportValuesUseOptions()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        PdfWidgetPlacement[] buttons = [FormDesignDocuments.Place(0, 0) with { ExportValue = "Extra large" }, FormDesignDocuments.Place(0, 1) with { ExportValue = Small }];
        var created = PdfFormFieldCreation.CreateField(document, new(PdfFieldType.RadioButton, "Size", buttons) { Value = Small });

        var field = FormDesignDocuments.Dictionary(document, created.FieldId);
        await Assert.That(field.GetArray(KnownName.Opt)!.Count).IsEqualTo(buttons.Length);
        await Assert.That(document.Objects.Names.GetString(field.GetName(KnownName.V))).IsEqualTo("1");
        await Assert.That(FormDesignDocuments.Widgets(document, 0)[1].Value).IsEqualTo(Small);
    }

    /// <summary>A combo box and a list box keep their options, labels and selection; a sorted list is written in order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoiceFieldsKeepOptionsAndSelection()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        PdfFieldOption[] options = [new("b", Banana), new("a", Apple), PdfFieldOption.FromValue(Cherry)];
        var combo = FormDesignDocuments.Field(PdfFieldType.ComboBox, "Fruit", 0) with { Options = options, Value = "a", Flags = PdfFieldFlags.Sort };
        var list = FormDesignDocuments.Field(PdfFieldType.ListBox, "Fruits", 1) with { Options = options, Values = ["b", Cherry], Flags = PdfFieldFlags.MultiSelect };
        _ = PdfFormFieldCreation.CreateFields(document, [combo, list]);

        var widgets = FormDesignDocuments.Widgets(document, 0);
        await Assert.That(widgets[0].Type).IsEqualTo(PdfFieldType.ComboBox);
        await Assert.That(widgets[0].Options).IsEquivalentTo(SortedLabels);
        await Assert.That(widgets[0].SelectedOption).IsEqualTo(0);
        await Assert.That(widgets[1].Type).IsEqualTo(PdfFieldType.ListBox);
        await Assert.That(widgets[1].Options).IsEquivalentTo(GivenLabels);
        await Assert.That(PdfFormEditing.SelectOption(PdfDocumentForms.GetForm(document), 0, 0, ThirdRow)).IsTrue();
    }

    /// <summary>A push button keeps its caption and runs its action as data; a signature field gets a placeholder appearance and no value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PushButtonAndSignatureFieldAreCreated()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        var push = PdfFormFieldCreation.CreateField(
            document,
            FormDesignDocuments.Field(PdfFieldType.PushButton, "Send", 0) with { Caption = "Send now", Actions = new() { ActivateUri = "https://example.com/send" } });
        var signature = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Field(PdfFieldType.Signature, "Sign", 1));

        var button = FormDesignDocuments.Dictionary(document, push.FieldId);
        await Assert.That(button.GetDictionary(KnownName.MK)!.GetStringBytes(KnownName.CA).Length).IsGreaterThan(0);
        await Assert.That(button.GetDictionary(KnownName.A)!.IsName(KnownName.S, KnownName.URI)).IsTrue();
        var sign = FormDesignDocuments.Dictionary(document, signature.FieldId);
        await Assert.That(sign.IsName(KnownName.FT, KnownName.Sig)).IsTrue();
        await Assert.That(sign.ContainsKey(KnownName.V)).IsFalse();
        await Assert.That(sign.GetDictionary(KnownName.AP)!.GetStream(KnownName.N)).IsNotNull();
    }

    /// <summary>A name with periods puts the field under group fields, and a clash or a field that holds a value above it is refused with a reason.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamesWithPeriodsNestAndClashesAreRefused()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text("Address.Street", 0));
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(CityName, 1));

        var fields = document.Catalog.GetDictionary(KnownName.AcroForm)!.GetArray(KnownName.Fields)!;
        await Assert.That(fields.Count).IsEqualTo(1);
        await Assert.That(fields.GetDictionary(0)!.GetText(KnownName.T)).IsEqualTo("Address");
        await Assert.That(fields.GetDictionary(0)!.GetArray(KnownName.Kids)!.Count).IsEqualTo(ThirdRow);
        await Assert.That(FormDesignDocuments.Widgets(document, 0)[1].Name).IsEqualTo(CityName);

        await Assert.That(() => PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(CityName, ThirdRow))).Throws<InvalidOperationException>();
        await Assert.That(() => PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text("Address.City.Zip", ThirdRow))).Throws<InvalidOperationException>();
        await Assert.That(() => PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text("Address", ThirdRow))).Throws<InvalidOperationException>();
        await Assert.That(() => PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text("A..B", ThirdRow))).Throws<ArgumentException>();
        await Assert.That(document.Catalog.GetDictionary(KnownName.AcroForm)!.GetArray(KnownName.Fields)!.Count).IsEqualTo(1);
    }

    /// <summary>Readers are asked to draw appearances only when the caller asks for no appearance.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeedAppearancesIsSetOnlyWithoutAppearances()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text("Plain", 0));
        await Assert.That(document.Catalog.GetDictionary(KnownName.AcroForm)!.ContainsKey(KnownName.NeedAppearances)).IsFalse();

        var bare = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text("Bare", 1) with { GenerateAppearances = false });
        await Assert.That(FormDesignDocuments.Dictionary(document, bare.FieldId).ContainsKey(KnownName.AP)).IsFalse();
        await Assert.That(document.Catalog.GetDictionary(KnownName.AcroForm)!.GetBoolean(KnownName.NeedAppearances)).IsTrue();
    }

    /// <summary>Creating a field is one undo step and redo brings it back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CreationUndoesAndRedoes()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(FirstName, 0));
        await Assert.That(FormDesignDocuments.Widgets(document, 0).Count).IsEqualTo(1);

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(FormDesignDocuments.Widgets(document, 0).Count).IsEqualTo(0);
        await Assert.That(document.Catalog.ContainsKey(KnownName.AcroForm)).IsFalse();

        await Assert.That(PdfDocumentEditing.Redo(document)).IsTrue();
        await Assert.That(FormDesignDocuments.Widgets(document, 0).Count).IsEqualTo(1);
    }

    /// <summary>A refused definition leaves the document unchanged, even when it follows good ones in the same call.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusedBatchLeavesNothingBehind()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        PdfFieldDefinition[] batch = [FormDesignDocuments.Text("Good", 0), FormDesignDocuments.Text("Good", 1)];

        await Assert.That(() => PdfFormFieldCreation.CreateFields(document, batch)).Throws<InvalidOperationException>();
        await Assert.That(document.Catalog.ContainsKey(KnownName.AcroForm)).IsFalse();
        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoCount).IsEqualTo(0);
    }

    /// <summary>Fields of every kind survive an incremental and a compact save, and both files pass qpdf.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryKindSurvivesBothSaves()
    {
        using var document = FormDesignDocuments.OpenPlain(Pages);
        PdfFieldDefinition[] kinds =
        [
            FormDesignDocuments.Text(FirstName, 0) with { Value = "Ada" },
            FormDesignDocuments.Field(PdfFieldType.CheckBox, "Agree", 1) with { IsChecked = true },
            new(PdfFieldType.RadioButton, "Colour", [FormDesignDocuments.Place(0, ThirdRow), FormDesignDocuments.Place(SecondPage, 0)]) { Value = "Choice2" },
            FormDesignDocuments.Field(PdfFieldType.ComboBox, "Fruit", FourthRow) with { Options = [new("a", Apple), new("b", Banana)], Value = "b" },
            FormDesignDocuments.Field(PdfFieldType.ListBox, "Fruits", FourthRow + 1) with { Options = [new("a", Apple), new("b", Banana)] },
            FormDesignDocuments.Field(PdfFieldType.PushButton, "Send", FourthRow + ThirdRow) with { Caption = "Send" },
            FormDesignDocuments.Field(PdfFieldType.Signature, "Sign", FourthRow + FourthRow),
        ];
        _ = PdfFormFieldCreation.CreateFields(document, kinds);

        var incremental = await FormDesignDocuments.SaveAsync(document);
        var compact = FormDesignDocuments.SaveCompact(document);
        foreach (var file in (byte[][])[incremental, compact])
        {
            using var reopened = PdfDocumentReader.Open(file, null);
            var widgets = FormDesignDocuments.Widgets(reopened, 0);
            await Assert.That(widgets.Count).IsEqualTo(kinds.Length);
            await Assert.That(widgets[0].Value).IsEqualTo("Ada");
            await Assert.That(widgets[1].IsChecked).IsTrue();
            await Assert.That(FormDesignDocuments.Widgets(reopened, SecondPage)[0].IsChecked).IsTrue();
            await Assert.That(widgets[FourthRow].Value).IsEqualTo("b");
        }

        await Assert.That(await FormDesignDocuments.PassesQpdfAsync("kinds-incremental", incremental)).IsTrue();
        await Assert.That(await FormDesignDocuments.PassesQpdfAsync("kinds-compact", compact)).IsTrue();
    }

    /// <summary>A thousand fields are created in one call and every widget is on its page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThousandFieldsAreCreatedTogether()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        var definitions = new PdfFieldDefinition[Bulk];
        for (var i = 0; i < Bulk; i++)
        {
            definitions[i] = FormDesignDocuments.Text($"Row{i}", i % BulkRows);
        }

        var created = await PdfFormFieldCreation.CreateFieldsAsync(document, definitions, CancellationToken.None);

        await Assert.That(created.Length).IsEqualTo(Bulk);
        await Assert.That(created[^1].Widgets[0].Index).IsEqualTo(Bulk - 1);
        await Assert.That(FormDesignDocuments.Widgets(document, 0).Count).IsEqualTo(Bulk);
        await Assert.That(FormDesignDocuments.FindDanglingReferences(document)).IsEmpty();
        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoCount).IsEqualTo(1);
    }
}
