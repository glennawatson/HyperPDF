// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Tests for the ASCII85, predictor and Flate header fixes.</summary>
public sealed class FilterFixTests
{
    /// <summary>The 'z' groups in the large ASCII85 sample.</summary>
    private const int ZeroGroups = 1000;

    /// <summary>The bytes each 'z' stands for.</summary>
    private const int ZeroGroupBytes = 4;

    /// <summary>How many times the sample content line repeats.</summary>
    private const int Repeats = 50;

    /// <summary>Eight bits per component.</summary>
    private const int EightBits = 8;

    /// <summary>The predictor value meaning TIFF.</summary>
    private const int TiffPredictor = 2;

    /// <summary>A value between the TIFF and PNG predictors, which names no predictor.</summary>
    private const int UnusedPredictor = 5;

    /// <summary>The PNG predictor value meaning "per-row filter".</summary>
    private const int PngPredictor = 12;

    /// <summary>The columns in the predictor samples.</summary>
    private const int Columns = 3;

    /// <summary>The columns in the 4-bit TIFF sample.</summary>
    private const int NibbleColumns = 4;

    /// <summary>Four bits per component.</summary>
    private const int FourBits = 4;

    /// <summary>A bit depth no predictor supports.</summary>
    private const int UnsupportedBits = 3;

    /// <summary>The second byte of a zlib header with a bad check value.</summary>
    private const byte BadHeaderCheck = 0x00;

    /// <summary>The ASCII85 form of <see cref="HelloText"/>.</summary>
    private const string HelloEncoded = "87cURD]i,\"Ebo80";

    /// <summary>The text <see cref="HelloEncoded"/> decodes to.</summary>
    private const string HelloText = "Hello World!";

    /// <summary>The bytes ASCII85 decoding stops at.</summary>
    private static readonly byte[] IllegalByte = [0x01];

    /// <summary>A PNG sample: a full Sub row, then a Up row cut short.</summary>
    private static readonly byte[] PngTruncated = [0x01, 0x01, 0x01, 0x01, 0x02, 0x01, 0x01];

    /// <summary>What the truncated PNG sample decodes to.</summary>
    private static readonly byte[] PngTruncatedDecoded = [0x01, 0x02, 0x03, 0x02, 0x03];

    /// <summary>Four 4-bit components stored as differences.</summary>
    private static readonly byte[] NibbleDifferences = [0x11, 0x11];

    /// <summary>The 4-bit components after the TIFF predictor.</summary>
    private static readonly byte[] NibbleDecoded = [0x12, 0x34];

    /// <summary>ASCII85 skips a leading prefix.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Ascii85SkipsPrefix() =>
        await Assert.That(Ascii85($"<~{HelloEncoded}~>")).IsEqualTo(HelloText);

    /// <summary>A long run of 'z' groups does not overflow the output.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Ascii85DecodesManyZeroGroups() =>
        await Assert.That(Ascii85($"{new string('z', ZeroGroups)}~>").Length).IsEqualTo(ZeroGroups * ZeroGroupBytes);

    /// <summary>A 'z' inside a group drops the partial group before it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Ascii85ZeroResetsGroup() =>
        await Assert.That(Ascii85("9jqoz~>")).IsEqualTo("\0\0\0\0");

    /// <summary>Decoding stops at the first illegal character.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Ascii85StopsAtIllegalCharacter()
    {
        var encoded = Encoding.Latin1.GetBytes(HelloEncoded).Concat(IllegalByte).Concat(Encoding.Latin1.GetBytes(HelloEncoded)).ToArray();

        await Assert.That(DecodeBytes(encoded)).IsEqualTo(HelloText);
    }

    /// <summary>Predictor values from 3 to 9 leave the data as it is.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnusedPredictorValuesPassThrough()
    {
        var parms = Parameters(UnusedPredictor, Columns, EightBits);

        await Assert.That(Predict(parms, PngTruncated)).IsEquivalentTo(PngTruncated);
    }

    /// <summary>An unsupported bit depth leaves the data as it is.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsupportedBitDepthPassesThrough()
    {
        var parms = Parameters(PngPredictor, Columns, UnsupportedBits);

        await Assert.That(Predict(parms, PngTruncated)).IsEquivalentTo(PngTruncated);
    }

    /// <summary>A PNG row cut short is still decoded as far as it goes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PngTrailingPartialRowIsDecoded() =>
        await Assert.That(Predict(Parameters(PngPredictor, Columns, EightBits), PngTruncated)).IsEquivalentTo(PngTruncatedDecoded);

    /// <summary>The TIFF predictor works on 4-bit components.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TiffPredictorHandlesFourBitComponents() =>
        await Assert.That(Predict(Parameters(TiffPredictor, NibbleColumns, FourBits), NibbleDifferences)).IsEquivalentTo(NibbleDecoded);

    /// <summary>A zlib header with a bad check value still decodes through raw deflate.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedZlibHeaderDecodes()
    {
        var original = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("BT /F1 12 Tf (Hello) Tj ET\n", Repeats)));
        var compressed = Compress(original);
        compressed[1] = BadHeaderCheck;

        await Assert.That(Inflate(compressed)).IsEquivalentTo(original);
    }

    /// <summary>Builds predictor parameters.</summary>
    /// <param name="predictor">The predictor.</param>
    /// <param name="columns">The columns.</param>
    /// <param name="bits">The bits per component.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Parameters(int predictor, int columns, int bits)
    {
        var parms = new PdfDictionary(null);
        parms.Add(KnownName.Predictor, PdfValue.FromInteger(predictor));
        parms.Add(KnownName.Columns, PdfValue.FromInteger(columns));
        parms.Add(KnownName.BitsPerComponent, PdfValue.FromInteger(bits));
        return parms;
    }

    /// <summary>Undoes a predictor.</summary>
    /// <param name="parms">The predictor parameters.</param>
    /// <param name="data">The filtered bytes.</param>
    /// <returns>The decoded bytes.</returns>
    private static byte[] Predict(PdfDictionary parms, byte[] data)
    {
        var buffer = new PooledBuffer(data.Length);
        try
        {
            buffer.Write(data);
            PredictorFilter.Apply(parms, ref buffer);
            return buffer.ToArray();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Decodes Flate data.</summary>
    /// <param name="data">The compressed bytes.</param>
    /// <returns>The decoded bytes.</returns>
    private static byte[] Inflate(byte[] data)
    {
        var output = default(PooledBuffer);
        try
        {
            FlateFilter.Decode(data, ref output);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Compresses data with the Flate encoder.</summary>
    /// <param name="data">The data.</param>
    /// <returns>The compressed bytes.</returns>
    private static byte[] Compress(byte[] data)
    {
        var output = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(data, ref output);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Decodes ASCII85 text.</summary>
    /// <param name="text">The encoded text.</param>
    /// <returns>The decoded text.</returns>
    private static string Ascii85(string text) => DecodeBytes(Encoding.Latin1.GetBytes(text));

    /// <summary>Decodes ASCII85 bytes.</summary>
    /// <param name="encoded">The encoded bytes.</param>
    /// <returns>The decoded text.</returns>
    private static string DecodeBytes(byte[] encoded)
    {
        var output = default(PooledBuffer);
        try
        {
            Ascii85Filter.Decode(encoded, ref output);
            return Encoding.Latin1.GetString(output.WrittenSpan);
        }
        finally
        {
            output.Dispose();
        }
    }
}
