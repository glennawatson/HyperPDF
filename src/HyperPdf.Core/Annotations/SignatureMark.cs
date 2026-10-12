// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json.Serialization;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Annotations;

/// <summary>
/// A signature or initials, ready to preview, place on a page and, when the user asks, remember: typed text, drawn
/// strokes or an image. <see cref="Width"/> and <see cref="Height"/> give the mark's own shape; placing it scales it.
/// </summary>
[DebuggerDisplay("SignatureMark: {Kind} {Style}, {Width} x {Height}")]
public sealed record SignatureMark
{
    /// <summary>The width of a typed character, as a share of the text size.</summary>
    internal const float TypedCharacterWidth = 0.55F;

    /// <summary>The height of a typed line, as a multiple of the text size.</summary>
    internal const float TypedLineHeight = 1.25F;

    /// <summary>The coordinates in each stored point.</summary>
    private const int Coordinates = 2;

    /// <summary>The bytes per BGRA pixel.</summary>
    private const int Channels = 4;

    /// <summary>The smallest drawn extent, so a straight line still has a height to scale.</summary>
    private const float MinDrawnExtent = 1;

    /// <summary>Gets whether this is a signature or initials.</summary>
    public SignatureMarkKind Kind { get; init; }

    /// <summary>Gets how the mark was made.</summary>
    public SignatureMarkStyle Style { get; init; }

    /// <summary>Gets the typed text, or an empty string.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Gets the mark's own width: text-size units when typed, drawing units when drawn, pixels for an image.</summary>
    public float Width { get; init; }

    /// <summary>Gets the mark's own height, in the same units as <see cref="Width"/>.</summary>
    public float Height { get; init; }

    /// <summary>Gets the drawn points as x, y pairs from the mark's top-left corner, stroke after stroke.</summary>
    public ReadOnlyMemory<float> Points { get; init; }

    /// <summary>Gets the number of points in each drawn stroke.</summary>
    public ReadOnlyMemory<int> StrokeLengths { get; init; }

    /// <summary>Gets the image as tightly packed, straight-alpha BGRA pixels.</summary>
    public ReadOnlyMemory<byte> Pixels { get; init; }

    /// <summary>Gets the width divided by the height.</summary>
    [JsonIgnore]
    public float AspectRatio => Height > 0 && Width > 0 ? Width / Height : 1;

    /// <summary>Gets a value indicating whether the mark has anything to place.</summary>
    [JsonIgnore]
    public bool IsValid => Width > 0 && Height > 0 && Style switch
    {
        SignatureMarkStyle.Typed => !string.IsNullOrWhiteSpace(Text),
        SignatureMarkStyle.Drawn => Points.Length >= Coordinates && Points.Length % Coordinates == 0 && SumOf(StrokeLengths.Span) * Coordinates == Points.Length,
        SignatureMarkStyle.Image => Pixels.Length == (long)Width * (long)Height * Channels,
        _ => false,
    };

    /// <summary>Makes a typed mark.</summary>
    /// <param name="kind">Signature or initials.</param>
    /// <param name="text">The text; surrounding spaces are removed.</param>
    /// <returns>The mark, or <see langword="null"/> when the text is blank.</returns>
    public static SignatureMark? Typed(SignatureMarkKind kind, string? text)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed)
            ? null
            : new() { Kind = kind, Style = SignatureMarkStyle.Typed, Text = trimmed, Width = trimmed.Length * TypedCharacterWidth, Height = TypedLineHeight };
    }

    /// <summary>Makes a drawn mark from pointer samples, smoothing each stroke and moving it to the top-left corner.</summary>
    /// <param name="kind">Signature or initials.</param>
    /// <param name="points">The samples, stroke after stroke, in any units with y growing downwards.</param>
    /// <param name="strokeLengths">The number of samples in each stroke.</param>
    /// <returns>The mark, or <see langword="null"/> when nothing was drawn.</returns>
    /// <exception cref="ArgumentException">The stroke lengths do not add up to the number of points.</exception>
    public static SignatureMark? Drawn(SignatureMarkKind kind, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> strokeLengths)
    {
        if (SumOf(strokeLengths) != points.Length)
        {
            throw new ArgumentException("The stroke lengths must add up to the number of points.", nameof(strokeLengths));
        }

        if (points.IsEmpty)
        {
            return null;
        }

        var bounds = Bounds(points);
        var smoothedTotal = 0;
        var strokeCount = 0;
        foreach (var length in strokeLengths)
        {
            if (length == 0)
            {
                continue;
            }

            // A tap is stored as a two-point stroke so the PDF draws a dot.
            smoothedTotal += Math.Max(Coordinates, SignatureStrokes.SmoothedLength(length));
            strokeCount++;
        }

        var smoothed = new PagePoint[smoothedTotal];
        var lengths = new int[strokeCount];
        var source = 0;
        var target = 0;
        var stroke = 0;
        foreach (var length in strokeLengths)
        {
            if (length == 0)
            {
                continue;
            }

            var written = SmoothStroke(points.Slice(source, length), smoothed.AsSpan(target));
            lengths[stroke] = written;
            stroke++;
            source += length;
            target += written;
        }

        var coordinates = new float[smoothedTotal * Coordinates];
        for (var i = 0; i < smoothed.Length; i++)
        {
            coordinates[i * Coordinates] = smoothed[i].X - bounds.Left;
            coordinates[(i * Coordinates) + 1] = smoothed[i].Y - bounds.Top;
        }

        return new()
        {
            Kind = kind,
            Style = SignatureMarkStyle.Drawn,
            Width = Math.Max(MinDrawnExtent, bounds.Width),
            Height = Math.Max(MinDrawnExtent, bounds.Height),
            Points = coordinates,
            StrokeLengths = lengths,
        };
    }

    /// <summary>Makes an image mark.</summary>
    /// <param name="kind">Signature or initials.</param>
    /// <param name="image">The prepared image.</param>
    /// <returns>The mark.</returns>
    public static SignatureMark FromImage(SignatureMarkKind kind, SignatureImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new() { Kind = kind, Style = SignatureMarkStyle.Image, Width = image.Width, Height = image.Height, Pixels = image.Pixels };
    }

    /// <summary>Adds up stroke lengths.</summary>
    /// <param name="lengths">The lengths.</param>
    /// <returns>The total.</returns>
    private static int SumOf(ReadOnlySpan<int> lengths)
    {
        var total = 0;
        foreach (var length in lengths)
        {
            if (length < 0)
            {
                return -1;
            }

            total = checked(total + length);
        }

        return total;
    }

    /// <summary>Finds the rectangle around every point.</summary>
    /// <param name="points">The points; not empty.</param>
    /// <returns>The rectangle.</returns>
    private static PageRect Bounds(ReadOnlySpan<PagePoint> points)
    {
        var left = float.MaxValue;
        var top = float.MaxValue;
        var right = float.MinValue;
        var bottom = float.MinValue;
        foreach (var point in points)
        {
            left = Math.Min(left, point.X);
            top = Math.Min(top, point.Y);
            right = Math.Max(right, point.X);
            bottom = Math.Max(bottom, point.Y);
        }

        return PageRect.FromEdges(left, top, right, bottom);
    }

    /// <summary>Smooths one stroke, doubling a single tap so it still draws.</summary>
    /// <param name="stroke">The samples.</param>
    /// <param name="destination">Where the smoothed points go.</param>
    /// <returns>The points written.</returns>
    private static int SmoothStroke(ReadOnlySpan<PagePoint> stroke, Span<PagePoint> destination)
    {
        if (stroke.Length == 1)
        {
            destination[0] = stroke[0];
            destination[1] = stroke[0];
            return Coordinates;
        }

        SignatureStrokes.Smooth(stroke, destination);
        return SignatureStrokes.SmoothedLength(stroke.Length);
    }
}
