// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Checks that zlib data read through a small window, with its output dropped, is judged as a full decode judges it.</summary>
public sealed class ZLibCheckTests
{
    /// <summary>The length of the sample data, many times the window.</summary>
    private const int InputLength = 300_000;

    /// <summary>The window the data is read through, far smaller than the data.</summary>
    private const int WindowLength = 1024;

    /// <summary>The multiplier that scatters sample values.</summary>
    private const int Scatter = 73;

    /// <summary>The divisor that cuts the compressed data short.</summary>
    private const int TruncateDivisor = 2;

    /// <summary>The offset of a byte inside the compressed data that the damage test changes.</summary>
    private const int DamagedOffset = 40;

    /// <summary>Sound data is clean at any length that holds it, and too long for a length one byte short of it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SoundDataIsCleanWithinItsLength()
    {
        var compressed = Compress(Samples());

        await Assert.That(Check(compressed, InputLength)).IsEqualTo(ZLibCheck.Clean);
        await Assert.That(Check(compressed, int.MaxValue)).IsEqualTo(ZLibCheck.Clean);
        await Assert.That(Check(compressed, InputLength - 1)).IsEqualTo(ZLibCheck.TooLong);
        await Assert.That(Check(compressed, 0)).IsEqualTo(ZLibCheck.TooLong);
    }

    /// <summary>Data that is cut short, damaged, empty or not zlib at all is not sound, so the caller decodes it in full.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BrokenDataIsNotSound()
    {
        var compressed = Compress(Samples());
        var damaged = (byte[])compressed.Clone();
        damaged[DamagedOffset] ^= byte.MaxValue;

        await Assert.That(Check(compressed.AsSpan(0, compressed.Length / TruncateDivisor).ToArray(), InputLength)).IsEqualTo(ZLibCheck.Unsound);
        await Assert.That(Check([], InputLength)).IsEqualTo(ZLibCheck.Unsound);
        await Assert.That(Check(Samples(), InputLength)).IsEqualTo(ZLibCheck.Unsound);
        await Assert.That(Check(damaged, InputLength)).IsNotEqualTo(ZLibCheck.Clean);
    }

    /// <summary>The check agrees with a full decode on whether sound and cut-short data ended cleanly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheCheckAgreesWithAFullDecode()
    {
        var compressed = Compress(Samples());
        byte[][] inputs = [compressed, compressed.AsSpan(0, compressed.Length / TruncateDivisor).ToArray()];
        foreach (var input in inputs)
        {
            var output = default(PooledBuffer);
            var clean = ZLibCodec.Decompress(input, raw: false, ref output);
            output.Dispose();

            await Assert.That(Check(input, InputLength) == ZLibCheck.Clean).IsEqualTo(clean);
        }
    }

    /// <summary>Checks data through the small window.</summary>
    /// <param name="input">The compressed bytes.</param>
    /// <param name="maximumLength">The most bytes the data may decode to.</param>
    /// <returns>What the check found.</returns>
    private static ZLibCheck Check(byte[] input, int maximumLength) =>
        ZLibCodec.Check(input, maximumLength, new byte[WindowLength], CancellationToken.None);

    /// <summary>Compresses bytes as zlib.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The compressed bytes.</returns>
    private static byte[] Compress(byte[] data)
    {
        var output = default(PooledBuffer);
        ZLibCodec.Compress(data, ref output);
        var compressed = output.ToArray();
        output.Dispose();
        return compressed;
    }

    /// <summary>Creates scattered sample bytes that still compress.</summary>
    /// <returns>The bytes.</returns>
    private static byte[] Samples()
    {
        var samples = new byte[InputLength];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (byte)((i / Scatter) + (i % Scatter));
        }

        return samples;
    }
}
