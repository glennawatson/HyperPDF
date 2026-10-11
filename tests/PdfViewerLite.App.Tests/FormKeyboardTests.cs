// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>
/// Headless checks of filling a form with the keyboard and of Fill &amp; Sign: Tab and Shift+Tab visit every kind of
/// field, Space ticks boxes and chooses radio buttons, a comb field takes one letter per box up to its limit, and
/// clicking blank page in Fill &amp; Sign types there while clicking a field fills it.
/// </summary>
public sealed class FormKeyboardTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>The comb's character limit.</summary>
    private const int CombBoxes = 6;

    /// <summary>Halves a length.</summary>
    private const double Half = 0.5;

    /// <summary>How far inside a field a click lands, in points.</summary>
    private const float Inside = 2;

    /// <summary>A blank spot on the page.</summary>
    private static readonly PagePoint Blank = new(300, 400);

    /// <summary>Tab and Shift+Tab move through text fields, the check box and the radio buttons; Space fills them in.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TabsThroughEveryField()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("form.pdf", TestPdf.CreateCombForm())]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var forms = tab.Forms;
            var canvas = await ReadyAsync(window, tab);
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            _ = await tab.FillAndSign.StartCommand.Execute().ToTask();
            await SettleLayout(window);
            var defaultTool = tab.Annotations.Tool;
            _ = canvas.Focus();
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            var first = await UiWait.UntilAsync(() => forms.Editing?.Name == "Name" && view.FieldEditor.IsKeyboardFocusWithin);
            window.KeyTextInput("Ada");
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            var comb = await UiWait.UntilAsync(() => forms.Editing?.Name == "Code" && view.FieldEditor.IsKeyboardFocusWithin);
            var limit = view.FieldEditor.MaxLength;
            window.KeyTextInput("1234567");
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            var onBox = await UiWait.UntilAsync(() => forms.Focused?.Kind == FormFieldKind.CheckBox && forms.Editing is null);
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
            var back = forms.Focused;
            var fields = new List<FormField>();
            ((IFormFiller)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(IFormFiller))!).GetFields(0, fields);

            await Assert.That(defaultTool).IsEqualTo(AnnotationTool.Text);
            await Assert.That(first && comb && onBox).IsTrue();
            await Assert.That(limit).IsEqualTo(CombBoxes);
            await Assert.That(fields.Single(static f => f.Name == "Name").Value).IsEqualTo("Ada");
            await Assert.That(fields.Single(static f => f.Name == "Code").Value).IsEqualTo("123456");
            await Assert.That(fields.Single(static f => f.Name == "Agree").IsChecked).IsTrue();
            await Assert.That(fields.Last(static f => f.Kind == FormFieldKind.RadioButton).IsChecked).IsTrue();
            await Assert.That(back?.Kind).IsEqualTo(FormFieldKind.RadioButton);
            await Assert.That(back?.Index).IsEqualTo(fields.First(static f => f.Kind == FormFieldKind.RadioButton).Index);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The Fill &amp; Sign button turns on typing on the page; then a click on a field fills it, and a click on blank page
    /// starts typing there.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FillAndSignTypesAnywhere()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("fill.pdf", TestPdf.CreateCombForm())]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var canvas = await ReadyAsync(window, tab);
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            Click(window, view.FillSignToggle.TranslatePoint(new(view.FillSignToggle.Bounds.Width * Half, view.FillSignToggle.Bounds.Height * Half), window)!.Value);
            await SettleLayout(window);
            var textTool = tab.Annotations.Tool == AnnotationTool.Text && view.FillSignToggle.IsChecked == true;
            var fields = new List<FormField>();
            ((IFormFiller)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(IFormFiller))!).GetFields(0, fields);
            var name = fields.Single(static f => f.Name == "Name");
            Click(window, ToWindow(canvas, window, new(name.Bounds.Left + Inside, name.Bounds.Top + Inside)));
            var filling = await UiWait.UntilAsync(() => tab.Forms.Editing?.Name == "Name");
            Click(window, ToWindow(canvas, window, Blank));
            var typing = await UiWait.UntilAsync(() => tab.Annotations.IsEditingText);
            window.KeyTextInput("Signed here");
            _ = tab.Annotations.CommitText();

            await Assert.That(textTool).IsTrue();
            await Assert.That(filling).IsTrue();
            await Assert.That(typing).IsTrue();
            await Assert.That(tab.Annotations.Items.Single().Annotation.Contents).IsEqualTo("Signed here");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Waits for the first page to be drawn.</summary>
    /// <param name="window">The window.</param>
    /// <param name="tab">The tab.</param>
    /// <returns>The page canvas.</returns>
    private static async Task<PageCanvas> ReadyAsync(Window window, DocumentTabViewModel tab)
    {
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<PageCanvas>().Any());
        var hub = tab.RenderHub;
        _ = await UiWait.UntilAsync(() => hub.Cache.Count > 0 && hub.Scheduler.QueueLength == 0);
        return window.GetVisualDescendants().OfType<PageCanvas>().Single();
    }

    /// <summary>Lets the tool rows that just appeared push the pages down before points on them are worked out.</summary>
    /// <param name="window">The window.</param>
    /// <returns>A task.</returns>
    private static async Task SettleLayout(Window window)
    {
        var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
        _ = await UiWait.UntilAsync(() => view.TextFormatBar.IsVisible && view.FillSignBar.IsVisible);
        window.UpdateLayout();
    }

    /// <summary>Converts a point on the first page to window coordinates.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="window">The window.</param>
    /// <param name="point">The page point.</param>
    /// <returns>The window point.</returns>
    private static Point ToWindow(PageCanvas canvas, Window window, PagePoint point) => canvas.TranslatePoint(canvas.PageToCanvas(0, point), window)!.Value;

    /// <summary>Clicks a window point with the left mouse button.</summary>
    /// <param name="window">The window.</param>
    /// <param name="point">The point.</param>
    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }
}
