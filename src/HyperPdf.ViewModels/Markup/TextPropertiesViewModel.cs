// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Text;
using ReactiveUI;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>
/// The text properties dialog: every setting of a text box in one place, with exact numbers, for when the tool bar's
/// choices are not enough. Typing is done on the page; this dialog is optional.
/// </summary>
[DebuggerDisplay("TextPropertiesViewModel: {FontFamily} {FontSize}")]
public sealed partial class TextPropertiesViewModel : ReactiveObject
{
    /// <summary>Initializes a new instance of the <see cref="TextPropertiesViewModel"/> class.</summary>
    /// <param name="text">The text.</param>
    /// <param name="format">The format.</param>
    /// <param name="wrapWidth">The wrap width in points, or 0.</param>
    /// <param name="families">The font families offered.</param>
    public TextPropertiesViewModel(string text, TextFormat format, float wrapWidth, IReadOnlyList<string> families)
    {
        ArgumentNullException.ThrowIfNull(format);
        FontFamilies = families;
        Text = text;
        FontFamily = format.FontFamily;
        FontSize = (decimal)format.FontSize;
        IsBold = format.IsBold;
        IsItalic = format.IsItalic;
        IsUnderline = format.IsUnderline;
        AlignmentIndex = (int)format.Alignment;
        LineSpacing = (decimal)format.LineSpacing;
        CharacterSpacing = (decimal)format.CharacterSpacing;
        CombCells = format.CombCells;
        WrapWidth = (decimal)wrapWidth;
        Color = format.Color;
        ColorIndex = IndexOfColor(format.Color);
        Answered = Signal.Merge(AcceptCommand, CancelCommand);
    }

    /// <summary>Gets the alignments offered, in <see cref="TextBoxAlignment"/> order.</summary>
    public static IReadOnlyList<string> Alignments { get; } = ["Left", "Centre", "Right"];

    /// <summary>Gets the colours offered, by name.</summary>
    public static IReadOnlyList<string> ColorNames { get; } = Names();

    /// <summary>Gets the font families offered.</summary>
    public IReadOnlyList<string> FontFamilies { get; }

    /// <summary>Gets the answer: whether the settings were accepted.</summary>
    public IObservable<bool> Answered { get; }

    /// <summary>Gets or sets the text.</summary>
    [Reactive]
    public partial string Text { get; set; }

    /// <summary>Gets or sets the font family.</summary>
    [Reactive]
    public partial string FontFamily { get; set; }

    /// <summary>Gets or sets the font size in points.</summary>
    [Reactive]
    public partial decimal FontSize { get; set; }

    /// <summary>Gets or sets a value indicating whether the text is bold.</summary>
    [Reactive]
    public partial bool IsBold { get; set; }

    /// <summary>Gets or sets a value indicating whether the text is italic.</summary>
    [Reactive]
    public partial bool IsItalic { get; set; }

    /// <summary>Gets or sets a value indicating whether the text is underlined.</summary>
    [Reactive]
    public partial bool IsUnderline { get; set; }

    /// <summary>Gets or sets the alignment, as an index into <see cref="Alignments"/>.</summary>
    [Reactive]
    public partial int AlignmentIndex { get; set; }

    /// <summary>Gets or sets the line height as a multiple of the size.</summary>
    [Reactive]
    public partial decimal LineSpacing { get; set; }

    /// <summary>Gets or sets the extra space after each character, in points.</summary>
    [Reactive]
    public partial decimal CharacterSpacing { get; set; }

    /// <summary>Gets or sets the number of comb boxes, or 0 for running text.</summary>
    [Reactive]
    public partial decimal CombCells { get; set; }

    /// <summary>Gets or sets the width lines wrap at, in points, or 0 for a box that grows with its text.</summary>
    [Reactive]
    public partial decimal WrapWidth { get; set; }

    /// <summary>Gets or sets the colour, as an index into <see cref="ColorNames"/>, or -1 for another colour.</summary>
    [Reactive]
    public partial int ColorIndex { get; set; }

    /// <summary>Gets the application font families used by the dialog's previews.</summary>
    public PageFontFamilies? PageFontFamilies { get; init; }

    /// <summary>Gets the colour as 0xRRGGBB, kept when it is not one of the colours offered.</summary>
    private uint Color { get; }

    /// <summary>Gets the chosen colour: the one offered at <see cref="ColorIndex"/>, or the text's own colour.</summary>
    private uint ChosenColor => (uint)ColorIndex < (uint)AnnotationsViewModel.TextColors.Count ? AnnotationsViewModel.TextColors[ColorIndex].Color : Color;

    /// <summary>Builds the format the settings describe.</summary>
    /// <returns>The format, clamped to usable values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TextFormat ToFormat() => new TextFormat(FontFamily, (float)FontSize, ChosenColor)
    {
        IsBold = IsBold,
        IsItalic = IsItalic,
        IsUnderline = IsUnderline,
        Alignment = (TextBoxAlignment)Math.Clamp(AlignmentIndex, 0, Alignments.Count - 1),
        LineSpacing = (float)LineSpacing,
        CharacterSpacing = (float)CharacterSpacing,
        CombCells = (int)CombCells,
    }.Clamped();

    /// <summary>Lists the names of the colours offered.</summary>
    /// <returns>The names.</returns>
    private static string[] Names()
    {
        var names = new string[AnnotationsViewModel.TextColors.Count];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = AnnotationsViewModel.TextColors[i].Name;
        }

        return names;
    }

    /// <summary>Finds a colour among those offered.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>Its index, or -1.</returns>
    private static int IndexOfColor(uint color)
    {
        for (var i = 0; i < AnnotationsViewModel.TextColors.Count; i++)
        {
            if (AnnotationsViewModel.TextColors[i].Color == color)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Gives up without changing the text.</summary>
    /// <returns>Always <see langword="false"/>.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Cancel() => false;

    /// <summary>Accepts the settings.</summary>
    /// <returns>Always <see langword="true"/>.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Accept() => true;
}
