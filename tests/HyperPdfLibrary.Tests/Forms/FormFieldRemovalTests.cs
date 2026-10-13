// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests removing form fields: widgets, the form's arrays, groups, the structure tree, actions and the saved files.</summary>
public sealed class FormFieldRemovalTests
{
    /// <summary>The pages in the test document.</summary>
    private const int Pages = 3;

    /// <summary>The second page's index.</summary>
    private const int SecondPage = 1;

    /// <summary>The third row.</summary>
    private const int ThirdRow = 2;

    /// <summary>The widgets of the two fields in the Address group.</summary>
    private const int GroupWidgets = 2;

    /// <summary>The first field's name.</summary>
    private const string First = "First";

    /// <summary>The second field's name.</summary>
    private const string Second = "Second";

    /// <summary>The first field of the Address group.</summary>
    private const string City = "Address.City";

    /// <summary>The second field of the Address group.</summary>
    private const string Zip = "Address.Zip";

    /// <summary>The name of the group.</summary>
    private const string Address = "Address";

    /// <summary>Removing a field takes its widget off the page and out of the form, and leaves no dangling reference in either save.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovingAFieldLeavesNoDanglingReference()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateFields(document, [FormDesignDocuments.Text(First, 0), FormDesignDocuments.Text(Second, 1) with { Value = "keep" }]);

        var result = PdfFormFieldRemoval.RemoveField(document, First);

        await Assert.That(result.Removed).IsEquivalentTo([First]);
        await Assert.That(result.Skipped).IsEmpty();
        await Assert.That(result.WidgetCount).IsEqualTo(1);
        var widgets = FormDesignDocuments.Widgets(document, 0);
        await Assert.That(widgets.Count).IsEqualTo(1);
        await Assert.That(widgets[0].Name).IsEqualTo(Second);
        await Assert.That(document.Catalog.GetDictionary(KnownName.AcroForm)!.GetArray(KnownName.Fields)!.Count).IsEqualTo(1);
        await Assert.That(FormDesignDocuments.FindDanglingReferences(document)).IsEmpty();
        await AssertSavesCleanAsync(document, "remove-one", 1);
    }

    /// <summary>Removing a radio group takes the widgets on every page with it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovingARadioGroupRemovesWidgetsOnEveryPage()
    {
        using var document = FormDesignDocuments.OpenPlain(Pages);
        _ = PdfFormFieldCreation.CreateField(
            document,
            new(PdfFieldType.RadioButton, "Colour", [FormDesignDocuments.Place(0, 0), FormDesignDocuments.Place(SecondPage, 0), FormDesignDocuments.Place(Pages - 1, 0)]));
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(Second, 1));

        var result = PdfFormFieldRemoval.RemoveField(document, "Colour");

        await Assert.That(result.WidgetCount).IsEqualTo(Pages);
        await Assert.That(FormDesignDocuments.Widgets(document, 0).Count).IsEqualTo(1);
        await Assert.That(FormDesignDocuments.Widgets(document, SecondPage)).IsEmpty();
        await Assert.That(FormDesignDocuments.Widgets(document, Pages - 1)).IsEmpty();
        await Assert.That(FormDesignDocuments.FindDanglingReferences(document)).IsEmpty();
        await AssertSavesCleanAsync(document, "remove-radio", 1);
    }

    /// <summary>A group that loses its last field is removed with it, and a group with a field left stays.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyGroupsGoWithTheirLastField()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateFields(document, [FormDesignDocuments.Text(City, 0), FormDesignDocuments.Text(Zip, 1)]);

        _ = PdfFormFieldRemoval.RemoveField(document, City);
        await Assert.That(PdfFormFieldEditing.GetField(document, Zip)).IsNotNull();
        await Assert.That(PdfFormFieldEditing.GetField(document, City)).IsNull();
        await Assert.That(document.Catalog.GetDictionary(KnownName.AcroForm)!.GetArray(KnownName.Fields)!.Count).IsEqualTo(1);

        _ = PdfFormFieldRemoval.RemoveField(document, Zip);
        await Assert.That(document.Catalog.GetDictionary(KnownName.AcroForm)!.GetArray(KnownName.Fields)!.Count).IsEqualTo(0);
        await Assert.That(FormDesignDocuments.Widgets(document, 0)).IsEmpty();
        await Assert.That(FormDesignDocuments.FindDanglingReferences(document)).IsEmpty();
        await AssertSavesCleanAsync(document, "remove-group", 0);
    }

    /// <summary>Removing a group by name removes every field below it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovingAGroupRemovesItsFields()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateFields(document, [FormDesignDocuments.Text(City, 0), FormDesignDocuments.Text(Zip, 1), FormDesignDocuments.Text(Second, ThirdRow)]);

        var result = PdfFormFieldRemoval.RemoveFields(document, [Address, City]);

        await Assert.That(result.Removed).IsEquivalentTo([Address]);
        await Assert.That(result.WidgetCount).IsEqualTo(GroupWidgets);
        await Assert.That(FormDesignDocuments.Widgets(document, 0).Count).IsEqualTo(1);
        await Assert.That(FormDesignDocuments.FindDanglingReferences(document)).IsEmpty();
    }

    /// <summary>A name that is no field is skipped with a reason and leaves no undo step.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownNameIsSkippedWithAReason()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(First, 0));
        var before = PdfDocumentEditing.GetHistory(document).UndoCount;

        var result = PdfFormFieldRemoval.RemoveField(document, "Missing");

        await Assert.That(result.Removed).IsEmpty();
        await Assert.That(result.Skipped.Length).IsEqualTo(1);
        await Assert.That(result.Skipped[0]).Contains("Missing");
        await Assert.That(FormDesignDocuments.Widgets(document, 0).Count).IsEqualTo(1);
        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoCount).IsEqualTo(before);
    }

    /// <summary>Removal is one undo step and redo removes the field again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovalUndoesAndRedoes()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(First, 0) with { Value = "Ada" });
        _ = PdfFormFieldRemoval.RemoveField(document, First);
        await Assert.That(FormDesignDocuments.Widgets(document, 0)).IsEmpty();

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        var restored = FormDesignDocuments.Widgets(document, 0);
        await Assert.That(restored.Count).IsEqualTo(1);
        await Assert.That(restored[0].Value).IsEqualTo("Ada");

        await Assert.That(PdfDocumentEditing.Redo(document)).IsTrue();
        await Assert.That(FormDesignDocuments.Widgets(document, 0)).IsEmpty();
        await Assert.That(FormDesignDocuments.FindDanglingReferences(document)).IsEmpty();
    }

    /// <summary>A calculated field leaves the calculation order when it is removed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CalculatedFieldLeavesTheCalculationOrder()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        _ = PdfFormFieldCreation.CreateFields(
            document,
            [FormDesignDocuments.Text(First, 0) with { Actions = new() { Calculate = "event.value = 1;" } }, FormDesignDocuments.Text(Second, 1)]);
        await Assert.That(document.Catalog.GetDictionary(KnownName.AcroForm)!.GetArray(KnownName.CO)!.Count).IsEqualTo(1);

        _ = PdfFormFieldRemoval.RemoveField(document, First);

        await Assert.That(document.Catalog.GetDictionary(KnownName.AcroForm)!.GetArray(KnownName.CO)?.Count ?? 0).IsEqualTo(0);
        await Assert.That(FormDesignDocuments.FindDanglingReferences(document)).IsEmpty();
    }

    /// <summary>A tagged document loses the Form elements and parent tree entries of a removed field, and stays valid.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedRemovalDropsStructureElements()
    {
        using var document = FormDesignDocuments.OpenTagged();
        var removed = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(First, 0));
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(Second, 1));
        var key = FormDesignDocuments.Dictionary(document, removed.FieldId).GetInt32(KnownName.StructParent, -1);
        var root = document.Catalog.GetDictionary(KnownName.StructTreeRoot)!;
        await Assert.That(NameTree.FindNumber(root.GetDictionary(KnownName.ParentTree), key).IsNull).IsFalse();

        _ = PdfFormFieldRemoval.RemoveField(document, First);

        await Assert.That(NameTree.FindNumber(document.Catalog.GetDictionary(KnownName.StructTreeRoot)!.GetDictionary(KnownName.ParentTree), key).IsNull).IsTrue();
        await Assert.That(FormDesignDocuments.FindDanglingReferences(document)).IsEmpty();
        var report = PdfDocumentAccessibility.GetAccessibilityReport(document);
        await Assert.That(report.GetCount(PdfAccessibilityCode.FormFieldNotTagged)).IsEqualTo(0);
        await AssertSavesCleanAsync(document, "remove-tagged", 1);
    }

    /// <summary>Removing a thousand fields in one call is one undo step and leaves no dangling reference.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ManyFieldsAreRemovedInOneStep()
    {
        const int count = 200;
        const int keep = 10;
        using var document = FormDesignDocuments.OpenPlain(1);
        var definitions = new PdfFieldDefinition[count];
        var names = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            definitions[i] = FormDesignDocuments.Text($"Row{i}", i % keep);
            names.Add($"Row{i}");
        }

        _ = PdfFormFieldCreation.CreateFields(document, definitions);
        var undoBefore = PdfDocumentEditing.GetHistory(document).UndoCount;

        var result = await PdfFormFieldRemoval.RemoveFieldsAsync(document, names.GetRange(keep, count - keep), CancellationToken.None);

        await Assert.That(result.Removed.Length).IsEqualTo(count - keep);
        await Assert.That(FormDesignDocuments.Widgets(document, 0).Count).IsEqualTo(keep);
        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoCount).IsEqualTo(undoBefore + 1);
        await Assert.That(FormDesignDocuments.FindDanglingReferences(document)).IsEmpty();
    }

    /// <summary>Saves a document both ways, reopens each file and checks the widget count and qpdf.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">A name for the files.</param>
    /// <param name="widgets">The widgets expected on the first page.</param>
    /// <returns>A task.</returns>
    private static async Task AssertSavesCleanAsync(PdfDocument document, string name, int widgets)
    {
        var incremental = await FormDesignDocuments.SaveAsync(document);
        var compact = FormDesignDocuments.SaveCompact(document);
        foreach (var file in (byte[][])[incremental, compact])
        {
            using var reopened = PdfDocumentReader.Open(file, null);
            await Assert.That(FormDesignDocuments.Widgets(reopened, 0).Count).IsEqualTo(widgets);
            await Assert.That(FormDesignDocuments.FindDanglingReferences(reopened)).IsEmpty();
        }

        await Assert.That(await FormDesignDocuments.PassesQpdfAsync($"{name}-incremental", incremental)).IsTrue();
        await Assert.That(await FormDesignDocuments.PassesQpdfAsync($"{name}-compact", compact)).IsTrue();
    }
}
