// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The fields of a slice header that give a picture its order count.</summary>
/// <param name="Lsb">The coded low bits of the count, for type 0.</param>
/// <param name="DeltaBottom">The offset of the bottom field's count in a frame, for type 0.</param>
/// <param name="Delta0">The first offset, for type 1.</param>
/// <param name="Delta1">The second offset, for type 1.</param>
internal readonly record struct H264PocFields(int Lsb, int DeltaBottom, int Delta0, int Delta1);
