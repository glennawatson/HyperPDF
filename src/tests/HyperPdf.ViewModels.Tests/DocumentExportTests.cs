// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms;
using HyperPdf.Core.Text;
using HyperPdf.TestAssets;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Tests changing the document's details and saving its text and form answers to other files.</summary>
public sealed class DocumentExportTests
{
    /// <summary>The pages of the text sample.</summary>
    private const int Pages = 2;

    /// <summary>The lines on each page of the text sample.</summary>
    private const int Lines = 3;

    /// <summary>The title typed into the details window.</summary>
    private const string NewTitle = "Quarterly report";

    /// <summary>The text is saved as plain text under the name the tab suggests, ending in .txt.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesTheTextAsPlainText()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("words.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var tab = main.SelectedTab!;
        var suggested = string.Empty;
        using var save = tab.ExportInteraction.RegisterHandler(context =>
        {
            suggested = context.Input;
            context.SetOutput(Path.Combine(test.Directory, context.Input));
        });

        _ = await tab.ExportTextCommand.Execute(DocumentTextFormat.PlainText).ToTask();
        var written = await File.ReadAllTextAsync(Path.Combine(test.Directory, suggested));

        await Assert.That(suggested).IsEqualTo("words.txt");
        await Assert.That(written).Contains("quick brown fox");
    }

    /// <summary>The form's answers are saved as XFDF.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesTheFormAnswersAsXfdf()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("form.pdf", TestPdf.CreateForm())]);
        var tab = main.SelectedTab!;
        using var save = tab.ExportInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, context.Input)));

        _ = await tab.ExportFormDataCommand.Execute(FormDataFormat.Xfdf).ToTask();
        var written = await File.ReadAllTextAsync(Path.Combine(test.Directory, "form.xfdf"));

        await Assert.That(written).Contains("<xfdf");
    }

    /// <summary>The details window starts with the document's details, and keeping new ones changes the document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangesTheDocumentsDetails()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("details.pdf", Pages)]);
        var tab = main.SelectedTab!;
        using var window = tab.DocumentInfoInteraction.RegisterHandler(static context =>
        {
            context.Input.Title = NewTitle;
            context.SetOutput(true);
        });

        _ = await tab.EditDocumentInfoCommand.Execute().ToTask();

        await Assert.That(tab.TryGetDocument()!.GetMetadata().Title).IsEqualTo(NewTitle);
    }

    /// <summary>Closing the details window without keeping them changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosingTheDetailsWindowChangesNothing()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("unchanged.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var before = tab.TryGetDocument()!.GetMetadata().Title;
        using var window = tab.DocumentInfoInteraction.RegisterHandler(static context =>
        {
            context.Input.Title = NewTitle;
            context.SetOutput(false);
        });

        _ = await tab.EditDocumentInfoCommand.Execute().ToTask();

        await Assert.That(tab.TryGetDocument()!.GetMetadata().Title).IsEqualTo(before);
    }
}
