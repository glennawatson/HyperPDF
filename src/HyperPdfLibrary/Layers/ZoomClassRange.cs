// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Layers;

/// <summary>The zoom classes, first to last, in which a group is shown. A class is the number of zoom limits at or below the magnification.</summary>
/// <param name="First">The first class in which the group is shown.</param>
/// <param name="EndExclusive">The class after the last in which the group is shown.</param>
[DebuggerDisplay("ZoomClassRange: {First}..{EndExclusive}")]
internal readonly record struct ZoomClassRange(int First, int EndExclusive)
{
    /// <summary>Determines whether the group is shown in a zoom class.</summary>
    /// <param name="zoomClass">The zoom class.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    internal bool Contains(int zoomClass) => zoomClass >= First && zoomClass < EndExclusive;
}
