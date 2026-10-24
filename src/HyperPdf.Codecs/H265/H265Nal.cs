// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Names the NAL unit types of H.265 and reads the two byte NAL unit header.</summary>
internal static class H265Nal
{
    /// <summary>The bytes of a NAL unit header.</summary>
    internal const int HeaderBytes = 2;

    /// <summary>The type of a coded slice segment of a RASL picture that is not used for reference by pictures of the same sub-layer.</summary>
    internal const int RaslN = 8;

    /// <summary>The type of a coded slice segment of a RASL picture.</summary>
    internal const int RaslR = 9;

    /// <summary>The type of a coded slice segment of a RADL picture that is not used for reference by pictures of the same sub-layer.</summary>
    internal const int RadlN = 6;

    /// <summary>The type of a coded slice segment of a RADL picture.</summary>
    internal const int RadlR = 7;

    /// <summary>The first type of a broken link access picture.</summary>
    internal const int BlaWLp = 16;

    /// <summary>The type of a broken link access picture without leading pictures.</summary>
    internal const int BlaNLp = 18;

    /// <summary>The type of an instantaneous decoding refresh picture that may have RADL pictures.</summary>
    internal const int IdrWRadl = 19;

    /// <summary>The type of an instantaneous decoding refresh picture without leading pictures.</summary>
    internal const int IdrNLp = 20;

    /// <summary>The type of a clean random access picture.</summary>
    internal const int Cra = 21;

    /// <summary>The last reserved type of an intra random access picture.</summary>
    internal const int ReservedIrapLast = 23;

    /// <summary>The last type that carries a slice segment of the base layer.</summary>
    internal const int LastVcl = 31;

    /// <summary>The type of a video parameter set.</summary>
    internal const int Vps = 32;

    /// <summary>The type of a sequence parameter set.</summary>
    internal const int Sps = 33;

    /// <summary>The type of a picture parameter set.</summary>
    internal const int Pps = 34;

    /// <summary>The type of an access unit delimiter.</summary>
    internal const int AccessUnitDelimiter = 35;

    /// <summary>The type of the end of a sequence.</summary>
    internal const int EndOfSequence = 36;

    /// <summary>The type of the end of a bitstream.</summary>
    internal const int EndOfBitstream = 37;

    /// <summary>The last type of a sub-layer non-reference picture that is not a leading picture or random access picture.</summary>
    private const int LastSubLayerNonReference = 14;

    /// <summary>The shift of the type in the first header byte.</summary>
    private const int TypeShift = 1;

    /// <summary>The mask of the type once shifted.</summary>
    private const int TypeMask = 0x3F;

    /// <summary>The shift of the high bit of the layer number from the first header byte.</summary>
    private const int LayerHighShift = 5;

    /// <summary>The shift of the low bits of the layer number in the second header byte.</summary>
    private const int LayerLowShift = 3;

    /// <summary>The mask of the temporal identifier plus one.</summary>
    private const int TemporalMask = 7;

    /// <summary>Reads the NAL unit type from a NAL unit.</summary>
    /// <param name="nal">The NAL unit including its header.</param>
    /// <returns>The type, from 0 to 63.</returns>
    internal static int TypeOf(ReadOnlySpan<byte> nal) => (nal[0] >> TypeShift) & TypeMask;

    /// <summary>Reads the layer number from a NAL unit.</summary>
    /// <param name="nal">The NAL unit including its header.</param>
    /// <returns>The layer number; 0 is the base layer.</returns>
    internal static int LayerOf(ReadOnlySpan<byte> nal) => ((nal[0] & 1) << LayerHighShift) | (nal[1] >> LayerLowShift);

    /// <summary>Reads the temporal sub-layer number from a NAL unit.</summary>
    /// <param name="nal">The NAL unit including its header.</param>
    /// <returns>The temporal identifier.</returns>
    internal static int TemporalIdOf(ReadOnlySpan<byte> nal) => (nal[1] & TemporalMask) - 1;

    /// <summary>Tells whether a type is that of an intra random access point picture.</summary>
    /// <param name="type">The NAL unit type.</param>
    /// <returns><see langword="true"/> for types 16 to 23.</returns>
    internal static bool IsIrap(int type) => type is >= BlaWLp and <= ReservedIrapLast;

    /// <summary>Tells whether a type is that of an instantaneous decoding refresh picture.</summary>
    /// <param name="type">The NAL unit type.</param>
    /// <returns><see langword="true"/> for IDR_W_RADL and IDR_N_LP.</returns>
    internal static bool IsIdr(int type) => type is IdrWRadl or IdrNLp;

    /// <summary>Tells whether a type is that of a broken link access picture.</summary>
    /// <param name="type">The NAL unit type.</param>
    /// <returns><see langword="true"/> for the three BLA types.</returns>
    internal static bool IsBla(int type) => type is >= BlaWLp and <= BlaNLp;

    /// <summary>Tells whether a type is that of a random access skipped leading picture.</summary>
    /// <param name="type">The NAL unit type.</param>
    /// <returns><see langword="true"/> for RASL_N and RASL_R.</returns>
    internal static bool IsRasl(int type) => type is RaslN or RaslR;

    /// <summary>Tells whether a picture of this type is never used for reference by pictures of its own sub-layer.</summary>
    /// <param name="type">The NAL unit type.</param>
    /// <returns><see langword="true"/> for the even types up to 14.</returns>
    internal static bool IsSubLayerNonReference(int type) => type <= LastSubLayerNonReference && (type & 1) == 0;

    /// <summary>Tells whether a type carries a slice segment.</summary>
    /// <param name="type">The NAL unit type.</param>
    /// <returns><see langword="true"/> for the types 0 to 9 and 16 to 21.</returns>
    internal static bool IsSlice(int type) => type <= RaslR || type is >= BlaWLp and <= Cra;
}
