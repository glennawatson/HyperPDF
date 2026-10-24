// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Whether residual DPCM applies to a block and in which direction (8.6.5).</summary>
internal enum H265RdpcmMode
{
    /// <summary>The residual is used as it is.</summary>
    None = 0,

    /// <summary>Each residual sample adds the one to its left.</summary>
    Horizontal = 1,

    /// <summary>Each residual sample adds the one above it.</summary>
    Vertical = 2,
}
