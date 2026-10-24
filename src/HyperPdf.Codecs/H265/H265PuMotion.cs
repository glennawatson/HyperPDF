// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The motion of a prediction block: a vector and reference index for each list. A negative reference index means the list is not used.</summary>
/// <param name="Vector0">The list 0 motion vector.</param>
/// <param name="Vector1">The list 1 motion vector.</param>
/// <param name="RefIdx0">The list 0 reference index, or -1.</param>
/// <param name="RefIdx1">The list 1 reference index, or -1.</param>
internal readonly record struct H265PuMotion(MotionVector Vector0, MotionVector Vector1, sbyte RefIdx0, sbyte RefIdx1)
{
    /// <summary>Gets the motion of a block that is not inter predicted.</summary>
    internal static H265PuMotion None { get; } = new(default, default, -1, -1);

    /// <summary>Gets a value indicating whether the block predicts from list 0.</summary>
    internal bool UsesList0 => RefIdx0 >= 0;

    /// <summary>Gets a value indicating whether the block predicts from list 1.</summary>
    internal bool UsesList1 => RefIdx1 >= 0;
}
