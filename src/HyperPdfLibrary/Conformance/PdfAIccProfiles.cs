// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Conformance;

/// <summary>Reads the ICC profile header fields the PDF/A rules check, as veraPDF reads them.</summary>
internal static class PdfAIccProfiles
{
    /// <summary>The printer (output) device class.</summary>
    internal const uint Printer = 0x70727472;

    /// <summary>The monitor (display) device class.</summary>
    internal const uint Monitor = 0x6D6E7472;

    /// <summary>The scanner (input) device class.</summary>
    internal const uint Scanner = 0x73636E72;

    /// <summary>The colour space conversion device class.</summary>
    internal const uint ColourSpaceClass = 0x73706163;

    /// <summary>The RGB data colour space.</summary>
    internal const uint Rgb = 0x52474220;

    /// <summary>The CMYK data colour space.</summary>
    internal const uint Cmyk = 0x434D594B;

    /// <summary>The grey data colour space.</summary>
    internal const uint Gray = 0x47524159;

    /// <summary>The CIELAB data colour space.</summary>
    internal const uint Lab = 0x4C616220;

    /// <summary>The colour space text veraPDF compares with for RGB.</summary>
    internal const string RgbText = "RGB ";

    /// <summary>The colour space text veraPDF compares with for CMYK.</summary>
    internal const string CmykText = "CMYK";

    /// <summary>The colour space text veraPDF compares with for grey.</summary>
    internal const string GrayText = "GRAY";

    /// <summary>The offset of the profile size.</summary>
    private const int SizeOffset = 0;

    /// <summary>The offset of the major version byte.</summary>
    private const int VersionOffset = 8;

    /// <summary>The offset of the device class signature.</summary>
    private const int DeviceClassOffset = 12;

    /// <summary>The offset of the data colour space signature.</summary>
    private const int ColourSpaceOffset = 16;

    /// <summary>The offset of the profile flags, zeroed before comparing profiles.</summary>
    private const int FlagsOffset = 44;

    /// <summary>The offset of the rendering intent, zeroed before comparing profiles.</summary>
    private const int IntentOffset = 64;

    /// <summary>The offset of the profile ID.</summary>
    private const int ProfileIdOffset = 84;

    /// <summary>The length of a header field signature.</summary>
    private const int SignatureLength = 4;

    /// <summary>The length of the profile ID.</summary>
    private const int ProfileIdLength = 16;

    /// <summary>The header bytes the version needs: the major and minor version bytes.</summary>
    private const int VersionBytes = VersionOffset + 2;

    /// <summary>Reads the header of an ICC profile stream.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="profile">The profile stream.</param>
    /// <returns>The header; empty fields when the stream is damaged or short.</returns>
    internal static PdfAIccHeader ReadHeader(PdfAColourState state, PdfStream profile)
    {
        var buffer = default(PooledBuffer);
        try
        {
            _ = PdfAStreams.TryDecode(profile, ref buffer, state.Context.CancellationToken);
            return Header(buffer.WrittenSpan);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Reads the header fields from profile data.</summary>
    /// <param name="data">The profile.</param>
    /// <returns>The header.</returns>
    internal static PdfAIccHeader Header(ReadOnlySpan<byte> data) => new(
        Signature(data, DeviceClassOffset),
        Signature(data, ColourSpaceOffset),
        data.Length >= VersionBytes ? data[VersionOffset] : -1);

    /// <summary>Gets the text veraPDF compares a colour space signature with.</summary>
    /// <param name="signature">The signature; zero for none.</param>
    /// <returns>The four characters, or null for none.</returns>
    internal static string? SpaceText(uint signature) => signature switch
    {
        0 => null,
        Rgb => RgbText,
        Cmyk => CmykText,
        Gray => GrayText,
        _ => Encoding.Latin1.GetString(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(signature))),
    };

    /// <summary>
    /// Gets what identifies a profile when veraPDF compares two: its profile ID when set, otherwise the MD5 digest of the
    /// profile with its flags, rendering intent and ID zeroed, which is what the ID would be.
    /// </summary>
    /// <param name="state">The walk.</param>
    /// <param name="profile">The profile stream.</param>
    /// <returns>The identity, or null when the profile cannot be read.</returns>
    internal static byte[]? Identity(PdfAColourState state, PdfStream profile)
    {
        var buffer = default(PooledBuffer);
        try
        {
            return PdfAStreams.TryDecode(profile, ref buffer, state.Context.CancellationToken) ? Identity(buffer.WrittenSpan) : null;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Gets the identity of profile data.</summary>
    /// <param name="data">The profile.</param>
    /// <returns>The identity, or null when the data is shorter than its declared size.</returns>
    internal static byte[]? Identity(ReadOnlySpan<byte> data)
    {
        if (data.Length >= ProfileIdOffset + ProfileIdLength && data.Slice(ProfileIdOffset, ProfileIdLength).ContainsAnyExcept((byte)0))
        {
            return data.Slice(ProfileIdOffset, ProfileIdLength).ToArray();
        }

        var size = data.Length >= SignatureLength ? (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(data[SizeOffset..]), int.MaxValue) : data.Length;
        if (size > data.Length)
        {
            return null;
        }

        // The profile ID is the MD5 of the profile with these fields zeroed (ISO 15076-1 7.2.18), so an ID and a digest compare.
        var bytes = data[..size].ToArray();
        Zero(bytes, FlagsOffset, SignatureLength);
        Zero(bytes, IntentOffset, SignatureLength);
        Zero(bytes, ProfileIdOffset, ProfileIdLength);
        var digest = new byte[Md5.HashLength];
        _ = Md5.HashData(bytes, digest);
        return digest;
    }

    /// <summary>Zeroes a field when the data holds all of it.</summary>
    /// <param name="bytes">The data.</param>
    /// <param name="offset">The field offset.</param>
    /// <param name="length">The field length.</param>
    private static void Zero(byte[] bytes, int offset, int length)
    {
        if (offset + length <= bytes.Length)
        {
            bytes.AsSpan(offset, length).Clear();
        }
    }

    /// <summary>Reads a four-byte header signature.</summary>
    /// <param name="data">The profile.</param>
    /// <param name="offset">The field offset.</param>
    /// <returns>The signature, or zero when missing.</returns>
    private static uint Signature(ReadOnlySpan<byte> data, int offset) =>
        data.Length >= offset + SignatureLength ? BinaryPrimitives.ReadUInt32BigEndian(data[offset..]) : 0;
}
