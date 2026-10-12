// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads the JP2 header box of JPEG 2000 image data for the PDF/A rules, following veraPDF's reading: the image header,
/// bits-per-component and colour specification boxes inside <c>jp2h</c>.
/// </summary>
internal static class PdfAJpxReader
{
    /// <summary>The length of the JP2 signature box.</summary>
    private const int SignatureLength = 12;

    /// <summary>The length of a box length or type field.</summary>
    private const int FieldLength = 4;

    /// <summary>The length of an extended box length.</summary>
    private const int ExtendedLength = 8;

    /// <summary>The box header length with an extended length.</summary>
    private const int ExtendedHeader = 16;

    /// <summary>The length of an image header box's contents.</summary>
    private const int ImageHeaderLength = 14;

    /// <summary>The bytes of an image header box before the component count: height and width.</summary>
    private const int ImageSizeLength = 8;

    /// <summary>The bytes of an image header box after the bit depth: compression, colour space and IPR flags.</summary>
    private const int ImageTailLength = 3;

    /// <summary>The bytes of the component count.</summary>
    private const int ComponentCountLength = 2;

    /// <summary>The fewest bytes a colour specification box holds: METH, PREC and APPROX.</summary>
    private const int ColourHeadLength = 3;

    /// <summary>The fewest bytes an enumerated colour specification box holds.</summary>
    private const int EnumeratedLength = 7;

    /// <summary>The METH value of an enumerated colour space.</summary>
    private const long Enumerated = 1;

    /// <summary>The METH value of a restricted ICC profile.</summary>
    private const long RestrictedProfile = 2;

    /// <summary>The <c>jp2h</c> box type.</summary>
    private const uint HeaderBox = 0x6A703268;

    /// <summary>The <c>ihdr</c> box type.</summary>
    private const uint ImageHeaderBox = 0x69686472;

    /// <summary>The <c>bpcc</c> box type.</summary>
    private const uint BitsPerComponentBox = 0x62706363;

    /// <summary>The <c>colr</c> box type.</summary>
    private const uint ColourBox = 0x636F6C72;

    /// <summary>The first eight bytes of the JP2 signature box: its length and type.</summary>
    private const ulong SignatureHead = 0x0000000C6A502020;

    /// <summary>The last four bytes of the JP2 signature box: its contents.</summary>
    private const uint SignatureTail = 0x0D0A870A;

    /// <summary>The offset of the signature box contents.</summary>
    private const int SignatureTailOffset = 8;

    /// <summary>Reads the header of a JPEG 2000 image stream.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="image">The image stream.</param>
    /// <returns>The header.</returns>
    internal static PdfAJpxHeader Read(PdfAColourState state, PdfStream image)
    {
        var buffer = default(PooledBuffer);
        try
        {
            _ = PdfAStreams.TryDecode(image, ref buffer, state.Context.CancellationToken);
            return Parse(buffer.WrittenSpan);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Reads the header of JPEG 2000 data.</summary>
    /// <param name="data">The data.</param>
    /// <returns>The header.</returns>
    internal static PdfAJpxHeader Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < SignatureLength
            || BinaryPrimitives.ReadUInt64BigEndian(data) != SignatureHead
            || BinaryPrimitives.ReadUInt32BigEndian(data[SignatureTailOffset..]) != SignatureTail)
        {
            return PdfAJpxHeader.Empty;
        }

        var cursor = new PdfAJpxCursor(data, SignatureLength);
        var boxes = new PdfAJpxBoxes();
        var left = FindHeader(ref cursor);
        if (left >= 0)
        {
            ReadHeaderBoxes(ref cursor, left, boxes);
        }

        return boxes.Build();
    }

    /// <summary>Finds the <c>jp2h</c> box.</summary>
    /// <param name="cursor">The cursor, left at the box contents.</param>
    /// <returns>The length of the contents; 0 when the box runs to the end; -1 when not found.</returns>
    private static long FindHeader(ref PdfAJpxCursor cursor)
    {
        while (TryReadBox(ref cursor, out var length, out var type, out var headerLength))
        {
            var left = length - headerLength;
            if (type == HeaderBox)
            {
                return HeaderContentLength(length, left);
            }

            if (length == 0 || left < 0)
            {
                return -1;
            }

            cursor.Skip(left);
        }

        return -1;
    }

    /// <summary>Gets the content length of the <c>jp2h</c> box.</summary>
    /// <param name="length">The box length.</param>
    /// <param name="left">The length less the box header.</param>
    /// <returns>0 when the box runs to the end; -1 when it is empty; otherwise the content length.</returns>
    private static long HeaderContentLength(long length, long left)
    {
        if (length == 0)
        {
            return 0;
        }

        return left <= 0 ? -1 : left;
    }

    /// <summary>Reads the boxes inside <c>jp2h</c>.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="headerLeft">The length of the contents; 0 when the box runs to the end.</param>
    /// <param name="boxes">What has been found.</param>
    private static void ReadHeaderBoxes(ref PdfAJpxCursor cursor, long headerLeft, PdfAJpxBoxes boxes)
    {
        var runsToEnd = headerLeft == 0;
        while (TryReadBox(ref cursor, out var length, out var type, out var headerLength))
        {
            if (!FitsHeader(length, headerLeft, runsToEnd) || !ReadBox(ref cursor, type, length, length - headerLength, boxes))
            {
                return;
            }

            headerLeft -= length;
            if (runsToEnd ? length == 0 : headerLeft == 0)
            {
                return;
            }
        }
    }

    /// <summary>Determines whether a box fits in what is left of <c>jp2h</c>.</summary>
    /// <param name="length">The box length.</param>
    /// <param name="headerLeft">The bytes left in <c>jp2h</c>.</param>
    /// <param name="runsToEnd">Whether <c>jp2h</c> runs to the end of the data.</param>
    /// <returns><see langword="true"/> when it fits.</returns>
    private static bool FitsHeader(long length, long headerLeft, bool runsToEnd) =>
        length >= 0 && (runsToEnd || (length != 0 && length <= headerLeft));

    /// <summary>Reads one box inside <c>jp2h</c>.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="type">The box type.</param>
    /// <param name="length">The box length.</param>
    /// <param name="contentLength">The length of its contents.</param>
    /// <param name="boxes">What has been found.</param>
    /// <returns><see langword="false"/> when reading must stop.</returns>
    private static bool ReadBox(ref PdfAJpxCursor cursor, uint type, long length, long contentLength, PdfAJpxBoxes boxes)
    {
        switch (type)
        {
            case ImageHeaderBox:
            {
                return ReadImageHeader(ref cursor, length, contentLength, boxes);
            }

            case BitsPerComponentBox:
            {
                boxes.HasBitsPerComponentBox = true;
                cursor.Skip(contentLength);
                return true;
            }

            case ColourBox:
            {
                return ReadColour(ref cursor, contentLength, boxes);
            }

            default:
            {
                cursor.Skip(contentLength);
                return true;
            }
        }
    }

    /// <summary>Reads the image header box: the component count and the bit depth.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="length">The box length.</param>
    /// <param name="contentLength">The length of its contents.</param>
    /// <param name="boxes">What has been found.</param>
    /// <returns><see langword="false"/> when reading must stop.</returns>
    private static bool ReadImageHeader(ref PdfAJpxCursor cursor, long length, long contentLength, PdfAJpxBoxes boxes)
    {
        if (contentLength != ImageHeaderLength && length != 0)
        {
            return false;
        }

        cursor.Skip(ImageSizeLength);
        if (!cursor.TryReadNumber(ComponentCountLength, out var channels))
        {
            return false;
        }

        boxes.Channels = channels;
        if (!cursor.TryRead(1, out var depth))
        {
            return false;
        }

        // veraPDF reads the depth byte as signed, so a signed-component depth (high bit set) becomes negative.
        boxes.BitDepth = (sbyte)depth[0] + 1;
        cursor.Skip(ImageTailLength);
        return true;
    }

    /// <summary>Reads a colour specification box.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="contentLength">The length of its contents.</param>
    /// <param name="boxes">What has been found.</param>
    /// <returns><see langword="false"/> when reading must stop.</returns>
    private static bool ReadColour(ref PdfAJpxCursor cursor, long contentLength, PdfAJpxBoxes boxes)
    {
        if (contentLength < ColourHeadLength)
        {
            return false;
        }

        boxes.Specifications++;
        if (!cursor.TryReadNumber(1, out var method))
        {
            return false;
        }

        boxes.FirstMethod = boxes.Specifications == 1 ? method : boxes.FirstMethod;
        cursor.Skip(1);
        if (!cursor.TryReadNumber(1, out var approximation))
        {
            return false;
        }

        var approximate = approximation == 1;
        if (approximate)
        {
            boxes.ApproximateMethod = boxes.Approximate == 0 ? method : boxes.ApproximateMethod;
            boxes.Approximate++;
        }

        long read = ColourHeadLength;
        var readOk = method switch
        {
            Enumerated => ReadEnumerated(ref cursor, contentLength, approximate, boxes, ref read),
            RestrictedProfile => ReadProfile(ref cursor, contentLength, approximate, boxes, ref read),
            _ => true,
        };
        cursor.Skip(contentLength - read);
        return readOk;
    }

    /// <summary>Reads an enumerated colour space.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="contentLength">The length of the box contents.</param>
    /// <param name="approximate">Whether the box has APPROX 1.</param>
    /// <param name="boxes">What has been found.</param>
    /// <param name="read">The bytes of the contents read so far.</param>
    /// <returns><see langword="false"/> when reading must stop.</returns>
    private static bool ReadEnumerated(ref PdfAJpxCursor cursor, long contentLength, bool approximate, PdfAJpxBoxes boxes, ref long read)
    {
        if (contentLength < EnumeratedLength || !cursor.TryReadNumber(FieldLength, out var value))
        {
            return false;
        }

        read += FieldLength;
        if (boxes.FirstEnumerated < 0)
        {
            boxes.FirstEnumerated = value;
            boxes.FirstSpace = PdfAJpxSpaces.FromEnumerated(value);
            boxes.FirstSpaceRead = true;
        }

        if (approximate && boxes.ApproximateEnumerated < 0)
        {
            boxes.ApproximateEnumerated = value;
            boxes.ApproximateSpace = PdfAJpxSpaces.FromEnumerated(value);
        }

        return true;
    }

    /// <summary>Reads an embedded ICC profile.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="contentLength">The length of the box contents.</param>
    /// <param name="approximate">Whether the box has APPROX 1.</param>
    /// <param name="boxes">What has been found.</param>
    /// <param name="read">The bytes of the contents read so far.</param>
    /// <returns><see langword="false"/> when reading must stop.</returns>
    private static bool ReadProfile(ref PdfAJpxCursor cursor, long contentLength, bool approximate, PdfAJpxBoxes boxes, ref long read)
    {
        var length = (int)Math.Clamp(contentLength - read, int.MinValue, int.MaxValue);
        if (!cursor.TryRead(length, out var profile))
        {
            return false;
        }

        read += length;
        if (!boxes.FirstSpaceRead)
        {
            boxes.FirstSpace = PdfAJpxSpaces.FromProfile(profile);
            boxes.FirstSpaceRead = true;
        }

        if (approximate && boxes.ApproximateSpace is null)
        {
            boxes.ApproximateSpace = PdfAJpxSpaces.FromProfile(profile);
        }

        return true;
    }

    /// <summary>Reads a box header.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="length">The box length, including the header; 0 when the box runs to the end.</param>
    /// <param name="type">The box type.</param>
    /// <param name="headerLength">The header length: 8, or 16 with an extended length.</param>
    /// <returns><see langword="false"/> at the end of the data.</returns>
    private static bool TryReadBox(ref PdfAJpxCursor cursor, out long length, out uint type, out int headerLength)
    {
        type = 0;
        headerLength = FieldLength + FieldLength;
        if (!cursor.TryReadNumber(FieldLength, out length) || !cursor.TryRead(FieldLength, out var typeBytes))
        {
            return false;
        }

        type = BinaryPrimitives.ReadUInt32BigEndian(typeBytes);
        if (length != 1)
        {
            return true;
        }

        headerLength = ExtendedHeader;
        return cursor.TryReadNumber(ExtendedLength, out length);
    }
}
