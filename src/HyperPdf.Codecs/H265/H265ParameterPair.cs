// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H265;

/// <summary>The sequence and picture parameter sets a picture uses.</summary>
/// <param name="Sps">The sequence parameter set.</param>
/// <param name="Pps">The picture parameter set.</param>
[DebuggerDisplay("H265ParameterPair: sequence {Sps.Id}, picture {Pps.Id}")]
internal readonly record struct H265ParameterPair(H265Sps Sps, H265Pps Pps);
