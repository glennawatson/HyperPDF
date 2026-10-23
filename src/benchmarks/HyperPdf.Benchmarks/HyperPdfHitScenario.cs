// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Benchmarks;

/// <summary>Focused query positions on dense, small, rotated and overlapping character geometry.</summary>
public enum HyperPdfHitScenario
{
    /// <summary>An exact hit in the first dense-page block.</summary>
    DenseEarly = 0,

    /// <summary>An exact hit in the middle dense-page block.</summary>
    DenseMiddle = 1,

    /// <summary>An exact hit in the final dense-page block.</summary>
    DenseLate = 2,

    /// <summary>A miss outside a dense page.</summary>
    DenseMiss = 3,

    /// <summary>A nearest tolerance hit beside the final dense-page character.</summary>
    DenseTolerance = 4,

    /// <summary>An exact hit at the beginning of a small page.</summary>
    SmallEarly = 5,

    /// <summary>An exact hit in the middle of a small page.</summary>
    SmallMiddle = 6,

    /// <summary>An exact hit at the end of a small page.</summary>
    SmallLate = 7,

    /// <summary>A miss outside a small page.</summary>
    SmallMiss = 8,

    /// <summary>A tolerance hit beside the final small-page character.</summary>
    SmallTolerance = 9,

    /// <summary>An exact hit near the end of rotated dense-page text.</summary>
    RotatedLate = 10,

    /// <summary>An earlier tolerance candidate followed by overlapping exact hits.</summary>
    OverlappingExact = 11,

    /// <summary>Equal nearest candidates in different blocks, preserving the first original index.</summary>
    OverlappingNearestTie = 12,
}
