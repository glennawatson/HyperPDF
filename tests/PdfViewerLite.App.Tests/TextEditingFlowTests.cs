// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Text;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>
/// Headless checks of the whole typing flow as a person uses it: the font list in the format row restyles the text
/// being typed at once, the format buttons work by mouse without ending the edit, the editor takes the usual editing
/// keys, the clipboard and composed (IME) text, and text saved with every setting reopens in a new window the same,
/// ready to edit again.
/// </summary>
public sealed class TextEditingFlowTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>The text boxes written before saving.</summary>
    private const int BoxesWritten = 2;

    /// <summary>A text size.</summary>
    private const float Size = 18;

    /// <summary>A wider line spacing.</summary>
    private const float WideLines = 1.5F;

    /// <summary>Some extra space between characters, in points.</summary>
    private const float Spacing = 1;

    /// <summary>A dark blue as 0xRRGGBB.</summary>
    private const uint Blue = 0x1F3A93;

    /// <summary>How close positions must be, in points.</summary>
    private const float Slack = 1;

    /// <summary>How close sizes must be.</summary>
    private const double Tolerance = 0.01;

    /// <summary>Halves a length.</summary>
    private const double Half = 0.5;

    /// <summary>The least width a number box in the properties window needs to show its value beside its buttons.</summary>
    private const double MinValueWidth = 140;

    /// <summary>Halves a length on the page.</summary>
    private const float HalfPoint = 0.5F;

    /// <summary>The first words typed.</summary>
    private const string Words = "Hello world";

    /// <summary>The first reading an input method shows while composing, before a choice is made.</summary>
    private const string FirstPreedit = "にほん";

    /// <summary>Text composed by an input method, committed as one string.</summary>
    private const string Composed = "日本語";

    /// <summary>The editor's text after copying, pasting on a new line and composing.</summary>
    private const string Expected = $"{Words}\n{Words}{Composed}";

    /// <summary>Two lines of text with accents and Greek.</summary>
    private const string TwoLines = "Grüße an alle\nΩμέγα";

    /// <summary>Where text is typed.</summary>
    private static readonly Core.Geometry.PagePoint TypeAt = new(120, 200);

    /// <summary>Where the second text is typed.</summary>
    private static readonly Core.Geometry.PagePoint SecondAt = new(120, 420);

    /// <summary>Picking a font in the format row changes the text being typed at once; Bold by mouse keeps the edit going.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontListRestylesAtOnce()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("fonts.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            _ = await ReadyAsync(window, tab);
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            _ = annotations.BeginText(0, TypeAt, 0);
            _ = await UiWait.UntilAsync(() => view.PageTextEditor.IsKeyboardFocusWithin);
            window.KeyTextInput(Words);
            view.FontFamilyBox.IsDropDownOpen = true;
            var listed = await UiWait.UntilAsync(() => view.FontFamilyBox.ItemCount >= StandardFontFamilies.All.Count);
            var families = view.FontFamilyBox.Items.OfType<string>().ToList();
            var family = families.Find(static f => !StandardFontFamilies.IsStandard(f)) ?? StandardFontFamilies.Serif;
            view.FontFamilyBox.SelectedItem = family;
            view.FontFamilyBox.IsDropDownOpen = false;
            var shownFamily = view.PageTextEditor.FontFamily;
            Click(window, Centre(view.BoldToggle, window));
            var bold = await UiWait.UntilAsync(() => view.PageTextEditor.FontWeight == FontWeight.Bold);
            var stillEditing = annotations.IsEditingText && view.PageTextEditor.Text == Words;
            Save(window, "font-list.png");
            _ = annotations.CommitText();
            var format = ((ITextBoxEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(ITextBoxEditor))!).GetTextBox(
                0,
                annotations.Items[0].Annotation.Index)!.Format;

            await Assert.That(listed).IsTrue();
            await Assert.That(families.Take(StandardFontFamilies.All.Count)).IsEquivalentTo(StandardFontFamilies.All);
            await Assert.That(shownFamily).IsEqualTo(PageFonts.Get(family));
            await Assert.That(bold).IsTrue();
            await Assert.That(stillEditing).IsTrue();
            await Assert.That(format.FontFamily).IsEqualTo(family);
            await Assert.That(format.IsBold).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The editor takes the usual keys: Home and Shift+End select, the clipboard copies and pastes, Enter starts a new
    /// line, Ctrl+Z undoes typing, and composed text from an input method is kept and written.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditorTakesEditingKeysClipboardAndComposedText()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("keys.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            _ = await ReadyAsync(window, tab);
            var editor = window.GetVisualDescendants().OfType<DocumentView>().Single().PageTextEditor;
            _ = annotations.BeginText(0, TypeAt, 0);
            _ = await UiWait.UntilAsync(() => editor.IsKeyboardFocusWithin);
            var inputMethod = InputMethod.GetIsInputMethodEnabled(editor);
            window.KeyTextInput(Words);
            window.KeyPress(Key.Home, RawInputModifiers.None, PhysicalKey.Home, null);
            window.KeyPress(Key.End, RawInputModifiers.Shift, PhysicalKey.End, null);
            var selected = editor.SelectedText;
            window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, null);
            window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, null);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, null);
            var pasted = await UiWait.UntilAsync(() => Lines(editor.Text) == $"{Words}\n{Words}");
            window.KeyTextInput("!");
            window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null);
            _ = await UiWait.UntilAsync(() => Lines(editor.Text) == $"{Words}\n{Words}");
            var afterUndo = Lines(editor.Text);
            window.KeyTextInput(Composed);
            var composedShown = Lines(editor.Text) == Expected;
            window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
            var written = await UiWait.UntilAsync(() => annotations.Items.Count == 1);
            var stored = ((ITextBoxEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(ITextBoxEditor))!).GetTextBox(
                0,
                annotations.Items[0].Annotation.Index)!.Text;

            await Assert.That(inputMethod).IsTrue();
            await Assert.That(selected).IsEqualTo(Words);
            await Assert.That(pasted).IsTrue();
            await Assert.That(afterUndo).IsEqualTo($"{Words}\n{Words}");
            await Assert.That(composedShown).IsTrue();
            await Assert.That(written).IsTrue();
            await Assert.That(Lines(stored)).IsEqualTo(Expected);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The properties window is only for advanced settings: clicking the page types with no window, and Properties…
    /// opens the window filled with the text and its format; its changes carry on into the same edit.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PropertiesWindowIsOnlyForAdvancedSettings()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("properties.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            var canvas = await ReadyAsync(window, tab);
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            _ = await annotations.SetToolCommand.Execute(AnnotationTool.Text).ToTask();
            Click(window, ToWindow(canvas, window, TypeAt));
            _ = await UiWait.UntilAsync(() => view.PageTextEditor.IsKeyboardFocusWithin);
            var windowsWhileTyping = window.OwnedWindows.Count;
            window.KeyTextInput(Words);
            var sizeBefore = (decimal)annotations.FontSize;
            Click(window, Centre(view.TextPropertiesButton, window));
            var opened = await UiWait.UntilAsync(() => window.OwnedWindows.OfType<TextPropertiesWindow>().Any());
            var dialog = window.OwnedWindows.OfType<TextPropertiesWindow>().Single();
            var shownText = dialog.ViewModel!.Text;
            var shownSize = dialog.ViewModel.FontSize;
            dialog.UpdateLayout();
            var narrowest = dialog.GetVisualDescendants().OfType<NumericUpDown>().Min(static n => n.Bounds.Width);
            var sizeShown = dialog.SizeInput.Text;
            dialog.ViewModel.FontSize = (decimal)Size;
            dialog.ViewModel.IsItalic = true;
            dialog.ViewModel.LineSpacing = (decimal)WideLines;
            dialog.ApplyButton.Command!.Execute(null);

            // The settings apply once the window has closed and the command carries on, so wait for them, not the close.
            var closed = await UiWait.UntilAsync(() => window.OwnedWindows.Count == 0 && annotations.CurrentTextFormat.IsItalic);
            var stillEditing = annotations.IsEditingText && annotations.EditingText == Words;
            var format = annotations.CurrentTextFormat;

            await Assert.That(windowsWhileTyping).IsEqualTo(0);
            await Assert.That(opened).IsTrue();
            await Assert.That(shownText).IsEqualTo(Words);
            await Assert.That(shownSize).IsEqualTo(sizeBefore);
            await Assert.That(narrowest).IsGreaterThanOrEqualTo(MinValueWidth);
            await Assert.That(sizeShown).IsEqualTo(sizeBefore.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture));
            await Assert.That(closed).IsTrue();
            await Assert.That(stillEditing).IsTrue();
            await Assert.That(format.FontSize).IsEqualTo(Size);
            await Assert.That(format.IsItalic).IsTrue();
            await Assert.That(format.LineSpacing).IsEqualTo(WideLines);
            await Assert.That(view.PageTextEditor.FontStyle).IsEqualTo(FontStyle.Italic);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// An input method composes in the editor: the text being composed shows at the caret but is not yet part of the
    /// text, a changed composition replaces it, and only the chosen characters are kept and written.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ComposesWithAnInputMethod()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("compose.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            _ = await ReadyAsync(window, tab);
            var editor = window.GetVisualDescendants().OfType<DocumentView>().Single().PageTextEditor;
            _ = annotations.BeginText(0, TypeAt, 0);
            _ = await UiWait.UntilAsync(() => editor.IsKeyboardFocusWithin);
            window.KeyTextInput(Words);
            var request = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
            editor.RaiseEvent(request);
            var client = request.Client!;
            var presenter = editor.GetVisualDescendants().OfType<TextPresenter>().Single();
            client.SetPreeditText(FirstPreedit);
            var firstShown = presenter.PreeditText;
            var textWhileComposing = editor.Text;
            client.SetPreeditText(Composed);
            var changedShown = presenter.PreeditText;
            client.SetPreeditText(null);
            window.KeyTextInput(Composed);
            var afterCommit = editor.Text;
            window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
            var written = await UiWait.UntilAsync(() => annotations.Items.Count == 1);
            var stored = ((ITextBoxEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(ITextBoxEditor))!).GetTextBox(
                0,
                annotations.Items[0].Annotation.Index)!.Text;

            await Assert.That(firstShown).IsEqualTo(FirstPreedit);
            await Assert.That(textWhileComposing).IsEqualTo(Words);
            await Assert.That(changedShown).IsEqualTo(Composed);
            await Assert.That(presenter.PreeditText).IsNull();
            await Assert.That(afterCommit).IsEqualTo($"{Words}{Composed}");
            await Assert.That(written).IsTrue();
            await Assert.That(stored).IsEqualTo($"{Words}{Composed}");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Keys that are also page shortcuts belong to the text while typing: Ctrl+I makes the text italic instead of
    /// changing the page tone, Ctrl+Left moves by word instead of rotating the page, and Ctrl+Z undoes typing instead
    /// of the last comment change.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TypingKeepsTextKeysFromPageShortcuts()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("shortcuts.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            _ = await ReadyAsync(window, tab);
            var editor = window.GetVisualDescendants().OfType<DocumentView>().Single().PageTextEditor;
            _ = annotations.BeginText(0, SecondAt, 0);
            annotations.EditingText = Words;
            _ = annotations.CommitText();
            var tone = main.PageToneEnabled;
            var rotation = tab.Rotation;
            _ = annotations.BeginText(0, TypeAt, 0);
            _ = await UiWait.UntilAsync(() => editor.IsKeyboardFocusWithin);
            window.KeyTextInput(Words);
            window.KeyPress(Key.I, RawInputModifiers.Control, PhysicalKey.I, null);
            var italic = annotations.IsItalic && editor.FontStyle == FontStyle.Italic;
            window.KeyPress(Key.Left, RawInputModifiers.Control, PhysicalKey.ArrowLeft, null);
            var caret = editor.CaretIndex;
            window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null);

            await Assert.That(italic).IsTrue();
            await Assert.That(main.PageToneEnabled).IsEqualTo(tone);
            await Assert.That(tab.Rotation).IsEqualTo(rotation);
            await Assert.That(caret).IsEqualTo(Words.IndexOf(' ', StringComparison.Ordinal) + 1);
            await Assert.That(annotations.IsEditingText).IsTrue();
            await Assert.That(annotations.CanUndo).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Text written with every setting is saved, reopened in a new window and comes back the same: words, lines,
    /// format and place. Editing it again loads its format into the format row.
    /// </summary>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <c>tab.TryGetDocument()</c> is <see langword="null"/>.</exception>
    [Test]
    public async Task SavedTextReopensForEditing()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "reopened.pdf");
        var (written, bounds) = await WriteAndSaveAsync(test, path);

        using var reopenedMain = new MainViewModel(test.Services);
        reopenedMain.Open([path]);
        var reopenedWindow = new MainWindow { DataContext = reopenedMain, Width = WindowWidth, Height = WindowHeight };
        reopenedWindow.Show();
        try
        {
            var tab = reopenedMain.SelectedTab!;
            var annotations = tab.Annotations;
            var canvas = await ReadyAsync(reopenedWindow, tab);
            var view = reopenedWindow.GetVisualDescendants().OfType<DocumentView>().Single();
            var document = tab.TryGetDocument() ?? throw new InvalidOperationException();
            var editor = (IAnnotationEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(IAnnotationEditor))!;
            var boxes = (ITextBoxEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(ITextBoxEditor))!;
            var found = new List<PageAnnotation>();
            (editor).GetAnnotations(0, found);
            var first = found.Find(static a => a.Kind == AnnotationKind.TextBox && a.Contents.StartsWith("Grüße", StringComparison.Ordinal))!;
            var content = boxes.GetTextBox(0, first.Index)!;
            var middle = ToWindow(canvas, reopenedWindow, new(first.Bounds.Left + (first.Bounds.Width * HalfPoint), first.Bounds.Top + (first.Bounds.Height * HalfPoint)));

            Click(reopenedWindow, middle);
            _ = await UiWait.UntilAsync(() => annotations.Selected?.Kind == AnnotationKind.TextBox);
            reopenedWindow.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            var editing = await UiWait.UntilAsync(
                () => annotations.IsEditingText && view.PageTextEditor.IsKeyboardFocusWithin,
                state: () => $"Selected={annotations.Selected?.Kind}, Editing={annotations.IsEditingText}, Visible={view.PageTextEditor.IsVisible}, "
                    + $"Focus={reopenedWindow.FocusManager?.GetFocusedElement()}");
            var shown = view.PageTextEditor.Text;
            var loaded = annotations.CurrentTextFormat;
            Save(reopenedWindow, "text-reopened.png");

            await Assert.That(found.Count(static a => a.Kind == AnnotationKind.TextBox)).IsEqualTo(BoxesWritten);
            await Assert.That(Lines(content.Text)).IsEqualTo(TwoLines);
            await Assert.That(content.Format).IsEqualTo(written);
            await Assert.That(Math.Abs(first.Bounds.Left - bounds.Left)).IsLessThan(Slack);
            await Assert.That(Math.Abs(first.Bounds.Top - bounds.Top)).IsLessThan(Slack);
            await Assert.That(Math.Abs(first.Bounds.Width - bounds.Width)).IsLessThan(Slack);
            await Assert.That(editing).IsTrue();
            await Assert.That(Lines(shown)).IsEqualTo(TwoLines);
            await Assert.That(loaded).IsEqualTo(written);
            await Assert.That(view.BoldToggle.IsChecked == true).IsTrue();
            await Assert.That(view.PageTextEditor.FontSize).IsEqualTo(Size * canvas.PageScale).Within(Tolerance);
        }
        finally
        {
            reopenedWindow.Close();
        }
    }

    /// <summary>Writes two text boxes, the first with every setting changed, and saves the document.</summary>
    /// <param name="test">The test services.</param>
    /// <param name="path">Where to save.</param>
    /// <returns>The first box's format and place.</returns>
    private static async Task<(TextFormat Format, Core.Geometry.PageRect Bounds)> WriteAndSaveAsync(TestServices test, string path)
    {
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("round-trip.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            _ = await ReadyAsync(window, tab);
            SetEverySetting(annotations);
            var written = annotations.CurrentTextFormat;
            _ = annotations.BeginText(0, TypeAt, 0);
            annotations.EditingText = TwoLines;
            _ = annotations.CommitText();
            var bounds = annotations.Items[0].Annotation.Bounds;
            _ = annotations.BeginText(0, SecondAt, 0);
            annotations.EditingText = Words;
            _ = annotations.CommitText();
            _ = tab.Save(path);
            return (written, bounds);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Sets every format setting away from its default.</summary>
    /// <param name="annotations">The annotations.</param>
    private static void SetEverySetting(AnnotationsViewModel annotations)
    {
        annotations.FontFamily = StandardFontFamilies.Serif;
        annotations.FontSize = Size;
        annotations.IsBold = true;
        annotations.IsItalic = true;
        annotations.IsUnderline = true;
        annotations.TextAlignment = TextBoxAlignment.Center;
        annotations.LineSpacing = WideLines;
        annotations.CharacterSpacing = Spacing;
        annotations.TextColor = Blue;
    }

    /// <summary>Writes line breaks the same on every platform.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with plain line feeds.</returns>
    private static string Lines(string? text) => (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

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
        window.UpdateLayout();
        return window.GetVisualDescendants().OfType<PageCanvas>().Single();
    }

    /// <summary>Converts a point on the first page to window coordinates.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="window">The window.</param>
    /// <param name="point">The page point.</param>
    /// <returns>The window point.</returns>
    private static Point ToWindow(PageCanvas canvas, Window window, Core.Geometry.PagePoint point) => canvas.TranslatePoint(canvas.PageToCanvas(0, point), window)!.Value;

    /// <summary>Finds the middle of a control in window coordinates.</summary>
    /// <param name="control">The control.</param>
    /// <param name="window">The window.</param>
    /// <returns>The point.</returns>
    private static Point Centre(Control control, Window window) =>
        control.TranslatePoint(new(control.Bounds.Width * Half, control.Bounds.Height * Half), window)!.Value;

    /// <summary>Clicks a window point with the left mouse button.</summary>
    /// <param name="window">The window.</param>
    /// <param name="point">The point.</param>
    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
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
