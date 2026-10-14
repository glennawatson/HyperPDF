// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>One scan an encoder writes.</summary>
/// <param name="Components">The components in the scan, one bit each, with component 0 in bit 0.</param>
/// <param name="Start">The first zigzag index of the spectral band (Ss).</param>
/// <param name="End">The last zigzag index of the spectral band (Se).</param>
/// <param name="High">The successive-approximation bit position of the previous scan of the band (Ah).</param>
/// <param name="Low">The successive-approximation bit position of this scan (Al).</param>
/// <param name="Progressive">Whether the scan belongs to a progressive frame.</param>
internal readonly record struct JpegScanSpec(int Components, int Start, int End, int High, int Low, bool Progressive)
{
    /// <summary>Gets a value indicating whether the scan codes the DC coefficients.</summary>
    internal bool IsDc => Start == 0;

    /// <summary>Gets a value indicating whether the scan adds precision to coefficients an earlier scan coded.</summary>
    internal bool IsRefinement => High != 0;
}
