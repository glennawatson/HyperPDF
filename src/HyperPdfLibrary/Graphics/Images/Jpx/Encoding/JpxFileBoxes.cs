// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// Writes the JP2 file format around a codestream (ISO 15444-1 annex I): the signature, file type and header boxes, with an
/// image header, one colour specification box (enumerated or an ICC profile) and, for an image with opacity, a channel
/// definition box. The codestream goes in the contiguous codestream box. No bits-per-component box is written, as every
/// component has the same depth.
/// </summary>
internal static class JpxFileBoxes
{
    /// <summary>The bytes of a box header.</summary>
    private const int BoxHeaderBytes = 8;

    /// <summary>The enumerated colour space of CMYK.</summary>
    private const int CmykSpace = 12;

    /// <summary>The enumerated colour space of sRGB.</summary>
    private const int SrgbSpace = 16;

    /// <summary>The enumerated colour space of greyscale.</summary>
    private const int GraySpace = 17;

    /// <summary>The colour channels of a greyscale image.</summary>
    private const int GrayChannels = 1;

    /// <summary>The colour channels of an RGB image.</summary>
    private const int RgbChannels = 3;

    /// <summary>The colour channels of a CMYK image.</summary>
    private const int CmykChannels = 4;

    /// <summary>The colour method of an enumerated colour space.</summary>
    private const byte EnumeratedMethod = 1;

    /// <summary>The colour method of a restricted ICC profile.</summary>
    private const byte IccMethod = 2;

    /// <summary>The colour method of any ICC profile (ISO 15444-2).</summary>
    private const byte AnyIccMethod = 3;

    /// <summary>The APPROX value of a colour specification that describes its colour space accurately (ISO 15444-2 table M.23).</summary>
    private const byte JpxAccurate = 1;

    /// <summary>The standard feature flag of an unrestricted JPEG 2000 Part 1 codestream (ISO 15444-2 table M.14, corrigendum 4).</summary>
    private const ushort CodestreamFlag = 5;

    /// <summary>The standard feature flag of a non-premultiplied opacity channel.</summary>
    private const ushort OpacityFlag = 9;

    /// <summary>The standard feature flag of a compositing layer that uses an Any ICC profile.</summary>
    private const ushort AnyIccFlag = 44;

    /// <summary>The standard feature flag of the CMYK enumerated colour space.</summary>
    private const ushort CmykFlag = 55;

    /// <summary>The most features the box lists.</summary>
    private const int MaxFeatures = 3;

    /// <summary>The features every JPX file lists: the codestream and the colour space.</summary>
    private const int TwoFeatures = 2;

    /// <summary>The bytes of a requirement mask: one covers up to eight requirements.</summary>
    private const int MaskLength = 1;

    /// <summary>The compression type of JPEG 2000 in the image header box.</summary>
    private const byte Compression = 7;

    /// <summary>The bytes of the image header box contents.</summary>
    private const int ImageHeaderBytes = 14;

    /// <summary>The offset of the width in the image header box.</summary>
    private const int ImageWidthOffset = 4;

    /// <summary>The offset of the component count in the image header box.</summary>
    private const int ImageComponentsOffset = 8;

    /// <summary>The offset of the bits per component in the image header box.</summary>
    private const int ImageBitsOffset = 10;

    /// <summary>The offset of the compression type in the image header box.</summary>
    private const int ImageCompressionOffset = 11;

    /// <summary>The bytes of a channel definition entry.</summary>
    private const int DefinitionBytes = 6;

    /// <summary>The offset of the channel type in a definition entry.</summary>
    private const int DefinitionTypeOffset = 2;

    /// <summary>The offset of the association in a definition entry.</summary>
    private const int DefinitionAssociationOffset = 4;

    /// <summary>The channel type of an opacity channel.</summary>
    private const int OpacityType = 1;

    /// <summary>The bytes of a brand or compatibility entry.</summary>
    private const int BrandBytes = 4;

    /// <summary>Gets the minor version of a JP2 file type box.</summary>
    private static ReadOnlySpan<byte> Jp2MinorVersion => [0, 0, 0, 0];

    /// <summary>Gets the minor version of a JPX file type box (ISO 15444-2 M.8, corrigendum 4).</summary>
    private static ReadOnlySpan<byte> JpxMinorVersion => [0, 0, 0, 1];

    /// <summary>Gets the signature box, which every JP2 file starts with.</summary>
    private static ReadOnlySpan<byte> Signature => [0x00, 0x00, 0x00, 0x0C, 0x6A, 0x50, 0x20, 0x20, 0x0D, 0x0A, 0x87, 0x0A];

    /// <summary>Checks that the colour description fits the image.</summary>
    /// <param name="image">The image.</param>
    /// <exception cref="ArgumentException">The colour space does not match the colour channels, or there is none to declare.</exception>
    internal static void CheckColour(PdfJpxImage image)
    {
        if (!image.IccProfile.IsEmpty)
        {
            return;
        }

        _ = EnumeratedSpace(image);
    }

    /// <summary>
    /// Determines whether the image needs the JPX extensions of ISO 15444-2 annex M: CMYK (enumerated space 12) and any ICC
    /// profile that is not of the restricted kind need a JPX file; the other images are JP2 files.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <returns><see langword="true"/> for a JPX file.</returns>
    internal static bool NeedsJpx(PdfJpxImage image) =>
        image.IccProfile.IsEmpty ? EnumeratedSpace(image) == CmykSpace : !JpxIccProfiles.IsRestricted(image.IccProfile.Span);

    /// <summary>Writes the boxes before the codestream.</summary>
    /// <param name="output">The file.</param>
    /// <param name="image">The image.</param>
    internal static void WriteHeader(ref PooledBuffer output, PdfJpxImage image)
    {
        output.Write(Signature);
        var jpx = NeedsJpx(image);
        WriteFileType(ref output, image, jpx);
        if (jpx)
        {
            WriteReaderRequirements(ref output, image);
        }

        var header = BeginBox(ref output, "jp2h"u8);
        WriteImageHeader(ref output, image);
        WriteColour(ref output, image, jpx);
        WriteDefinitions(ref output, image);
        EndBox(ref output, header);
    }

    /// <summary>Starts the codestream box.</summary>
    /// <param name="output">The file.</param>
    /// <returns>The box position, for <see cref="EndCodestream"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int BeginCodestream(ref PooledBuffer output) => BeginBox(ref output, "jp2c"u8);

    /// <summary>Writes the length of the codestream box.</summary>
    /// <param name="output">The file.</param>
    /// <param name="position">The position <see cref="BeginCodestream"/> returned.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void EndCodestream(ref PooledBuffer output, int position) => EndBox(ref output, position);

    /// <summary>Gets the enumerated colour space of an image.</summary>
    /// <param name="image">The image.</param>
    /// <returns>The enumerated value.</returns>
    /// <exception cref="ArgumentException">The colour space does not match the colour channels, or there is none to declare.</exception>
    private static int EnumeratedSpace(PdfJpxImage image)
    {
        var space = image.ColorSpace switch
        {
            PdfJpxColorSpace.Gray => GraySpace,
            PdfJpxColorSpace.Srgb => SrgbSpace,
            PdfJpxColorSpace.Cmyk => CmykSpace,
            _ => 0,
        };
        var expected = image.ColourChannels switch
        {
            GrayChannels => GraySpace,
            RgbChannels => SrgbSpace,
            CmykChannels => CmykSpace,
            _ => 0,
        };
        if (expected == 0 || (space != 0 && space != expected))
        {
            throw new ArgumentException("The colour space does not match the number of colour channels; give an ICC profile for other channel counts.", nameof(image));
        }

        return expected;
    }

    /// <summary>Starts a box and leaves its length to <see cref="EndBox"/>.</summary>
    /// <param name="output">The file.</param>
    /// <param name="type">The four-character box type.</param>
    /// <returns>The box position.</returns>
    private static int BeginBox(ref PooledBuffer output, ReadOnlySpan<byte> type)
    {
        var position = output.Length;
        var span = output.GetSpan(BoxHeaderBytes);
        span[..BoxHeaderBytes].Clear();
        type.CopyTo(span[BrandBytes..]);
        output.Advance(BoxHeaderBytes);
        return position;
    }

    /// <summary>Writes the length of a box.</summary>
    /// <param name="output">The file.</param>
    /// <param name="position">The box position.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EndBox(ref PooledBuffer output, int position) =>
        JpxEncodeHeaders.PatchLength(ref output, position, (uint)(output.Length - position));

    /// <summary>Writes the image header box (I.5.3.1).</summary>
    /// <param name="output">The file.</param>
    /// <param name="image">The image.</param>
    private static void WriteImageHeader(ref PooledBuffer output, PdfJpxImage image)
    {
        var box = BeginBox(ref output, "ihdr"u8);
        var span = output.GetSpan(ImageHeaderBytes);
        span[..ImageHeaderBytes].Clear();
        BinaryPrimitives.WriteInt32BigEndian(span, image.Height);
        BinaryPrimitives.WriteInt32BigEndian(span[ImageWidthOffset..], image.Width);
        BinaryPrimitives.WriteUInt16BigEndian(span[ImageComponentsOffset..], (ushort)image.Components);
        span[ImageBitsOffset] = (byte)(image.BitsPerComponent - 1);
        span[ImageCompressionOffset] = Compression;
        output.Advance(ImageHeaderBytes);
        EndBox(ref output, box);
    }

    /// <summary>
    /// Writes the file type box. A JP2 file has the brand and one compatibility entry 'jp2 ' and a minor version of 0 (T.800
    /// I.5.2). A JPX file has the brand and compatibility entry 'jpx ' and a minor version of 1 (ISO 15444-2 M.8, corrigendum
    /// 4), and also 'jpxb', the baseline entry, when its colour specification is one the baseline allows (M.9.2.4): an ICC profile.
    /// </summary>
    /// <param name="output">The file.</param>
    /// <param name="image">The image.</param>
    /// <param name="jpx">Whether the file is a JPX file.</param>
    private static void WriteFileType(ref PooledBuffer output, PdfJpxImage image, bool jpx)
    {
        var fileType = BeginBox(ref output, "ftyp"u8);
        output.Write(jpx ? "jpx "u8 : "jp2 "u8);
        output.Write(jpx ? JpxMinorVersion : Jp2MinorVersion);
        output.Write(jpx ? "jpx "u8 : "jp2 "u8);
        if (jpx && !image.IccProfile.IsEmpty)
        {
            output.Write("jpxb"u8);
        }

        EndBox(ref output, fileType);
    }

    /// <summary>
    /// Writes the Reader Requirements box that a JPX file must have straight after the file type box (ISO 15444-2 M.11.1). It
    /// lists the features of the file, each as its own requirement, so a reader needs all of them: an unrestricted Part 1
    /// codestream (flag 5), the colour space (CMYK, flag 55, or Any ICC, flag 44) and, with opacity, a non-premultiplied opacity channel (flag 9).
    /// </summary>
    /// <param name="output">The file.</param>
    /// <param name="image">The image.</param>
    private static void WriteReaderRequirements(ref PooledBuffer output, PdfJpxImage image)
    {
        Span<ushort> flags = stackalloc ushort[MaxFeatures];
        flags[0] = CodestreamFlag;
        flags[1] = image.IccProfile.IsEmpty ? CmykFlag : AnyIccFlag;
        var count = TwoFeatures;
        if (image.HasAlpha)
        {
            flags[count] = OpacityFlag;
            count++;
        }

        var box = BeginBox(ref output, "rreq"u8);
        var all = (byte)((1 << count) - 1);
        output.Write([MaskLength, all, all]);
        var span = output.GetSpan(sizeof(ushort) + (count * (sizeof(ushort) + MaskLength)) + sizeof(ushort));
        BinaryPrimitives.WriteUInt16BigEndian(span, (ushort)count);
        for (var i = 0; i < count; i++)
        {
            var entry = span[(sizeof(ushort) + (i * (sizeof(ushort) + MaskLength)))..];
            BinaryPrimitives.WriteUInt16BigEndian(entry, flags[i]);
            entry[sizeof(ushort)] = (byte)(1 << i);
        }

        // No vendor features.
        BinaryPrimitives.WriteUInt16BigEndian(span[(sizeof(ushort) + (count * (sizeof(ushort) + MaskLength)))..], 0);
        output.Advance(sizeof(ushort) + (count * (sizeof(ushort) + MaskLength)) + sizeof(ushort));
        EndBox(ref output, box);
    }

    /// <summary>
    /// Writes the colour specification box (T.800 I.5.3.3, ISO 15444-2 M.11.7.2). A JP2 file has PREC and APPROX 0 and
    /// the enumerated or restricted ICC method. A JPX file must give APPROX 1 to 4, so 1 (an accurate description), and uses the
    /// enumerated method, or for a profile that is not restricted the Any ICC method (3).
    /// </summary>
    /// <param name="output">The file.</param>
    /// <param name="image">The image.</param>
    /// <param name="jpx">Whether the file is a JPX file.</param>
    private static void WriteColour(ref PooledBuffer output, PdfJpxImage image, bool jpx)
    {
        var box = BeginBox(ref output, "colr"u8);
        var approximation = jpx ? JpxAccurate : (byte)0;
        if (image.IccProfile.IsEmpty)
        {
            output.Write([EnumeratedMethod, 0, approximation]);
            var span = output.GetSpan(sizeof(int));
            BinaryPrimitives.WriteInt32BigEndian(span, EnumeratedSpace(image));
            output.Advance(sizeof(int));
        }
        else
        {
            output.Write([jpx ? AnyIccMethod : IccMethod, 0, approximation]);
            output.Write(image.IccProfile.Span);
        }

        EndBox(ref output, box);
    }

    /// <summary>Writes the channel definition box (I.5.3.6) that names the colour channels and the opacity channel.</summary>
    /// <param name="output">The file.</param>
    /// <param name="image">The image.</param>
    private static void WriteDefinitions(ref PooledBuffer output, PdfJpxImage image)
    {
        if (!image.HasAlpha)
        {
            return;
        }

        var box = BeginBox(ref output, "cdef"u8);
        var span = output.GetSpan(sizeof(ushort) + (image.Components * DefinitionBytes));
        span[..(sizeof(ushort) + (image.Components * DefinitionBytes))].Clear();
        BinaryPrimitives.WriteUInt16BigEndian(span, (ushort)image.Components);
        for (var channel = 0; channel < image.Components; channel++)
        {
            var entry = span[(sizeof(ushort) + (channel * DefinitionBytes))..];
            var opacity = channel == image.ColourChannels;
            BinaryPrimitives.WriteUInt16BigEndian(entry, (ushort)channel);
            BinaryPrimitives.WriteUInt16BigEndian(entry[DefinitionTypeOffset..], opacity ? (ushort)OpacityType : (ushort)0);
            BinaryPrimitives.WriteUInt16BigEndian(entry[DefinitionAssociationOffset..], opacity ? (ushort)0 : (ushort)(channel + 1));
        }

        output.Advance(sizeof(ushort) + (image.Components * DefinitionBytes));
        EndBox(ref output, box);
    }
}
