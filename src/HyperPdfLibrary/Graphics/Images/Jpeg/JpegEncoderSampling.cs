// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>The sampling and table choices of one component an encoder writes.</summary>
/// <param name="Horizontal">The horizontal sampling factor from 1 to 4.</param>
/// <param name="Vertical">The vertical sampling factor from 1 to 4.</param>
/// <param name="QuantSlot">The quantization and Huffman table slot: 0 for luminance, 1 for chrominance.</param>
internal readonly record struct JpegEncoderSampling(int Horizontal, int Vertical, int QuantSlot);
