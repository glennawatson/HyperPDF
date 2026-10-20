// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdf.TestAssets;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Hand-built documents for the optimiser tests. Object 1 is the catalog, 2 the page tree and 3 the first page.</summary>
internal static class OptimizerSamples
{
    /// <summary>The text the font samples show.</summary>
    internal const string FontText = "Hello World";

    /// <summary>The heading of the untagged text sample.</summary>
    internal const string Heading = "Annual Report";

    /// <summary>The first paragraph of the untagged text sample.</summary>
    internal const string FirstParagraph = "The first paragraph explains the results.";

    /// <summary>The second paragraph of the untagged text sample.</summary>
    internal const string SecondParagraph = "The second paragraph gives the outlook.";

    /// <summary>The image entries of an 8-bit RGB image.</summary>
    internal const string Rgb = "/ColorSpace /DeviceRGB /BitsPerComponent 8";

    /// <summary>The image entries of an 8-bit grey image.</summary>
    internal const string Grey = "/ColorSpace /DeviceGray /BitsPerComponent 8";

    /// <summary>The page width in points.</summary>
    internal const int PageWidth = 612;

    /// <summary>The page height in points.</summary>
    internal const int PageHeight = 792;

    /// <summary>The catalog of a document with no extra entries.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The page tree of a one-page document.</summary>
    private const string OnePage = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";

    /// <summary>The content that draws /Im1 into a 100 point square.</summary>
    private const string DrawSmall = "q 100 0 0 100 72 72 cm /Im1 Do Q\n";

    /// <summary>The bytes of an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The highest 8-bit sample.</summary>
    private const int MaxSample = 255;

    /// <summary>The period of the photo's ripples, in pixels.</summary>
    private const double Ripple = 23.0;

    /// <summary>The strength of the photo's ripples.</summary>
    private const double RippleStrength = 40.0;

    /// <summary>The spread of the photo's noise.</summary>
    private const uint NoiseSpread = 9;

    /// <summary>A prime that scatters x into the noise hash.</summary>
    private const uint NoiseX = 73_856_093;

    /// <summary>A prime that scatters y into the noise hash.</summary>
    private const uint NoiseY = 19_349_663;

    /// <summary>The shift that mixes the noise hash's high bits down.</summary>
    private const int NoiseShift = 13;

    /// <summary>The width of a black stroke in the bilevel pattern, in pixels.</summary>
    private const int StrokeWidth = 3;

    /// <summary>The spacing of the bilevel pattern's strokes.</summary>
    private const int StrokeSpacing = 11;

    /// <summary>The page's left margin.</summary>
    private const int Margin = 72;

    /// <summary>The first char code given a width.</summary>
    private const int FirstChar = 32;

    /// <summary>The last char code given a width.</summary>
    private const int LastChar = 126;

    /// <summary>The red sample a colour key masks out.</summary>
    private const int KeyRed = 10;

    /// <summary>The size of the small images.</summary>
    private const int SmallImage = 88;

    /// <summary>The XMP packet of the clutter sample.</summary>
    private const string ClutterXmp =
        "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
        + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">"
        + "<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">Clutter</rdf:li></rdf:Alt></dc:title></rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";

    /// <summary>Builds a page with one continuous-tone RGB image.</summary>
    /// <param name="pixels">The image's width and height in pixels.</param>
    /// <param name="drawPoints">The size it is drawn at, in points.</param>
    /// <param name="extraImageEntries">Extra image dictionary entries.</param>
    /// <returns>The PDF.</returns>
    internal static byte[] Photo(int pixels, int drawPoints, string extraImageEntries) =>
        ImagePage(ImageObject(PhotoPixels(pixels, pixels), pixels, pixels, $"{Rgb} {extraImageEntries}"), drawPoints);

    /// <summary>Builds a page with one image stream given as an object body, drawn as a square.</summary>
    /// <param name="image">The image object body.</param>
    /// <param name="drawPoints">The size it is drawn at, in points.</param>
    /// <param name="extraObjects">Objects added after the image, numbered from 6.</param>
    /// <returns>The PDF.</returns>
    internal static byte[] ImagePage(string image, int drawPoints, params string[] extraObjects)
    {
        var content = Format($"q {drawPoints} 0 0 {drawPoints} {Margin} {Margin} cm /Im1 Do Q\n");
        string[] objects =
        [
            Catalog,
            OnePage,
            Page("/XObject << /Im1 5 0 R >>"),
            MiniPdf.Stream(string.Empty, content),
            image,
        ];
        return MiniPdf.Build([.. objects, .. extraObjects]);
    }

    /// <summary>Builds an image object, Flate-compressed at the fastest level.</summary>
    /// <param name="samples">The samples.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="entries">The colour space, bits and other entries.</param>
    /// <returns>The object body.</returns>
    internal static string ImageObject(byte[] samples, int width, int height, string entries) =>
        MiniPdf.Stream(Format($"/Type /XObject /Subtype /Image /Width {width} /Height {height} {entries} /Filter /FlateDecode"), OptimizerTestKit.Latin1(OptimizerTestKit.Deflate(samples)));

    /// <summary>Makes a smooth, noisy colour image, like a photograph.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The RGB samples.</returns>
    internal static byte[] PhotoPixels(int width, int height)
    {
        var samples = new byte[width * height * RgbBytes];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var ripple = Math.Sin(x / Ripple) * Math.Cos(y / Ripple) * RippleStrength;
                var offset = ((y * width) + x) * RgbBytes;
                var noise = Noise(x, y);
                samples[offset] = Sample(((double)x * MaxSample / width) + ripple + noise);
                samples[offset + 1] = Sample(((double)y * MaxSample / height) - ripple + noise);
                samples[offset + RgbBytes - 1] = Sample(((double)(x + y) * MaxSample / (width + height)) + noise);
            }
        }

        return samples;
    }

    /// <summary>Makes a smooth grey ramp, used as a soft mask.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The grey samples.</returns>
    internal static byte[] Ramp(int width, int height)
    {
        var samples = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                samples[(y * width) + x] = (byte)(x * MaxSample / Math.Max(1, width - 1));
            }
        }

        return samples;
    }

    /// <summary>Makes black strokes on white, one byte per pixel: 0 black, 255 white.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The grey samples.</returns>
    internal static byte[] Strokes(int width, int height)
    {
        var samples = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var diagonal = (x + (y / StrokeWidth)) % StrokeSpacing < StrokeWidth;
                var rule = y % StrokeSpacing < StrokeWidth && x % (StrokeSpacing + StrokeWidth) < StrokeSpacing;
                samples[(y * width) + x] = diagonal || rule ? (byte)0 : (byte)MaxSample;
            }
        }

        return samples;
    }

    /// <summary>Packs grey samples into 1-bit rows: 255 becomes 1.</summary>
    /// <param name="samples">The grey samples.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The packed rows.</returns>
    internal static byte[] Pack(byte[] samples, int width, int height)
    {
        var stride = (width + ByteBits - 1) / ByteBits;
        var rows = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (samples[(y * width) + x] == 0)
                {
                    continue;
                }

                rows[(y * stride) + (x / ByteBits)] |= (byte)(1 << (ByteBits - 1 - (x % ByteBits)));
            }
        }

        return rows;
    }

    /// <summary>Builds a page with a colour image whose soft mask is a grey ramp.</summary>
    /// <param name="pixels">The image's width and height.</param>
    /// <param name="drawPoints">The drawn size.</param>
    /// <returns>The PDF.</returns>
    internal static byte[] Masked(int pixels, int drawPoints)
    {
        var image = ImageObject(PhotoPixels(pixels, pixels), pixels, pixels, $"{Rgb} /SMask 6 0 R");
        var mask = ImageObject(Ramp(pixels, pixels), pixels, pixels, Grey);
        return ImagePage(image, drawPoints, mask);
    }

    /// <summary>Builds a page with a colour-keyed photo, which must keep its exact samples.</summary>
    /// <param name="pixels">The image's width and height.</param>
    /// <param name="drawPoints">The drawn size.</param>
    /// <returns>The PDF.</returns>
    internal static byte[] ColorKeyed(int pixels, int drawPoints) =>
        Photo(pixels, drawPoints, Format($"/Mask [0 {KeyRed} 0 {MaxSample} 0 {MaxSample}]"));

    /// <summary>Builds a page that fills a rectangle with a tiling pattern drawing a photo, whose drawn size is unknown.</summary>
    /// <param name="pixels">The image's width and height.</param>
    /// <returns>The PDF.</returns>
    internal static byte[] PatternPhoto(int pixels)
    {
        const string Cell = "q 50 0 0 50 0 0 cm /Im1 Do Q\n";
        const string Content = "/Pattern cs /P1 scn 72 72 200 200 re f\n";
        const string Entries = "/Type /Pattern /PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 50 50] /XStep 50 /YStep 50 /Resources << /XObject << /Im1 5 0 R >> >>";
        return MiniPdf.Build(
            Catalog,
            OnePage,
            Page("/Pattern << /P1 6 0 R >>"),
            MiniPdf.Stream(string.Empty, Content),
            ImageObject(PhotoPixels(pixels, pixels), pixels, pixels, Rgb),
            MiniPdf.Stream(Entries, Cell));
    }

    /// <summary>Builds a page that shows text in the embedded test TrueType font.</summary>
    /// <param name="formUsesFont">Whether the interactive form's default resources also name the font.</param>
    /// <returns>The PDF.</returns>
    internal static byte[] EmbeddedFont(bool formUsesFont)
    {
        const string Descriptor = "<< /Type /FontDescriptor /FontName /PVLTestSans /Flags 32 /FontBBox [0 -200 1000 800] /ItalicAngle 0 "
            + "/Ascent 800 /Descent -200 /CapHeight 700 /StemV 80 /FontFile2 7 0 R >>";
        var font = TestFont.Create();
        var content = Format($"BT /F1 24 Tf {Margin} 700 Td ({FontText}) Tj ET\n");
        var form = formUsesFont ? " /AcroForm << /Fields [] /DR << /Font << /F1 5 0 R >> >> /DA (/F1 0 Tf 0 g) >>" : string.Empty;
        return MiniPdf.Build(
            Format($"<< /Type /Catalog /Pages 2 0 R{form} >>"),
            OnePage,
            Page("/Font << /F1 5 0 R >>"),
            MiniPdf.Stream(string.Empty, content),
            Format($"<< /Type /Font /Subtype /TrueType /BaseFont /PVLTestSans /FirstChar {FirstChar} /LastChar {LastChar} /Widths [{Widths()}] /Encoding /WinAnsiEncoding /FontDescriptor 6 0 R >>"),
            Descriptor,
            MiniPdf.Stream(Format($"/Length1 {font.Length}"), OptimizerTestKit.Latin1(font)));
    }

    /// <summary>Builds a page that shows text in the CID-keyed test font with Compact Font Format outlines, as a Type 0 font.</summary>
    /// <param name="openType">Whether the program is the whole OpenType file (<c>/OpenType</c>) rather than the bare CFF program (<c>/CIDFontType0C</c>).</param>
    /// <returns>The PDF.</returns>
    internal static byte[] EmbeddedCffFont(bool openType)
    {
        const string Descriptor = "<< /Type /FontDescriptor /FontName /PVLTestCidCff /Flags 4 /FontBBox [0 -200 1000 800] /ItalicAngle 0 "
            + "/Ascent 800 /Descent -200 /CapHeight 700 /StemV 80 /FontFile3 8 0 R >>";
        var file = TestCffFont.Create(new() { CidKeyed = true });
        var program = openType ? file : TestCffFont.ExtractProgram(file);
        var subtype = openType ? "OpenType" : "CIDFontType0C";
        var shown = string.Concat(FontText.Select(static letter => TestFont.GlyphOf(letter).ToString("X4", CultureInfo.InvariantCulture)));
        var content = Format($"BT /F1 24 Tf {Margin} 700 Td <{shown}> Tj ET\n");
        return MiniPdf.Build(
            Catalog,
            OnePage,
            Page("/Font << /F1 5 0 R >>"),
            MiniPdf.Stream(string.Empty, content),
            "<< /Type /Font /Subtype /Type0 /BaseFont /PVLTestCidCff /Encoding /Identity-H /DescendantFonts [6 0 R] >>",
            "<< /Type /Font /Subtype /CIDFontType0 /BaseFont /PVLTestCidCff /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /DW 600 /FontDescriptor 7 0 R >>",
            Descriptor,
            MiniPdf.Stream($"/Subtype /{subtype}", OptimizerTestKit.Latin1(program)));
    }

    /// <summary>Builds two pages that each draw their own copy of the same image.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] Duplicates()
    {
        var image = ImageObject(PhotoPixels(SmallImage, SmallImage), SmallImage, SmallImage, Rgb);
        return MiniPdf.Build(
            Catalog,
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            Page("/XObject << /Im1 6 0 R >>").Replace("/Contents 4 0 R", "/Contents 5 0 R", StringComparison.Ordinal),
            Page("/XObject << /Im1 7 0 R >>").Replace("/Contents 4 0 R", "/Contents 8 0 R", StringComparison.Ordinal),
            MiniPdf.Stream(string.Empty, DrawSmall),
            image,
            image,
            MiniPdf.Stream(string.Empty, DrawSmall));
    }

    /// <summary>Builds a page with a thumbnail, private data, unused resources, an empty annotation array and XMP.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] Clutter()
    {
        const string Resources = "/XObject << /Im1 5 0 R /Im2 6 0 R >> /Font << /F9 7 0 R >>";
        const string Extras = "/Thumb 8 0 R /PieceInfo << /Editor << /Private (page data) >> >> /Annots [] ";
        return MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Metadata 9 0 R /PieceInfo << /Editor << /Private (catalog data) >> >> >>",
            OnePage,
            Page(Resources).Replace("/Resources", $"{Extras}/Resources", StringComparison.Ordinal),
            MiniPdf.Stream(string.Empty, DrawSmall),
            ImageObject(PhotoPixels(StrokeSpacing, StrokeSpacing), StrokeSpacing, StrokeSpacing, Rgb),
            ImageObject(Ramp(StrokeSpacing, StrokeSpacing), StrokeSpacing, StrokeSpacing, Grey),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            ImageObject(Ramp(ByteBits, ByteBits), ByteBits, ByteBits, Grey),
            MiniPdf.Stream("/Type /Metadata /Subtype /XML", ClutterXmp));
    }

    /// <summary>Builds an untagged page with a heading, two paragraphs in Helvetica and an image.</summary>
    /// <param name="title">The /Info title, or <see langword="null"/> for none.</param>
    /// <returns>The PDF.</returns>
    internal static byte[] UntaggedText(string? title)
    {
        var content = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"BT /F1 24 Tf {Margin} 700 Td ({Heading}) Tj ET\n")
            .Append(CultureInfo.InvariantCulture, $"BT /F1 12 Tf {Margin} 650 Td ({FirstParagraph}) Tj ET\n")
            .Append("q 100 0 0 100 72 450 cm /Im1 Do Q\n")
            .Append(CultureInfo.InvariantCulture, $"BT /F1 12 Tf {Margin} 400 Td ({SecondParagraph}) Tj ET\n")
            .ToString();
        return Build(
            title is null ? string.Empty : "/Info 7 0 R",
            Catalog,
            OnePage,
            Page("/Font << /F1 5 0 R >> /XObject << /Im1 6 0 R >>"),
            MiniPdf.Stream(string.Empty, content),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            ImageObject(PhotoPixels(StrokeSpacing, StrokeSpacing), StrokeSpacing, StrokeSpacing, Rgb),
            Format($"<< /Title ({title}) >>"));
    }

    /// <summary>Builds a file like <see cref="MiniPdf.Build"/>, with extra trailer entries.</summary>
    /// <param name="trailer">The extra trailer entries, such as <c>/Info 7 0 R</c>.</param>
    /// <param name="objects">The object bodies; object 1 is the catalog.</param>
    /// <returns>The file.</returns>
    internal static byte[] Build(string trailer, params string[] objects)
    {
        var output = new StringBuilder("%PDF-1.7\n");
        var offsets = new int[objects.Length];
        for (var i = 0; i < objects.Length; i++)
        {
            offsets[i] = Encoding.Latin1.GetByteCount(output.ToString());
            _ = output.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = Encoding.Latin1.GetByteCount(output.ToString());
        _ = output.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            _ = output.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        _ = output.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R {trailer} >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(output.ToString());
    }

    /// <summary>Formats with the invariant culture.</summary>
    /// <param name="handler">The interpolated string.</param>
    /// <returns>The string.</returns>
    internal static string Format(ref DefaultInterpolatedStringHandler handler) => string.Create(CultureInfo.InvariantCulture, ref handler);

    /// <summary>Writes the first page, object 3, with its content in object 4.</summary>
    /// <param name="resources">The resource entries.</param>
    /// <returns>The page object body.</returns>
    private static string Page(string resources) =>
        Format($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << {resources} >> /Contents 4 0 R >>");

    /// <summary>Gets a repeatable noise value for a pixel.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>A value from 0 to the noise spread.</returns>
    private static int Noise(int x, int y)
    {
        var hash = ((uint)x * NoiseX) ^ ((uint)y * NoiseY);
        hash ^= hash >> NoiseShift;
        return (int)(hash % NoiseSpread);
    }

    /// <summary>Clamps a computed sample to a byte.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The byte.</returns>
    private static byte Sample(double value) => (byte)Math.Clamp((int)value, 0, MaxSample);

    /// <summary>Lists the test font's widths for codes 32 to 126.</summary>
    /// <returns>The widths, separated by spaces.</returns>
    private static string Widths()
    {
        var widths = new StringBuilder();
        for (var code = FirstChar; code <= LastChar; code++)
        {
            _ = widths.Append(CultureInfo.InvariantCulture, $"{TestFont.AdvanceOf((char)code)} ");
        }

        return widths.ToString().TrimEnd();
    }
}
