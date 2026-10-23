// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Conformance;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests reading fields and changing their attributes, options, export values, look and place.</summary>
public sealed class FormFieldEditingTests
{
    /// <summary>The second part of ISO 19005.</summary>
    private const int PdfA2 = 2;

    /// <summary>The pages of the two page test documents.</summary>
    private const int TwoPages = 2;

    /// <summary>The option labelled Apple.</summary>
    private const string Apple = "Apple";

    /// <summary>The option labelled Banana.</summary>
    private const string Banana = "Banana";

    /// <summary>The second default radio export value.</summary>
    private const string SecondChoice = "Choice2";

    /// <summary>A radio export value with a space in it.</summary>
    private const string GreenBlue = "Green blue";

    /// <summary>The name of the check box.</summary>
    private const string AgreeName = "Agree";

    /// <summary>A tooltip written by a test.</summary>
    private const string GivenName = "Given name";

    /// <summary>The name of the text field.</summary>
    private const string TextName = "Name";

    /// <summary>The name of the combo box.</summary>
    private const string ComboName = "Fruit";

    /// <summary>The name of the radio group.</summary>
    private const string RadioName = "Colour";

    /// <summary>The most characters the text field takes after a change.</summary>
    private const int Limit = 8;

    /// <summary>The fourth row.</summary>
    private const int FourthRow = 3;

    /// <summary>The third row.</summary>
    private const int ThirdRow = 2;

    /// <summary>The distance a widget is moved.</summary>
    private const float Shift = 30;

    /// <summary>The width of the widget after a resize.</summary>
    private const float NewWidth = 200;

    /// <summary>The border width set by a test.</summary>
    private const float ThickBorder = 3;

    /// <summary>All the fields are listed with their kind, value, options and widget places.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FieldsAreListedWithTheirDetails()
    {
        using var document = FormDesignDocuments.OpenPlain(TwoPages);
        _ = PdfFormFieldCreation.CreateFields(
            document,
            [
                FormDesignDocuments.Text(TextName, 0) with { Value = "Ada", MaxLength = Limit, Quadding = 1, Tooltip = "Your name" },
                FormDesignDocuments.Field(PdfFieldType.ComboBox, ComboName, 1) with { Options = [new("a", Apple), new("b", Banana)], Value = "b" },
                new(PdfFieldType.RadioButton, RadioName, [FormDesignDocuments.Place(0, ThirdRow), FormDesignDocuments.Place(1, 0)]) { Value = SecondChoice },
            ]);

        var fields = await PdfFormFieldEditing.GetFieldsAsync(document, CancellationToken.None);

        await Assert.That(fields.Length).IsEqualTo(ThirdRow + 1);
        await Assert.That(fields[0].Value).IsEqualTo("Ada");
        await Assert.That(fields[0].MaxLength).IsEqualTo(Limit);
        await Assert.That(fields[0].Quadding).IsEqualTo(1);
        await Assert.That(fields[0].Tooltip).IsEqualTo("Your name");
        await Assert.That(fields[1].Options.Length).IsEqualTo(ThirdRow);
        await Assert.That(fields[1].Options[1].Label).IsEqualTo(Banana);
        await Assert.That(fields[1].Value).IsEqualTo("b");
        await Assert.That(fields[ThirdRow].Value).IsEqualTo(SecondChoice);
        await Assert.That(fields[ThirdRow].Widgets[1].Location.PageIndex).IsEqualTo(1);
        await Assert.That(fields[ThirdRow].Widgets[1].IsChecked).IsTrue();
    }

    /// <summary>Changing a text field's attributes redraws it, keeps what was not changed, and survives both saves and qpdf.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextFieldChangesAreWrittenAndSaved()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(TextName, 0) with { Value = "Ada" });

        var result = PdfFormFieldEditing.ChangeField(
            document,
            TextName,
            new()
            {
                MaxLength = Limit,
                Quadding = 1,
                AddFlags = PdfFieldFlags.Required | PdfFieldFlags.ReadOnly,
                Tooltip = GivenName,
                DefaultValue = "Anon",
                BorderColor = PdfFieldColor.FromRgb(1, 0, 0),
                BackgroundColor = PdfFieldColor.FromRgb(1, 1, 0),
                BorderWidth = ThickBorder,
                BorderStyle = PdfFieldBorderStyle.Dashed,
            });

        await Assert.That(result.Applied).IsTrue();
        var field = PdfFormFieldEditing.GetField(document, TextName)!;
        await Assert.That(field.Value).IsEqualTo("Ada");
        await Assert.That(field.MaxLength).IsEqualTo(Limit);
        await Assert.That(field.Quadding).IsEqualTo(1);
        await Assert.That((field.Flags & (PdfFieldFlags.Required | PdfFieldFlags.ReadOnly)) == (PdfFieldFlags.Required | PdfFieldFlags.ReadOnly)).IsTrue();
        await Assert.That(field.Tooltip).IsEqualTo(GivenName);
        await Assert.That(field.DefaultValue).IsEqualTo("Anon");
        await Assert.That(field.BorderColor).IsEqualTo(PdfFieldColor.FromRgb(1, 0, 0));
        await Assert.That(field.BackgroundColor).IsEqualTo(PdfFieldColor.FromRgb(1, 1, 0));
        await Assert.That(field.BorderWidth).IsEqualTo(ThickBorder);
        await Assert.That(field.BorderStyle).IsEqualTo(PdfFieldBorderStyle.Dashed);
        await Assert.That(FormDesignDocuments.FindDanglingReferences(document)).IsEmpty();
        foreach (var file in (byte[][])[await FormDesignDocuments.SaveAsync(document), FormDesignDocuments.SaveCompact(document)])
        {
            using var reopened = PdfDocumentReader.Open(file, null);
            await Assert.That(PdfFormFieldEditing.GetField(reopened, TextName)!.MaxLength).IsEqualTo(Limit);
            await Assert.That(await FormDesignDocuments.PassesQpdfAsync("edit-text", file)).IsTrue();
        }
    }

    /// <summary>A change that breaks a rule is refused with a reason and writes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusedChangesLeaveTheFieldAlone()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateFields(
            document,
            [FormDesignDocuments.Text(TextName, 0) with { Value = "Ada" }, FormDesignDocuments.Field(PdfFieldType.ComboBox, ComboName, 1) with { Options = [new("a", Apple)] }]);
        var undo = PdfDocumentEditing.GetHistory(document).UndoCount;

        var tooLong = PdfFormFieldEditing.ChangeField(document, TextName, new() { Value = "much too long a value", MaxLength = Limit });
        var kind = PdfFormFieldEditing.ChangeField(document, TextName, new() { AddFlags = PdfFieldFlags.Combo });
        var missingFont = PdfFormFieldEditing.ChangeField(document, TextName, new() { DefaultAppearance = "/NoSuchFont 12 Tf 0 g" });
        var notOption = PdfFormFieldEditing.ChangeField(document, ComboName, new() { Value = "zzz" });
        var noName = PdfFormFieldEditing.ChangeField(document, "Missing", new() { Tooltip = "x" });
        var wrongKind = PdfFormFieldEditing.ChangeField(document, TextName, new() { Options = [new("a", "A")] });

        await Assert.That(new[] { tooLong, kind, missingFont, notOption, noName, wrongKind }.Count(static r => r.Applied)).IsEqualTo(0);
        await Assert.That(tooLong.Reason).Contains("longer");
        await Assert.That(kind.Reason).Contains("kind");
        await Assert.That(missingFont.Reason).Contains("NoSuchFont");
        await Assert.That(noName.Reason).Contains("No field");
        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoCount).IsEqualTo(undo);
        await Assert.That(PdfFormFieldEditing.GetField(document, TextName)!.Value).IsEqualTo("Ada");
    }

    /// <summary>Changing the options of a combo box drops a selection that is no longer an option and keeps the others.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptionChangesDropAStaleSelection()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateField(
            document,
            FormDesignDocuments.Field(PdfFieldType.ComboBox, ComboName, 0) with { Options = [new("a", Apple), new("b", Banana)], Value = "b" });

        var kept = PdfFormFieldEditing.ChangeField(document, ComboName, new() { Options = [new("b", "Bananas"), new("c", "Cherry")] });
        await Assert.That(kept.Applied).IsTrue();
        var afterKept = PdfFormFieldEditing.GetField(document, ComboName)!;
        await Assert.That(afterKept.Value).IsEqualTo("b");
        await Assert.That(afterKept.Options[0].Label).IsEqualTo("Bananas");

        var dropped = PdfFormFieldEditing.ChangeField(document, ComboName, new() { Options = [new("c", "Cherry")] });
        await Assert.That(dropped.Applied).IsTrue();
        await Assert.That(PdfFormFieldEditing.GetField(document, ComboName)!.Value).IsNull();

        var selected = PdfFormFieldEditing.ChangeField(document, ComboName, new() { Value = "c" });
        await Assert.That(selected.Applied).IsTrue();
        await Assert.That(PdfFormFieldEditing.GetField(document, ComboName)!.Value).IsEqualTo("c");
        await Assert.That(await FormDesignDocuments.PassesQpdfAsync("edit-combo", FormDesignDocuments.SaveCompact(document))).IsTrue();
    }

    /// <summary>The export values of a radio group can be changed, and the selection follows the button.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RadioExportValuesAreChanged()
    {
        using var document = FormDesignDocuments.OpenPlain(TwoPages);
        _ = PdfFormFieldCreation.CreateField(
            document,
            new(PdfFieldType.RadioButton, RadioName, [FormDesignDocuments.Place(0, 0), FormDesignDocuments.Place(1, 0)]) { Value = SecondChoice });

        var result = PdfFormFieldEditing.ChangeField(document, RadioName, new() { ExportValues = ["Red", GreenBlue], Value = GreenBlue });

        await Assert.That(result.Applied).IsTrue();
        var field = PdfFormFieldEditing.GetField(document, RadioName)!;
        await Assert.That(field.Widgets[0].ExportValue).IsEqualTo("Red");
        await Assert.That(field.Widgets[1].ExportValue).IsEqualTo(GreenBlue);
        await Assert.That(field.Value).IsEqualTo(GreenBlue);
        await Assert.That(PdfFormFieldEditing.ChangeField(document, RadioName, new() { ExportValues = ["Same", "Same"] }).Applied).IsFalse();
        await Assert.That(PdfFormFieldEditing.ChangeField(document, RadioName, new() { ExportValues = ["Only one"] }).Applied).IsFalse();
        foreach (var file in (byte[][])[await FormDesignDocuments.SaveAsync(document), FormDesignDocuments.SaveCompact(document)])
        {
            using var reopened = PdfDocumentReader.Open(file, null);
            await Assert.That(FormDesignDocuments.Widgets(reopened, 1)[0].IsChecked).IsTrue();
            await Assert.That(await FormDesignDocuments.PassesQpdfAsync("edit-radio", file)).IsTrue();
        }
    }

    /// <summary>A check box can be switched on and its export value changed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CheckBoxStateAndExportValueAreChanged()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Field(PdfFieldType.CheckBox, AgreeName, 0));

        var result = PdfFormFieldEditing.ChangeField(document, AgreeName, new() { IsChecked = true, ExportValues = ["Yes"] });

        await Assert.That(result.Applied).IsTrue();
        var field = PdfFormFieldEditing.GetField(document, AgreeName)!;
        await Assert.That(field.IsChecked).IsTrue();
        await Assert.That(field.Widgets[0].ExportValue).IsEqualTo("Yes");
    }

    /// <summary>A push button's caption and action can be changed and removed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PushButtonCaptionAndActionAreChanged()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Field(PdfFieldType.PushButton, "Go", 0) with { Caption = "Go" });

        _ = PdfFormFieldEditing.ChangeField(document, "Go", new() { Caption = "Send", Actions = new() { ActivateUri = "https://example.com/" } });
        var changed = PdfFormFieldEditing.GetField(document, "Go")!;
        await Assert.That(changed.Caption).IsEqualTo("Send");
        await Assert.That(changed.Actions!.ActivateUri).IsEqualTo("https://example.com/");

        _ = PdfFormFieldEditing.ChangeField(document, "Go", new() { Actions = new() });
        await Assert.That(PdfFormFieldEditing.GetField(document, "Go")!.Actions).IsNull();
    }

    /// <summary>A widget moves and resizes, and the change is one undo step.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WidgetMovesAndResizesWithUndo()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(TextName, 0) with { Value = "Ada" });
        var original = FormDesignDocuments.Box(0);
        var target = new PdfRectangle(original.Left + Shift, original.Bottom + Shift, original.Left + Shift + NewWidth, original.Top + Shift);

        var result = PdfFormFieldEditing.SetWidgetBounds(document, TextName, 0, target);

        await Assert.That(result.Applied).IsTrue();
        await Assert.That(PdfFormFieldEditing.GetField(document, TextName)!.Widgets[0].Bounds).IsEqualTo(target);
        await Assert.That(PdfFormFieldEditing.SetWidgetBounds(document, TextName, FourthRow, target).Applied).IsFalse();
        await Assert.That(PdfFormFieldEditing.SetWidgetBounds(document, TextName, 0, new(0, 0, 0, 0)).Applied).IsFalse();
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfFormFieldEditing.GetField(document, TextName)!.Widgets[0].Bounds).IsEqualTo(original);
        await Assert.That(PdfDocumentEditing.Redo(document)).IsTrue();
        await Assert.That(PdfFormFieldEditing.GetField(document, TextName)!.Widgets[0].Bounds).IsEqualTo(target);
        await Assert.That(await FormDesignDocuments.PassesQpdfAsync("edit-move", await FormDesignDocuments.SaveAsync(document))).IsTrue();
    }

    /// <summary>A change is one undo step and redo brings it back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangeUndoesAndRedoes()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(TextName, 0) with { Value = "Ada" });
        _ = PdfFormFieldEditing.ChangeField(document, TextName, new() { Value = "Grace" });

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfFormFieldEditing.GetField(document, TextName)!.Value).IsEqualTo("Ada");
        await Assert.That(PdfDocumentEditing.Redo(document)).IsTrue();
        await Assert.That(PdfFormFieldEditing.GetField(document, TextName)!.Value).IsEqualTo("Grace");
    }

    /// <summary>A PDF/A document refuses a script and a font it does not embed, and takes a change that needs neither.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfADocumentRefusesWhatBreaksTheClaim()
    {
        using var document = OpenClaimingPdfA();
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Field(PdfFieldType.CheckBox, AgreeName, 0));
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Field(PdfFieldType.PushButton, "Go", 1));

        var script = PdfFormFieldEditing.ChangeField(document, "Go", new() { Actions = new() { Activate = "app.alert(1)" } });
        var plain = PdfFormFieldEditing.ChangeField(document, AgreeName, new() { IsChecked = true, Tooltip = "Agree to the terms" });

        await Assert.That(script.Applied).IsFalse();
        await Assert.That(script.Reason).Contains("PDF/A");
        await Assert.That(plain.Applied).IsTrue();
        await Assert.That(document.Catalog.GetDictionary(KnownName.AcroForm)!.ContainsKey(KnownName.NeedAppearances)).IsFalse();
    }

    /// <summary>A tagged document keeps its Form elements through a change.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedDocumentKeepsItsStructureThroughAChange()
    {
        using var document = FormDesignDocuments.OpenTagged();
        var created = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(TextName, 0));
        var key = FormDesignDocuments.Dictionary(document, created.FieldId).GetInt32(KnownName.StructParent, -1);

        _ = PdfFormFieldEditing.ChangeField(document, TextName, new() { Tooltip = GivenName, Value = "Ada" });

        await Assert.That(FormDesignDocuments.Dictionary(document, created.FieldId).GetInt32(KnownName.StructParent, -1)).IsEqualTo(key);
        await Assert.That(FormDesignDocuments.FindDanglingReferences(document)).IsEmpty();
        await Assert.That(await FormDesignDocuments.PassesQpdfAsync("edit-tagged", await FormDesignDocuments.SaveAsync(document))).IsTrue();
    }

    /// <summary>Opens a one page document whose XMP claims PDF/A-2b.</summary>
    /// <returns>The document.</returns>
    private static PdfDocument OpenClaimingPdfA()
    {
        var pdf = ConformancePdf.Page();
        pdf.Content = "0 g 0 0 1 1 re f 1 0 0 rg 0 0 1 1 re f";
        ConformancePdf.Claim(pdf, PdfA2, "B", null);
        return PdfDocumentReader.Open(pdf.ToBytes(), null);
    }
}
