// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Text;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Views;

/// <summary>
/// Typing on the page: the editor that sits where text is written, in the text's own font, size and colour, and the
/// text format row that changes it as it is typed.
/// </summary>
public sealed partial class DocumentView
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

    /// <summary>Gets the key binding that keeps the typed text on Ctrl+Enter.</summary>
    internal KeyBinding CommitTextKey { get; } = new() { Gesture = new(Key.Enter, KeyModifiers.Control) };

    /// <summary>Formats a text size for the size box.</summary>
    /// <param name="size">The size in points.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string SizeText(float size) => size.ToString("0.#", CultureInfo.CurrentCulture);

    /// <summary>Gets the text alignment of the editor.</summary>
    /// <param name="alignment">The text box alignment.</param>
    /// <returns>The editor's alignment.</returns>
    private static TextAlignment EditorAlignment(TextBoxAlignment alignment) => alignment switch
    {
        TextBoxAlignment.Center => TextAlignment.Center,
        TextBoxAlignment.Right => TextAlignment.Right,
        _ => TextAlignment.Left,
    };

    /// <summary>Runs an action for a key press.</summary>
    /// <param name="action">The action.</param>
    /// <returns><see langword="true"/>, so the key is handled.</returns>
    private static bool Run(Action action)
    {
        action();
        return true;
    }

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

    /// <summary>Makes the brush of a colour swatch.</summary>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>The brush.</returns>
    private static SolidColorBrush Swatch(uint color) => new(Color.FromUInt32(OpaqueAlpha | color));

    /// <summary>Sets up the text format row's fixed choices.</summary>
    private void SetUpTextFormat()
    {
        FontFamilyBox.ItemTemplate = new FuncDataTemplate<string>(static (name, _) => new TextBlock { Text = PageFonts.Label(name), FontFamily = PageFonts.Get(name) });
        var sizes = new string[AnnotationsViewModel.TextSizes.Count];
        for (var i = 0; i < sizes.Length; i++)
        {
            sizes[i] = SizeText(AnnotationsViewModel.TextSizes[i]);
        }

        FontSizeBox.ItemsSource = sizes;
        FieldLabels.Link((FontFamilyBox, FontFamilyLabel), (FontSizeBox, FontSizeLabel));
    }

    /// <summary>Binds the text format row.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindTextFormat(MultipleDisposable bindings)
    {
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsTextFormatShown, static v => v.TextFormatBar.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.FontFamilies, static v => v.FontFamilyBox.ItemsSource));
        bindings.Add(this.Bind(
            ViewModel,
            static vm => vm.Annotations.FontFamily,
            static v => v.FontFamilyBox.SelectedItem,
            static family => family,
            static item => item as string ?? StandardFontFamilies.Sans));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.FontSize, static v => v.FontSizeBox.Text, SizeText));
        bindings.Add(FontSizeBox.Entries.SubscribeSafe(ApplyFontSize, OnError));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.ShrinkTextCommand, static v => v.SmallerTextButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.GrowTextCommand, static v => v.LargerTextButton));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsBold, static v => v.BoldToggle.IsChecked, static on => on, static on => on == true));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsItalic, static v => v.ItalicToggle.IsChecked, static on => on, static on => on == true));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsUnderline, static v => v.UnderlineToggle.IsChecked, static on => on, static on => on == true));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsAlignedLeft, static v => v.AlignLeftToggle.IsChecked, static on => on, static on => on == true));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsAlignedCenter, static v => v.AlignCenterToggle.IsChecked, static on => on, static on => on == true));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsAlignedRight, static v => v.AlignRightToggle.IsChecked, static on => on, static on => on == true));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.TextColorName, static v => v.TextColourText.Text));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.TextColor, static v => v.TextColourSwatch.Background, Swatch));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetTextColorCommand, static v => v.BlackTextItem, "Black".AsCommandParameter));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetTextColorCommand, static v => v.DarkBlueTextItem, "Dark blue".AsCommandParameter));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetTextColorCommand, static v => v.BlueTextItem, "Blue".AsCommandParameter));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetTextColorCommand, static v => v.RedTextItem, "Red".AsCommandParameter));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetTextColorCommand, static v => v.GreenTextItem, "Green".AsCommandParameter));
        BindSpacing(bindings);
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.EditTextPropertiesCommand, static v => v.TextPropertiesButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.FinishTextCommand, static v => v.FinishTextButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.IsEditingText, static v => v.FinishTextButton.IsEnabled));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsTextTool, static v => v.AddTextTool.IsChecked, static on => on, static on => on == true));
        bindings.Add(this.WhenChanged(static v => v.ViewModel!.Annotations.TextPropertiesInteraction).HandleWith(ShowTextPropertiesAsync));
    }

    /// <summary>Binds the spacing menu.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindSpacing(MultipleDisposable bindings)
    {
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.SpacingName, static v => v.SpacingText.Text));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetLineSpacingCommand, static v => v.SingleSpacingItem, "Single".AsCommandParameter));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetLineSpacingCommand, static v => v.NormalSpacingItem, "Normal".AsCommandParameter));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetLineSpacingCommand, static v => v.OneHalfSpacingItem, "One and a half".AsCommandParameter));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetLineSpacingCommand, static v => v.DoubleSpacingItem, "Double".AsCommandParameter));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetCharacterSpacingCommand, static v => v.TightLettersItem, "Tight".AsCommandParameter));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetCharacterSpacingCommand, static v => v.NormalLettersItem, "Normal".AsCommandParameter));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetCharacterSpacingCommand, static v => v.WideLettersItem, "Wide".AsCommandParameter));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetCharacterSpacingCommand, static v => v.WiderLettersItem, "Wider".AsCommandParameter));
    }

    /// <summary>Binds the editor that sits on the page where text is typed.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindPageTextEditor(MultipleDisposable bindings)
    {
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.EditingText, static v => v.PageTextEditor.Text, static text => text, static text => text ?? string.Empty));
        bindings.Add(this.WhenChanged(static v => v.ViewModel!.Annotations.TextEdit).SubscribeSafe(ShowPageTextEditor, OnError));
        bindings.Add(this.WhenChanged(static v => v.ViewModel!.Annotations.CurrentTextFormat, static v => v.ViewModel!.Zoom, static v => v.ViewModel!.Rotation, static (format, _, _) => format)
            .AsVoid
            .Merge(Canvas.WhenChanged(static c => c.PageScale).AsVoid)
            .SubscribeSafe(_ => RestylePageTextEditor(), OnError));
        bindings.Add(PageTextEditor.KeyDowns.SubscribeSafe(OnPageTextKey, OnError));

        // The editor handles Ctrl+Enter as a new line before any handler sees it; a key binding runs first.
        bindings.Add(this.WhenChanged(static v => v.ViewModel!.Annotations.FinishTextCommand).Select(static command => (ICommand?)command).BindTo(this, static v => v.CommitTextKey.Command));
        bindings.Add(PageTextEditor.LostFocuses.Where(static _ => !FocusAnnouncementRepair.IsRepeating).SubscribeSafe(_ => OnPageTextFocusLost(), OnError));
        bindings.Add(FontFamilyBox.DropDownOpenings.InjectLatest(_tabs).SubscribeSafe(static tab => tab.Annotations.RefreshFontFamilies(), OnError));
        bindings.Add(PageTextEditor.TextChanges.SubscribeSafe(_ => PageTextUnderline.InvalidateVisual(), OnError));

        // A format change puts the focus back in the editor once the click is done, so typing carries on.
        bindings.Add(Signal.Merge(FontFamilyBox.SelectionChanges.AsVoid, TextFormatBar.DescendantClicks.AsVoid)
            .WithLatestFrom(
                this.WhenChanged(static v => v.ViewModel!.Annotations.IsEditingText, static v => v.PageTextEditor.IsVisible, static (editing, shown) => editing && shown),
                static (_, typing) => typing)
            .Where(static typing => typing)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeSafe(_ => PageTextEditor.Focus(), OnError));
    }

    /// <summary>Sets the text size from what was typed or picked in the size box.</summary>
    /// <param name="text">The size box's text.</param>
    private void ApplyFontSize(string? text)
    {
        if (ViewModel is not { } tab || !float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var size) || !float.IsFinite(size))
        {
            return;
        }

        tab.Annotations.FontSize = Math.Clamp(size, TextFormat.MinFontSize, TextFormat.MaxFontSize);
        FontSizeBox.Text = SizeText(tab.Annotations.FontSize);
    }

    /// <summary>Places the editor where the text box is and focuses it, or hides it.</summary>
    /// <param name="edit">The text being typed, or <see langword="null"/>.</param>
    private void ShowPageTextEditor(TextEditSession? edit)
    {
        if (edit is null)
        {
            var hadFocus = PageTextEditor.IsKeyboardFocusWithin;
            PageTextEditor.IsVisible = false;
            if (hadFocus)
            {
                _ = Canvas.Focus();
            }

            return;
        }

        // Shown first, so the editor lays its text out and its baseline can be lined up with the written one.
        PageTextEditor.IsVisible = true;
        RestylePageTextEditor();
        _ = Signal.Return(RxVoid.Default).ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(_ => FocusPageText(), OnError);
    }

    /// <summary>Focuses the editor with the caret after the text.</summary>
    private void FocusPageText()
    {
        _ = PageTextEditor.Focus();
        PageTextEditor.CaretIndex = PageTextEditor.Text?.Length ?? 0;
    }

    /// <summary>Gives the editor the text's font, size, colour, style, spacing and place at the current zoom and rotation.</summary>
    private void RestylePageTextEditor()
    {
        if (ViewModel is not { Annotations.TextEdit: { } edit } tab)
        {
            return;
        }

        var format = tab.Annotations.CurrentTextFormat;
        var scale = Canvas.PageScale;
        var editor = PageTextEditor;
        editor.FontFamily = PageFonts.Get(format.FontFamily);
        editor.FontSize = format.FontSize * scale;
        editor.FontWeight = format.IsBold ? FontWeight.Bold : FontWeight.Normal;
        editor.FontStyle = format.IsItalic ? FontStyle.Italic : FontStyle.Normal;
        editor.TextAlignment = EditorAlignment(format.Alignment);
        editor.LineHeight = format.FontSize * format.LineSpacing * scale;
        editor.Foreground = new SolidColorBrush(Color.FromUInt32(OpaqueAlpha | Toned(format.Color, tab.PageTone)));
        editor.Background = PaperBrush.Get(tab.PageTone);
        editor.TextWrapping = edit.WrapWidth > 0 ? TextWrapping.Wrap : TextWrapping.NoWrap;
        editor.Width = edit.WrapWidth > 0 ? edit.WrapWidth * scale : double.NaN;
        editor.MinWidth = Math.Max(MinEditorWidth, format.FontSize * scale);
        editor.MaxLength = format.CombCells;
        editor.AcceptsReturn = format.CombCells == 0;
        SpacePageTextLetters(format, edit.WrapWidth * scale);
        PlacePageTextEditor(edit, tab.Rotation, (tab.Annotations.GetTypedTextBaseline() * scale) - EditorBaseline());
        PageTextUnderline.IsVisible = format.IsUnderline;
        PageTextUnderline.InvalidateVisual();
    }

    /// <summary>
    /// Spaces the editor's letters as they will be written: by the character spacing, or for a row of character boxes,
    /// one letter in the middle of each box, as the written text is laid out.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <param name="width">The text box's width on the canvas.</param>
    private void SpacePageTextLetters(TextFormat format, double width)
    {
        var editor = PageTextEditor;
        if (format.CombCells <= 0 || width <= 0)
        {
            editor.LetterSpacing = format.CharacterSpacing * Canvas.PageScale;
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
    /// <param name="rotation">The page rotation.</param>
    /// <param name="drop">How far the editor moves down its own lines so its baseline meets the written one, or NaN to leave it.</param>
    private void PlacePageTextEditor(TextEditSession edit, PageRotation rotation, double drop)
    {
        var anchor = Canvas.PageToCanvas(edit.Page, edit.Location);
        var turns = rotation switch
        {
            PageRotation.Rotate90 => 1,
            PageRotation.Rotate180 => HalfTurn,
            PageRotation.Rotate270 => ThreeQuarterTurns,
            _ => 0,
        };

        // The border sits outside the text, so the editor also moves back by its width; both shifts turn with the page.
        var (across, down) = (-PageTextEditor.BorderThickness.Left, double.IsFinite(drop) ? drop : 0);
        var shift = turns switch
        {
            1 => new Point(-down, across),
            HalfTurn => new Point(-across, -down),
            ThreeQuarterTurns => new Point(down, -across),
            _ => new Point(across, down),
        };
        Avalonia.Controls.Canvas.SetLeft(PageTextEditor, anchor.X + shift.X);
        Avalonia.Controls.Canvas.SetTop(PageTextEditor, anchor.Y + shift.Y);
        Avalonia.Controls.Canvas.SetLeft(PageTextUnderline, anchor.X);
        Avalonia.Controls.Canvas.SetTop(PageTextUnderline, anchor.Y);
        PageTextEditor.RenderTransform = turns == 0 ? null : new RotateTransform(turns * QuarterTurn);
        PageTextUnderline.RenderTransform = PageTextEditor.RenderTransform;
    }

    /// <summary>Handles the editor's keys: keeping or cancelling the text, and the style shortcuts.</summary>
    /// <param name="e">The key press.</param>
    private void OnPageTextKey(KeyEventArgs e)
    {
        if (ViewModel?.Annotations is not { } annotations)
        {
            return;
        }

        var control = (e.KeyModifiers & KeyModifiers.Control) != 0;
        e.Handled = (e.Key, control) switch
        {
            (Key.Escape, false) => Run(annotations.CancelText),
            (Key.Tab, false) => Run(() => annotations.CommitText()),
            (Key.Enter, false) when !PageTextEditor.AcceptsReturn => Run(() => annotations.CommitText()),
            (Key.B, true) => Run(() => annotations.IsBold = !annotations.IsBold),
            (Key.I, true) => Run(() => annotations.IsItalic = !annotations.IsItalic),
            (Key.U, true) => Run(() => annotations.IsUnderline = !annotations.IsUnderline),
            _ => false,
        };
    }

    /// <summary>Keeps the typed text when the focus moves elsewhere in the window, but not to the text format row or a dialog.</summary>
    private void OnPageTextFocusLost()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is not Visual visual || !ReferenceEquals(TopLevel.GetTopLevel(visual), TopLevel.GetTopLevel(this))
            || TextFormatBar.IsVisualAncestorOf(visual) || (visual is ILogical logical && TextFormatBar.IsLogicalAncestorOf(logical)))
        {
            return;
        }

        _ = ViewModel?.Annotations.CommitText();
    }

    /// <summary>Shows the text properties window.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ShowTextPropertiesAsync(IInteractionContext<TextPropertiesViewModel, bool> context)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            context.SetOutput(false);
            return;
        }

        context.SetOutput(await new TextPropertiesWindow { ViewModel = context.Input }.ShowDialog<bool>(owner));
    }
}
