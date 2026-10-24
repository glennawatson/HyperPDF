// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H265;

/// <summary>Everything a hardware decoder needs to decode one picture.</summary>
/// <param name="Picture">The picture to decode into, which already has its surface.</param>
/// <param name="Sps">The sequence parameter set.</param>
/// <param name="Pps">The picture parameter set.</param>
/// <param name="References">The reference picture set of the picture.</param>
/// <param name="Buffer">The pictures in the decoded picture buffer, which are the ones it may refer to and some more.</param>
/// <param name="Slices">The slice segments of the picture.</param>
/// <param name="Bytes">The NAL units of the slice segments one after the other, as they were in the packet.</param>
[DebuggerDisplay("H265HardwarePicture: POC {Picture.Poc}, {Slices.Count} slices")]
internal sealed record H265HardwarePicture(
    H265Picture Picture,
    H265Sps Sps,
    H265Pps Pps,
    H265RefPicSet References,
    IReadOnlyList<H265Picture> Buffer,
    IReadOnlyList<H265HardwareSlice> Slices,
    ReadOnlyMemory<byte> Bytes);
