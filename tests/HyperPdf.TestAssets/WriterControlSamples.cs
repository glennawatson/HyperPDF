// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace HyperPdf.TestAssets;

/// <summary>Small PDFs with split page content, inline images, filtered streams and unreferenced objects, for the writer control tests.</summary>
public static class WriterControlSamples
{
    /// <summary>The width and height of each inline image in pixels.</summary>
    public static readonly int ImageSide = 80;

    /// <summary>The marker text in the unreferenced dictionary of <see cref="WithOrphans"/>.</summary>
    public static readonly string OrphanMarker = "orphan-marker-text";

    /// <summary>The multiplier of the pseudo-random generator.</summary>
    private const uint Multiplier = 1_664_525;

    /// <summary>The increment of the pseudo-random generator.</summary>
    private const uint Increment = 1_013_904_223;

    /// <summary>The first state of the pseudo-random generator, fixed so the files are the same on every run.</summary>
    private const uint Seed = 42;

    /// <summary>The shift that moves the best bits of the generator state into the low byte.</summary>
    private const int ByteShift = 24;

    /// <summary>The bytes per pixel of an RGB image.</summary>
    private const int RgbBytes = 3;

    /// <summary>The bit that marks a byte as neither white space nor a letter, so the end of the image is not mistaken.</summary>
    private const byte HighBit = 0x80;

    /// <summary>The number of rectangles in the repetitive content.</summary>
    private const int Rectangles = 600;

    /// <summary>The number of grey levels the repetitive content cycles through.</summary>
    private const int GreyLevels = 10;

    /// <summary>The distance the repetitive rectangles move across the page.</summary>
    private const int Travel = 190;

    /// <summary>The step of the repetitive rectangles down the page.</summary>
    private const int Rise = 7;

    /// <summary>The width and height of the unfiltered image of <see cref="FilteredStreams"/>.</summary>
    private const int SmallSide = 8;

    /// <summary>The catalog object.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The page tree object, with one page.</summary>
    private const string Pages = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";

    /// <summary>The page object, shared by the samples that build one page.</summary>
    private const string PageObject = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R >>";

    /// <summary>The first content stream of <see cref="ContentPage"/>.</summary>
    private const string FirstContent = "1 0 0 rg 10 10 50 50 re f\n";

    /// <summary>The blue rectangle drawn between the images of <see cref="ContentPage"/>.</summary>
    private const string BlueRectangle = "0 0 1 rg 150 10 40 40 re f\n";

    /// <summary>Gets the content of the ASCIIHex-coded stream of <see cref="FilteredStreams"/>.</summary>
    private static ReadOnlySpan<byte> FilteredContent => "1 0 0 rg 20 20 100 100 re f q 60 0 0 60 100 100 cm /Im Do Q"u8;

    /// <summary>Creates a page whose content is three streams and holds three large inline images: gray, Flate-compressed RGB and one that names a colour space in the page resources.</summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] ContentPage()
    {
        var side = ImageSide;
        var seed = Seed;
        var gray = Latin1(HighBytes(ref seed, side * side));
        var compressed = Latin1(Compress(Bytes(ref seed, side * side * RgbBytes), CompressionLevel.Optimal));
        var named = Latin1(HighBytes(ref seed, side * side * RgbBytes));
        var second = $"q 100 0 0 100 50 50 cm\nBI /W {side} /H {side} /BPC 8 /CS /G ID {gray}\nEI Q\n";
        var third = $"q 60 0 0 60 120 120 cm\nBI /W {side} /H {side} /BPC 8 /CS /RGB /F /Fl ID {compressed}\nEI Q\n"
            + BlueRectangle
            + $"q 40 0 0 40 10 150 cm\nBI /W {side} /H {side} /BPC 8 /CS /Cs1 ID {named}\nEI Q\n";
        return MiniPdf.Build(
            Catalog,
            Pages,
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents [4 0 R 5 0 R 6 0 R] "
            + "/Resources << /ColorSpace << /Cs1 [/CalRGB << /WhitePoint [0.9505 1 1.089] >>] >> >> >>",
            MiniPdf.Stream(string.Empty, FirstContent),
            MiniPdf.Stream(string.Empty, second),
            MiniPdf.Stream(string.Empty, third));
    }

    /// <summary>Creates a page whose content is a repetitive stream compressed at the fastest level, so a higher level can make it smaller.</summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] RepetitivePage()
    {
        var content = new StringBuilder();
        for (var i = 0; i < Rectangles; i++)
        {
            _ = content.Append(CultureInfo.InvariantCulture, $"0.{i % GreyLevels} g {i % Travel} {i * Rise % Travel} 5 5 re f\n");
        }

        var compressed = Latin1(Compress(Encoding.ASCII.GetBytes(content.ToString()), CompressionLevel.Fastest));
        return MiniPdf.Build(Catalog, Pages, PageObject, MiniPdf.Stream("/Filter /FlateDecode", compressed));
    }

    /// <summary>
    /// Creates a page whose content is ASCIIHex-coded and that draws an unfiltered image, and optionally a JPEG image whose
    /// bytes are not a real picture, to test that image codecs are left alone.
    /// </summary>
    /// <param name="includeJpeg">Whether to add the fake JPEG image.</param>
    /// <returns>The PDF bytes.</returns>
    public static byte[] FilteredStreams(bool includeJpeg)
    {
        var hex = $"{Convert.ToHexString(FilteredContent)}>";
        var jpeg = includeJpeg ? " /Jpg 6 0 R" : string.Empty;
        var objects = new List<string>
        {
            Catalog,
            Pages,
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R /Resources << /XObject << /Im 5 0 R{jpeg} >> >> >>",
            MiniPdf.Stream("/Filter /ASCIIHexDecode", hex),
            MiniPdf.Stream(ImageEntries(string.Empty), new('A', SmallSide * SmallSide)),
        };
        if (includeJpeg)
        {
            objects.Add(MiniPdf.Stream(ImageEntries("/Filter /DCTDecode"), "not-a-real-jpeg"));
        }

        return MiniPdf.Build([.. objects]);
    }

    /// <summary>Creates a one page document with two objects that no reference reaches: a dictionary with a marker and a stream.</summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] WithOrphans() =>
        MiniPdf.Build(
            Catalog,
            Pages,
            PageObject,
            MiniPdf.Stream(string.Empty, "0 1 0 rg 20 20 100 100 re f"),
            $"<< /Orphan ({OrphanMarker}) >>",
            MiniPdf.Stream("/Orphan true", "orphan stream data"));

    /// <summary>Writes the dictionary entries of a small gray image.</summary>
    /// <param name="extra">More entries, such as a filter.</param>
    /// <returns>The entries.</returns>
    private static string ImageEntries(string extra) =>
        $"/Type /XObject /Subtype /Image /Width {SmallSide} /Height {SmallSide} /BitsPerComponent 8 /ColorSpace /DeviceGray {extra}";

    /// <summary>Creates repeatable pseudo-random bytes.</summary>
    /// <param name="state">The generator state, advanced.</param>
    /// <param name="count">The number of bytes.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Bytes(ref uint state, int count)
    {
        var bytes = new byte[count];
        for (var i = 0; i < count; i++)
        {
            state = (state * Multiplier) + Increment;
            bytes[i] = (byte)(state >> ByteShift);
        }

        return bytes;
    }

    /// <summary>Creates repeatable pseudo-random bytes that all have the high bit set, so none is white space or a letter.</summary>
    /// <param name="state">The generator state, advanced.</param>
    /// <param name="count">The number of bytes.</param>
    /// <returns>The bytes.</returns>
    private static byte[] HighBytes(ref uint state, int count)
    {
        var bytes = Bytes(ref state, count);
        for (var i = 0; i < count; i++)
        {
            bytes[i] |= HighBit;
        }

        return bytes;
    }

    /// <summary>Compresses bytes as zlib.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="level">The level.</param>
    /// <returns>The zlib data.</returns>
    private static byte[] Compress(byte[] data, CompressionLevel level)
    {
        using var stream = new MemoryStream();
        using (var zlib = new ZLibStream(stream, level, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return stream.ToArray();
    }

    /// <summary>Turns bytes into a string with one character per byte, as <see cref="MiniPdf"/> writes it.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The string.</returns>
    private static string Latin1(byte[] data) => Encoding.Latin1.GetString(data);
}
