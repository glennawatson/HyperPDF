// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// Reads ICC profiles for ICCBased colour spaces. Profiles with an AToB0 look-up table (CMYK, Lab, N-colour and RGB or gray
/// profiles that have one) convert through <see cref="IccLutTransform"/>; other RGB and gray profiles use their matrix and
/// curves. Profiles that cannot be read make the colour space fall back to its /Alternate. Each stream caches its parsed
/// result, and a document's drawing services share transforms between streams containing the same bytes.
/// </summary>
internal static class IccProfile
{
    /// <summary>The size of the profile header.</summary>
    private const int HeaderSize = 128;

    /// <summary>The offset of the data colour space signature.</summary>
    private const int ColorSpaceOffset = 16;

    /// <summary>The offset of the profile connection space signature.</summary>
    private const int ConnectionSpaceOffset = 20;

    /// <summary>The offset of the 'acsp' file signature.</summary>
    private const int FileSignatureOffset = 36;

    /// <summary>The bytes in a tag table entry.</summary>
    private const int TagEntrySize = 12;

    /// <summary>The most tags read.</summary>
    private const int MaxTags = 256;

    /// <summary>The bytes in the tag count.</summary>
    private const int TagCountSize = 4;

    /// <summary>The offset of the offset within a tag table entry.</summary>
    private const int TagOffsetField = 4;

    /// <summary>The offset of the size within a tag table entry.</summary>
    private const int TagSizeField = 8;

    /// <summary>The offset of the first XYZ number in an XYZ tag.</summary>
    private const int XyzOffset = 8;

    /// <summary>The bytes in an s15Fixed16 number.</summary>
    private const int FixedSize = 4;

    /// <summary>The scale of an s15Fixed16 number.</summary>
    private const float FixedScale = 65536F;

    /// <summary>The bytes in an XYZ tag.</summary>
    private const int XyzTagSize = 20;

    /// <summary>The 'acsp' signature.</summary>
    private const uint FileSignature = 0x61637370;

    /// <summary>The 'RGB ' data colour space.</summary>
    private const uint RgbSpace = 0x52474220;

    /// <summary>The 'GRAY' data colour space.</summary>
    private const uint GraySpace = 0x47524159;

    /// <summary>The 'XYZ ' connection space and tag type.</summary>
    private const uint XyzSpace = 0x58595A20;

    /// <summary>The 'Lab ' connection space and data colour space.</summary>
    private const uint LabSpace = 0x4C616220;

    /// <summary>The 'CMYK' data colour space.</summary>
    private const uint CmykSpace = 0x434D594B;

    /// <summary>The 'A2B0' tag, the perceptual look-up table.</summary>
    private const uint PerceptualTable = 0x41324230;

    /// <summary>The offset of the major version byte.</summary>
    private const int VersionOffset = 8;

    /// <summary>The channels of RGB and Lab.</summary>
    private const int RgbChannels = 3;

    /// <summary>The channels of CMYK.</summary>
    private const int CmykChannels = 4;

    /// <summary>The stored value of a Lab a* or b* of zero.</summary>
    private const float LabZeroChroma = 128F / 255F;

    /// <summary>The bit position of the digit in a 'nCLR' signature.</summary>
    private const int ColorantDigitShift = 24;

    /// <summary>The mask of the 'CLR' part of a 'nCLR' signature.</summary>
    private const uint ColorantSuffixMask = 0x00FFFFFF;

    /// <summary>The 'CLR' part of a 'nCLR' signature.</summary>
    private const uint ColorantSuffix = 0x434C52;

    /// <summary>The fewest channels of a 'nCLR' colour space.</summary>
    private const int MinColorants = 2;

    /// <summary>The most channels of a 'nCLR' colour space that are supported.</summary>
    private const int MaxColorants = 8;

    /// <summary>The 'rXYZ' tag.</summary>
    private const uint RedColorant = 0x7258595A;

    /// <summary>The 'gXYZ' tag.</summary>
    private const uint GreenColorant = 0x6758595A;

    /// <summary>The 'bXYZ' tag.</summary>
    private const uint BlueColorant = 0x6258595A;

    /// <summary>The 'rTRC' tag.</summary>
    private const uint RedCurve = 0x72545243;

    /// <summary>The 'gTRC' tag.</summary>
    private const uint GreenCurve = 0x67545243;

    /// <summary>The 'bTRC' tag.</summary>
    private const uint BlueCurve = 0x62545243;

    /// <summary>The 'kTRC' tag.</summary>
    private const uint GrayCurve = 0x6B545243;

    /// <summary>D50 XYZ to linear sRGB, red from X.</summary>
    private const float RR = 3.1338561F;

    /// <summary>D50 XYZ to linear sRGB, red from Y.</summary>
    private const float RG = -1.6168667F;

    /// <summary>D50 XYZ to linear sRGB, red from Z.</summary>
    private const float RB = -0.4906146F;

    /// <summary>D50 XYZ to linear sRGB, green from X.</summary>
    private const float GR = -0.9787684F;

    /// <summary>D50 XYZ to linear sRGB, green from Y.</summary>
    private const float GG = 1.9161415F;

    /// <summary>D50 XYZ to linear sRGB, green from Z.</summary>
    private const float GB = 0.0334540F;

    /// <summary>D50 XYZ to linear sRGB, blue from X.</summary>
    private const float BR = 0.0719453F;

    /// <summary>D50 XYZ to linear sRGB, blue from Y.</summary>
    private const float BG = -0.2289914F;

    /// <summary>D50 XYZ to linear sRGB, blue from Z.</summary>
    private const float BB = 1.4052427F;

    /// <summary>Gets the matrix from D50 XYZ (the profile connection space) to linear sRGB, Bradford adapted.</summary>
    internal static Matrix3 XyzToSrgb { get; } = new(RR, RG, RB, GR, GG, GB, BR, BG, BB);

    /// <summary>Gets the transform of a profile stream.</summary>
    /// <param name="stream">The ICC profile stream.</param>
    /// <param name="components">The /N value the colour space declares.</param>
    /// <returns>The transform, or <see langword="null"/> when the profile is unreadable, unsupported or has another component count.</returns>
    internal static IccTransform? Get(PdfStream stream, int components) =>
        stream.Dictionary.Owner?.Context?.DrawingServices is { } services
            ? services.Profiles.Get(stream, components)
            : IccProfileCache.ReadUnowned(stream, components);

    /// <summary>Reads a profile from bytes.</summary>
    /// <param name="profile">The profile data.</param>
    /// <returns>The transform, or <see langword="null"/> when the profile is invalid or unsupported.</returns>
    internal static IccTransform? Create(ReadOnlySpan<byte> profile) =>
        HasSupportedHeader(profile) ? CreateLookUp(profile) ?? CreateForSpace(profile) : null;

    /// <summary>Finds a tag's data.</summary>
    /// <param name="profile">The profile data.</param>
    /// <param name="signature">The tag signature.</param>
    /// <returns>The tag data, or an empty span when absent or out of bounds.</returns>
    internal static ReadOnlySpan<byte> FindTag(ReadOnlySpan<byte> profile, uint signature)
    {
        var count = (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(profile[HeaderSize..]), MaxTags);
        for (var i = 0; i < count; i++)
        {
            var entry = HeaderSize + TagCountSize + (i * TagEntrySize);
            if (entry + TagEntrySize > profile.Length)
            {
                break;
            }

            if (BinaryPrimitives.ReadUInt32BigEndian(profile[entry..]) != signature)
            {
                continue;
            }

            var offset = (long)BinaryPrimitives.ReadUInt32BigEndian(profile[(entry + TagOffsetField)..]);
            var size = (long)BinaryPrimitives.ReadUInt32BigEndian(profile[(entry + TagSizeField)..]);
            return offset + size <= profile.Length ? profile.Slice((int)offset, (int)size) : default;
        }

        return default;
    }

    /// <summary>Checks the file signature and that the profile connection space is XYZ or Lab.</summary>
    /// <param name="profile">The profile data.</param>
    /// <returns><see langword="true"/> when the header is valid and uses a supported connection space.</returns>
    private static bool HasSupportedHeader(ReadOnlySpan<byte> profile) =>
        profile.Length >= HeaderSize + TagCountSize
        && BinaryPrimitives.ReadUInt32BigEndian(profile[FileSignatureOffset..]) == FileSignature
        && BinaryPrimitives.ReadUInt32BigEndian(profile[ConnectionSpaceOffset..]) is XyzSpace or LabSpace;

    /// <summary>Builds the transform for the profile's matrix or gray tags.</summary>
    /// <param name="profile">The profile data.</param>
    /// <returns>The transform, or <see langword="null"/> for other colour spaces or a Lab connection space.</returns>
    private static IccTransform? CreateForSpace(ReadOnlySpan<byte> profile) =>
        BinaryPrimitives.ReadUInt32BigEndian(profile[ConnectionSpaceOffset..]) != XyzSpace
            ? null
            : BinaryPrimitives.ReadUInt32BigEndian(profile[ColorSpaceOffset..]) switch
            {
                RgbSpace => CreateRgb(profile),
                GraySpace => CreateGray(profile),
                _ => null,
            };

    /// <summary>Builds the transform of a profile that has an AToB0 look-up table.</summary>
    /// <param name="profile">The profile data.</param>
    /// <returns>The transform, or <see langword="null"/> when the profile has no valid table or has an unsupported colour space.</returns>
    private static IccLutTransform? CreateLookUp(ReadOnlySpan<byte> profile)
    {
        var shape = ShapeOf(profile);
        return shape is null || FindTag(profile, PerceptualTable).IsEmpty ? null : IccLutProfile.Create(profile.ToArray(), shape)?.Get(IccRendering.Default);
    }

    /// <summary>Reads the colour spaces and version from the header.</summary>
    /// <param name="profile">The profile data.</param>
    /// <returns>The shape, or <see langword="null"/> when the device colour space is not supported.</returns>
    private static IccLutShape? ShapeOf(ReadOnlySpan<byte> profile)
    {
        var labPcs = BinaryPrimitives.ReadUInt32BigEndian(profile[ConnectionSpaceOffset..]) == LabSpace;
        var version = profile[VersionOffset];
        var space = BinaryPrimitives.ReadUInt32BigEndian(profile[ColorSpaceOffset..]);
        return space switch
        {
            GraySpace => new(1, labPcs, false, version, [0F]),
            RgbSpace => new(RgbChannels, labPcs, false, version, [0F, 0F, 0F]),
            CmykSpace => new(CmykChannels, labPcs, false, version, [1F, 1F, 1F, 1F]),
            LabSpace => new(RgbChannels, labPcs, true, version, [0F, LabZeroChroma, LabZeroChroma]),
            _ => ColorantCount(space) is var count and > 0 ? new(count, labPcs, false, version, null) : null,
        };
    }

    /// <summary>Gets the number of channels of a 'nCLR' colour space signature.</summary>
    /// <param name="space">The signature.</param>
    /// <returns>The channel count from 2 to 8, or zero for other signatures.</returns>
    private static int ColorantCount(uint space)
    {
        var digit = (int)(space >> ColorantDigitShift) - '0';
        return (space & ColorantSuffixMask) == ColorantSuffix && digit is >= MinColorants and <= MaxColorants ? digit : 0;
    }

    /// <summary>Builds the transform of an RGB matrix profile.</summary>
    /// <param name="profile">The profile data.</param>
    /// <returns>The transform, or <see langword="null"/> when a colorant or curve tag is missing.</returns>
    private static IccMatrixTransform? CreateRgb(ReadOnlySpan<byte> profile)
    {
        var red = FindTag(profile, RedColorant);
        var green = FindTag(profile, GreenColorant);
        var blue = FindTag(profile, BlueColorant);
        var curves = new IccCurve?[]
        {
            IccCurve.Parse(FindTag(profile, RedCurve)),
            IccCurve.Parse(FindTag(profile, GreenCurve)),
            IccCurve.Parse(FindTag(profile, BlueCurve)),
        };
        return !TryReadXyz(red, out var r) || !TryReadXyz(green, out var g) || !TryReadXyz(blue, out var b) || curves[0] is null || curves[1] is null || curves[^1] is null
            ? null
            : new IccMatrixTransform([curves[0]!, curves[1]!, curves[^1]!], new(r.X, g.X, b.X, r.Y, g.Y, b.Y, r.Z, g.Z, b.Z));
    }

    /// <summary>Builds the transform of a gray profile.</summary>
    /// <param name="profile">The profile data.</param>
    /// <returns>The transform, or <see langword="null"/> when the curve tag is missing.</returns>
    private static IccGrayTransform? CreateGray(ReadOnlySpan<byte> profile)
    {
        var curve = IccCurve.Parse(FindTag(profile, GrayCurve));
        return curve is null ? null : new(curve);
    }

    /// <summary>Reads an XYZ tag.</summary>
    /// <param name="tag">The tag data.</param>
    /// <param name="xyz">Receives the colorant.</param>
    /// <returns><see langword="true"/> when the tag is a valid XYZ tag.</returns>
    private static bool TryReadXyz(ReadOnlySpan<byte> tag, out Float3 xyz)
    {
        xyz = default;
        if (tag.Length < XyzTagSize || BinaryPrimitives.ReadUInt32BigEndian(tag) != XyzSpace)
        {
            return false;
        }

        xyz = new(
            BinaryPrimitives.ReadInt32BigEndian(tag[XyzOffset..]) / FixedScale,
            BinaryPrimitives.ReadInt32BigEndian(tag[(XyzOffset + FixedSize)..]) / FixedScale,
            BinaryPrimitives.ReadInt32BigEndian(tag[(XyzOffset + (PdfColorSpace.PairSize * FixedSize))..]) / FixedScale);
        return true;
    }
}
