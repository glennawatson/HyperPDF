// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The fixed limits of the coding structure.</summary>
internal static class H265CodingLimits
{
    /// <summary>The base 2 logarithm of the largest coding tree block: 64 samples.</summary>
    internal const int MaxCtbLog2 = 6;

    /// <summary>The base 2 logarithm of the largest transform block: 32 samples.</summary>
    internal const int MaxTbLog2 = 5;

    /// <summary>The base 2 logarithm of the smallest transform block: 4 samples.</summary>
    internal const int MinTbLog2 = 2;
}
