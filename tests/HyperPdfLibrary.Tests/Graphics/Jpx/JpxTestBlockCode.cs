// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>A coded code-block: its magnitude bit-planes and its terminated segments.</summary>
/// <param name="Bits">The magnitude bit-planes; zero when every coefficient is zero.</param>
/// <param name="Segments">The codeword segments in order.</param>
[DebuggerDisplay("JpxTestBlockCode: {Bits} bits, {Segments.Count} segments")]
internal sealed record JpxTestBlockCode(int Bits, List<JpxTestSegment> Segments)
{
    /// <summary>Gets the HT placeholder sets before the first cleanup pass, whose passes <see cref="Segments"/> already counts in its first segment.</summary>
    internal int PlaceholderSets { get; init; }

    /// <summary>Gets the number of coding passes.</summary>
    internal int Passes
    {
        get
        {
            var total = 0;
            foreach (var segment in Segments)
            {
                total += segment.Passes;
            }

            return total;
        }
    }
}
