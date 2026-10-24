// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The angle that splits a band between its two halves and the gains and bit split it gives.</summary>
/// <param name="Angle">The angle, 0 to 16384 for a quarter turn.</param>
/// <param name="Mid">The gain of the first half, out of 32768.</param>
/// <param name="Side">The gain of the second half, out of 32768.</param>
/// <param name="Delta">The bias of the bit split towards the first or second half.</param>
/// <param name="Fill">The fill mask after the angle cleared the halves that get no pulses.</param>
internal readonly record struct CeltSplitAngle(int Angle, int Mid, int Side, int Delta, int Fill)
{
    /// <summary>The scale of the integer gains.</summary>
    private const float GainScale = 1F / 32768F;

    /// <summary>Gets the gain of the first half.</summary>
    internal float MidGain => Mid * GainScale;

    /// <summary>Gets the gain of the second half.</summary>
    internal float SideGain => Side * GainScale;
}
