// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The part of a picture parameter set that streams of the high profiles add.</summary>
/// <param name="Transform8x8">Whether macroblocks may use the 8x8 transform.</param>
/// <param name="Scaling">The scaling lists the picture uses.</param>
/// <param name="SecondChromaOffset">The offset of the Cr quantiser from the luma quantiser.</param>
internal sealed record H264PpsExtension(bool Transform8x8, H264ScalingLists Scaling, int SecondChromaOffset);
