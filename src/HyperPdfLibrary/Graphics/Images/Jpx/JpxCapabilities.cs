// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Numerics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// What a codestream declares about the parts of JPEG 2000 it uses: the SIZ capabilities Rsiz and the CAP marker
/// (ISO 15444-1 A.5.2), with the Part 15 (high-throughput) capabilities Ccap15 of T.814. Decoding does not
/// depend on them; each tile-component's coding style says which block coder it uses (T.814 table A.1).
/// </summary>
/// <param name="Rsiz">The Rsiz field of SIZ.</param>
/// <param name="Parts">The Pcap field of CAP: bit 32 - i set when Part i is used; zero without CAP.</param>
/// <param name="HighThroughput">The Ccap15 field, or zero.</param>
internal readonly record struct JpxCapabilities(int Rsiz, uint Parts, int HighThroughput)
{
    /// <summary>The Rsiz bit that says a CAP marker is present.</summary>
    internal const int CapabilitiesFlag = 0x4000;

    /// <summary>The Pcap bit of Part 15.</summary>
    internal const uint Part15 = 0x0002_0000;

    /// <summary>The Ccap15 bits of the code-block mix: 00 HT only, 10 HT declared, 11 MIXED.</summary>
    private const int MixMask = 0xC000;

    /// <summary>The Ccap15 code-block mix of the MIXED mode.</summary>
    private const int Mixed = 0xC000;

    /// <summary>The bytes of Pcap.</summary>
    private const int PartsBytes = 4;

    /// <summary>The bytes of each Ccap entry.</summary>
    private const int EntryBytes = 2;

    /// <summary>The bit index of Part 15 in Pcap.</summary>
    private const int Part15Bit = 17;

    /// <summary>Gets a value indicating whether the codestream declares high-throughput block coding.</summary>
    internal bool DeclaresHighThroughput => (Rsiz & CapabilitiesFlag) != 0 && (Parts & Part15) != 0;

    /// <summary>Gets a value indicating whether the codestream declares the MIXED mode of HT and regular code-blocks.</summary>
    internal bool DeclaresMixed => DeclaresHighThroughput && (HighThroughput & MixMask) == Mixed;

    /// <summary>Reads a CAP marker segment.</summary>
    /// <param name="rsiz">The Rsiz field of SIZ.</param>
    /// <param name="segment">The segment after its length field.</param>
    /// <returns>The capabilities; a short segment gives only what fits.</returns>
    internal static JpxCapabilities Read(int rsiz, ReadOnlySpan<byte> segment)
    {
        if (segment.Length < PartsBytes)
        {
            return new(rsiz, 0, 0);
        }

        var parts = BinaryPrimitives.ReadUInt32BigEndian(segment);
        if ((parts & Part15) == 0)
        {
            return new(rsiz, parts, 0);
        }

        // Ccap entries follow in Part order, one per bit set in Pcap; Part 15's comes after those of the parts before it.
        var offset = PartsBytes + (BitOperations.PopCount(parts >> (Part15Bit + 1)) * EntryBytes);
        var value = offset + EntryBytes <= segment.Length ? BinaryPrimitives.ReadUInt16BigEndian(segment[offset..]) : 0;
        return new(rsiz, parts, value);
    }
}
