// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Text;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>
/// Headless checks of typing on the page: clicking with the Text tool gives a caret straight away with no dialog,
/// double-clicking text edits it in place, the format row changes text as it is typed and changes picked text,
/// Escape cancels, resizing a text box wraps it, and the editor follows the zoom and rotation.
/// </summary>
public sealed class TextEditingUiTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>A text size.</summary>
    private const float Size = 20;

    /// <summary>A narrow wrap width, in points.</summary>
    private const float Narrow = 80;

    /// <summary>How close sizes must be.</summary>
    private const double Tolerance = 0.01;

    /// <summary>A quarter turn in degrees.</summary>
    private const double QuarterTurn = 90;

    /// <summary>A zoom that doubles the page.</summary>
    private const double DoubleZoom = 2;

    /// <summary>Halves a length.</summary>
    private const float Half = 0.5F;

    /// <summary>The words typed over the first.</summary>
    private const string Changed = "Changed";

    /// <summary>The words typed first.</summary>
    private const string Typed = "Hello page";

    /// <summary>The words typed to compare the preview with the page.</summary>
    private const string PreviewWords = "Hamburg";

    /// <summary>The width of the area compared, in points.</summary>
    private const float PreviewWidth = 160;

    /// <summary>How far inside the area the editor's border is left out, in window units.</summary>
    private const double BorderInset = 3;

    /// <summary>How far apart, in frame pixels, the preview's and the page's ink may be.</summary>
    private const double PixelSlack = 4;

    /// <summary>The smallest written to typed width ratio accepted.</summary>
    private const double WidthLow = 0.9;

    /// <summary>The largest written to typed width ratio accepted.</summary>
    private const double WidthHigh = 1.1;

    /// <summary>The green level below which a pixel counts as ink on white paper.</summary>
    private const byte InkLevel = 128;

    /// <summary>The bytes in a frame pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The green byte of a frame pixel.</summary>
    private const int GreenByte = 1;

    /// <summary>The pause between checks that tiles have stopped arriving.</summary>
    private static readonly TimeSpan SettlePause = TimeSpan.FromMilliseconds(150);

    /// <summary>Where the preview is compared, in page space; at twice the size it stays well inside the window.</summary>
    private static readonly PagePoint PreviewAt = new(100, 150);

    /// <summary>Where text is typed, in page space.</summary>
    private static readonly PagePoint TypeAt = new(120, 200);

    /// <summary>A blank spot on the page, away from the text.</summary>
    private static readonly PagePoint Elsewhere = new(400, 600);

    /// <summary>Clicking with the Text tool shows a focused caret at once; typing and Ctrl+Enter write a text box, with no dialog.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClickingTypesWithoutADialog()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("type.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            var canvas = await ReadyAsync(window, tab);
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var prompted = false;
            using var prompt = annotations.PromptInteraction.RegisterHandler(context =>
            {
                prompted = true;
                context.SetOutput(null);
            });
            _ = await annotations.SetToolCommand.Execute(AnnotationTool.Text).ToTask();
            Click(window, ToWindow(canvas, window, TypeAt));
            var focused = await UiWait.UntilAsync(() => view.PageTextEditor.IsVisible && view.PageTextEditor.IsKeyboardFocusWithin);
            window.KeyTextInput(Typed);
            var live = annotations.EditingText;
            Save(window, "text-typing.png");
            window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
            var written = await UiWait.UntilAsync(() => annotations.Items.Count == 1);
            Save(window, "text-written.png");

            await Assert.That(focused).IsTrue();
            await Assert.That(live).IsEqualTo(Typed);
            await Assert.That(written).IsTrue();
            await Assert.That(annotations.Items[0].Annotation.Kind).IsEqualTo(AnnotationKind.TextBox);
            await Assert.That(annotations.Items[0].Annotation.Contents).IsEqualTo(Typed);
            await Assert.That(view.PageTextEditor.IsVisible).IsFalse();
            await Assert.That(view.TextFormatBar.IsVisible).IsTrue();
            await Assert.That(prompted).IsFalse();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Double-clicking a text box edits it where it is; clicking elsewhere keeps the edit, and one undo puts the old text back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DoubleClickEditsInPlace()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("edit.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            var canvas = await ReadyAsync(window, tab);
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            Write(annotations, Typed);
            var box = annotations.Items[0].Annotation;
            var middle = ToWindow(canvas, window, new(box.Bounds.Left + (box.Bounds.Width * Half), box.Bounds.Top + (box.Bounds.Height * Half)));

            // Picking the box first lets its first-time work finish, so the two clicks after it land as one double-click.
            Click(window, middle);
            _ = await UiWait.UntilAsync(() => annotations.Selected?.Kind == AnnotationKind.TextBox);
            window.MouseDown(middle, MouseButton.Left);
            window.MouseUp(middle, MouseButton.Left);
            window.MouseDown(middle, MouseButton.Left);
            window.MouseUp(middle, MouseButton.Left);
            var editing = await UiWait.UntilAsync(() => view.PageTextEditor.IsVisible && view.PageTextEditor.IsKeyboardFocusWithin);
            var shown = view.PageTextEditor.Text;
            view.PageTextEditor.SelectAll();
            window.KeyTextInput(Changed);
            Click(window, ToWindow(canvas, window, Elsewhere));
            var changed = await UiWait.UntilAsync(() => annotations.Items.Count == 1 && annotations.Items[0].Annotation.Contents == Changed);
            _ = canvas.Focus();
            window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null);
            var undone = await UiWait.UntilAsync(() => annotations.Items.Count == 1 && annotations.Items[0].Annotation.Contents == Typed);
            window.KeyPress(Key.Y, RawInputModifiers.Control, PhysicalKey.Y, null);
            var redone = await UiWait.UntilAsync(() => annotations.Items.Count == 1 && annotations.Items[0].Annotation.Contents == Changed);

            await Assert.That(editing).IsTrue();
            await Assert.That(shown).IsEqualTo(Typed);
            await Assert.That(changed).IsTrue();
            await Assert.That(undone).IsTrue();
            await Assert.That(redone).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The format row restyles text as it is typed, Ctrl+B toggles bold, and the written text keeps the format.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormatsWhileTyping()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("format.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            var canvas = await ReadyAsync(window, tab);
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            _ = annotations.BeginText(0, TypeAt, 0);
            _ = await UiWait.UntilAsync(() => view.PageTextEditor.IsKeyboardFocusWithin);
            window.KeyTextInput("Styled");
            window.KeyPress(Key.B, RawInputModifiers.Control, PhysicalKey.B, null);
            view.ItalicToggle.IsChecked = true;
            view.FontSizeBox.Text = "20";
            _ = view.FontSizeBox.Focus();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            var weight = view.PageTextEditor.FontWeight;
            var style = view.PageTextEditor.FontStyle;
            var fontSize = view.PageTextEditor.FontSize;
            var stillEditing = annotations.IsEditingText;
            _ = annotations.CommitText();
            var index = annotations.Items[0].Annotation.Index;
            var format = ((ITextBoxEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(ITextBoxEditor))!).GetTextBox(0, index)!.Format;

            await Assert.That(weight).IsEqualTo(FontWeight.Bold);
            await Assert.That(style).IsEqualTo(FontStyle.Italic);
            await Assert.That(fontSize).IsEqualTo(Size * canvas.PageScale).Within(Tolerance);
            await Assert.That(stillEditing).IsTrue();
            await Assert.That(format.IsBold && format.IsItalic).IsTrue();
            await Assert.That(format.FontSize).IsEqualTo(Size);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Changing the format with a text box picked rewrites it at once, and undo puts the old format back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormatsPickedText()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("picked.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            _ = await ReadyAsync(window, tab);
            var editor = (ITextBoxEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(ITextBoxEditor))!;
            Write(annotations, Typed);
            annotations.Select(annotations.Items[0].Annotation);
            annotations.FontFamily = StandardFontFamilies.Serif;
            var serif = editor.GetTextBox(0, annotations.Selected!.Index)!.Format.FontFamily;
            _ = await annotations.UndoCommand.Execute().ToTask();
            var restored = editor.GetTextBox(0, annotations.Items[0].Annotation.Index)!.Format.FontFamily;

            await Assert.That(serif).IsEqualTo(StandardFontFamilies.Serif);
            await Assert.That(annotations.Items.Count).IsEqualTo(1);
            await Assert.That(restored).IsEqualTo(StandardFontFamilies.Sans);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Escape cancels new text; blank text writes nothing; the editor follows zoom and rotation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelsAndFollowsTheView()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("cancel.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            var canvas = await ReadyAsync(window, tab);
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            _ = annotations.BeginText(0, TypeAt, 0);
            _ = await UiWait.UntilAsync(() => view.PageTextEditor.IsKeyboardFocusWithin);
            window.KeyTextInput("Not kept");
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            var cancelled = !annotations.IsEditingText && annotations.Items.Count == 0;
            _ = annotations.BeginText(0, TypeAt, 0);
            var blank = !annotations.CommitText();
            _ = annotations.BeginText(0, TypeAt, 0);
            tab.SetZoom(DoubleZoom);
            _ = await UiWait.UntilAsync(() => Math.Abs(view.PageTextEditor.FontSize - (annotations.FontSize * canvas.PageScale)) < Tolerance);
            var zoomed = view.PageTextEditor.FontSize;
            tab.Rotation = PageRotation.Rotate90;
            _ = await UiWait.UntilAsync(() => view.PageTextEditor.RenderTransform is RotateTransform);
            var turned = (view.PageTextEditor.RenderTransform as RotateTransform)?.Angle;

            await Assert.That(cancelled).IsTrue();
            await Assert.That(blank).IsTrue();
            await Assert.That(zoomed).IsEqualTo(annotations.FontSize * canvas.PageScale).Within(Tolerance);
            await Assert.That(turned).IsEqualTo(QuarterTurn);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Making a text box narrower wraps its text to the new width instead of squashing it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResizingWrapsText()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("wrap.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            _ = await ReadyAsync(window, tab);
            Write(annotations, "A sentence long enough to wrap onto several lines");
            var before = annotations.Items[0].Annotation;
            var moved = annotations.Move(before, before.Bounds with { Width = Narrow }, false);
            var after = annotations.Items[0].Annotation;
            var content = ((ITextBoxEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(ITextBoxEditor))!).GetTextBox(0, after.Index)!;

            await Assert.That(moved).IsTrue();
            await Assert.That(content.WrapWidth).IsEqualTo(Narrow);
            await Assert.That(after.Bounds.Height).IsGreaterThan(before.Bounds.Height);
            await Assert.That(content.Format.FontSize).IsEqualTo(annotations.FontSize);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The text shown while typing sits where the written text is drawn: at twice the size, the ink of the editor and
    /// of the page agree to within a few pixels, so keeping the text does not make it jump.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreviewMatchesWrittenText()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("preview.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            var canvas = await ReadyAsync(window, tab);
            tab.PageTone = Core.Rendering.PageTone.None;
            tab.SetZoom(DoubleZoom);
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            annotations.FontSize = Size;
            _ = annotations.BeginTextAt(0, PreviewAt, 0);
            _ = await UiWait.UntilAsync(() => view.PageTextEditor.IsKeyboardFocusWithin);
            window.KeyTextInput(PreviewWords);
            await Settle(test);
            var area = new Rect(ToWindow(canvas, window, PreviewAt), ToWindow(canvas, window, new(PreviewAt.X + PreviewWidth, PreviewAt.Y + Size + Size))).Deflate(BorderInset);
            TestContext.Current?.Output.WriteLine($"window {window.Bounds.Size}, area {area}");
            await Assert.That(new Rect(window.Bounds.Size).Contains(area)).IsTrue();
            var typing = InkBounds(window, area);
            Save(window, "text-preview-typing.png");
            _ = annotations.CommitText();
            await Settle(test);
            var written = InkBounds(window, area);
            Save(window, "text-preview-written.png");
            TestContext.Current?.Output.WriteLine($"typing {typing}, written {written}");

            await Assert.That(typing.Width).IsGreaterThan(0D);
            await Assert.That(Math.Abs(typing.Left - written.Left)).IsLessThanOrEqualTo(PixelSlack);

            // The caret is taller than the letters, so the tops differ while typing; the descender's bottom does not.
            await Assert.That(Math.Abs(typing.Bottom - written.Bottom)).IsLessThanOrEqualTo(PixelSlack);
            await Assert.That(written.Width / typing.Width).IsBetween(WidthLow, WidthHigh);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Writes text through the editor's view model.</summary>
    /// <param name="annotations">The annotation state.</param>
    /// <param name="text">The text.</param>
    private static void Write(AnnotationsViewModel annotations, string text)
    {
        _ = annotations.BeginText(0, TypeAt, 0);
        annotations.EditingText = text;
        _ = annotations.CommitText();
    }

    /// <summary>Waits for the first page and turns on the annotation tools.</summary>
    /// <param name="window">The window.</param>
    /// <param name="tab">The tab.</param>
    /// <returns>The page canvas.</returns>
    private static async Task<PageCanvas> ReadyAsync(Window window, DocumentTabViewModel tab)
    {
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<PageCanvas>().Any());
        _ = await tab.Annotations.StartCommand.Execute().ToTask();
        var hub = tab.RenderHub;
        _ = await UiWait.UntilAsync(() => hub.Cache.Count > 0 && hub.Scheduler.QueueLength == 0);
        return window.GetVisualDescendants().OfType<PageCanvas>().Single();
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

    /// <summary>Waits until every page tile has been drawn.</summary>
    /// <param name="test">The test services.</param>
    /// <returns>A task.</returns>
    private static async Task Settle(TestServices test)
    {
        using var pause = new PeriodicTimer(SettlePause);
        var count = -1;
        while (count != test.Services.RenderHub.Cache.Count && await pause.WaitForNextTickAsync())
        {
            count = test.Services.RenderHub.Cache.Count;
            _ = await UiWait.UntilAsync(() => test.Services.RenderHub.Scheduler.QueueLength == 0);
        }
    }

    /// <summary>Finds the box around the dark ink in an area of the window's frame.</summary>
    /// <param name="window">The window.</param>
    /// <param name="area">The area, in window units.</param>
    /// <returns>The ink's box in frame pixels, or an empty box.</returns>
    private static Rect InkBounds(Window window, in Rect area)
    {
        using var frame = window.CaptureRenderedFrame()!;
        using var locked = frame.Lock();
        var scale = locked.Size.Width / window.Bounds.Width;

        // The frame is read through a raw pointer, so the scan stays inside it even when the area runs off the window.
        var (left, top) = (Math.Max(0, (int)(area.X * scale)), Math.Max(0, (int)(area.Y * scale)));
        var right = Math.Min(Math.Min(locked.Size.Width, locked.RowBytes / PixelBytes), (int)(area.Right * scale));
        var bottom = Math.Min(locked.Size.Height, (int)(area.Bottom * scale));
        var (minX, minY, maxX, maxY) = (int.MaxValue, int.MaxValue, -1, -1);
        unsafe
        {
            for (var y = top; y < bottom; y++)
            {
                var row = new ReadOnlySpan<byte>((byte*)locked.Address + ((long)y * locked.RowBytes), locked.RowBytes);
                for (var x = left; x < right; x++)
                {
                    if (row[(x * PixelBytes) + GreenByte] > InkLevel)
                    {
                        continue;
                    }

                    (minX, minY, maxX, maxY) = (Math.Min(minX, x), Math.Min(minY, y), Math.Max(maxX, x), Math.Max(maxY, y));
                }
            }
        }

        return maxX < 0 ? default : new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>Saves the window's frame when <c>PDFVIEWERLITE_SCREENSHOTS</c> names a folder.</summary>
    /// <param name="window">The window.</param>
    /// <param name="name">The file name.</param>
    private static void Save(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("PDFVIEWERLITE_SCREENSHOTS");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        using var frame = window.CaptureRenderedFrame();
        _ = Directory.CreateDirectory(directory);
        frame?.Save(Path.Combine(directory, name), new PngBitmapEncoderOptions());
    }
}
