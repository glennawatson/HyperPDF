// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks replying to comments and setting their review status from the app: the sidebar shows the thread and status, and saving keeps them.</summary>
public sealed class ReplyViewModelTests
{
    /// <summary>The reply written.</summary>
    private const string ReplyText = "Checked, it is right.";

    /// <summary>Where the comment goes.</summary>
    private static readonly PagePoint NoteAt = new(120, 120);

    /// <summary>A reply and a status show under the comment in the sidebar and survive saving and reopening.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepliesAndSetsStatus()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var path = test.CreateDocument("review.pdf", 1);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        var annotations = tab.Annotations;
        tab.SidebarMode = SidebarMode.Annotations;
        var answers = new Queue<string>(["Please check the total.", ReplyText]);
        using var prompt = annotations.PromptInteraction.RegisterHandler(context => context.SetOutput(answers.Dequeue()));

        await annotations.AddNoteAsync(0, NoteAt);
        await annotations.ReplyAsync(annotations.Items[0].Annotation);
        var accepted = annotations.SetStatus(annotations.Items[0].Annotation, ReviewState.Accepted);
        var item = annotations.Items.Single();
        var saved = tab.Save(path);
        main.CloseTabWithoutAsking(tab);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var reopened = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => reopened.IsLoaded)).IsTrue();
        reopened.SidebarMode = SidebarMode.Annotations;
        var after = reopened.Annotations.Items.Single();

        await Assert.That(accepted).IsTrue();
        await Assert.That(item.Status).IsEqualTo(ReviewState.Accepted);
        await Assert.That(item.Heading).EndsWith("· Accepted");
        await Assert.That(item.RepliesText).Contains(ReplyText);
        await Assert.That(saved).IsTrue();
        await Assert.That(after.Status).IsEqualTo(ReviewState.Accepted);
        await Assert.That(after.RepliesText).Contains(ReplyText);
    }
}
