// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Mp3;

/// <summary>The version of the MPEG audio standard a frame is coded with.</summary>
internal enum MpegVersion
{
    /// <summary>MPEG 1: 32, 44.1 and 48 kHz, two granules in a layer 3 frame.</summary>
    Mpeg1 = 1,

    /// <summary>MPEG 2: 16, 22.05 and 24 kHz, one granule in a layer 3 frame.</summary>
    Mpeg2 = 2,

    /// <summary>The unofficial MPEG 2.5: 8, 11.025 and 12 kHz.</summary>
    Mpeg25 = 3,
}
