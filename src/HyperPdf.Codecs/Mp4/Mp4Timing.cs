// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Mp4;

/// <summary>How a track's time in ticks becomes seconds from the start of the file.</summary>
/// <param name="Scale">The ticks in a second.</param>
/// <param name="Shift">The seconds added to a time, from the track's edit list.</param>
internal readonly record struct Mp4Timing(double Scale, double Shift)
{
    /// <summary>Turns a time in ticks into seconds from the start of the file.</summary>
    /// <param name="ticks">The time in ticks.</param>
    /// <returns>The seconds, which can be negative.</returns>
    internal double Seconds(long ticks) => (ticks / Scale) + Shift;

    /// <summary>Turns a length in ticks into seconds.</summary>
    /// <param name="ticks">The length in ticks.</param>
    /// <returns>The seconds.</returns>
    internal double Length(long ticks) => ticks / Scale;
}
