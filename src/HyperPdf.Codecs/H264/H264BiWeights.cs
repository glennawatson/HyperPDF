// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The weights for the three colour planes of a block predicted from both lists.</summary>
/// <param name="Luma">The luma weights.</param>
/// <param name="Cb">The Cb weights.</param>
/// <param name="Cr">The Cr weights.</param>
internal readonly record struct H264BiWeights(H264PlaneWeights Luma, H264PlaneWeights Cb, H264PlaneWeights Cr);
