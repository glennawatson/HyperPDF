// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Tests adding a list box, a pair of radio buttons and a box to sign.</summary>
public sealed class FormFieldKindsTests
{
    /// <summary>The pages of the document.</summary>
    private const int Pages = 1;

    /// <summary>The buttons of a new radio pair.</summary>
    private const int RadioButtons = 2;

    /// <summary>The choices of a new list box.</summary>
    private const int ListChoices = 3;

    /// <summary>Each command adds one field of its kind; the radio pair has two buttons and the list box three choices.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddsAListBoxARadioPairAndABoxToSign()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("fields.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var design = tab.FormDesign;

        _ = await design.AddListBoxCommand.Execute().ToTask();
        _ = await design.AddRadioButtonsCommand.Execute().ToTask();
        _ = await design.AddSignatureFieldCommand.Execute().ToTask();
        var fields = await DocumentFeature<IFormDesigner>.Get(tab.TryGetDocument())!.GetFields().ToTask();
        var list = Array.Find(fields, static field => field.Kind == FormFieldKind.ListBox);
        var radio = Array.Find(fields, static field => field.Kind == FormFieldKind.RadioButton);
        var sign = Array.Find(fields, static field => field.Kind == FormFieldKind.Signature);

        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Options.Length).IsEqualTo(ListChoices);
        await Assert.That(radio).IsNotNull();
        await Assert.That(radio!.Widgets.Length).IsEqualTo(RadioButtons);
        await Assert.That(sign).IsNotNull();
    }
}
