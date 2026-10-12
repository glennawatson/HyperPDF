// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Maps the colour specifications of JPEG 2000 data to colour spaces, as veraPDF does.</summary>
internal static class PdfAJpxSpaces
{
    /// <summary>The enumerated CMYK space.</summary>
    private const long EnumeratedCmyk = 12;

    /// <summary>The enumerated CIELab space.</summary>
    private const long EnumeratedLab = 14;

    /// <summary>The enumerated sRGB space.</summary>
    private const long EnumeratedSrgb = 16;

    /// <summary>The enumerated greyscale space.</summary>
    private const long EnumeratedGray = 17;

    /// <summary>The enumerated sYCC space.</summary>
    private const long EnumeratedSycc = 18;

    /// <summary>The enumerated e-sRGB space.</summary>
    private const long EnumeratedESrgb = 20;

    /// <summary>The enumerated ROMM-RGB space.</summary>
    private const long EnumeratedRomm = 21;

    /// <summary>The enumerated e-sYCC space.</summary>
    private const long EnumeratedESycc = 24;

    /// <summary>The fewest bytes a profile needs to hold its colour space.</summary>
    private const int ProfileSpaceEnd = 20;

    /// <summary>The offset of the data colour space in a profile.</summary>
    private const int ProfileSpaceOffset = 16;

    /// <summary>The components of a grey space.</summary>
    private const int Gray = 1;

    /// <summary>The components of a three-component space.</summary>
    private const int Three = 3;

    /// <summary>The components of a four-component space.</summary>
    private const int Four = 4;

    /// <summary>The low three bytes of a generic colourant signature, <c>CLR</c>.</summary>
    private const uint ColourantSuffix = 0x00434C52;

    /// <summary>Selects the low three bytes of a signature.</summary>
    private const uint ColourantSuffixMask = 0x00FFFFFF;

    /// <summary>The shift that brings a signature's first byte down.</summary>
    private const int ColourantDigitShift = 24;

    /// <summary>The fewest colourants a generic colourant signature names.</summary>
    private const int MinimumColourants = 2;

    /// <summary>Gets the space of an enumerated colour specification.</summary>
    /// <param name="value">The enumerated value.</param>
    /// <returns>The space, or null for values veraPDF does not map.</returns>
    internal static PdfAJpxSpace? FromEnumerated(long value) => value switch
    {
        EnumeratedCmyk => new(KnownName.DeviceCMYK, Four, null),
        EnumeratedLab => new(KnownName.Lab, Three, null),
        EnumeratedGray => new(KnownName.ICCBased, Gray, null),
        EnumeratedSrgb or EnumeratedSycc or EnumeratedESrgb or EnumeratedRomm or EnumeratedESycc => new(KnownName.ICCBased, Three, null),
        _ => null,
    };

    /// <summary>Gets the space of an embedded ICC profile from the colour space in its header.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The space, or null when the header names no known colour space.</returns>
    internal static PdfAJpxSpace? FromProfile(ReadOnlySpan<byte> profile)
    {
        if (profile.Length < ProfileSpaceEnd)
        {
            return null;
        }

        var components = Components(BinaryPrimitives.ReadUInt32BigEndian(profile[ProfileSpaceOffset..]));
        return components > 0 ? new(KnownName.ICCBased, components, profile.ToArray()) : null;
    }

    /// <summary>Gets the number of components of an ICC colour space signature.</summary>
    /// <param name="signature">The signature.</param>
    /// <returns>The components, or zero when unknown.</returns>
    private static int Components(uint signature) => signature switch
    {
        PdfAIccProfiles.Gray => Gray,
        0x58595A20 or 0x4C616220 or 0x4C757620 or 0x59436272 or 0x59787920 or 0x52474220 or 0x48535620 or 0x484C5320 or 0x434D5920 => Three,
        PdfAIccProfiles.Cmyk => Four,
        _ => ColourantCount(signature),
    };

    /// <summary>Gets the component count of a generic <c>nCLR</c> signature, where n is a hexadecimal digit from 2 to F.</summary>
    /// <param name="signature">The signature.</param>
    /// <returns>The count, or zero when the signature is not one.</returns>
    private static int ColourantCount(uint signature)
    {
        if ((signature & ColourantSuffixMask) != ColourantSuffix)
        {
            return 0;
        }

        Span<char> digit = [(char)(signature >> ColourantDigitShift)];
        return char.IsAsciiHexDigitUpper(digit[0])
            && int.TryParse(digit, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var count) && count >= MinimumColourants ? count : 0;
    }
}
