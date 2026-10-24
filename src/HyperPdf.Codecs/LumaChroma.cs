// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs;

/// <summary>A value for the luma component and one shared by both chroma components.</summary>
/// <param name="Luma">The luma value.</param>
/// <param name="Chroma">The chroma value.</param>
internal readonly record struct LumaChroma(int Luma, int Chroma);
