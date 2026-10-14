// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// The order of a code-block's coding passes and how they divide into codeword segments (ISO 15444-1 D.4 and D.6, B.10.6).
/// The first bit-plane has only a cleanup pass; every later one has significance propagation, magnitude refinement and
/// cleanup passes. The segment rules are the ones the packet headers follow, which the decoder repeats.
/// </summary>
internal static class JpxPassPlan
{
    /// <summary>The pass type of a significance propagation pass.</summary>
    internal const int SignificancePass = 0;

    /// <summary>The pass type of a magnitude refinement pass.</summary>
    internal const int RefinementPass = 1;

    /// <summary>The pass type of a cleanup pass.</summary>
    internal const int CleanupPass = 2;

    /// <summary>The passes of a bit-plane after the first.</summary>
    private const int PassesPerPlane = 3;

    /// <summary>The passes saved by the first bit-plane having only a cleanup pass.</summary>
    private const int FirstPlaneSaving = 2;

    /// <summary>The passes of the first bypass segment: the first cleanup pass and three full bit-planes.</summary>
    private const int FirstBypassPasses = 10;

    /// <summary>The passes of a raw bypass segment.</summary>
    private const int RawPasses = 2;

    /// <summary>The most passes of a segment without mode switches (B.10.6).</summary>
    private const int UnlimitedPasses = 109;

    /// <summary>Gets the coding passes of a block with a number of magnitude bit-planes.</summary>
    /// <param name="bits">The bit-planes, at least one.</param>
    /// <returns>The passes.</returns>
    internal static int Passes(int bits) => (PassesPerPlane * bits) - FirstPlaneSaving;

    /// <summary>Gets the type of a pass.</summary>
    /// <param name="pass">The pass, from zero.</param>
    /// <returns>The pass type.</returns>
    internal static int Type(int pass) => pass == 0 ? CleanupPass : (pass - 1) % PassesPerPlane;

    /// <summary>Gets the bit-plane of a pass, counting the least significant plane as zero.</summary>
    /// <param name="pass">The pass, from zero.</param>
    /// <param name="bits">The bit-planes of the block.</param>
    /// <returns>The bit-plane.</returns>
    internal static int Plane(int pass, int bits) => pass == 0 ? bits - 1 : bits - FirstPlaneSaving - ((pass - 1) / PassesPerPlane);

    /// <summary>Gets the most passes the next codeword segment may hold.</summary>
    /// <param name="style">The mode switches.</param>
    /// <param name="previous">The most passes the previous segment could hold, or zero for the first segment.</param>
    /// <returns>The most passes.</returns>
    internal static int SegmentLimit(JpxBlockStyle style, int previous)
    {
        if ((style & JpxBlockStyle.TerminateAll) != 0)
        {
            return 1;
        }

        if ((style & JpxBlockStyle.Bypass) == 0)
        {
            return UnlimitedPasses;
        }

        if (previous == 0)
        {
            return FirstBypassPasses;
        }

        return previous is 1 or FirstBypassPasses ? RawPasses : 1;
    }

    /// <summary>Determines whether a pass is a raw bypass pass: a significance or refinement pass after the first ten passes.</summary>
    /// <param name="style">The mode switches.</param>
    /// <param name="pass">The pass, from zero.</param>
    /// <returns><see langword="true"/> when the pass writes raw bits.</returns>
    internal static bool IsRaw(JpxBlockStyle style, int pass) =>
        (style & JpxBlockStyle.Bypass) != 0 && pass >= FirstBypassPasses && Type(pass) != CleanupPass;
}
