// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Tests for colour spaces: RGB conversion, row conversion and parsing.</summary>
public sealed class ColorSpaceTests
{
    /// <summary>The tolerance for float colours.</summary>
    private const float Tolerance = 1e-3F;

    /// <summary>The largest difference allowed between row conversion and <see cref="PdfColorSpace.ToRgb"/>, in bytes.</summary>
    private const int ByteTolerance = 1;

    /// <summary>The seed of the random samples.</summary>
    private const uint Seed = 1234;

    /// <summary>The multiplier of the sample generator.</summary>
    private const uint Multiplier = 1_103_515_245;

    /// <summary>The increment of the sample generator.</summary>
    private const uint Increment = 12_345;

    /// <summary>The shift that takes well-mixed bits from the generator state.</summary>
    private const int ByteShift = 16;

    /// <summary>The bits of a row sample.</summary>
    private const int SampleBits = 8;

    /// <summary>The numbers in a min/max pair.</summary>
    private const int PairSize = 2;

    /// <summary>The largest 4-bit index.</summary>
    private const float FourBitMaximum = 15;

    /// <summary>The offset of the third pixel in a BGRA row.</summary>
    private const int ThirdPixel = 8;

    /// <summary>The /FunctionType of an exponential function.</summary>
    private const int ExponentialType = 2;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The index of the red byte in a BGRA pixel.</summary>
    private const int RedByte = 2;

    /// <summary>The index of the alpha byte in a BGRA pixel.</summary>
    private const int AlphaByte = 3;

    /// <summary>The largest byte.</summary>
    private const int MaxByte = 255;

    /// <summary>Half intensity.</summary>
    private const float Half = 0.5F;

    /// <summary>A quarter intensity.</summary>
    private const float Quarter = 0.25F;

    /// <summary>The gamma of the calibrated test spaces.</summary>
    private const float Gamma = 1.8F;

    /// <summary>The largest L* value.</summary>
    private const float White = 100;

    /// <summary>The components of CMYK.</summary>
    private const int Cmyk = 4;

    /// <summary>The components of RGB.</summary>
    private const int Rgb = 3;

    /// <summary>The largest difference between the CMYK polynomial and its colour model: 3 bytes of 255.</summary>
    private const float ModelTolerance = 3F / 255F;

    /// <summary>The highest index of the random palette.</summary>
    private const int FullPalette = 255;

    /// <summary>The second index of the two-entry palette.</summary>
    private const int SecondIndex = 1;

    /// <summary>Gets the row lengths that exercise the vector bodies and their scalar tails.</summary>
    private static ReadOnlySpan<byte> RowLengthTable => [0x01, 0x03, 0x04, 0x05, 0x0F, 0x10, 0x11, 0x21, 0x47];

    /// <summary>Gets the row lengths as test data.</summary>
    /// <returns>The lengths.</returns>
    public static int[] RowLengths()
    {
        var table = RowLengthTable;
        var lengths = new int[table.Length];
        for (var i = 0; i < lengths.Length; i++)
        {
            lengths[i] = table[i];
        }

        return lengths;
    }

    /// <summary>Device spaces convert with the documented formulas.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeviceSpacesConvert()
    {
        var gray = ToRgb(PdfColorSpace.DeviceGray, Half);
        var rgb = ToRgb(PdfColorSpace.DeviceRgb, Quarter, Half, 1);
        var cmyk = ToRgb(PdfColorSpace.DeviceCmyk, 0, 0, 0, 0);

        await Assert.That(gray[1]).IsEqualTo(Half).Within(Tolerance);
        await Assert.That(rgb[0]).IsEqualTo(Quarter).Within(Tolerance);
        await Assert.That(rgb[2]).IsEqualTo(1).Within(Tolerance);

        // CMYK conversion values are covered by CmykTests; no ink is white.
        await Assert.That(cmyk[0]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(cmyk[1]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(cmyk[2]).IsEqualTo(1).Within(Tolerance);
    }

    /// <summary>The initial colours follow the specification.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InitialColors()
    {
        var cmyk = new float[Cmyk];
        PdfColorSpace.DeviceCmyk.GetInitialColor(cmyk);
        var separation = new float[1];
        Separation().GetInitialColor(separation);

        await Assert.That(cmyk).IsEquivalentTo([0F, 0F, 0F, 1F]);
        await Assert.That(separation[0]).IsEqualTo(1);
    }

    /// <summary>Lab white and black convert to sRGB white and black.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LabConvertsToSrgb()
    {
        var space = Parse(Array(Name(KnownName.Lab), PdfValue.FromDictionary(new(null))));
        var white = ToRgb(space, White, 0, 0);
        var black = ToRgb(space, 0, 0, 0);

        await Assert.That(space.Kind).IsEqualTo(PdfColorSpaceKind.Lab);
        await Assert.That(white[0]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(white[1]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(white[2]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(black[1]).IsEqualTo(0).Within(Tolerance);
    }

    /// <summary>An Indexed space reads its palette from a string.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IndexedReadsLookupString()
    {
        var space = Parse(Array(Name(KnownName.Indexed), Name(KnownName.DeviceRGB), PdfValue.FromInteger(SecondIndex), PdfValue.FromString([0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00])));
        var second = ToRgb(space, SecondIndex);
        var bgra = Convert(space, [0x01, 0x00, 0x07]);

        await Assert.That(space.Kind).IsEqualTo(PdfColorSpaceKind.Indexed);
        await Assert.That(second[0]).IsEqualTo(0).Within(Tolerance);
        await Assert.That(second[1]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(space.GetDefaultDecode(Cmyk)).IsEquivalentTo([0F, FourBitMaximum]);
        await Assert.That(bgra[1]).IsEqualTo((byte)MaxByte);
        await Assert.That(bgra[BytesPerPixel + RedByte]).IsEqualTo((byte)MaxByte);

        // ISO 32000-2 8.6.6.3: an index past /HiVal is adjusted to the last palette entry, which is green here.
        await Assert.That(bgra[ThirdPixel + 1]).IsEqualTo((byte)MaxByte);
        await Assert.That(bgra[ThirdPixel + RedByte]).IsEqualTo((byte)0);
        await Assert.That(bgra[ThirdPixel + AlphaByte]).IsEqualTo((byte)MaxByte);
    }

    /// <summary>A Separation space converts its tint through the tint transform.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeparationUsesTintTransform()
    {
        var space = Separation();
        var full = ToRgb(space, 1);
        var none = ToRgb(space, 0);

        await Assert.That(space.Kind).IsEqualTo(PdfColorSpaceKind.Separation);

        // Full cyan through the DeviceCMYK polynomial matches the colour model it was fitted to, within its fit error.
        var model = CmykNeugebauerModel.ToSrgb(1, 0, 0, 0);
        await Assert.That(full[0]).IsEqualTo((float)model.Red).Within(ModelTolerance);
        await Assert.That(full[1]).IsEqualTo((float)model.Green).Within(ModelTolerance);
        await Assert.That(none[0]).IsEqualTo(1).Within(Tolerance);
    }

    /// <summary>Names resolve through resources and abbreviations; unknown values fall back to DeviceGray.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParsesNamesAndFallsBack()
    {
        var store = new PdfNameTable();
        var resourceName = store.Intern("CS0");
        var resources = new PdfDictionary(null);
        resources.Add(resourceName, Array(Name(KnownName.ICCBased), PdfValue.FromStream(IccStream(Cmyk))));

        await Assert.That(Parse(PdfValue.FromName(resourceName), resources).Kind).IsEqualTo(PdfColorSpaceKind.IccBased);
        await Assert.That(Parse(PdfValue.FromName(resourceName), resources).Components).IsEqualTo(Cmyk);
        await Assert.That(Parse(Name(KnownName.RGB))).IsSameReferenceAs(PdfColorSpace.DeviceRgb);
        await Assert.That(Parse(Name(KnownName.CMYK))).IsSameReferenceAs(PdfColorSpace.DeviceCmyk);
        await Assert.That(Parse(Name(KnownName.G))).IsSameReferenceAs(PdfColorSpace.DeviceGray);
        await Assert.That(Parse(PdfValue.FromName(resourceName))).IsSameReferenceAs(PdfColorSpace.DeviceGray);
        await Assert.That(Parse(PdfValue.FromInteger(1))).IsSameReferenceAs(PdfColorSpace.DeviceGray);
        await Assert.That(Parse(Array(Name(KnownName.Pattern), Name(KnownName.DeviceRGB))).Components).IsEqualTo(Rgb);
        await Assert.That(Parse(Array(Name(KnownName.ICCBased), PdfValue.FromStream(IccStream(Rgb)))).Components).IsEqualTo(Rgb);
    }

    /// <summary>Row conversion agrees with <see cref="PdfColorSpace.ToRgb"/> for every space and row length.</summary>
    /// <param name="length">The pixels in the row.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(RowLengths))]
    public async Task ConvertRowMatchesToRgb(int length)
    {
        PdfColorSpace[] spaces =
        [
            PdfColorSpace.DeviceGray,
            PdfColorSpace.DeviceRgb,
            PdfColorSpace.DeviceCmyk,
            Parse(Array(Name(KnownName.CalGray), PdfValue.FromDictionary(Dictionary(KnownName.Gamma, PdfValue.FromReal(Gamma))))),
            Parse(Array(Name(KnownName.CalRGB), PdfValue.FromDictionary(Dictionary(KnownName.Gamma, Numbers(Gamma, 1, Gamma))))),
            Parse(Array(Name(KnownName.Lab), PdfValue.FromDictionary(new(null)))),
            Parse(Array(Name(KnownName.Indexed), Name(KnownName.DeviceRGB), PdfValue.FromInteger(FullPalette), PdfValue.FromString(RandomBytes((FullPalette + 1) * Rgb)))),
            Separation(),
            DeviceN(),
            Parse(Array(Name(KnownName.ICCBased), PdfValue.FromStream(IccStream(Cmyk)))),
        ];

        foreach (var space in spaces)
        {
            await Assert.That(MaxDifference(space, length)).IsLessThanOrEqualTo(ByteTolerance);
        }
    }

    /// <summary>Converts a colour to RGB.</summary>
    /// <param name="space">The space.</param>
    /// <param name="components">The components.</param>
    /// <returns>The RGB values.</returns>
    private static float[] ToRgb(PdfColorSpace space, params float[] components)
    {
        var rgb = new float[Rgb];
        space.ToRgb(components, rgb);
        return rgb;
    }

    /// <summary>Converts one-byte samples to BGRA.</summary>
    /// <param name="space">The space.</param>
    /// <param name="samples">The samples.</param>
    /// <returns>The BGRA bytes.</returns>
    private static byte[] Convert(PdfColorSpace space, byte[] samples)
    {
        var count = samples.Length / space.Components;
        var bgra = new byte[count * BytesPerPixel];
        space.ConvertRow(samples, bgra, count);
        return bgra;
    }

    /// <summary>Finds the largest byte difference between row conversion and per-colour conversion of random samples.</summary>
    /// <param name="space">The space.</param>
    /// <param name="length">The pixels in the row.</param>
    /// <returns>The largest difference; 255 when a pixel is not opaque.</returns>
    private static int MaxDifference(PdfColorSpace space, int length)
    {
        var count = space.Components;
        var samples = RandomBytes(length * count);
        var bgra = Convert(space, samples);
        var decode = space.GetDefaultDecode(SampleBits);
        var components = new float[count];
        var rgb = new float[Rgb];
        var worst = 0;
        for (var p = 0; p < length; p++)
        {
            for (var c = 0; c < count; c++)
            {
                components[c] = decode[PairSize * c] + (samples[(p * count) + c] * (decode[(PairSize * c) + 1] - decode[PairSize * c]) / MaxByte);
            }

            space.ToRgb(components, rgb);
            var pixel = bgra.AsSpan(p * BytesPerPixel, BytesPerPixel);
            worst = Math.Max(worst, pixel[AlphaByte] == MaxByte ? 0 : MaxByte);
            worst = Math.Max(worst, Math.Abs(pixel[RedByte] - ToByte(rgb[0])));
            worst = Math.Max(worst, Math.Abs(pixel[1] - ToByte(rgb[1])));
            worst = Math.Max(worst, Math.Abs(pixel[0] - ToByte(rgb[2])));
        }

        return worst;
    }

    /// <summary>Converts a value from 0 to 1 to a byte.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The byte.</returns>
    private static int ToByte(float value) => (int)MathF.Round(Math.Clamp(value, 0, 1) * MaxByte);

    /// <summary>Creates random bytes from a fixed seed.</summary>
    /// <param name="length">The length.</param>
    /// <returns>The bytes.</returns>
    private static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        var state = Seed;
        for (var i = 0; i < bytes.Length; i++)
        {
            state = unchecked((state * Multiplier) + Increment);
            bytes[i] = (byte)(state >>> ByteShift);
        }

        return bytes;
    }

    /// <summary>Parses a colour space without resources.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The space.</returns>
    private static PdfColorSpace Parse(PdfValue value) => PdfColorSpace.Parse(value, null);

    /// <summary>Parses a colour space with resources.</summary>
    /// <param name="value">The value.</param>
    /// <param name="resources">The /ColorSpace resources.</param>
    /// <returns>The space.</returns>
    private static PdfColorSpace Parse(PdfValue value, PdfDictionary resources) => PdfColorSpace.Parse(value, resources);

    /// <summary>Creates a name value.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The value.</returns>
    private static PdfValue Name(KnownName name) => PdfValue.FromName(name);

    /// <summary>Creates an array value.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The value.</returns>
    private static PdfValue Array(params PdfValue[] items) => PdfValue.FromArray(new(null, items));

    /// <summary>Creates a number array value.</summary>
    /// <param name="values">The numbers.</param>
    /// <returns>The value.</returns>
    private static PdfValue Numbers(params float[] values) => PdfValue.FromArray(PdfArray.FromNumbers(null, values));

    /// <summary>Creates a dictionary with one entry.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Dictionary(KnownName key, PdfValue value)
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(key, value);
        return dictionary;
    }

    /// <summary>Creates an ICC profile stream with /N and no profile data.</summary>
    /// <param name="components">The /N value.</param>
    /// <returns>The stream.</returns>
    private static PdfStream IccStream(int components) => new(Dictionary(KnownName.N, PdfValue.FromInteger(components)), []);

    /// <summary>Creates a Separation space whose tint maps to cyan in DeviceCMYK.</summary>
    /// <returns>The space.</returns>
    private static PdfColorSpace Separation()
    {
        var function = Dictionary(KnownName.FunctionType, PdfValue.FromInteger(ExponentialType));
        function.Add(KnownName.Domain, Numbers(0, 1));
        function.Add(KnownName.C0, Numbers(0, 0, 0, 0));
        function.Add(KnownName.C1, Numbers(1, 0, 0, 0));
        function.Add(KnownName.N, PdfValue.FromInteger(1));
        return Parse(Array(Name(KnownName.Separation), Name(KnownName.All), Name(KnownName.DeviceCMYK), PdfValue.FromDictionary(function)));
    }

    /// <summary>Creates a two-tint DeviceN space converting through a PostScript function to DeviceRGB.</summary>
    /// <returns>The space.</returns>
    private static PdfColorSpace DeviceN()
    {
        var function = Dictionary(KnownName.FunctionType, PdfValue.FromInteger(Cmyk));
        function.Add(KnownName.Domain, Numbers(0, 1, 0, 1));
        function.Add(KnownName.Range, Numbers(0, 1, 0, 1, 0, 1));
        var program = new PdfStream(function, "{ 2 copy add 2 div }"u8.ToArray());
        return Parse(Array(Name(KnownName.DeviceN), Array(Name(KnownName.All), Name(KnownName.All)), Name(KnownName.DeviceRGB), PdfValue.FromStream(program)));
    }
}
