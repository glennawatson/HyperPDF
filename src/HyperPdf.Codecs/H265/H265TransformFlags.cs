// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The coded block flags of a transform unit.</summary>
/// <param name="Luma">Whether the luma block has coefficients.</param>
/// <param name="Chroma">The flags of the chroma blocks that belong to the unit; for 4x4 luma units of subsampled video these are the parent's flags.</param>
internal readonly record struct H265TransformFlags(bool Luma, H265ChromaCbf Chroma);
