// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Names the ways a coding unit divides into prediction blocks (Table 7-10).</summary>
internal static class H265PartMode
{
    /// <summary>One block the size of the coding unit.</summary>
    internal const int Part2Nx2N = 0;

    /// <summary>Two blocks, one above the other.</summary>
    internal const int Part2NxN = 1;

    /// <summary>Two blocks, side by side.</summary>
    internal const int PartNx2N = 2;

    /// <summary>Four blocks.</summary>
    internal const int NxN = 3;

    /// <summary>Two blocks, the upper a quarter of the height.</summary>
    internal const int Part2NxnU = 4;

    /// <summary>Two blocks, the lower a quarter of the height.</summary>
    internal const int Part2NxnD = 5;

    /// <summary>Two blocks, the left a quarter of the width.</summary>
    internal const int PartnLx2N = 6;

    /// <summary>Two blocks, the right a quarter of the width.</summary>
    internal const int PartnRx2N = 7;
}
