// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.Vorbis;

/// <summary>The whole number arithmetic of a type 1 floor: predicting a height from its neighbours, placing it, and drawing a line.</summary>
internal static class VorbisFloorMath
{
    /// <summary>Predicts the height at a position on the straight line between two points.</summary>
    /// <param name="x0">The left position.</param>
    /// <param name="y0">The left height.</param>
    /// <param name="x1">The right position.</param>
    /// <param name="y1">The right height.</param>
    /// <param name="x">The position.</param>
    /// <returns>The height.</returns>
    internal static int Predict(int x0, int y0, int x1, int y1, int x)
    {
        var change = y1 - y0;
        var offset = Math.Abs(change) * (x - x0) / (x1 - x0);
        return change < 0 ? y0 - offset : y0 + offset;
    }

    /// <summary>Works out a height that is beyond the room either side of the prediction.</summary>
    /// <param name="value">The coded height.</param>
    /// <param name="predicted">The prediction.</param>
    /// <param name="highRoom">The room above the prediction.</param>
    /// <param name="lowRoom">The room below the prediction.</param>
    /// <returns>The height.</returns>
    internal static int OutsideRoom(int value, int predicted, int highRoom, int lowRoom) =>
        highRoom > lowRoom ? value - lowRoom + predicted : predicted - value + highRoom - 1;

    /// <summary>Works out a height that is within the room around the prediction: odd values go below it, even above.</summary>
    /// <param name="value">The coded height.</param>
    /// <param name="predicted">The prediction.</param>
    /// <returns>The height.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int InsideRoom(int value, int predicted) => (value & 1) != 0 ? predicted - ((value + 1) >> 1) : predicted + (value >> 1);

    /// <summary>Draws a line with whole number steps.</summary>
    /// <param name="x0">The left position.</param>
    /// <param name="y0">The left height.</param>
    /// <param name="x1">The right position.</param>
    /// <param name="y1">The right height.</param>
    /// <param name="curve">Receives the heights; its length is the number of lines available.</param>
    internal static void DrawLine(int x0, int y0, int x1, int y1, Span<int> curve)
    {
        var change = y1 - y0;
        var width = x1 - x0;
        if (width <= 0)
        {
            return;
        }

        var slope = change / width;
        var step = change < 0 ? slope - 1 : slope + 1;
        var rest = Math.Abs(change) - (Math.Abs(slope) * width);
        var error = 0;
        var y = y0;
        if (x0 < curve.Length)
        {
            curve[x0] = y;
        }

        for (var x = x0 + 1; x < x1 && x < curve.Length; x++)
        {
            error += rest;
            if (error >= width)
            {
                error -= width;
                y += step;
            }
            else
            {
                y += slope;
            }

            curve[x] = y;
        }
    }
}
