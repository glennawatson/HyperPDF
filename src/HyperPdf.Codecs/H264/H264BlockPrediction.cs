// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>How a block is predicted: from list 0, from list 1, or from both.</summary>
/// <param name="L0">The motion for list 0.</param>
/// <param name="L1">The motion for list 1.</param>
internal readonly record struct H264BlockPrediction(H264ListMotion L0, H264ListMotion L1);
