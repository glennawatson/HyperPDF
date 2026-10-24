// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>Names the NAL unit types of H.264 and reads the one byte NAL unit header.</summary>
internal static class H264Nal
{
    /// <summary>The NAL unit type of a coded slice of a picture that is not an IDR picture.</summary>
    internal const int Slice = 1;

    /// <summary>The NAL unit type of a coded slice of an IDR picture.</summary>
    internal const int IdrSlice = 5;

    /// <summary>The NAL unit type of supplemental enhancement information.</summary>
    internal const int Sei = 6;

    /// <summary>The NAL unit type of a sequence parameter set.</summary>
    internal const int Sps = 7;

    /// <summary>The NAL unit type of a picture parameter set.</summary>
    internal const int Pps = 8;

    /// <summary>The NAL unit type of an access unit delimiter.</summary>
    internal const int AccessUnitDelimiter = 9;

    /// <summary>The NAL unit type of the end of a sequence.</summary>
    internal const int EndOfSequence = 10;

    /// <summary>The NAL unit type of the end of a stream.</summary>
    internal const int EndOfStream = 11;

    /// <summary>The bits of the NAL unit type in the header byte.</summary>
    private const int TypeMask = 0x1F;

    /// <summary>The shift and mask of the reference indication in the header byte.</summary>
    private const int RefShift = 5;

    /// <summary>The mask of the reference indication once shifted.</summary>
    private const int RefMask = 3;

    /// <summary>Reads the type from a NAL unit header byte.</summary>
    /// <param name="header">The header byte.</param>
    /// <returns>The NAL unit type.</returns>
    internal static int TypeOf(byte header) => header & TypeMask;

    /// <summary>Reads the reference indication from a NAL unit header byte.</summary>
    /// <param name="header">The header byte.</param>
    /// <returns>0 for a unit nothing refers to, otherwise how important it is to keep.</returns>
    internal static int ReferenceIdcOf(byte header) => (header >> RefShift) & RefMask;
}
