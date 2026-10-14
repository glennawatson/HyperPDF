// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>The coding process named by a JPEG frame header.</summary>
internal enum JpegProcess
{
    /// <summary>Sequential Huffman coding (SOF0 and SOF1).</summary>
    Sequential = 0,

    /// <summary>Progressive Huffman coding (SOF2).</summary>
    Progressive = 1,

    /// <summary>A frame marker that is not a defined process.</summary>
    Unsupported = 2,

    /// <summary>Arithmetic coding (SOF9, SOF10 and SOF13 to SOF15), which PDF does not carry and the managed decoder does not read.</summary>
    Arithmetic = 3,

    /// <summary>Lossless coding (SOF3 and SOF11), which PDF does not carry and the managed decoder does not read.</summary>
    Lossless = 4,

    /// <summary>Differential frames of the hierarchical mode (SOF5 to SOF7), which PDF does not carry and the managed decoder does not read.</summary>
    Hierarchical = 5,
}
