// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>How a picture marks the reference pictures.</summary>
/// <param name="NoOutputOfPriorPics">Whether an IDR picture drops the pictures that were not yet output.</param>
/// <param name="LongTermReference">Whether an IDR picture is kept as a long-term reference.</param>
/// <param name="Adaptive">Whether the reference pictures are marked by operations rather than by the sliding window.</param>
/// <param name="Operations">The memory management control operations.</param>
internal sealed record H264Marking(bool NoOutputOfPriorPics, bool LongTermReference, bool Adaptive, H264Mmco[] Operations)
{
    /// <summary>Gets the marking of a picture that nothing refers to, which marks nothing.</summary>
    internal static H264Marking None { get; } = new(false, false, false, []);
}
