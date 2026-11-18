// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks that preparing a form from the tab adds fields to the document.</summary>
public sealed class FormDesignViewModelTests
{
    /// <summary>The number of fields the test adds.</summary>
    private const int AddedFields = 4;

    /// <summary>Adds a text field, a check box, a drop-down list and a button, which the form then lists with their own names.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddsFieldsToTheForm()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("form-design.pdf", TestPdf.CreateForm())]);
        var tab = main.SelectedTab!;
        var designer = DocumentFeature<IFormDesigner>.Get(tab.TryGetDocument())!;
        var before = (await designer.GetFields().ToTask()).Length;

        _ = await tab.FormDesign.AddTextFieldCommand.Execute().ToTask();
        _ = await tab.FormDesign.AddCheckBoxCommand.Execute().ToTask();
        _ = await tab.FormDesign.AddDropDownCommand.Execute().ToTask();
        _ = await tab.FormDesign.AddButtonCommand.Execute().ToTask();
        var fields = await designer.GetFields().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(fields.Length).IsEqualTo(before + AddedFields);
            await Assert.That(Array.Exists(fields, static f => f.Kind == FormFieldKind.CheckBox)).IsTrue();
            await Assert.That(Array.Exists(fields, static f => f.Kind == FormFieldKind.ComboBox)).IsTrue();
            await Assert.That(Array.Exists(fields, static f => f.Kind == FormFieldKind.PushButton)).IsTrue();
            await Assert.That(fields.Select(static f => f.Name).Distinct().Count()).IsEqualTo(fields.Length);
        }
    }
}
