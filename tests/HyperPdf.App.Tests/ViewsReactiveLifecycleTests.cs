// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;

namespace HyperPdf.App.Tests;

/// <summary>Checks that dialog bindings follow replacement view models only while activated.</summary>
public sealed class ViewsReactiveLifecycleTests
{
    /// <summary>The second confirmation's question.</summary>
    private const string SecondQuestion = "Second question";

    /// <summary>The second confirmation's message.</summary>
    private const string SecondMessage = "Second message";

    /// <summary>Checks initial and replacement confirmation view models, then that bindings stop after closing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConfirmationFollowsViewModelsWhileActivated()
    {
        var window = new ConfirmWindow { ViewModel = new(new("First question", "First message", "Continue")) };
        window.Show();
        try
        {
            await Assert.That(await UiWait.UntilAsync(() => window.IsLoaded && window.Title == "First question")).IsTrue();
            window.ViewModel = new(new(SecondQuestion, SecondMessage, "Delete"));
            var message = window.FindControl<TextBlock>("MessageText")!;
            var button = window.FindControl<Button>("ConfirmButton")!;
            await Assert.That(await UiWait.UntilAsync(() => window.Title == SecondQuestion && message.Text == SecondMessage)).IsTrue();
            await Assert.That(button.Content).IsEqualTo("Delete");
            window.Close();
            await Assert.That(await UiWait.UntilAsync(() => !window.IsLoaded)).IsTrue();
            window.ViewModel = new(new("Closed question", "Closed message", "Accept"));
            await Assert.That(window.Title).IsEqualTo(SecondQuestion);
            await Assert.That(message.Text).IsEqualTo(SecondMessage);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Checks that a replacement prompt view model updates text and multiline controls.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PromptFollowsReplacementViewModel()
    {
        var window = new PromptWindow { ViewModel = new(new TextPrompt("Note", "Write a note", "First", "Save", false)) };
        window.Show();
        try
        {
            var input = window.FindControl<TextBox>("InputBox")!;
            var button = window.FindControl<Button>("ConfirmButton")!;
            await Assert.That(await UiWait.UntilAsync(() => window.IsLoaded && input.Text == "First")).IsTrue();
            await Assert.That(button.IsDefault).IsTrue();
            window.ViewModel = new(new TextPrompt("Long note", "Write several lines", "Second", "Apply", true));
            await Assert.That(await UiWait.UntilAsync(() => window.Title == "Long note" && input.Text == "Second" && input.AcceptsReturn)).IsTrue();
            await Assert.That(button.IsDefault).IsFalse();
            await Assert.That(button.Content).IsEqualTo("Apply");
        }
        finally
        {
            window.Close();
        }
    }
}
