// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Export;

/// <summary>Writes Photoshop documents with the pixels raw, one plane for each channel.</summary>
internal static class PsdRasterWriter
{
    /// <summary>The bytes of the file header.</summary>
    private const int HeaderBytes = 26;

    /// <summary>The bytes of the colour mode, image resources and layer sections, each only a zero length, and the compression field.</summary>
    private const int EmptySectionBytes = (sizeof(uint) * 3) + sizeof(ushort);

    /// <summary>The version number of the format.</summary>
    private const ushort Version = 1;

    /// <summary>The reserved bytes after the version.</summary>
    private const int ReservedBytes = 6;

    /// <summary>The bits in each sample.</summary>
    private const ushort Depth = 8;

    /// <summary>The colour mode for grey.</summary>
    private const ushort ModeGrey = 1;

    /// <summary>The colour mode for red-green-blue.</summary>
    private const ushort ModeRgb = 3;

    /// <summary>Gets the signature, "8BPS".</summary>
    private static ReadOnlySpan<byte> Signature => "8BPS"u8;

    /// <summary>Writes a Photoshop file.</summary>
    /// <param name="samples">The image.</param>
    /// <returns>The file.</returns>
    internal static byte[] Write(RasterSamples samples)
    {
        var writer = new RasterByteWriter(new byte[HeaderBytes + EmptySectionBytes + samples.Data.Length]);
        writer.WriteBytes(Signature);
        writer.WriteUInt16BigEndian(Version);
        writer.Skip(ReservedBytes);
        writer.WriteUInt16BigEndian((ushort)samples.Channels);
        writer.WriteUInt32BigEndian((uint)samples.Height);
        writer.WriteUInt32BigEndian((uint)samples.Width);
        writer.WriteUInt16BigEndian(Depth);
        writer.WriteUInt16BigEndian(samples.IsGrey ? ModeGrey : ModeRgb);

        // The colour mode data, image resources and layer and mask sections are empty; the raw pixels follow with compression 0.
        writer.Skip(EmptySectionBytes);
        for (var channel = 0; channel < samples.Channels; channel++)
        {
            for (var i = channel; i < samples.Data.Length; i += samples.Channels)
            {
                writer.WriteByte(samples.Data[i]);
            }
        }

        return writer.Buffer;
    }
}
