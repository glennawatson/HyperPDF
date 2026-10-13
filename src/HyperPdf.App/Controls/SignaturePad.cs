// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.App.Controls;

/// <summary>
/// A piece of paper to draw a signature on with a mouse, pen or finger. Each finished stroke is passed to
/// <see cref="StrokeCommand"/>. When nothing has been drawn here yet it shows <see cref="Mark"/>, such as a remembered
/// drawing.
/// </summary>
[DebuggerDisplay("SignaturePad: {_drawn.Count} strokes")]
public sealed class SignaturePad : Control, IActivatableView
{
    /// <summary>The mark shown when nothing has been drawn here; setting it to <see langword="null"/> clears the pad.</summary>
    public static readonly StyledProperty<SignatureMark?> MarkProperty = AvaloniaProperty.Register<SignaturePad, SignatureMark?>(nameof(Mark));

    /// <summary>The command given each finished stroke, as an array of points in pad coordinates.</summary>
    public static readonly StyledProperty<ICommand?> StrokeCommandProperty = AvaloniaProperty.Register<SignaturePad, ICommand?>(nameof(StrokeCommand));

    /// <summary>The ink width in device independent pixels.</summary>
    private const double InkWidth = 2.5;

    /// <summary>How far a remembered drawing sits inside the pad's edge.</summary>
    private const double MarkInset = 12;

    /// <summary>The most points kept in one stroke.</summary>
    private const int MaxStrokePoints = 4096;

    /// <summary>Half, to centre a mark.</summary>
    private const double Half = 0.5;

    /// <summary>The ink brush, the colour signatures are written in.</summary>
    private static readonly IImmutableSolidColorBrush InkBrush = new ImmutableSolidColorBrush(Color.FromUInt32(0xFF000000U | AnnotationColors.Ink));

    /// <summary>The ink pen.</summary>
    private static readonly IPen InkPen = new ImmutablePen(InkBrush, InkWidth, lineCap: PenLineCap.Round);

    /// <summary>The finished strokes drawn here, in pad coordinates.</summary>
    private readonly List<PagePoint[]> _drawn = [];

    /// <summary>The stroke being drawn.</summary>
    private readonly List<PagePoint> _stroke = [];

    /// <summary>Draws a remembered mark, while shown.</summary>
    private SignatureMarkPainter? _painter;

    /// <summary>Initializes static members of the <see cref="SignaturePad"/> class.</summary>
    static SignaturePad() => AffectsRender<SignaturePad>(MarkProperty);

    /// <summary>Initializes a new instance of the <see cref="SignaturePad"/> class.</summary>
    public SignaturePad()
    {
        Cursor = new(StandardCursorType.Cross);
        _ = this.WhenActivated(disposables =>
        {
            // A press starts a stroke and keeps the pointer until it is released.
            disposables.Add(this.PointerPresses.MarkHandled().SubscribeSafe(
                press =>
                {
                    var point = press.GetPosition(this);
                    _stroke.Clear();
                    _stroke.Add(new((float)point.X, (float)point.Y));
                    press.Pointer.Capture(this);
                    InvalidateVisual();
                },
                OnError));
            disposables.Add(this.PointerMoves.Where(_ => _stroke.Count is > 0 and < MaxStrokePoints).SubscribeSafe(
                move =>
                {
                    var point = move.GetPosition(this);
                    _stroke.Add(new((float)Math.Clamp(point.X, 0, Bounds.Width), (float)Math.Clamp(point.Y, 0, Bounds.Height)));
                    InvalidateVisual();
                },
                OnError));

            // A release finishes the stroke and passes it to the stroke command.
            disposables.Add(this.PointerReleases.Where(_ => _stroke.Count > 0).SubscribeSafe(
                release =>
                {
                    release.Pointer.Capture(null);
                    PagePoint[] stroke = [.. _stroke];
                    _stroke.Clear();
                    _drawn.Add(stroke);
                    InvalidateVisual();
                    if (StrokeCommand is { } command && command.CanExecute(stroke))
                    {
                        command.Execute(stroke);
                    }
                },
                OnError));

            // Clearing the mark clears what was drawn here too, so Clear Drawing starts again.
            disposables.Add(this.WhenChanged(static pad => pad.Mark).Where(static mark => mark is null).SubscribeSafe(_ => Clear(), OnError));

            // The cached picture is freed once the pad is no longer shown.
            disposables.Add(Scope.Create(this, static pad => pad.FreePainter()));
        });
    }

    /// <summary>Gets or sets the mark shown when nothing has been drawn here.</summary>
    public SignatureMark? Mark
    {
        get => GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }

    /// <summary>Gets or sets the command given each finished stroke.</summary>
    public ICommand? StrokeCommand
    {
        get => GetValue(StrokeCommandProperty);
        set => SetValue(StrokeCommandProperty, value);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A transparent fill makes the whole pad answer the pointer, not just the ink.
        context.FillRectangle(Brushes.Transparent, new(Bounds.Size));
        if (_drawn.Count == 0 && _stroke.Count == 0)
        {
            if (Mark is { } mark)
            {
                var area = new Rect(Bounds.Size).Deflate(MarkInset);
                (_painter ??= new()).Draw(context, mark, Fit(mark, area), InkBrush, InkPen);
            }

            return;
        }

        foreach (var stroke in _drawn)
        {
            DrawStroke(context, stroke);
        }

        DrawStroke(context, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_stroke));
    }

    /// <summary>Fits a mark inside a rectangle, keeping its shape, centred.</summary>
    /// <param name="mark">The mark.</param>
    /// <param name="area">The rectangle.</param>
    /// <returns>The mark's rectangle.</returns>
    internal static Rect Fit(SignatureMark mark, in Rect area)
    {
        var scale = Math.Min(area.Width / mark.Width, area.Height / mark.Height);
        var width = mark.Width * scale;
        var height = mark.Height * scale;
        return new(area.X + ((area.Width - width) * Half), area.Y + ((area.Height - height) * Half), width, height);
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Draws one stroke as joined lines.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="stroke">The points.</param>
    private static void DrawStroke(DrawingContext context, ReadOnlySpan<PagePoint> stroke)
    {
        for (var i = 1; i < stroke.Length; i++)
        {
            context.DrawLine(InkPen, new(stroke[i - 1].X, stroke[i - 1].Y), new(stroke[i].X, stroke[i].Y));
        }
    }

    /// <summary>Frees the cached picture.</summary>
    private void FreePainter()
    {
        _painter?.Dispose();
        _painter = null;
    }

    /// <summary>Forgets every stroke drawn here.</summary>
    private void Clear()
    {
        _drawn.Clear();
        _stroke.Clear();
        InvalidateVisual();
    }
}
