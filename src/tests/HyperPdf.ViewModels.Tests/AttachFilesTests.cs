// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Tests attaching a file to the document and taking it out again.</summary>
public sealed class AttachFilesTests
{
    /// <summary>The pages of the document.</summary>
    private const int Pages = 2;

    /// <summary>The name of the file attached.</summary>
    private const string NoteFile = "notes.txt";

    /// <summary>A chosen file is attached and listed; taking it out asks first, and a yes removes it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AttachesAFileAndTakesItOutAfterAsking()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("attach.pdf", Pages)]);
        var attachments = main.SelectedTab!.Attachments;
        var file = Path.Combine(test.Directory, NoteFile);
        await File.WriteAllTextAsync(file, "Remember the milk.");
        var asked = false;
        using var choose = attachments.AttachInteraction.RegisterHandler(context => context.SetOutput([file]));
        using var confirm = attachments.RemoveInteraction.RegisterHandler(context =>
        {
            asked = true;
            context.SetOutput(true);
        });

        _ = await attachments.AttachFilesCommand.Execute().ToTask();
        var attached = attachments.Items.Count;
        var name = attached == 1 ? attachments.Items[0].Name : string.Empty;
        attachments.Selected = attachments.Items[0];
        _ = await attachments.RemoveSelectedCommand.Execute().ToTask();

        await Assert.That(attached).IsEqualTo(1);
        await Assert.That(name).IsEqualTo(NoteFile);
        await Assert.That(asked).IsTrue();
        await Assert.That(attachments.Items.Count).IsEqualTo(0);
    }

    /// <summary>Answering no keeps the file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnsweringNoKeepsTheFile()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("keep.pdf", Pages)]);
        var attachments = main.SelectedTab!.Attachments;
        var file = Path.Combine(test.Directory, NoteFile);
        await File.WriteAllTextAsync(file, "Keep me.");
        using var choose = attachments.AttachInteraction.RegisterHandler(context => context.SetOutput([file]));
        using var confirm = attachments.RemoveInteraction.RegisterHandler(static context => context.SetOutput(false));

        _ = await attachments.AttachFilesCommand.Execute().ToTask();
        attachments.Selected = attachments.Items[0];
        _ = await attachments.RemoveSelectedCommand.Execute().ToTask();

        await Assert.That(attachments.Items.Count).IsEqualTo(1);
    }
}
