// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using HyperPdf.TestAssets;

namespace HyperPdf.App.Tests;

/// <summary>Checks filling a form whose fields carry scripts that only the sandboxed interpreter runs.</summary>
public sealed class FormProgramViewModelTests
{
    /// <summary>Half, to find the middle of a field.</summary>
    private const float Half = 0.5F;

    /// <summary>The quantity field.</summary>
    private const string QuantityField = "Quantity";

    /// <summary>The price field.</summary>
    private const string PriceField = "Price";

    /// <summary>The code field.</summary>
    private const string CodeField = "Code";

    /// <summary>The field a script hides.</summary>
    private const string SpareField = "Spare";

    /// <summary>Scripts format a value, calculate a subtotal from other fields, and format that result too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormatsAndCalculatesWithPrograms()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tab = await OpenAsync(test, main);

        Type(tab, PriceField, "12.5");
        Type(tab, QuantityField, "4");

        await Assert.That(Value(tab, QuantityField)).IsEqualTo("004");
        await Assert.That(Value(tab, "Subtotal")).IsEqualTo("$50.00");
    }

    /// <summary>A validate program that refuses a value shows its own message and keeps the editor open; a good value is accepted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesAValueWithTheProgramsMessage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tab = await OpenAsync(test, main);

        Type(tab, CodeField, "ab");
        var notice = tab.Notice;
        var stillEditing = tab.Forms.Editing?.Name;
        tab.Forms.Cancel();
        Type(tab, CodeField, "abc");

        await Assert.That(notice).IsEqualTo("The code must have three letters.");
        await Assert.That(stillEditing).IsEqualTo(CodeField);
        await Assert.That(Value(tab, CodeField)).IsEqualTo("abc");
    }

    /// <summary>A calculation program hides a field while the quantity is above five and shows it again afterwards.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HidesAndShowsAFieldFromAProgram()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tab = await OpenAsync(test, main);
        var visibility = DocumentFeature<IFormVisibility>.Get(tab.TryGetDocument())!;
        var before = tab.Forms.LastPageChange;

        Type(tab, QuantityField, "9");
        var hiddenAlready = !visibility.SetHidden(SpareField, true);
        var redrawn = tab.Forms.LastPageChange != before;
        Type(tab, QuantityField, "2");
        var shownAlready = !visibility.SetHidden(SpareField, false);

        await Assert.That(hiddenAlready).IsTrue();
        await Assert.That(redrawn).IsTrue();
        await Assert.That(shownAlready).IsTrue();
    }

    /// <summary>A calculation program makes a field read-only while the quantity is above eight: clicks no longer reach it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LocksAFieldFromAProgram()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tab = await OpenAsync(test, main);
        var code = Field(tab, CodeField);
        var centre = new PagePoint(code.Bounds.Left + (code.Bounds.Width * Half), code.Bounds.Top + (code.Bounds.Height * Half));

        var before = tab.Forms.HitTest(0, centre)?.Name;
        Type(tab, QuantityField, "9");
        var locked = tab.Forms.HitTest(0, centre);
        Type(tab, QuantityField, "2");
        var after = tab.Forms.HitTest(0, centre)?.Name;

        await Assert.That(before).IsEqualTo(CodeField);
        await Assert.That(locked).IsNull();
        await Assert.That(after).IsEqualTo(CodeField);
    }

    /// <summary>Opens the scripted form.</summary>
    /// <param name="test">The services.</param>
    /// <param name="main">The main view model.</param>
    /// <returns>The tab.</returns>
    private static async Task<DocumentTabViewModel> OpenAsync(TestServices test, MainViewModel main)
    {
        var path = Path.Combine(test.Directory, "scripted.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateScriptedForm());
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
        _ = tab.Forms.Activate(Field(tab, name));
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
        DocumentFeature<IFormFiller>.Cast(tab.TryGetDocument()!)!.GetFields(0, fields);
        return fields.Single(field => field.Name == name);
    }
}
