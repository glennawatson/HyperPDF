// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The coded block flags of the chroma blocks of a transform tree node: one per colour component, and for 4:2:2 video a second per component for the lower block.</summary>
/// <param name="Cb0">The flag of the first Cb block.</param>
/// <param name="Cb1">The flag of the second Cb block of 4:2:2 video.</param>
/// <param name="Cr0">The flag of the first Cr block.</param>
/// <param name="Cr1">The flag of the second Cr block of 4:2:2 video.</param>
internal readonly record struct H265ChromaCbf(bool Cb0, bool Cb1, bool Cr0, bool Cr1)
{
    /// <summary>Gets a value indicating whether any chroma block is coded.</summary>
    internal bool Any => Cb0 || Cb1 || Cr0 || Cr1;

    /// <summary>Gets the flag of a block.</summary>
    /// <param name="component">1 for Cb, 2 for Cr.</param>
    /// <param name="index">0 for the first block, 1 for the second.</param>
    /// <returns>The flag.</returns>
    internal bool Of(int component, int index) => component == 1 ? Pick(Cb0, Cb1, index) : Pick(Cr0, Cr1, index);

    /// <summary>Picks the flag of the first or second block.</summary>
    /// <param name="first">The flag of the first block.</param>
    /// <param name="second">The flag of the second block.</param>
    /// <param name="index">0 for the first block, 1 for the second.</param>
    /// <returns>The flag.</returns>
    private static bool Pick(bool first, bool second, int index) => index == 0 ? first : second;
}
