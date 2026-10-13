// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>How a decoded channel value becomes an 8-bit output sample.</summary>
internal enum JpxSampleMode
{
    /// <summary>The value is scaled over the full range of its bit depth to 0 to 255.</summary>
    Scaled = 0,

    /// <summary>The value is a palette index of an Indexed colour space and is kept as it is, limited to 255.</summary>
    RawIndices = 1,
}
