// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Text;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>
/// The editor that sits on the page where text is typed, in the text's own font, size and colour, and the underline
/// beneath it. The text format bar changes the text as it is typed.
/// </summary>
[DebuggerDisplay("PageTextEditorView: {ViewModel}")]
public sealed partial class PageTextEditorView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>The degrees in a quarter turn.</summary>
    private const double QuarterTurn = 90;

    /// <summary>The bit offset of red in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit offset of green in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>The red byte of a BGRA pixel.</summary>
    private const int RedByte = 2;

    /// <summary>The green byte of a BGRA pixel.</summary>
    private const int GreenByte = 1;

    /// <summary>Two quarter turns.</summary>
    private const int HalfTurn = 2;

    /// <summary>Three quarter turns.</summary>
    private const int ThreeQuarterTurns = 3;

    /// <summary>The narrowest editor, in device independent pixels, so the caret always shows.</summary>
    private const double MinEditorWidth = 4;

    /// <summary>Halves the room around a comb letter, to centre it in its box.</summary>
    private const double HalfCell = 0.5;

    /// <summary>Initializes a new instance of the <see cref="PageTextEditorView"/> class.</summary>
    public PageTextEditorView()
    {
        InitializeComponent();
        var tabs = this.WhenChanged(static v => v.ViewModel);
        var commitKey = new KeyBinding { Gesture = new(Key.Enter, KeyModifiers.Control) };
        PageTextEditor.KeyBindings.Add(commitKey);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.EditingText, static v => v.PageTextEditor.Text, static text => text, static text => text ?? string.Empty));
            var edits = this.WhenChanged(static v => v.ViewModel!.Annotations.TextEdit);
            disposables.Add(edits.SubscribeSafe(ShowPageTextEditor, ErrorTrace.Write));

            // The editor takes the keyboard once it is placed and the press that started the text has finished.
            disposables.Add(edits
                .KeepNotNull()
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .SelectWith(PageTextEditor, static (editor, _) => editor)
                .FocusAtEnd());
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Annotations.CurrentTextFormat, static v => v.ViewModel!.Zoom, static v => v.ViewModel!.Rotation, static (format, _, _) => format)
                .AsVoid
                .Merge(Pages!.WhenChanged(static c => c.PageScale).AsVoid)
                .SubscribeSafe(_ => RestylePageTextEditor(), ErrorTrace.Write));
            disposables.Add(PageTextEditor.KeyDowns.Inject(tabs).SubscribeSafe(HandleKey, ErrorTrace.Write));

            // The editor handles Ctrl+Enter as a new line before any handler sees it; a key binding runs first.
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Annotations.FinishTextCommand).Select(static command => (ICommand?)command).BindTo(commitKey, static key => key.Command));
            disposables.Add(PageTextEditor.LostFocuses
                .Where(static _ => !FocusAnnouncementRepair.IsRepeating)
                .WhereWith(this, static (editor, _) => editor.FocusLeftTheText())
                .InjectLatest(tabs)
                .SubscribeSafe(static tab => tab.Annotations.CommitText(), ErrorTrace.Write));
            disposables.Add(PageTextEditor.TextChanges.SubscribeSafe(_ => PageTextUnderline.InvalidateVisual(), ErrorTrace.Write));
        });
    }

    /// <summary>Gets or sets the pages the editor sits over, which place and scale it.</summary>
    internal PageCanvas? Pages { get; set; }

    /// <summary>Gets the text alignment of the editor.</summary>
    /// <param name="alignment">The text box alignment.</param>
    /// <returns>The editor's alignment.</returns>
    private static TextAlignment EditorAlignment(TextBoxAlignment alignment) => alignment switch
    {
        TextBoxAlignment.Center => TextAlignment.Center,
        TextBoxAlignment.Right => TextAlignment.Right,
        _ => TextAlignment.Left,
    };

    /// <summary>Recolours text the way the page's colour tone recolours the page, so typed text looks as it will once written.</summary>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="tone">The page tone.</param>
    /// <returns>The toned colour.</returns>
    private static uint Toned(uint color, Core.Rendering.PageTone tone)
    {
        Span<byte> pixel = [(byte)color, (byte)(color >> GreenShift), (byte)(color >> RedShift), byte.MaxValue];
        tone.Apply(pixel);
        return ((uint)pixel[RedByte] << RedShift) | ((uint)pixel[GreenByte] << GreenShift) | pixel[0];
    }

    /// <summary>Runs an action for a key press.</summary>
    /// <param name="action">The action.</param>
    /// <returns><see langword="true"/>, so the key is handled.</returns>
    private static bool Run(Action action)
    {
        action();
        return true;
    }

    /// <summary>Handles the editor's keys: keeping or cancelling the text, and the style shortcuts.</summary>
    /// <param name="press">The key press and the tab being edited.</param>
    private void HandleKey(Injected<KeyEventArgs, DocumentTabViewModel> press)
    {
        var (e, tab) = press;
        var annotations = tab.Annotations;
        var control = (e.KeyModifiers & KeyModifiers.Control) != 0;
        e.Handled = new Shortcut(e.Key, control) switch
        {
            { Key: Key.Escape, Control: false } => Run(annotations.CancelText),
            { Key: Key.Tab, Control: false } => Run(() => annotations.CommitText()),
            { Key: Key.Enter, Control: false } when !PageTextEditor.AcceptsReturn => Run(() => annotations.CommitText()),
            { Key: Key.B, Control: true } => Run(() => annotations.IsBold = !annotations.IsBold),
            { Key: Key.I, Control: true } => Run(() => annotations.IsItalic = !annotations.IsItalic),
            { Key: Key.U, Control: true } => Run(() => annotations.IsUnderline = !annotations.IsUnderline),
            _ => false,
        };
    }

    /// <summary>
    /// Gets whether the focus moved elsewhere in the window, but not to the text format row or a dialog, which keeps the typed text.
    /// </summary>
    /// <returns><see langword="true"/> when the typed text should be kept.</returns>
    private bool FocusLeftTheText()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        return focused is Visual visual
            && ReferenceEquals(TopLevel.GetTopLevel(visual), TopLevel.GetTopLevel(this))
            && visual.FindAncestorOfType<TextFormatBarView>() is null
            && !(visual is ILogical logical && logical.FindLogicalAncestorOfType<TextFormatBarView>() is not null);
    }

    /// <summary>Places the editor where the text box is, or hides it.</summary>
    /// <param name="edit">The text being typed, or <see langword="null"/>.</param>
    private void ShowPageTextEditor(TextEditSession? edit)
    {
        if (edit is null)
        {
            var hadFocus = PageTextEditor.IsKeyboardFocusWithin;
            PageTextEditor.IsVisible = false;
            if (hadFocus)
            {
                _ = Pages?.Focus();
            }

            return;
        }

        // Shown first, so the editor lays its text out and its baseline can be lined up with the written one.
        PageTextEditor.IsVisible = true;
        RestylePageTextEditor();
    }

    /// <summary>Gives the editor the text's font, size, colour, style, spacing and place at the current zoom and rotation.</summary>
    private void RestylePageTextEditor()
    {
        if (ViewModel is not { Annotations.TextEdit: { } edit } tab || Pages is not { } pages)
        {
            return;
        }

        var format = tab.Annotations.CurrentTextFormat;
        var scale = pages.PageScale;
        var editor = PageTextEditor;
        editor.FontFamily = PageFonts.Get(format.FontFamily);
        editor.FontSize = format.FontSize * scale;
        editor.FontWeight = format.IsBold ? FontWeight.Bold : FontWeight.Normal;
        editor.FontStyle = format.IsItalic ? FontStyle.Italic : FontStyle.Normal;
        editor.TextAlignment = EditorAlignment(format.Alignment);
        editor.LineHeight = format.FontSize * format.LineSpacing * scale;
        editor.Foreground = ColourBrushes.Opaque(Toned(format.Color, tab.PageTone));
        editor.Background = PaperBrush.Get(tab.PageTone);
        editor.TextWrapping = edit.WrapWidth > 0 ? TextWrapping.Wrap : TextWrapping.NoWrap;
        editor.Width = edit.WrapWidth > 0 ? edit.WrapWidth * scale : double.NaN;
        editor.MinWidth = Math.Max(MinEditorWidth, format.FontSize * scale);
        editor.MaxLength = format.CombCells;
        editor.AcceptsReturn = format.CombCells == 0;
        SpacePageTextLetters(format, edit.WrapWidth * scale, scale);
        PlacePageTextEditor(edit, pages, tab.Rotation, (tab.Annotations.GetTypedTextBaseline() * scale) - EditorBaseline());
        PageTextUnderline.IsVisible = format.IsUnderline;
        PageTextUnderline.InvalidateVisual();
    }

    /// <summary>
    /// Spaces the editor's letters as they will be written: by the character spacing, or for a row of character boxes,
    /// one letter in the middle of each box, as the written text is laid out.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <param name="width">The text box's width on the canvas.</param>
    /// <param name="scale">The scale of the page on the canvas.</param>
    private void SpacePageTextLetters(TextFormat format, double width, double scale)
    {
        var editor = PageTextEditor;
        if (format.CombCells <= 0 || width <= 0)
        {
            editor.LetterSpacing = format.CharacterSpacing * scale;
            editor.ClearValue(TextBox.PaddingProperty);
            return;
        }

        var typeface = new Typeface(editor.FontFamily, editor.FontStyle, editor.FontWeight);
        var advance = new FormattedText("0", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, editor.FontSize, null).WidthIncludingTrailingWhitespace;
        var cell = width / format.CombCells;
        editor.TextAlignment = TextAlignment.Left;
        editor.TextWrapping = TextWrapping.NoWrap;
        editor.LetterSpacing = cell - advance;
        editor.Padding = new((cell - advance) * HalfCell, 0, 0, 0);

        // The spacing also follows the last letter; room for it and the border keeps the row from scrolling sideways.
        editor.Width = width + (cell - advance) + editor.BorderThickness.Left + editor.BorderThickness.Right;
    }

    /// <summary>
    /// Gets how far below the editor's top its first baseline is drawn. The editor lays text out with the screen font's
    /// own metrics, which may differ from those of the font the text is written in.
    /// </summary>
    /// <returns>The baseline's depth in device independent pixels, or <see cref="double.NaN"/> before the editor has a layout.</returns>
    private double EditorBaseline()
    {
        var editor = PageTextEditor;
        editor.ApplyTemplate();
        editor.Measure(Size.Infinity);
        return SpellingUnderlines.FindPresenter(editor) is { TextLayout.TextLines: [var first, ..] }
            ? editor.BorderThickness.Top + editor.Padding.Top + first.Baseline
            : double.NaN;
    }

    /// <summary>
    /// Moves the editor to the text box's top-left corner, shifted so its text sits where it will be written, and turns
    /// it with the page.
    /// </summary>
    /// <param name="edit">The text being typed.</param>
    /// <param name="pages">The pages the text is typed on.</param>
    /// <param name="rotation">The page rotation.</param>
    /// <param name="drop">How far the editor moves down its own lines so its baseline meets the written one, or NaN to leave it.</param>
    private void PlacePageTextEditor(TextEditSession edit, PageCanvas pages, PageRotation rotation, double drop)
    {
        var anchor = pages.PageToCanvas(edit.Page, edit.Location);
        var turns = rotation switch
        {
            PageRotation.Rotate90 => 1,
            PageRotation.Rotate180 => HalfTurn,
            PageRotation.Rotate270 => ThreeQuarterTurns,
            _ => 0,
        };

        // The border sits outside the text, so the editor also moves back by its width; both shifts turn with the page.
        var across = -PageTextEditor.BorderThickness.Left;
        var down = double.IsFinite(drop) ? drop : 0;
        var shift = turns switch
        {
            1 => new Point(-down, across),
            HalfTurn => new Point(-across, -down),
            ThreeQuarterTurns => new Point(down, -across),
            _ => new Point(across, down),
        };
        Canvas.SetLeft(PageTextEditor, anchor.X + shift.X);
        Canvas.SetTop(PageTextEditor, anchor.Y + shift.Y);
        Canvas.SetLeft(PageTextUnderline, anchor.X);
        Canvas.SetTop(PageTextUnderline, anchor.Y);
        PageTextEditor.RenderTransform = turns == 0 ? null : new RotateTransform(turns * QuarterTurn);
        PageTextUnderline.RenderTransform = PageTextEditor.RenderTransform;
    }

    /// <summary>A key and whether Ctrl is held with it.</summary>
    /// <param name="Key">The key.</param>
    /// <param name="Control">Whether Ctrl is held.</param>
    private readonly record struct Shortcut(Key Key, bool Control);
}
