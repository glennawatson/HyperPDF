// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>The shape of a JPEG written by <see cref="JpegTestEncoder"/>.</summary>
/// <param name="BlocksAcross">The 8x8 blocks across.</param>
/// <param name="BlocksDown">The 8x8 blocks down.</param>
/// <param name="Components">The components, each sampled 1x1.</param>
/// <param name="AdobeTransform">The Adobe transform byte, or <see cref="JpegTestEncoder.NoAdobe"/> to omit the marker.</param>
/// <param name="RestartInterval">The restart interval in blocks, or zero.</param>
internal readonly record struct JpegTestLayout(int BlocksAcross, int BlocksDown, int Components, int AdobeTransform, int RestartInterval)
{
    /// <summary>Gets the sample precision: 8 for a baseline frame or 12 for an extended sequential frame.</summary>
    internal int Precision { get; init; } = 8;

    /// <summary>Gets a value indicating whether the frame header gives zero lines and a DNL marker after the scan gives the real count.</summary>
    internal bool UseDnl { get; init; }
}
