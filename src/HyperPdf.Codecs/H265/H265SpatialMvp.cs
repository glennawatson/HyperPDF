// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The spatial candidates of a motion vector predictor.</summary>
/// <param name="FoundA">Whether a candidate was found on the left.</param>
/// <param name="A">The candidate on the left.</param>
/// <param name="FoundB">Whether a candidate was found above.</param>
/// <param name="B">The candidate above.</param>
internal readonly record struct H265SpatialMvp(bool FoundA, MotionVector A, bool FoundB, MotionVector B)
{
    /// <summary>Gets a value indicating whether two different candidates were found, which fill the predictor list.</summary>
    internal bool Distinct => FoundA && FoundB && A != B;

    /// <summary>Gets a value indicating whether any candidate was found.</summary>
    internal bool Any => FoundA || FoundB;

    /// <summary>Gets the first candidate in list order.</summary>
    internal MotionVector First => FoundA ? A : B;
}
