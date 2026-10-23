// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using HyperPdf.App.Controls;

namespace HyperPdf.App.Tests;

/// <summary>Checks the lifetime and routing of document input.</summary>
public sealed class PageCanvasTests
{
    /// <summary>Checks that input is owned by attachment and remains active after reattachment.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HandlesKeyboardOnlyWhileAttached()
    {
        var canvas = new PageCanvas();
        var window = new Window { Content = canvas };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            var attached = CopyKey();
            canvas.RaiseEvent(attached);
            await Assert.That(attached.Handled).IsTrue();

            var unhandled = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F1 };
            canvas.RaiseEvent(unhandled);
            await Assert.That(unhandled.Handled).IsFalse();

            window.Content = null;
            var detached = CopyKey();
            canvas.RaiseEvent(detached);
            await Assert.That(detached.Handled).IsFalse();

            window.Content = canvas;
            Dispatcher.UIThread.RunJobs();
            var reattached = CopyKey();
            canvas.RaiseEvent(reattached);
            await Assert.That(reattached.Handled).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Creates the copy action's routed input.</summary>
    /// <returns>The input.</returns>
    private static KeyEventArgs CopyKey() => new() { RoutedEvent = InputElement.KeyDownEvent, Key = Key.C, KeyModifiers = KeyModifiers.Control };
}
