// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdf.Media.Native.VideoToolbox;

/// <summary>A <c>CMTime</c>: a count of ticks and how many ticks make a second, laid out as CoreMedia lays it out.</summary>
/// <param name="Value">The number of ticks.</param>
/// <param name="Scale">The ticks in a second.</param>
/// <param name="Flags">The flags; bit 0 says the time is valid.</param>
/// <param name="Epoch">The epoch, which is 0 here.</param>
[DebuggerDisplay("CoreMediaTime: {Value}/{Scale}")]
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CoreMediaTime(long Value, int Scale, uint Flags, long Epoch)
{
    /// <summary>The flag that says a time holds a value (<c>kCMTimeFlags_Valid</c>).</summary>
    private const uint ValidFlag = 1;

    /// <summary>The ticks in a second of the times this decoder writes.</summary>
    private const int TicksPerSecond = 1_000_000;

    /// <summary>Gets a time that holds no value.</summary>
    internal static CoreMediaTime Invalid => default;

    /// <summary>Gets a value indicating whether the time holds a value.</summary>
    internal bool IsValid => (Flags & ValidFlag) != 0 && Scale > 0;

    /// <summary>Gets the time in seconds, or 0 when it holds no value.</summary>
    internal double Seconds => IsValid ? (double)Value / Scale : 0;

    /// <summary>Makes a time from seconds.</summary>
    /// <param name="seconds">The seconds.</param>
    /// <returns>The time.</returns>
    internal static CoreMediaTime FromSeconds(double seconds) => new((long)Math.Round(seconds * TicksPerSecond), TicksPerSecond, ValidFlag, 0);
}
