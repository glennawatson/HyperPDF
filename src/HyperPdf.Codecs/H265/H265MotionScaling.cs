// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Scales a motion vector by the ratio of two picture order count distances (8-179 to 8-183).</summary>
internal static class H265MotionScaling
{
    /// <summary>The numerator of the reciprocal that approximates the division by the distance.</summary>
    private const int ReciprocalBase = 16_384;

    /// <summary>The smallest distance after clipping.</summary>
    private const int DistanceMin = -128;

    /// <summary>The largest distance after clipping.</summary>
    private const int DistanceMax = 127;

    /// <summary>The smallest scale factor.</summary>
    private const int FactorMin = -4_096;

    /// <summary>The largest scale factor.</summary>
    private const int FactorMax = 4_095;

    /// <summary>The rounding term of the scale factor.</summary>
    private const int FactorRound = 32;

    /// <summary>The shift of the scale factor.</summary>
    private const int FactorShift = 6;

    /// <summary>The rounding term of the scaled component.</summary>
    private const int ComponentRound = 127;

    /// <summary>The shift of the scaled component.</summary>
    private const int ComponentShift = 8;

    /// <summary>Scales a vector.</summary>
    /// <param name="vector">The vector to scale.</param>
    /// <param name="distanceOfVector">The picture order count distance the vector spans.</param>
    /// <param name="distanceWanted">The distance the scaled vector should span.</param>
    /// <returns>The scaled vector, or the vector itself when the distance it spans is zero.</returns>
    internal static MotionVector Scale(MotionVector vector, int distanceOfVector, int distanceWanted)
    {
        var td = Math.Clamp(distanceOfVector, DistanceMin, DistanceMax);
        if (td == 0)
        {
            return vector;
        }

        var tb = Math.Clamp(distanceWanted, DistanceMin, DistanceMax);
        var tx = (ReciprocalBase + (Math.Abs(td) >> 1)) / td;
        var factor = Math.Clamp(((tb * tx) + FactorRound) >> FactorShift, FactorMin, FactorMax);
        return new(ScaleComponent(vector.X, factor), ScaleComponent(vector.Y, factor));
    }

    /// <summary>Scales one component of a vector.</summary>
    /// <param name="value">The component.</param>
    /// <param name="factor">The scale factor.</param>
    /// <returns>The scaled component, clipped to 16 bits.</returns>
    private static int ScaleComponent(int value, int factor)
    {
        var product = factor * value;
        var scaled = Math.Sign(product) * ((Math.Abs(product) + ComponentRound) >> ComponentShift);
        return Math.Clamp(scaled, short.MinValue, short.MaxValue);
    }
}
