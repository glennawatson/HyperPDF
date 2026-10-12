// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;
using HyperPdf.Core.Reading;

namespace HyperPdf.App.Controls;

/// <summary>Draws a steady wavy line under each misspelled word of a text box it lies over. It never blinks or moves on its own.</summary>
[DebuggerDisplay("SpellingUnderlines: {_ranges.Count} words")]
public sealed class SpellingUnderlines : Control
{
    /// <summary>Defines the <see cref="Stroke"/> property.</summary>
    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<SpellingUnderlines, IBrush?>(nameof(Stroke));

    /// <summary>The height of a wave.</summary>
    private const double WaveHeight = 2;

    /// <summary>The width of half a wave.</summary>
    private const double HalfWave = 2;

    /// <summary>The line's thickness.</summary>
    private const double Thickness = 1.2;

    /// <summary>The misspelled words.</summary>
    private readonly List<TextRange> _ranges = [];

    /// <summary>The pen, rebuilt when the stroke changes.</summary>
    private Pen? _pen;

    /// <summary>Initializes static members of the <see cref="SpellingUnderlines"/> class.</summary>
    static SpellingUnderlines() => AffectsRender<SpellingUnderlines>(StrokeProperty);

    /// <summary>Gets or sets the brush the lines are drawn with.</summary>
    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>Gets or sets the text box whose words are marked.</summary>
    public TextBox? Editor { get; set; }

    /// <summary>Gets the words marked, for tests and assistive descriptions.</summary>
    public IReadOnlyList<TextRange> Ranges => _ranges;

    /// <summary>Finds the part of a text box that lays out its text.</summary>
    /// <param name="editor">The text box.</param>
    /// <returns>The presenter, or <see langword="null"/> before the text box is shown.</returns>
    public static TextPresenter? FindPresenter(TextBox? editor)
    {
        if (editor is null)
        {
            return null;
        }

        foreach (var visual in editor.GetVisualDescendants())
        {
            if (visual is TextPresenter presenter)
            {
                return presenter;
            }
        }

        return null;
    }

    /// <summary>Marks these words, replacing any marked before.</summary>
    /// <param name="ranges">The misspelled words' places in the text.</param>
    public void Show(IReadOnlyList<TextRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        _ranges.Clear();
        _ranges.AddRange(ranges);
        InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_ranges.Count == 0 || Stroke is null || FindPresenter(Editor) is not { } presenter)
        {
            return;
        }

        var pen = _pen is { } existing && ReferenceEquals(existing.Brush, Stroke) ? existing : _pen = new(Stroke, Thickness);
        var layout = presenter.TextLayout;
        foreach (var range in _ranges)
        {
            foreach (var rect in layout.HitTestTextRange(range.Start, range.Length))
            {
                if (presenter.TranslatePoint(rect.BottomLeft, this) is { } start)
                {
                    DrawWave(context, pen, start, rect.Width);
                }
            }
        }
    }

    /// <summary>Draws one wavy line.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="pen">The pen.</param>
    /// <param name="start">The line's left end, under the word.</param>
    /// <param name="width">The word's width.</param>
    private static void DrawWave(DrawingContext context, Pen pen, Point start, double width)
    {
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(start, false);
            var up = true;
            for (var x = HalfWave; x <= width; x += HalfWave)
            {
                path.LineTo(new(start.X + x, start.Y + (up ? -WaveHeight : 0)));
                up = !up;
            }

            path.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }
}
