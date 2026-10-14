// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// Tells whether an ICC profile is of the kind the JP2 restricted ICC method allows (T.800 I.5.3.3, table I.9): a monochrome
/// input or display profile, or a three-component matrix-based input or display profile, with the PCSXYZ connection space.
/// Any other profile needs the Any ICC method of a JPX file (ISO 15444-2 M.3.3).
/// </summary>
internal static class JpxIccProfiles
{
    /// <summary>The bytes of the profile header.</summary>
    private const int HeaderBytes = 128;

    /// <summary>The offset of the profile class.</summary>
    private const int ClassOffset = 12;

    /// <summary>The offset of the data colour space.</summary>
    private const int SpaceOffset = 16;

    /// <summary>The offset of the profile connection space.</summary>
    private const int ConnectionOffset = 20;

    /// <summary>The bytes of a tag table entry: signature, offset and size.</summary>
    private const int TagBytes = 12;

    /// <summary>The bytes of a four-character code.</summary>
    private const int CodeBytes = 4;

    /// <summary>Gets the code of an input device profile, 'scnr'.</summary>
    private static ReadOnlySpan<byte> InputClass => "scnr"u8;

    /// <summary>Gets the code of a display device profile, 'mntr'.</summary>
    private static ReadOnlySpan<byte> DisplayClass => "mntr"u8;

    /// <summary>Gets the code of the grey colour space, 'GRAY'.</summary>
    private static ReadOnlySpan<byte> GraySpace => "GRAY"u8;

    /// <summary>Gets the code of the RGB colour space, 'RGB '.</summary>
    private static ReadOnlySpan<byte> RgbSpace => "RGB "u8;

    /// <summary>Gets the code of the XYZ connection space, 'XYZ '.</summary>
    private static ReadOnlySpan<byte> XyzSpace => "XYZ "u8;

    /// <summary>Gets the tag of the grey tone curve, 'kTRC'.</summary>
    private static ReadOnlySpan<byte> GrayCurve => "kTRC"u8;

    /// <summary>Gets the tag of the red colorant, 'rXYZ', which only a matrix-based profile has.</summary>
    private static ReadOnlySpan<byte> RedColorant => "rXYZ"u8;

    /// <summary>Determines whether a profile is of the restricted kind.</summary>
    /// <param name="profile">The profile bytes.</param>
    /// <returns><see langword="true"/> for a monochrome or matrix-based input or display profile with the XYZ connection space.</returns>
    internal static bool IsRestricted(ReadOnlySpan<byte> profile)
    {
        if (profile.Length < HeaderBytes + sizeof(int))
        {
            return false;
        }

        var kind = profile.Slice(ClassOffset, CodeBytes);
        var space = profile.Slice(SpaceOffset, CodeBytes);
        if ((!kind.SequenceEqual(InputClass) && !kind.SequenceEqual(DisplayClass)) || !profile.Slice(ConnectionOffset, CodeBytes).SequenceEqual(XyzSpace))
        {
            return false;
        }

        return space.SequenceEqual(GraySpace) ? HasTag(profile, GrayCurve) : space.SequenceEqual(RgbSpace) && HasTag(profile, RedColorant);
    }

    /// <summary>Determines whether the tag table lists a tag.</summary>
    /// <param name="profile">The profile bytes.</param>
    /// <param name="code">The four-character code of the tag.</param>
    /// <returns><see langword="true"/> when the tag is listed.</returns>
    private static bool HasTag(ReadOnlySpan<byte> profile, ReadOnlySpan<byte> code)
    {
        var count = BinaryPrimitives.ReadUInt32BigEndian(profile[HeaderBytes..]);
        var available = (profile.Length - HeaderBytes - sizeof(int)) / TagBytes;
        var listed = (int)Math.Min(count, (uint)available);
        for (var i = 0; i < listed; i++)
        {
            if (profile.Slice(HeaderBytes + sizeof(int) + (i * TagBytes), CodeBytes).SequenceEqual(code))
            {
                return true;
            }
        }

        return false;
    }
}
