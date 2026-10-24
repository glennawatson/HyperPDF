// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>A partition of a macroblock, in 4x4 blocks from the macroblock's top left corner.</summary>
/// <param name="Column">The first block column.</param>
/// <param name="Row">The first block row.</param>
/// <param name="Width">The width in blocks.</param>
/// <param name="Height">The height in blocks.</param>
/// <param name="Shape">The shape code that picks the directional prediction rule, see <see cref="H264MotionPrediction"/>.</param>
internal readonly record struct H264Partition(int Column, int Row, int Width, int Height, int Shape);
