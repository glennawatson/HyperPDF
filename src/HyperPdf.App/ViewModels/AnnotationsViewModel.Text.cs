// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.App.Services;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Text;
using HyperPdf.Core.Text.Fonts;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// Typing on the page. Clicking with the Text tool starts a box with a caret straight away; double-clicking a text box
/// edits it where it is. The text format (font, size, colour, bold, italic, underline, alignment and spacing) shows
/// what is being typed or what is picked, and changing it changes that text at once. Committing writes the text;
/// editing a text box replaces it, so one undo puts the old text back.
/// </summary>
public sealed partial class AnnotationsViewModel
{
    /// <summary>How far an edit may move or widen a box, in points, and still count as the same place.</summary>
    private const float PlaceTolerance = 0.5F;

    /// <summary>The step the size buttons change text by, in points.</summary>
    private const float SizeStep = 1;

    /// <summary>Black, the default text colour.</summary>
    private const uint Black = 0x000000;

    /// <summary>The blue offered for text.</summary>
    private const uint TextBlue = 0x1F5FBF;

    /// <summary>The red offered for text.</summary>
    private const uint TextRed = 0xB3261E;

    /// <summary>The green offered for text.</summary>
    private const uint TextGreen = 0x2E7D32;

    /// <summary>One and a half line spacing.</summary>
    private const float OneAndAHalf = 1.5F;

    /// <summary>Double line spacing.</summary>
    private const float DoubleSpacing = 2F;

    /// <summary>Tight character spacing, in points.</summary>
    private const float TightSpacing = -0.5F;

    /// <summary>Wider character spacing, in points.</summary>
    private const float WiderSpacing = 2F;

    /// <summary>Halves a length.</summary>
    private const float Half = 0.5F;

    /// <summary>Whether the format is being loaded from text, so the change is not applied back to it.</summary>
    private bool _loadingFormat;

    /// <summary>Gets the text sizes offered in the size box, in points.</summary>
    public static IReadOnlyList<float> TextSizes { get; } = [6, 8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 72];

    /// <summary>Gets the text colours offered, by name.</summary>
    public static IReadOnlyList<TextColorOption> TextColors { get; } =
        [new(nameof(Black), Black), new("Dark blue", AnnotationColors.Ink), new("Blue", TextBlue), new("Red", TextRed), new("Green", TextGreen)];

    /// <summary>Gets the line spacings offered, by name.</summary>
    public static IReadOnlyList<LineSpacingOption> LineSpacings { get; } =
        [new(nameof(Single), 1F), new("Normal", TextFormat.DefaultLineSpacing), new("One and a half", OneAndAHalf), new(nameof(Double), DoubleSpacing)];

    /// <summary>Gets the character spacings offered, by name, in points.</summary>
    public static IReadOnlyList<LineSpacingOption> CharacterSpacings { get; } = [new("Tight", TightSpacing), new("Normal", 0F), new("Wide", 1F), new("Wider", WiderSpacing)];

    /// <summary>Gets the interaction showing the text properties dialog; the output says whether to apply it.</summary>
    public Interaction<TextPropertiesViewModel, bool> TextPropertiesInteraction { get; } = new();

    /// <summary>Gets or sets the font family of new and picked text.</summary>
    [Reactive(nameof(CurrentTextFormat))]
    public partial string FontFamily { get; set; } = StandardFontFamilies.Sans;

    /// <summary>Gets or sets a value indicating whether text is bold.</summary>
    [Reactive(nameof(CurrentTextFormat))]
    public partial bool IsBold { get; set; }

    /// <summary>Gets or sets a value indicating whether text is italic.</summary>
    [Reactive(nameof(CurrentTextFormat))]
    public partial bool IsItalic { get; set; }

    /// <summary>Gets or sets a value indicating whether text is underlined.</summary>
    [Reactive(nameof(CurrentTextFormat))]
    public partial bool IsUnderline { get; set; }

    /// <summary>Gets or sets how lines of text sit across their box.</summary>
    [Reactive(nameof(IsAlignedLeft), nameof(IsAlignedCenter), nameof(IsAlignedRight), nameof(CurrentTextFormat))]
    public partial TextBoxAlignment TextAlignment { get; set; }

    /// <summary>Gets or sets the line height as a multiple of the text size.</summary>
    [Reactive(nameof(SpacingName), nameof(CurrentTextFormat))]
    public partial float LineSpacing { get; set; } = TextFormat.DefaultLineSpacing;

    /// <summary>Gets or sets the extra space after each character, in points.</summary>
    [Reactive(nameof(SpacingName), nameof(CurrentTextFormat))]
    public partial float CharacterSpacing { get; set; }

    /// <summary>Gets or sets the number of comb boxes, one character each, or 0 for running text.</summary>
    [Reactive(nameof(CurrentTextFormat))]
    public partial int CombCells { get; set; }

    /// <summary>Gets or sets the text colour as 0xRRGGBB.</summary>
    [Reactive(nameof(TextColorName), nameof(CurrentTextFormat))]
    public partial uint TextColor { get; set; } = Black;

    /// <summary>Gets the families text can be written in: the built in three, then installed fonts once they are read.</summary>
    [Reactive]
    public partial IReadOnlyList<string> FontFamilies { get; private set; } = StandardFontFamilies.All;

    /// <summary>Gets the text being typed on the page, or <see langword="null"/>.</summary>
    [Reactive(nameof(IsEditingText))]
    public partial TextEditSession? TextEdit { get; private set; }

    /// <summary>Gets or sets the text in the on-page editor.</summary>
    [Reactive]
    public partial string EditingText { get; set; } = string.Empty;

    /// <summary>Gets a value indicating whether text is being typed on the page.</summary>
    public bool IsEditingText => TextEdit is not null;

    /// <summary>Gets or sets a value indicating whether lines start at the left.</summary>
    public bool IsAlignedLeft
    {
        get => TextAlignment == TextBoxAlignment.Left;
        set => Align(value, TextBoxAlignment.Left);
    }

    /// <summary>Gets or sets a value indicating whether lines are centred.</summary>
    public bool IsAlignedCenter
    {
        get => TextAlignment == TextBoxAlignment.Center;
        set => Align(value, TextBoxAlignment.Center);
    }

    /// <summary>Gets or sets a value indicating whether lines end at the right.</summary>
    public bool IsAlignedRight
    {
        get => TextAlignment == TextBoxAlignment.Right;
        set => Align(value, TextBoxAlignment.Right);
    }

    /// <summary>Gets the text colour's name.</summary>
    public string TextColorName => NameOf(TextColors, TextColor);

    /// <summary>Gets the spacing button's label: the line and character spacing by name.</summary>
    public string SpacingName => $"Spacing: {NameOf(LineSpacings, LineSpacing)}, {NameOf(CharacterSpacings, CharacterSpacing)}";

    /// <summary>Gets the format text is typed in now.</summary>
    public TextFormat CurrentTextFormat => new TextFormat(FontFamily, FontSize, TextColor)
    {
        IsBold = IsBold,
        IsItalic = IsItalic,
        IsUnderline = IsUnderline,
        Alignment = TextAlignment,
        LineSpacing = LineSpacing,
        CharacterSpacing = CharacterSpacing,
        CombCells = CombCells,
    }.Clamped();

    /// <summary>Gets the text box editor, or <see langword="null"/> when the document cannot hold text boxes.</summary>
    private ITextBoxEditor? TextBoxes => DocumentFeature<ITextBoxEditor>.Get(_owner.TryGetDocument());

    /// <summary>Gets how far below the top of the text being typed its first baseline will be written, in points.</summary>
    /// <returns>The baseline's depth, or <see cref="float.NaN"/> when it is not known.</returns>
    public float GetTypedTextBaseline() => TextBoxes?.GetFirstBaseline(EditingText ?? string.Empty, CurrentTextFormat) ?? float.NaN;

    /// <summary>Offers the installed font families, with or without the preview only ones as the preference says.</summary>
    public void RefreshFontFamilies()
    {
        var catalog = _owner.Services.FontCatalog;
        var families = _owner.ShowPreviewOnlyFonts ? catalog.AllFamilies : catalog.Families;
        if (!ReferenceEquals(families, FontFamilies))
        {
            FontFamilies = families;
        }
    }

    /// <summary>
    /// Starts typing new text with a caret at a point. The point is the middle of the first line, so the text sits
    /// where the click was; any text being typed elsewhere is kept first.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="point">The point clicked, in page space.</param>
    /// <param name="wrapWidth">The width lines wrap at, or 0 for a box that grows as text is typed.</param>
    /// <returns><see langword="true"/> when typing started.</returns>
    public bool BeginText(int page, PagePoint point, float wrapWidth)
    {
        _ = CommitText();
        if (DocumentFeature<ITextBoxEditor>.Get(DocumentForChange()) is not
            {
            })
        {
            return false;
        }

        // Text placed freely is running text, even after typing into a row of character boxes.
        _loadingFormat = true;
        CombCells = 0;
        _loadingFormat = false;
        var lineHeight = FontSize * LineSpacing;
        EditingText = string.Empty;
        TextEdit = new(page, new(point.X, point.Y - (lineHeight * Half)), Math.Max(wrapWidth, 0), null);
        return true;
    }

    /// <summary>Starts typing in a box of a given place and size, such as a field found on a flat form.</summary>
    /// <param name="page">The page.</param>
    /// <param name="location">The box's top-left corner.</param>
    /// <param name="wrapWidth">The box's width, or 0.</param>
    /// <returns><see langword="true"/> when typing started.</returns>
    public bool BeginTextAt(int page, PagePoint location, float wrapWidth)
    {
        _ = CommitText();
        if (DocumentFeature<ITextBoxEditor>.Get(DocumentForChange()) is not
            {
            })
        {
            return false;
        }

        EditingText = string.Empty;
        TextEdit = new(page, location, Math.Max(wrapWidth, 0), null);
        return true;
    }

    /// <summary>Edits a text box where it is: its text goes into the editor and its format into the format controls.</summary>
    /// <param name="annotation">The text box.</param>
    /// <returns><see langword="true"/> when editing started.</returns>
    public bool EditText(PageAnnotation? annotation)
    {
        if (annotation is not { Kind: AnnotationKind.TextBox } || ReferenceEquals(TextEdit?.Replacing, annotation))
        {
            return false;
        }

        _ = CommitText();
        if (DocumentFeature<ITextBoxEditor>.Get(DocumentForChange()) is not
            {
            } boxes || Find(annotation.PageIndex, annotation.Index) is not { } current
            || boxes.GetTextBox(current.PageIndex, current.Index) is not { } content)
        {
            return false;
        }

        LoadFormat(content.Format);
        Selected = current;
        EditingText = content.Text;
        TextEdit = new(current.PageIndex, new(content.Bounds.Left, content.Bounds.Top), content.WrapWidth, current);
        return true;
    }

    /// <summary>Writes the typed text and closes the editor. Blank new text writes nothing; blanking a text box removes it.</summary>
    /// <returns><see langword="true"/> when the document changed.</returns>
    public bool CommitText()
    {
        if (TextEdit is not { } edit)
        {
            return false;
        }

        var text = EditingText;
        TextEdit = null;
        EditingText = string.Empty;
        if (DocumentFeature<ITextBoxEditor>.Get(DocumentForChange()) is not
            {
            } boxes)
        {
            return false;
        }

        var format = CurrentTextFormat;
        if (edit.Replacing is { } old)
        {
            return !IsUnchanged(boxes, old, new(edit, text, format)) && Replace(old, new(edit.Location, edit.WrapWidth), text, format);
        }

        var index = string.IsNullOrWhiteSpace(text) ? -1 : boxes.AddTextBox(edit.Page, edit.Location, edit.WrapWidth, text, format);
        if (!Added(edit.Page, index))
        {
            return false;
        }

        Selected = Find(edit.Page, index);
        return true;
    }

    /// <summary>Closes the editor without changing the document.</summary>
    public void CancelText()
    {
        TextEdit = null;
        EditingText = string.Empty;
    }

    /// <summary>Applies the format controls to the picked text box, writing it again in the new format.</summary>
    /// <returns><see langword="true"/> when it changed.</returns>
    public bool ReformatSelected()
    {
        if (TextEdit is not null || Selected is not { Kind: AnnotationKind.TextBox } selected || TextBoxes is not { } boxes
            || boxes.GetTextBox(selected.PageIndex, selected.Index) is not { } content)
        {
            return false;
        }

        var format = CurrentTextFormat;
        return content.Format != format && Replace(selected, new(new(content.Bounds.Left, content.Bounds.Top), content.WrapWidth), content.Text, format);
    }

    /// <summary>Gets the value of a named choice, or a fallback.</summary>
    /// <param name="choices">The choices.</param>
    /// <param name="name">The name.</param>
    /// <param name="fallback">The fallback.</param>
    /// <returns>The value.</returns>
    private static float ValueOf(IReadOnlyList<LineSpacingOption> choices, string name, float fallback)
    {
        foreach (var (choiceName, value) in choices)
        {
            if (string.Equals(choiceName, name, StringComparison.Ordinal))
            {
                return value;
            }
        }

        return fallback;
    }

    /// <summary>Finds the name of a colour among named choices.</summary>
    /// <param name="choices">The choices.</param>
    /// <param name="color">The colour.</param>
    /// <returns>The name, or the colour in hexadecimal.</returns>
    private static string NameOf(IReadOnlyList<TextColorOption> choices, uint color)
    {
        foreach (var (name, choice) in choices)
        {
            if (choice == color)
            {
                return name;
            }
        }

        return string.Create(CultureInfo.InvariantCulture, $"#{color:X6}");
    }

    /// <summary>Finds the name of a line-spacing value among its choices.</summary>
    /// <param name="choices">The available choices.</param>
    /// <param name="value">The spacing value.</param>
    /// <returns>The matching name, or the value in points.</returns>
    private static string NameOf(IReadOnlyList<LineSpacingOption> choices, float value)
    {
        foreach (var choice in choices)
        {
            if (System.Math.Abs(choice.Value - value) < float.Epsilon)
            {
                return choice.Name;
            }
        }

        return $"{value:0.#} pt";
    }

    /// <summary>Determines whether an edit leaves a text box as it was.</summary>
    /// <param name="boxes">The text box editor.</param>
    /// <param name="old">The text box.</param>
    /// <param name="change">The edit, its text and format.</param>
    /// <returns><see langword="true"/> when nothing changed.</returns>
    private static bool IsUnchanged(ITextBoxEditor boxes, PageAnnotation old, TextEditChange change) =>
        boxes.GetTextBox(old.PageIndex, old.Index) is { } before
        && string.Equals(before.Text, change.Text, StringComparison.Ordinal)
        && before.Format == change.Format
        && Math.Abs(before.WrapWidth - change.Edit.WrapWidth) < PlaceTolerance;

    /// <summary>Moves a text box, or when its width changes, wraps its text to the new width.</summary>
    /// <param name="annotation">The text box.</param>
    /// <param name="bounds">The new bounds.</param>
    /// <returns><see langword="true"/> when it was written again; <see langword="false"/> when a plain move will do.</returns>
    private bool RewrapTextBox(PageAnnotation annotation, PageRect bounds)
    {
        if (annotation.Kind != AnnotationKind.TextBox || Math.Abs(bounds.Width - annotation.Bounds.Width) < PlaceTolerance
            || TextBoxes is not { } boxes || boxes.GetTextBox(annotation.PageIndex, annotation.Index) is not { } content)
        {
            return false;
        }

        return Replace(annotation, new(new(bounds.Left, bounds.Top), bounds.Width), content.Text, content.Format);
    }

    /// <summary>Makes text bigger by one point.</summary>
    [ReactiveCommand]
    private void GrowText() => FontSize = Math.Min(FontSize + SizeStep, TextFormat.MaxFontSize);

    /// <summary>Makes text smaller by one point.</summary>
    [ReactiveCommand]
    private void ShrinkText() => FontSize = Math.Max(FontSize - SizeStep, TextFormat.MinFontSize);

    /// <summary>Chooses a text colour by its name.</summary>
    /// <param name="name">The name, such as "Black".</param>
    [ReactiveCommand]
    private void SetTextColor(string name)
    {
        foreach (var (colorName, color) in TextColors)
        {
            if (!string.Equals(colorName, name, StringComparison.Ordinal))
            {
                continue;
            }

            TextColor = color;
            return;
        }
    }

    /// <summary>Chooses a line spacing by its name.</summary>
    /// <param name="name">The name, such as "Double".</param>
    [ReactiveCommand]
    private void SetLineSpacing(string name) => LineSpacing = ValueOf(LineSpacings, name, LineSpacing);

    /// <summary>Chooses a character spacing by its name.</summary>
    /// <param name="name">The name, such as "Wide".</param>
    [ReactiveCommand]
    private void SetCharacterSpacing(string name) => CharacterSpacing = ValueOf(CharacterSpacings, name, CharacterSpacing);

    /// <summary>Starts typing new text where the page was right-clicked.</summary>
    /// <param name="location">The page and point.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TypeTextHere(PageLocation location) => _ = BeginText(location.Page, location.Point, 0);

    /// <summary>Edits the picked text box in place.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EditSelectedText() => _ = EditText(Selected);

    /// <summary>
    /// Shows every setting of the text being typed, or of the picked text box, in the text properties dialog, and
    /// applies them when accepted.
    /// </summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task EditTextPropertiesAsync()
    {
        var edit = TextEdit;
        var target = edit is null ? Selected : null;
        var content = target is { Kind: AnnotationKind.TextBox } ? TextBoxes?.GetTextBox(target.PageIndex, target.Index) : null;
        var properties = PropertiesFor(edit, content);
        if (properties is null || !await TextPropertiesInteraction.Handle(properties).ToTask().ConfigureAwait(true))
        {
            return;
        }

        var format = properties.ToFormat();
        var wrap = (float)Math.Max(properties.WrapWidth, 0);
        LoadFormat(format);
        if (edit is not null)
        {
            EditingText = properties.Text;
            TextEdit = ReferenceEquals(TextEdit, edit) ? edit with { WrapWidth = wrap } : TextEdit;
            return;
        }

        _ = Replace(target!, new(new(content!.Bounds.Left, content.Bounds.Top), wrap), properties.Text, format);
    }

    /// <summary>Fills the text properties dialog from the text being typed, or else from a picked text box.</summary>
    /// <param name="edit">The text being typed, or <see langword="null"/>.</param>
    /// <param name="content">The picked text box, or <see langword="null"/>.</param>
    /// <returns>The dialog's settings, or <see langword="null"/> when there is no text to show.</returns>
    private TextPropertiesViewModel? PropertiesFor(TextEditSession? edit, TextBoxContent? content)
    {
        if (edit is not null)
        {
            return new(EditingText, CurrentTextFormat, edit.WrapWidth, FontFamilies) { PageFontFamilies = _owner.Services.PageFontFamilies };
        }

        return content is null ? null : new(content.Text, content.Format, content.WrapWidth, FontFamilies) { PageFontFamilies = _owner.Services.PageFontFamilies };
    }

    /// <summary>Keeps the typed text.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FinishText() => _ = CommitText();

    /// <summary>Reads the formats' changes and the pick, applying format changes to the picked text box.</summary>
    /// <returns>The subscriptions.</returns>
    private IDisposable[] WatchTextFormat() =>
    [

        // A format change reformats the picked text box, unless the change came from reading that box's format.
        Signal.Merge(
                this.WhenChanged(static x => x.FontFamily, static x => x.FontSize, static x => x.TextColor, static (_, _, _) => RxVoid.Default).Skip(1),
                this.WhenChanged(static x => x.IsBold, static x => x.IsItalic, static x => x.IsUnderline, static (_, _, _) => RxVoid.Default).Skip(1),
                this.WhenChanged(static x => x.TextAlignment, static x => x.LineSpacing, static x => x.CharacterSpacing, static (_, _, _) => RxVoid.Default).Skip(1),
                this.WhenChanged(static x => x.CombCells).Skip(1).Select(static _ => RxVoid.Default))
            .WhereWith(this, static (annotations, _) => !annotations._loadingFormat)
            .SubscribeSafe(_ => ReformatSelected(), ErrorTrace.Write),
        this.WhenChanged(static x => x.Selected).Skip(1).SubscribeSafe(LoadSelectedFormat, ErrorTrace.Write),
    ];

    /// <summary>Reads the installed font families off the UI thread, then offers them.</summary>
    /// <returns>A task.</returns>
    private async Task LoadFontFamiliesAsync()
    {
        _ = await Task.Run(() => _owner.Services.FontCatalog).ConfigureAwait(true);
        RefreshFontFamilies();
    }

    /// <summary>Shows a picked text box's format in the format controls.</summary>
    /// <param name="selected">The pick.</param>
    private void LoadSelectedFormat(PageAnnotation? selected)
    {
        if (TextEdit is null && selected is { Kind: AnnotationKind.TextBox } && TextBoxes?.GetTextBox(selected.PageIndex, selected.Index) is { } content)
        {
            LoadFormat(content.Format);
        }
    }

    /// <summary>Puts a format into the format controls without applying it back.</summary>
    /// <param name="format">The format.</param>
    private void LoadFormat(TextFormat format)
    {
        _loadingFormat = true;
        try
        {
            FontFamily = format.FontFamily;
            FontSize = format.FontSize;
            TextColor = format.Color;
            IsBold = format.IsBold;
            IsItalic = format.IsItalic;
            IsUnderline = format.IsUnderline;
            TextAlignment = format.Alignment;
            LineSpacing = format.LineSpacing;
            CharacterSpacing = format.CharacterSpacing;
            CombCells = format.CombCells;
        }
        finally
        {
            _loadingFormat = false;
        }
    }

    /// <summary>Replaces a text box with new text, format or place, as one undoable step.</summary>
    /// <param name="old">The text box.</param>
    /// <param name="place">The new box's top-left corner and wrap width.</param>
    /// <param name="text">The new text; blank text removes the box.</param>
    /// <param name="format">The new format.</param>
    /// <returns><see langword="true"/> when the document changed.</returns>
    private bool Replace(PageAnnotation old, TextBoxPlacement place, string text, TextFormat format)
    {
        var document = DocumentForChange();
        if (document is null
            || DocumentFeature<IAnnotationEditor>.Get(document) is not { } editor
            || DocumentFeature<ITextBoxEditor>.Get(document) is not { } boxes)
        {
            return false;
        }

        var page = old.PageIndex;
        BeginGroup();
        var changed = false;
        if (editor.SetRemoved(page, old.Index, true))
        {
            Record(new AnnotationDeleted(page, old.Index));
            changed = true;
        }

        var index = string.IsNullOrWhiteSpace(text) ? -1 : boxes.AddTextBox(page, place.Location, place.WrapWidth, text, format);
        changed |= Added(page, index);
        EndGroup();
        Selected = index >= 0 ? Find(page, index) : null;
        return changed;
    }

    /// <summary>Chooses an alignment, keeping one always chosen.</summary>
    /// <param name="on">Whether its button was turned on.</param>
    /// <param name="alignment">The alignment.</param>
    private void Align(bool on, TextBoxAlignment alignment)
    {
        if (on)
        {
            TextAlignment = alignment;
        }
    }
}
