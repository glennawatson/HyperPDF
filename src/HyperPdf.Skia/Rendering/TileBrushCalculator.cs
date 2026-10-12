// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

namespace HyperPdf.Skia.Rendering;

/// <summary>Calculates sizing and transformations for tile brushes.</summary>
internal sealed class TileBrushCalculator
{
    /// <summary>The half divisor.</summary>
    private const double HalfDivisor = 2.0;

    /// <summary>The image size.</summary>
    private readonly Size _imageSize;

    /// <summary>The draw rect.</summary>
    private readonly Rect _drawRect;

    /// <summary>Initializes a new instance of the <see cref = "TileBrushCalculator"/> class.</summary>
    /// <param name = "brush">The tile brush.</param>
    /// <param name = "contentSize">The size of the content.</param>
    /// <param name = "targetSize">The target size.</param>
    public TileBrushCalculator(ITileBrush brush, Size contentSize, Size targetSize)
    {
        _imageSize = contentSize;
        SourceRect = brush.SourceRect.ToPixels(_imageSize);
        DestinationRect = brush.DestinationRect.ToPixels(targetSize);
        var scale = brush.Stretch.CalculateScaling(DestinationRect.Size, SourceRect.Size);
        var translate = CalculateTranslate(brush.AlignmentX, brush.AlignmentY, SourceRect, DestinationRect, scale);
        IntermediateSize = brush.TileMode == TileMode.None ? targetSize : DestinationRect.Size;
        IntermediateTransform = CalculateIntermediateTransform(brush.TileMode, SourceRect, DestinationRect, scale, translate, out _drawRect);
    }

    /// <summary>Gets the destination rectangle.</summary>
    public Rect DestinationRect { get; }

    /// <summary>Gets the intermediate clip rectangle.</summary>
    public Rect IntermediateClip => _drawRect;

    /// <summary>Gets the intermediate surface size.</summary>
    public Size IntermediateSize { get; }

    /// <summary>Gets the intermediate transform matrix.</summary>
    public Matrix IntermediateTransform { get; }

    /// <summary>Gets the source rectangle.</summary>
    public Rect SourceRect { get; }

    /// <summary>Calculates the alignment translation.</summary>
    /// <param name = "alignmentX">Horizontal alignment.</param>
    /// <param name = "alignmentY">Vertical alignment.</param>
    /// <param name = "sourceRect">Source rectangle.</param>
    /// <param name = "destinationRect">Destination rectangle.</param>
    /// <param name = "scale">Scale factors.</param>
    /// <returns>The translation vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector CalculateTranslate(
        AlignmentX alignmentX,
        AlignmentY alignmentY,
        in Rect sourceRect,
        in Rect destinationRect,
        Vector scale) => CalculateTranslate(alignmentX, alignmentY, sourceRect.Size * scale, destinationRect.Size);

    /// <summary>Calculates the alignment translation.</summary>
    /// <param name = "alignmentX">Horizontal alignment.</param>
    /// <param name = "alignmentY">Vertical alignment.</param>
    /// <param name = "sourceSize">Source size.</param>
    /// <param name = "destinationSize">Destination size.</param>
    /// <returns>The translation vector.</returns>
    internal static Vector CalculateTranslate(AlignmentX alignmentX, AlignmentY alignmentY, Size sourceSize, Size destinationSize)
    {
        var x = 0.0;
        var y = 0.0;
        switch (alignmentX)
        {
            case AlignmentX.Center:
                {
                    x += (destinationSize.Width - sourceSize.Width) / HalfDivisor;
                    break;
                }

            case AlignmentX.Right:
                {
                    x += destinationSize.Width - sourceSize.Width;
                    break;
                }

            default:
                break;
        }

        switch (alignmentY)
        {
            case AlignmentY.Center:
                {
                    y += (destinationSize.Height - sourceSize.Height) / HalfDivisor;
                    break;
                }

            case AlignmentY.Bottom:
                {
                    y += destinationSize.Height - sourceSize.Height;
                    break;
                }

            default:
                break;
        }

        return new(x, y);
    }

    /// <summary>Calculates intermediate matrix transformation.</summary>
    /// <param name = "tileMode">The tile mode.</param>
    /// <param name = "sourceRect">Source rectangle.</param>
    /// <param name = "destinationRect">Destination rectangle.</param>
    /// <param name = "scale">Scale factors.</param>
    /// <param name = "translate">Translation vector.</param>
    /// <param name = "drawRect">Output draw rectangle.</param>
    /// <returns>The intermediate transform matrix.</returns>
    internal static Matrix CalculateIntermediateTransform(TileMode tileMode, in Rect sourceRect, in Rect destinationRect, Vector scale, Vector translate, out Rect drawRect)
    {
        var transform = Matrix.CreateTranslation(-sourceRect.Position) * Matrix.CreateScale(scale) * Matrix.CreateTranslation(translate);
        if (tileMode == TileMode.None)
        {
            drawRect = destinationRect;
            transform *= Matrix.CreateTranslation(destinationRect.Position);
        }
        else
        {
            drawRect = new(destinationRect.Size);
        }

        return transform;
    }
}
