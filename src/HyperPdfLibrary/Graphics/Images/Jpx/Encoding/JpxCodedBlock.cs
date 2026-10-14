// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>What the block coder made of one code-block: its magnitude bit-planes, its passes and where its bytes and pass lengths sit in the tile's stores.</summary>
internal record struct JpxCodedBlock
{
    /// <summary>Gets or sets the magnitude bit-planes the block uses; zero for a block of zeros.</summary>
    internal int Bits { get; set; }

    /// <summary>Gets or sets the coding passes.</summary>
    internal int Passes { get; set; }

    /// <summary>Gets or sets the offset of the block's first byte in the tile's data store.</summary>
    internal int DataStart { get; set; }

    /// <summary>Gets or sets the bytes of the block.</summary>
    internal int DataLength { get; set; }

    /// <summary>Gets or sets the index in the pass-length store of the first pass.</summary>
    internal int PassStart { get; set; }
}
