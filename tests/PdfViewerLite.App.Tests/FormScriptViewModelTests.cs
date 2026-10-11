// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks filling a form whose fields carry Recognised PDF scripts: totals recalculate, values are formatted, and bad values are refused with a message.</summary>
public sealed class FormScriptViewModelTests
{
    /// <summary>The price field.</summary>
    private const string PriceField = "Price";

    /// <summary>The quantity field.</summary>
    private const string QuantityField = "Quantity";

    /// <summary>The percentage field.</summary>
    private const string PercentField = "Percent";

    /// <summary>The date field.</summary>
    private const string DateField = "Date";

    /// <summary>Typing a price and quantity fills in the total and tax, formatted as currency.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CalculatesAndFormats()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tab = await OpenAsync(test, main);
        var forms = tab.Forms;

        Type(tab, PriceField, "12.5");
        Type(tab, QuantityField, "4");
        Type(tab, DateField, "2026-03-07");

        await Assert.That(Value(tab, PriceField)).IsEqualTo("12.50");
        await Assert.That(Value(tab, "Total")).IsEqualTo("$50.00");
        await Assert.That(Value(tab, "Tax")).IsEqualTo("$5.00");
        await Assert.That(Value(tab, DateField)).IsEqualTo("07/03/2026");
        await Assert.That(forms.Editing).IsNull();
    }

    /// <summary>A word in a number field and a percentage over 100 are refused, the editor stays open and a message explains why.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesBadValues()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tab = await OpenAsync(test, main);

        Type(tab, QuantityField, "lots");
        var quantityNotice = tab.Notice;
        var stillEditing = tab.Forms.Editing?.Name;
        tab.Forms.Cancel();
        Type(tab, PercentField, "150");
        var percentNotice = tab.Notice;

        await Assert.That(quantityNotice).IsEqualTo("Type a number.");
        await Assert.That(stillEditing).IsEqualTo(QuantityField);
        await Assert.That(percentNotice).Contains("between 0 and 100");
        await Assert.That(Value(tab, PercentField)).IsEqualTo(string.Empty);
    }

    /// <summary>Opens the calculated form.</summary>
    /// <param name="test">The services.</param>
    /// <param name="main">The main view model.</param>
    /// <returns>The tab.</returns>
    private static async Task<DocumentTabViewModel> OpenAsync(TestServices test, MainViewModel main)
    {
        var path = Path.Combine(test.Directory, "order.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateCalculatedForm());
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        return tab;
    }

    /// <summary>Types a value into a field and commits it, as clicking the field and pressing Enter does.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="name">The field's name.</param>
    /// <param name="text">The text.</param>
    private static void Type(DocumentTabViewModel tab, string name, string text)
    {
        var field = Field(tab, name);
        _ = tab.Forms.Activate(field);
        tab.Forms.EditText = text;
        tab.Forms.Commit();
    }

    /// <summary>Gets a field's current value.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="name">The field's name.</param>
    /// <returns>The value.</returns>
    private static string Value(DocumentTabViewModel tab, string name) => Field(tab, name).Value;

    /// <summary>Finds a field by name on the first page.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="name">The field's name.</param>
    /// <returns>The field.</returns>
    private static FormField Field(DocumentTabViewModel tab, string name)
    {
        var fields = new List<FormField>();
        ((IFormFiller)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(IFormFiller))!).GetFields(0, fields);
        return fields.Single(field => field.Name == name);
    }
}
