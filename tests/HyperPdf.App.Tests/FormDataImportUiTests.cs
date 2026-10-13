// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.App.Tests;

/// <summary>
/// Checks a form's Import Data action in the app: the reader is asked, naming the file the form wants, and then picks the
/// file; the form never chooses the file; page actions that run by themselves are never allowed to ask; and a declined or
/// invalid import changes nothing.
/// </summary>
public sealed class FormDataImportUiTests
{
    /// <summary>The length of a file name far longer than the app shows.</summary>
    private const int LongName = 500;

    /// <summary>The most characters of a file name the app shows.</summary>
    private const int ShownLength = 80;

    /// <summary>The file the reader chooses, whatever its name.</summary>
    private const string ChosenFile = "my-answers.xfdf";

    /// <summary>The text of an XFDF file that sets "Name" and "Note" and names one field the form has not.</summary>
    private const string Answers = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xfdf xmlns="http://ns.adobe.com/xfdf/"><fields>
          <field name="Name"><value>new</value></field>
          <field name="Note"><value>a note</value></field>
          <field name="Missing"><value>x</value></field>
        </fields></xfdf>
        """;

    /// <summary>The reader agrees and picks a file: the answers load, the form is redrawn and a notice says how many.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReaderAgreesAndChoosesTheFile()
    {
        using var test = new TestServices(new HyperPdfEngine());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("import.pdf", FormImportSamples.Create())]);
        var tab = main.SelectedTab!;
        var chosen = Path.Combine(test.Directory, ChosenFile);
        await File.WriteAllTextAsync(chosen, Answers);
        string? confirmed = null;
        string? offered = null;
        using var confirm = tab.Forms.ConfirmImportInteraction.RegisterHandler(context =>
        {
            confirmed = context.Input;
            context.SetOutput(true);
        });
        using var choose = tab.Forms.ChooseImportFileInteraction.RegisterHandler(context =>
        {
            offered = context.Input;
            context.SetOutput(chosen);
        });

        var result = await tab.Forms.RunButtonAsync(Field(tab, "Load"));

        await Assert.That(result).IsEqualTo(FormActionResult.Ran | FormActionResult.Changed);
        await Assert.That(confirmed).IsEqualTo(FormImportSamples.NamedFile);
        await Assert.That(offered).IsEqualTo(FormImportSamples.NamedFile);
        await Assert.That(Field(tab, "Name").Value).IsEqualTo("new");
        await Assert.That(Field(tab, "Note").Value).IsEqualTo("a note");
        await Assert.That(tab.Notice).Contains("Imported 2 entries").And.Contains(ChosenFile).And.Contains("1 entry was ignored");
        await Assert.That(PdfDocumentEditing.GetHistory(((HyperPdfDocument)tab.TryGetDocument()!).Document).UndoLabel).IsEqualTo("Import form data");
    }

    /// <summary>A reader who says no, or who cancels the file chooser, loads nothing and nothing is read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecliningOrCancellingLoadsNothing()
    {
        using var test = new TestServices(new HyperPdfEngine());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("decline.pdf", FormImportSamples.Create())]);
        var tab = main.SelectedTab!;
        var agree = false;
        var chooserAsked = false;
        using var confirm = tab.Forms.ConfirmImportInteraction.RegisterHandler(context => context.SetOutput(agree));
        using var choose = tab.Forms.ChooseImportFileInteraction.RegisterHandler(context =>
        {
            chooserAsked = true;
            context.SetOutput(null);
        });

        var declined = await tab.Forms.RunButtonAsync(Field(tab, "Load"));
        var chooserAfterDecline = chooserAsked;
        agree = true;
        var cancelled = await tab.Forms.RunButtonAsync(Field(tab, "Load"));

        await Assert.That(declined).IsEqualTo(FormActionResult.Declined);
        await Assert.That(chooserAfterDecline).IsFalse();
        await Assert.That(cancelled).IsEqualTo(FormActionResult.Declined);
        await Assert.That(chooserAsked).IsTrue();
        await Assert.That(Field(tab, "Name").Value).IsEqualTo(FormImportSamples.OriginalName);
    }

    /// <summary>A file that is not FDF or XFDF changes nothing and the notice says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidFileIsRefusedAndExplained()
    {
        using var test = new TestServices(new HyperPdfEngine());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("invalid.pdf", FormImportSamples.Create())]);
        var tab = main.SelectedTab!;
        var chosen = Path.Combine(test.Directory, "notes.txt");
        await File.WriteAllTextAsync(chosen, "just some notes");
        using var confirm = tab.Forms.ConfirmImportInteraction.RegisterHandler(static context => context.SetOutput(true));
        using var choose = tab.Forms.ChooseImportFileInteraction.RegisterHandler(context => context.SetOutput(chosen));

        var result = await tab.Forms.RunButtonAsync(Field(tab, "Load"));

        await Assert.That(result).IsEqualTo(FormActionResult.Declined);
        await Assert.That(tab.Notice).Contains("is not a valid FDF or XFDF file");
        await Assert.That(Field(tab, "Name").Value).IsEqualTo(FormImportSamples.OriginalName);
    }

    /// <summary>An action that runs by itself, such as a page's open action, may not put a question to the reader, so it loads nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ActionsThatRunByThemselvesNeverAsk()
    {
        using var test = new TestServices(new HyperPdfEngine());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("quiet.pdf", FormImportSamples.Create())]);
        var tab = main.SelectedTab!;
        var asked = false;
        using var confirm = tab.Forms.ConfirmImportInteraction.RegisterHandler(context =>
        {
            asked = true;
            context.SetOutput(true);
        });

        var imported = await new TabFormActionHost(tab, false).ImportAsync(FormImportSamples.NamedFile, new RefusingImporter(), CancellationToken.None);

        await Assert.That(imported).IsFalse();
        await Assert.That(asked).IsFalse();
    }

    /// <summary>The name shown to the reader is the last part of the path the form gave, without control characters, and not too long.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FileNamesShownToTheReaderAreCleaned()
    {
        await Assert.That(FormDataFiles.DisplayName("C:\\Users\\x\\answers.fdf")).IsEqualTo("answers.fdf");
        await Assert.That(FormDataFiles.DisplayName("/etc/passwd\r\nDelete everything?")).IsEqualTo("passwd  Delete everything?");
        await Assert.That(FormDataFiles.DisplayName(new('a', LongName)).Length).IsEqualTo(ShownLength);
        await Assert.That(FormDataFiles.DisplayName(null)).IsEqualTo("a data file");
        await Assert.That(FormDataFiles.DisplayName("   ")).IsEqualTo("a data file");
    }

    /// <summary>A file longer than the limit is not read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OversizedFilesAreNotRead()
    {
        using var test = new TestServices(new HyperPdfEngine());
        var big = Path.Combine(test.Directory, "big.xfdf");
        await using (var stream = File.Create(big))
        {
            stream.SetLength(FormDataFiles.MaxBytes + 1);
        }

        await Assert.That(await FormDataFiles.ReadAsync(big, CancellationToken.None)).IsNull();
        await Assert.That(await FormDataFiles.ReadAsync(Path.Combine(test.Directory, "missing.xfdf"), CancellationToken.None)).IsNull();
    }

    /// <summary>Finds a field of the form on the first page.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The field.</returns>
    private static FormField Field(DocumentTabViewModel tab, string name)
    {
        var fields = new List<FormField>();
        DocumentFeature<IFormFiller>.Cast(tab.TryGetDocument()!)!.GetFields(0, fields);
        return fields.Single(field => field.Name == name);
    }

    /// <summary>An importer that must not be reached.</summary>
    private sealed class RefusingImporter : IFormDataImporter
    {
        /// <inheritdoc/>
        public FormDataImportResult Import(ReadOnlyMemory<byte> data) => throw new InvalidOperationException("The importer must not be reached.");
    }
}
