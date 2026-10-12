// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Geometry;

/// <summary>Helpers for <see cref="PageRotation"/>.</summary>
public static class PageRotationExtensions
{
    /// <summary>The number of quarter turns in a full turn.</summary>
    private const int QuarterTurns = 4;

    /// <summary>The number of degrees in a quarter turn.</summary>
    private const int DegreesPerQuarterTurn = 90;

    /// <summary>Extension members for <see cref="PageRotation"/>.</summary>
    /// <param name="rotation">The current rotation.</param>
    extension(PageRotation rotation)
    {
        /// <summary>Gets the rotation a quarter turn clockwise.</summary>
        public PageRotation Clockwise => (PageRotation)(((int)rotation + 1) % QuarterTurns);

        /// <summary>Gets the rotation a quarter turn anti-clockwise.</summary>
        public PageRotation CounterClockwise => (PageRotation)(((int)rotation + QuarterTurns - 1) % QuarterTurns);

        /// <summary>Gets the rotation in degrees.</summary>
        public int Degrees => (int)rotation * DegreesPerQuarterTurn;
    }
}
