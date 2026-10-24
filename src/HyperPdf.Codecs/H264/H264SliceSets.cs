// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The parameter sets a slice uses.</summary>
/// <param name="Sps">The sequence parameter set.</param>
/// <param name="Pps">The picture parameter set.</param>
internal readonly record struct H264SliceSets(H264Sps Sps, H264Pps Pps);
