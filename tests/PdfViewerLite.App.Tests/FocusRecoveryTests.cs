// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks focus is never left nowhere, and that focus changes a screen reader missed are repeated.</summary>
public sealed class FocusRecoveryTests
{
    /// <summary>The pages in the test document.</summary>
    private const int Pages = 2;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>The text in the repaired text box.</summary>
    private const string Text = "find these words";

    /// <summary>The start of the selection kept across a repeat.</summary>
    private const int SelectionStart = 5;

    /// <summary>The end of the selection kept across a repeat.</summary>
    private const int SelectionEnd = 10;

    /// <summary>Half of a field's size, to find its centre.</summary>
    private const float Half = 0.5F;

    /// <summary>The focus changes a repeat makes: the first arrival and its repeat.</summary>
    private const int ArrivalAndRepeat = 2;

    /// <summary>How long a check would take to come back, were one started.</summary>
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(100);

    /// <summary>Turning Focus Mode on hides the pages that held focus; focus moves into Focus Mode, and back when it closes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FocusModeKeepsFocus()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("focus.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var canvas = await FocusCanvasAsync(window);
            main.SelectedTab!.FocusMode.IsOn = true;
            await Assert.That(await UiWait.UntilAsync(() => Focused(window) is Button { Name: "BackToPagesButton" })).IsTrue();

            main.SelectedTab!.FocusMode.IsOn = false;
            await Assert.That(await UiWait.UntilAsync(() => ReferenceEquals(Focused(window), canvas))).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Closing the last tab removes the document that held focus; focus moves to the start page's Open button.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosingTheLastTabFocusesOpen()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("close.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await FocusCanvasAsync(window);
            main.CloseTabWithoutAsking(main.SelectedTab);
            await Assert.That(await UiWait.UntilAsync(() => Focused(window) is Button { Name: "OpenButton" })).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A focus change the screen reader missed is repeated once, keeping the text box's selection.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnheardFocusIsRepeatedOnce()
    {
        using var check = new FakeFocusAnnouncementCheck(true);
        var (window, box) = ShowTextBox();
        using var repair = new FocusAnnouncementRepair(check, () => [window]);
        var arrivals = 0;
        using var counting = InputElement.GotFocusEvent.Raised.SubscribeSafe(
            raised =>
            {
                // Counts arrivals on this test's box only; the count lives in the test.
                if (ReferenceEquals(raised.Item1, box))
                {
                    arrivals++;
                }
            },
            static _ => { });
        try
        {
            _ = box.Focus();
            box.SelectionStart = SelectionStart;
            box.SelectionEnd = SelectionEnd;
            await Assert.That(await UiWait.UntilAsync(() => arrivals == ArrivalAndRepeat && check.Checks == ArrivalAndRepeat)).IsTrue();
            Dispatcher.UIThread.RunJobs();

            await Assert.That(arrivals).IsEqualTo(ArrivalAndRepeat);
            await Assert.That(ReferenceEquals(window.FocusManager?.GetFocusedElement(), box)).IsTrue();
            await Assert.That((box.SelectionStart, box.SelectionEnd)).IsEqualTo((SelectionStart, SelectionEnd));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Repeating focus on a form field's editor keeps the field open. The editor commits when it loses focus, and the
    /// repeat moves focus away for a moment.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatKeepsAFormFieldOpen()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var path = Path.Combine(test.Directory, "form.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateForm());
        await TestServices.OpenAndWaitAsync(main, [path]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        using var check = new FakeFocusAnnouncementCheck(true);
        using var repair = new FocusAnnouncementRepair(check, () => [window]);
        try
        {
            var tab = main.SelectedTab!;
            _ = await FocusCanvasAsync(window);
            var fields = new List<FormField>();
            ((IFormFiller)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(IFormFiller))!).GetFields(0, fields);
            var nameField = fields.Single(static f => f.Kind == FormFieldKind.Text);
            var centre = new PagePoint(nameField.Bounds.Left + (nameField.Bounds.Width * Half), nameField.Bounds.Top + (nameField.Bounds.Height * Half));
            var before = check.Checks;
            _ = tab.Forms.Activate(tab.Forms.HitTest(0, centre)!);
            var editor = window.GetVisualDescendants().OfType<TextBox>().Single(static t => t.Name == "FieldEditor");
            await Assert.That(await UiWait.UntilAsync(() => check.Checks >= before + ArrivalAndRepeat)).IsTrue();
            Dispatcher.UIThread.RunJobs();

            await Assert.That(tab.Forms.Editing).IsNotNull();
            await Assert.That(editor.IsVisible).IsTrue();
            await Assert.That(ReferenceEquals(Focused(window), editor)).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>When a screen reader starts after the app, the control that already has focus is checked and repeated.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FocusIsCheckedWhenAScreenReaderStarts()
    {
        using var check = new FakeFocusAnnouncementCheck(false);
        var (window, box) = ShowTextBox();
        using var repair = new FocusAnnouncementRepair(check, () => [window]);
        try
        {
            _ = box.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            await Assert.That(check.Checks).IsEqualTo(0);

            check.Activate();
            await Assert.That(await UiWait.UntilAsync(() => check.Checks == ArrivalAndRepeat)).IsTrue();
            await Assert.That(ReferenceEquals(window.FocusManager?.GetFocusedElement(), box)).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Focus that arrives with a pointer press is never repeated, so the press still clicks.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PointerFocusIsNotRepeated()
    {
        using var check = new FakeFocusAnnouncementCheck(true);
        var (window, box) = ShowTextBox();
        using var repair = new FocusAnnouncementRepair(check, () => [window]);
        try
        {
            _ = box.Focus(NavigationMethod.Pointer);
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(SettleTime);
            Dispatcher.UIThread.RunJobs();

            await Assert.That(check.Checks).IsEqualTo(0);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Without a screen reader nothing is checked or repeated.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NothingIsRepeatedWithoutAScreenReader()
    {
        using var check = new FakeFocusAnnouncementCheck(false);
        var (window, box) = ShowTextBox();
        using var repair = new FocusAnnouncementRepair(check, () => [window]);
        try
        {
            _ = box.Focus();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(SettleTime);
            Dispatcher.UIThread.RunJobs();

            await Assert.That(check.Checks).IsEqualTo(0);
            await Assert.That(ReferenceEquals(window.FocusManager?.GetFocusedElement(), box)).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Without a screen reader a focus change costs the repair no managed memory.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FocusChangesAllocateNothingWithoutAScreenReader()
    {
        const int repetitions = 1000;
        var (window, box) = ShowTextBox();
        using var repair = new FocusAnnouncementRepair(PdfViewerLite.Core.Platform.NoFocusAnnouncementCheck.Instance, () => [window]);
        var focus = ((object)box, (Avalonia.Interactivity.RoutedEventArgs)new FocusChangedEventArgs(InputElement.GotFocusEvent) { NewFocusedElement = box });
        try
        {
            for (var i = 0; i < repetitions; i++)
            {
                repair.OnGotFocus(focus);
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < repetitions; i++)
            {
                repair.OnGotFocus(focus);
            }

            await Assert.That(GC.GetAllocatedBytesForCurrentThread() - before).IsEqualTo(0);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Shows a window holding a text box and a button.</summary>
    /// <returns>The window and its text box.</returns>
    private static (Window Window, TextBox Box) ShowTextBox()
    {
        var box = new TextBox { Text = Text };
        var panel = new StackPanel();
        panel.Children.Add(box);
        panel.Children.Add(new Button { Content = "Other" });
        var window = new Window { Content = panel, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, box);
    }

    /// <summary>Waits for the document and focuses its pages.</summary>
    /// <param name="window">The main window.</param>
    /// <returns>The page canvas.</returns>
    private static async Task<PageCanvas> FocusCanvasAsync(MainWindow window)
    {
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<PageCanvas>().Any(static c => c.IsEffectivelyVisible));
        var canvas = window.GetVisualDescendants().OfType<PageCanvas>().First(static c => c.IsEffectivelyVisible);
        _ = canvas.Focus();
        _ = await UiWait.UntilAsync(() => ReferenceEquals(Focused(window), canvas));
        return canvas;
    }

    /// <summary>Gets the focused control.</summary>
    /// <param name="window">The window.</param>
    /// <returns>The control, if any.</returns>
    private static IInputElement? Focused(Window window) => window.FocusManager?.GetFocusedElement();
}
