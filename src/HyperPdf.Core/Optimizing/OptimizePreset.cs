// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Optimizing;

/// <summary>How much a saved copy may trade picture detail for a smaller file.</summary>
public enum OptimizePreset
{
    /// <summary>The smallest file: images are scaled down more and compressed harder.</summary>
    Smaller = 0,

    /// <summary>A good size with little visible change.</summary>
    Balanced = 1,

    /// <summary>No picture loses detail; the file shrinks only by lossless means.</summary>
    KeepQuality = 2,
}
