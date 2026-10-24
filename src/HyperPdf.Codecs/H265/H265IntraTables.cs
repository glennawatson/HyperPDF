// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The angle tables of angular intra prediction (Tables 8-5 and 8-6).</summary>
internal static class H265IntraTables
{
    /// <summary>The first angular mode.</summary>
    internal const int FirstAngular = 2;

    /// <summary>The first mode with an inverse angle.</summary>
    private const int FirstInverse = 11;

    /// <summary>Gets the prediction angle of the modes 2 to 34, in thirty-seconds of a sample.</summary>
    private static ReadOnlySpan<byte> Angles =>
    [
        0x20, 0x1A, 0x15, 0x11, 0x0D, 0x09, 0x05, 0x02, 0x00, 0xFE, 0xFB, 0xF7, 0xF3, 0xEF, 0xEB, 0xE6,
        0xE0, 0xE6, 0xEB, 0xEF, 0xF3, 0xF7, 0xFB, 0xFE, 0x00, 0x02, 0x05, 0x09, 0x0D, 0x11, 0x15, 0x1A, 0x20,
    ];

    /// <summary>Gets the inverse prediction angle of the modes 11 to 25.</summary>
    private static ReadOnlySpan<ushort> InverseAngles =>
    [
        0xF000, 0xF99A, 0xFC72, 0xFD8A, 0xFE1E, 0xFE7A, 0xFEC5, 0xFF00, 0xFEC5, 0xFE7A, 0xFE1E, 0xFD8A, 0xFC72, 0xF99A, 0xF000,
    ];

    /// <summary>Gets the prediction angle of an angular mode.</summary>
    /// <param name="mode">The mode, from 2 to 34.</param>
    /// <returns>The angle.</returns>
    internal static int AngleOf(int mode) => (sbyte)Angles[mode - FirstAngular];

    /// <summary>Gets the inverse prediction angle of an angular mode that has one.</summary>
    /// <param name="mode">The mode, from 11 to 25.</param>
    /// <returns>The inverse angle.</returns>
    internal static int InverseAngleOf(int mode) => (short)InverseAngles[mode - FirstInverse];
}
