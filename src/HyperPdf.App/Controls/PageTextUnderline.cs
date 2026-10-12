// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HyperPdf.App.Controls;

/// <summary>
/// Draws the underline of text typed on the page, under each line of the editor it lies over, in the text's colour.
/// Text boxes cannot underline their own text, so this shows it as it will be written.
/// </summary>
[DebuggerDisplay("PageTextUnderline: {Editor}")]
public sealed class PageTextUnderline : Control
{
    /// <summary>The line's thickness as a share of the font size.</summary>
    private const double ThicknessShare = 0.05;

    /// <summary>How far below the baseline the line sits, as a share of the font size.</summary>
    private const double OffsetShare = 0.1;

    /// <summary>The thinnest line.</summary>
    private const double MinThickness = 1;

    /// <summary>Gets or sets the text box whose lines are underlined.</summary>
    public TextBox? Editor { get; set; }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Editor is not { IsVisible: true, Text.Length: > 0 } editor || editor.Foreground is not { } brush
            || SpellingUnderlines.FindPresenter(editor) is not { } presenter)
        {
            return;
        }

        var pen = new Pen(brush, Math.Max(MinThickness, editor.FontSize * ThicknessShare));
        var layout = presenter.TextLayout;
        var top = 0D;
        foreach (var line in layout.TextLines)
        {
            var baseline = top + line.Baseline + (editor.FontSize * OffsetShare);
            foreach (var rect in layout.HitTestTextRange(line.FirstTextSourceIndex, line.Length - line.NewLineLength))
            {
                if (rect.Width > 0 && presenter.TranslatePoint(new(rect.Left, baseline), this) is { } start
                    && presenter.TranslatePoint(new(rect.Right, baseline), this) is { } end)
                {
                    context.DrawLine(pen, start, end);
                }
            }

            top += line.Height;
        }
    }
}
