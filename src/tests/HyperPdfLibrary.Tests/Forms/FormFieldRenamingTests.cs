// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests renaming form fields: partial and fully qualified names, clashes, re-nesting, tooltips and actions that name fields.</summary>
public sealed class FormFieldRenamingTests
{
    /// <summary>The first field's name.</summary>
    private const string First = "First";

    /// <summary>The second field's name.</summary>
    private const string Second = "Second";

    /// <summary>The name the first field is renamed to.</summary>
    private const string Renamed = "Renamed";

    /// <summary>The object number of the push button in the hide action document.</summary>
    private const int ButtonObject = 5;

    /// <summary>The root fields left after one of two moves into a group and out again.</summary>
    private const int TwoRoots = 2;

    /// <summary>The City field after its group is renamed.</summary>
    private const string HomeCity = "Home.City";

    /// <summary>The City field renamed to Town.</summary>
    private const string RenamedCity = "Address.Town";

    /// <summary>A field moved under the Address group.</summary>
    private const string Street = "Address.Street";

    /// <summary>The first field of the Address group.</summary>
    private const string City = "Address.City";

    /// <summary>The second field of the Address group.</summary>
    private const string Zip = "Address.Zip";

    /// <summary>A partial rename changes only the last part, keeps the value, and survives both saves.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PartialRenameKeepsTheParentsAndTheValue()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(City, 0) with { Value = "Hobart" });

        var result = PdfFormFieldRenaming.RenameFieldPartial(document, City, "Town");

        await Assert.That(result.Renamed).IsTrue();
        await Assert.That(result.NewName).IsEqualTo(RenamedCity);
        var widgets = FormDesignDocuments.Widgets(document, 0);
        await Assert.That(widgets[0].Name).IsEqualTo(RenamedCity);
        await Assert.That(widgets[0].Value).IsEqualTo("Hobart");
        await Assert.That(PdfFormFieldEditing.GetField(document, City)).IsNull();
        foreach (var file in (byte[][])[await FormDesignDocuments.SaveAsync(document), FormDesignDocuments.SaveCompact(document)])
        {
            using var reopened = PdfDocumentReader.Open(file, null);
            await Assert.That(FormDesignDocuments.Widgets(reopened, 0)[0].Name).IsEqualTo(RenamedCity);
            await Assert.That(await FormDesignDocuments.PassesQpdfAsync("rename-partial", file)).IsTrue();
        }
    }

    /// <summary>A name that is already used is refused with a reason and the document is not touched.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClashingNameIsRefused()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateFields(document, [FormDesignDocuments.Text(First, 0), FormDesignDocuments.Text(Second, 1)]);
        var undo = PdfDocumentEditing.GetHistory(document).UndoCount;

        var result = PdfFormFieldRenaming.RenameField(document, First, Second);

        await Assert.That(result.Renamed).IsFalse();
        await Assert.That(result.Reason).Contains("already exists");
        await Assert.That(result.NewName).IsEqualTo(Second);
        await Assert.That(FormDesignDocuments.Widgets(document, 0)[0].Name).IsEqualTo(First);
        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoCount).IsEqualTo(undo);
    }

    /// <summary>A name with a period moves the field under a group, which is made when it is missing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NameWithAPeriodReNestsTheField()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateFields(document, [FormDesignDocuments.Text(First, 0) with { Value = "x" }, FormDesignDocuments.Text(City, 1)]);

        var result = PdfFormFieldRenaming.RenameField(document, First, Street);

        await Assert.That(result.Renamed).IsTrue();
        var field = PdfFormFieldEditing.GetField(document, Street);
        await Assert.That(field).IsNotNull();
        await Assert.That(field!.Value).IsEqualTo("x");
        await Assert.That(document.Catalog.GetDictionary(KnownName.AcroForm)!.GetArray(KnownName.Fields)!.Count).IsEqualTo(1);
        await Assert.That(FormDesignDocuments.FindDanglingReferences(document)).IsEmpty();

        var moved = PdfFormFieldRenaming.RenameField(document, Street, "Top");
        await Assert.That(moved.Renamed).IsTrue();
        await Assert.That(document.Catalog.GetDictionary(KnownName.AcroForm)!.GetArray(KnownName.Fields)!.Count).IsEqualTo(TwoRoots);
        await Assert.That(await FormDesignDocuments.PassesQpdfAsync("rename-nest", FormDesignDocuments.SaveCompact(document))).IsTrue();
    }

    /// <summary>A field cannot be moved under a field that holds a value, into itself, or to a name that is not valid.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImpossibleMovesAreRefusedWithAReason()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateFields(document, [FormDesignDocuments.Text(First, 0), FormDesignDocuments.Text(City, 1)]);

        var underValue = PdfFormFieldRenaming.RenameField(document, City, "First.Child");
        var intoItself = PdfFormFieldRenaming.RenameField(document, "Address", "Address.Inner");
        var badName = PdfFormFieldRenaming.RenameField(document, First, "A..B");
        var partialWithPeriod = PdfFormFieldRenaming.RenameFieldPartial(document, First, "A.B");
        var missing = PdfFormFieldRenaming.RenameField(document, "Nothing", "Other");

        await Assert.That(underValue.Reason).Contains("holds a value");
        await Assert.That(intoItself.Reason).Contains("inside itself");
        await Assert.That(badName.Reason).IsNotNull();
        await Assert.That(partialWithPeriod.Reason).Contains("period");
        await Assert.That(missing.Reason).Contains("No field is named");
        await Assert.That(new[] { underValue, intoItself, badName, partialWithPeriod, missing }.Count(static r => r.Renamed)).IsEqualTo(0);
    }

    /// <summary>Renaming a group changes the names of every field below it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RenamingAGroupRenamesItsFields()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateFields(document, [FormDesignDocuments.Text(City, 0), FormDesignDocuments.Text(Zip, 1)]);

        var result = PdfFormFieldRenaming.RenameField(document, "Address", "Home");

        await Assert.That(result.Renamed).IsTrue();
        await Assert.That(PdfFormFieldEditing.GetField(document, HomeCity)).IsNotNull();
        await Assert.That(PdfFormFieldEditing.GetField(document, "Home.Zip")).IsNotNull();
        await Assert.That(PdfFormFieldEditing.GetField(document, City)).IsNull();
        await Assert.That(PdfFormFieldEditing.GetField(document, HomeCity)!.Tooltip).IsEqualTo(HomeCity);
    }

    /// <summary>The tooltip that was the field name follows the rename, and a tooltip the user wrote stays.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultTooltipFollowsTheNewName()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateFields(document, [FormDesignDocuments.Text(First, 0), FormDesignDocuments.Text(Second, 1) with { Tooltip = "Your surname" }]);

        _ = PdfFormFieldRenaming.RenameField(document, First, Renamed);
        _ = PdfFormFieldRenaming.RenameField(document, Second, "Surname");

        await Assert.That(PdfFormFieldEditing.GetField(document, Renamed)!.Tooltip).IsEqualTo(Renamed);
        await Assert.That(PdfFormFieldEditing.GetField(document, "Surname")!.Tooltip).IsEqualTo("Your surname");
    }

    /// <summary>A rename is one undo step.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RenameUndoesAndRedoes()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(First, 0));
        _ = PdfFormFieldRenaming.RenameField(document, First, Renamed);

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(FormDesignDocuments.Widgets(document, 0)[0].Name).IsEqualTo(First);
        await Assert.That(PdfDocumentEditing.Redo(document)).IsTrue();
        await Assert.That(FormDesignDocuments.Widgets(document, 0)[0].Name).IsEqualTo(Renamed);
    }

    /// <summary>A hide action that names the field follows the rename, and one that names a removed field loses it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HideActionFollowsTheRename()
    {
        using var document = PdfDocumentReader.Open(CreateWithHideAction(), null);

        var result = PdfFormFieldRenaming.RenameField(document, "Name", Renamed);

        await Assert.That(result.Renamed).IsTrue();
        await Assert.That(result.UpdatedReferences).IsEqualTo(1);
        var action = StoreReading.GetDictionary(document.Objects, new(ButtonObject, 0))!.GetDictionary(KnownName.A)!;
        await Assert.That(action.GetText(KnownName.T)).IsEqualTo(Renamed);
    }

    /// <summary>Creates a document with a text field and a push button whose action hides the text field by name.</summary>
    /// <returns>The file bytes.</returns>
    private static byte[] CreateWithHideAction() =>
        MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R] >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 5 0 R] >>",
            "<< /FT /Tx /T (Name) /Type /Annot /Subtype /Widget /Rect [10 10 100 30] /P 3 0 R >>",
            "<< /FT /Btn /Ff 65536 /T (Hide) /Type /Annot /Subtype /Widget /Rect [10 50 100 70] /P 3 0 R /A << /S /Hide /T (Name) /H true >> >>");
}
