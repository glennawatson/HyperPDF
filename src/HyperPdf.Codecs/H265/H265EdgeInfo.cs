// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>A four sample segment of a block edge in the deblocking filter.</summary>
/// <param name="X">The column of the first Q sample in luma samples.</param>
/// <param name="Y">The row of the first Q sample in luma samples.</param>
/// <param name="IndexP">The 4x4 grid index of the P block.</param>
/// <param name="IndexQ">The 4x4 grid index of the Q block.</param>
/// <param name="Vertical">Whether the edge is vertical.</param>
/// <param name="Strength">The boundary filtering strength.</param>
internal readonly record struct H265EdgeInfo(int X, int Y, int IndexP, int IndexQ, bool Vertical, int Strength);
