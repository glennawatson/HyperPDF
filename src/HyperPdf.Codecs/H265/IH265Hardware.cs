// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>
/// A hardware decoder the H.265 decoder hands the reconstruction of pictures to. The decoder still reads the headers, works out picture order counts,
/// keeps the reference picture buffer and orders the output; the hardware only turns the slices of one picture into samples.
/// </summary>
internal interface IH265Hardware
{
    /// <summary>Takes a surface for a picture.</summary>
    /// <returns>The surface.</returns>
    uint RentSurface();

    /// <summary>Gives a surface back once its picture is no longer a reference.</summary>
    /// <param name="surface">The surface.</param>
    void ReturnSurface(uint surface);

    /// <summary>Decodes one picture and reads the shown part of it back.</summary>
    /// <param name="picture">The picture, with its slices and references.</param>
    /// <param name="frame">Receives the shown part of the picture; every sample is written.</param>
    void Decode(H265HardwarePicture picture, VideoFrame frame);
}
