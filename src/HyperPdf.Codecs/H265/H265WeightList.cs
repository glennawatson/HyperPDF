// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The prediction weights for the pictures of one reference list.</summary>
/// <param name="LumaWeight">The luma weight of each reference picture.</param>
/// <param name="LumaOffset">The luma offset of each reference picture.</param>
/// <param name="ChromaWeight">The Cb then Cr weight of each reference picture.</param>
/// <param name="ChromaOffset">The Cb then Cr offset of each reference picture.</param>
internal sealed record H265WeightList(int[] LumaWeight, int[] LumaOffset, int[] ChromaWeight, int[] ChromaOffset);
