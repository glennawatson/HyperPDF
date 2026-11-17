// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>
/// How a line of text reads on the page as it is shown, at any angle. The default value is a line that reads to the right and whose
/// size is its bounds.
/// </summary>
/// <param name="DirectionX">The way the baseline runs across the page, to the right; with <paramref name="DirectionY"/> it is a unit vector in page space, which runs downward.</param>
/// <param name="DirectionY">The way the baseline runs down the page.</param>
/// <param name="Length">How far the line reaches along its baseline, or zero or less for a line that reads to the right and is as long as its bounds are wide.</param>
/// <param name="Height">How tall the line is across its baseline.</param>
[DebuggerDisplay("TextReading: ({DirectionX}, {DirectionY}) for {Length} by {Height}")]
public readonly record struct TextReading(float DirectionX, float DirectionY, float Length, float Height)
{
    /// <summary>Gets a value indicating whether the line reads to the right, so its bounds are its own shape.</summary>
    public bool IsUpright => Length <= 0F;
}
