// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HyperPdf.Core.Annotations;

namespace HyperPdf.App.Controls;

/// <summary>
/// Draws a signature or initials mark into a rectangle, as it will look once placed: typed text, drawn strokes or the
/// picture. Keeps the picture's bitmap while the same mark is drawn, so redraws do not copy its pixels again.
/// </summary>
[DebuggerDisplay("SignatureMarkPainter: Image cached: {_image != null}")]
public sealed class SignatureMarkPainter : IDisposable
{
    /// <summary>The screen resolution bitmaps are made at.</summary>
    private const double Dpi = 96;

    /// <summary>The bytes per BGRA pixel.</summary>
    private const int Channels = 4;

    /// <summary>The coordinates stored for each drawn point.</summary>
    private const int Coordinates = 2;

    /// <summary>The smallest text size change that lays the text out again.</summary>
    private const double SizeTolerance = 0.01;

    /// <summary>The mark the cached bitmap shows.</summary>
    private SignatureMark? _imageFor;

    /// <summary>The cached bitmap of a picture mark.</summary>
    private WriteableBitmap? _image;

    /// <summary>The typed mark the cached text shows.</summary>
    private SignatureMark? _textFor;

    /// <summary>The size of the cached text.</summary>
    private double _textSize;

    /// <summary>The ink of the cached text.</summary>
    private IBrush? _textInk;

    /// <summary>The cached text of a typed mark.</summary>
    private FormattedText? _text;

    /// <summary>Draws a mark.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="mark">The mark.</param>
    /// <param name="target">Where it goes.</param>
    /// <param name="ink">The ink for typed text.</param>
    /// <param name="pen">The pen for drawn strokes.</param>
    public void Draw(DrawingContext context, SignatureMark mark, in Rect target, IBrush ink, IPen pen)
    {
        if (!mark.IsValid || target.Width <= 0 || target.Height <= 0)
        {
            return;
        }

        switch (mark.Style)
        {
            case SignatureMarkStyle.Typed:
            {
                using (context.PushClip(target))
                {
                    context.DrawText(GetText(mark, SignatureMarkLayout.TypedFontSize(target.Height), ink), target.TopLeft);
                }

                break;
            }

            case SignatureMarkStyle.Drawn:
            {
                DrawStrokes(context, mark, target, pen);
                break;
            }

            case SignatureMarkStyle.Image:
            {
                context.DrawImage(GetImage(mark), target);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _image?.Dispose();
        _image = null;
        _imageFor = null;
        _text = null;
        _textFor = null;
        _textInk = null;
    }

    /// <summary>Views unmanaged framebuffer memory as a span.</summary>
    /// <param name="address">The address.</param>
    /// <param name="length">The length in bytes.</param>
    /// <returns>The span.</returns>
    private static unsafe Span<byte> Framebuffer(nint address, int length) => new((void*)address, length);

    /// <summary>Draws a drawn mark's strokes scaled into a rectangle.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="mark">The drawn mark.</param>
    /// <param name="target">Where it goes.</param>
    /// <param name="pen">The pen.</param>
    private static void DrawStrokes(DrawingContext context, SignatureMark mark, in Rect target, IPen pen)
    {
        var points = mark.Points.Span;
        var scaleX = target.Width / mark.Width;
        var scaleY = target.Height / mark.Height;
        var start = 0;
        foreach (var length in mark.StrokeLengths.Span)
        {
            for (var i = 1; i < length; i++)
            {
                var from = (start + i - 1) * Coordinates;
                var to = (start + i) * Coordinates;
                context.DrawLine(
                    pen,
                    new(target.X + (points[from] * scaleX), target.Y + (points[from + 1] * scaleY)),
                    new(target.X + (points[to] * scaleX), target.Y + (points[to + 1] * scaleY)));
            }

            start += length;
        }
    }

    /// <summary>Gets the laid out text of a typed mark, laying it out again only when the mark, size or ink changes.</summary>
    /// <param name="mark">The typed mark.</param>
    /// <param name="size">The text size.</param>
    /// <param name="ink">The ink.</param>
    /// <returns>The text.</returns>
    private FormattedText GetText(SignatureMark mark, double size, IBrush ink)
    {
        if (_text is not null && ReferenceEquals(_textFor, mark) && Math.Abs(_textSize - size) < SizeTolerance && ReferenceEquals(_textInk, ink))
        {
            return _text;
        }

        _text = new(mark.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, size, ink);
        _textFor = mark;
        _textSize = size;
        _textInk = ink;
        return _text;
    }

    /// <summary>Gets the bitmap of a picture mark, copying its pixels only when the mark changes.</summary>
    /// <param name="mark">The picture mark.</param>
    /// <returns>The bitmap.</returns>
    private WriteableBitmap GetImage(SignatureMark mark)
    {
        if (_image is not null && ReferenceEquals(_imageFor, mark))
        {
            return _image;
        }

        _image?.Dispose();
        var width = (int)mark.Width;
        var height = (int)mark.Height;
        var bitmap = new WriteableBitmap(new(width, height), new(Dpi, Dpi), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var frame = bitmap.Lock())
        {
            var stride = width * Channels;
            var pixels = mark.Pixels.Span;
            for (var row = 0; row < height; row++)
            {
                var source = pixels.Slice(row * stride, stride);
                source.CopyTo(Framebuffer(frame.Address + (row * frame.RowBytes), stride));
            }
        }

        _image = bitmap;
        _imageFor = mark;
        return bitmap;
    }
}
