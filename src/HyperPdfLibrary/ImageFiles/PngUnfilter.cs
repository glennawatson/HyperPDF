// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Undoes the five PNG row filters (PNG specification 9.2).</summary>
internal static class PngUnfilter
{
    /// <summary>The filter that adds the byte to the left.</summary>
    private const int Sub = 1;

    /// <summary>The filter that adds the byte above.</summary>
    private const int Up = 2;

    /// <summary>The filter that adds the average of the left and upper bytes.</summary>
    private const int Average = 3;

    /// <summary>The filter that adds the Paeth predictor.</summary>
    private const int Paeth = 4;

    /// <summary>Undoes a row's filter in place.</summary>
    /// <param name="filter">The filter type.</param>
    /// <param name="line">The filtered row, which becomes the plain row.</param>
    /// <param name="prior">The plain row above, or zeros for the first row.</param>
    /// <param name="bytesPerPixel">The bytes to the pixel on the left; one for pixels smaller than a byte.</param>
    /// <exception cref="InvalidDataException">The filter type is not 0 to 4.</exception>
    internal static void Apply(int filter, Span<byte> line, ReadOnlySpan<byte> prior, int bytesPerPixel)
    {
        switch (filter)
        {
            case 0:
            {
                return;
            }

            case Sub:
            {
                UnSub(line, bytesPerPixel);
                return;
            }

            case Up:
            {
                UnUp(line, prior);
                return;
            }

            case Average:
            {
                UnAverage(line, prior, bytesPerPixel);
                return;
            }

            case Paeth:
            {
                UnPaeth(line, prior, bytesPerPixel);
                return;
            }

            default:
            {
                throw new InvalidDataException("A PNG row uses a filter the standard does not define.");
            }
        }
    }

    /// <summary>Undoes the Sub filter: each byte was stored as its difference from the byte one pixel to the left.</summary>
    /// <param name="line">The row, changed in place.</param>
    /// <param name="bytesPerPixel">The bytes to the pixel on the left.</param>
    private static void UnSub(Span<byte> line, int bytesPerPixel)
    {
        for (var i = bytesPerPixel; i < line.Length; i++)
        {
            line[i] += line[i - bytesPerPixel];
        }
    }

    /// <summary>Undoes the Up filter: each byte was stored as its difference from the byte above.</summary>
    /// <param name="line">The row, changed in place.</param>
    /// <param name="prior">The plain row above.</param>
    private static void UnUp(Span<byte> line, ReadOnlySpan<byte> prior)
    {
        var done = 0;
        if (Vector.IsHardwareAccelerated)
        {
            var width = Vector<byte>.Count;
            for (; done + width <= line.Length; done += width)
            {
                (new Vector<byte>(line.Slice(done, width)) + new Vector<byte>(prior.Slice(done, width))).CopyTo(line.Slice(done, width));
            }
        }

        for (; done < line.Length; done++)
        {
            line[done] += prior[done];
        }
    }

    /// <summary>Undoes the Average filter: each byte was stored as its difference from the average of the left and upper bytes.</summary>
    /// <param name="line">The row, changed in place.</param>
    /// <param name="prior">The plain row above.</param>
    /// <param name="bytesPerPixel">The bytes to the pixel on the left.</param>
    private static void UnAverage(Span<byte> line, ReadOnlySpan<byte> prior, int bytesPerPixel)
    {
        var head = Math.Min(bytesPerPixel, line.Length);
        for (var i = 0; i < head; i++)
        {
            line[i] += (byte)(prior[i] >> 1);
        }

        for (var i = head; i < line.Length; i++)
        {
            line[i] += (byte)((line[i - bytesPerPixel] + prior[i]) >> 1);
        }
    }

    /// <summary>Undoes the Paeth filter: each byte was stored as its difference from the Paeth predictor.</summary>
    /// <param name="line">The row, changed in place.</param>
    /// <param name="prior">The plain row above.</param>
    /// <param name="bytesPerPixel">The bytes to the pixel on the left.</param>
    private static void UnPaeth(Span<byte> line, ReadOnlySpan<byte> prior, int bytesPerPixel)
    {
        // The first pixel has no left or upper-left neighbour, so the predictor is the byte above.
        var head = Math.Min(bytesPerPixel, line.Length);
        for (var i = 0; i < head; i++)
        {
            line[i] += prior[i];
        }

        for (var i = head; i < line.Length; i++)
        {
            line[i] += Predict(line[i - bytesPerPixel], prior[i], prior[i - bytesPerPixel]);
        }
    }

    /// <summary>Gets the Paeth predictor: whichever of the left, upper and upper left bytes is nearest to left + upper - upper left.</summary>
    /// <param name="left">The byte to the left.</param>
    /// <param name="upper">The byte above.</param>
    /// <param name="upperLeft">The byte above and to the left.</param>
    /// <returns>The prediction.</returns>
    private static byte Predict(int left, int upper, int upperLeft)
    {
        var estimate = left + upper - upperLeft;
        var distanceLeft = Math.Abs(estimate - left);
        var distanceUpper = Math.Abs(estimate - upper);
        var distanceUpperLeft = Math.Abs(estimate - upperLeft);
        if (distanceLeft <= distanceUpper && distanceLeft <= distanceUpperLeft)
        {
            return (byte)left;
        }

        return (byte)(distanceUpper <= distanceUpperLeft ? upper : upperLeft);
    }
}
