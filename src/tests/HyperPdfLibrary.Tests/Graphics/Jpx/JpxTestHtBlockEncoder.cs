// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// A plain HT block encoder (ITU-T Rec. T.814) for round-trip tests, written from the standard over two-dimensional
/// arrays. It codes one HT set: a cleanup pass at bit-plane zero, or, when asked and every coefficient of magnitude one
/// would be reached, a cleanup pass at bit-plane one with SigProp and MagRef passes for bit-plane zero. Both are lossless.
/// It shares only the VLC tables with the decoder.
/// </summary>
internal sealed class JpxTestHtBlockEncoder
{
    /// <summary>The rows of a stripe.</summary>
    private const int StripeRows = 4;

    /// <summary>The last row of a stripe.</summary>
    private const int LastRow = StripeRows - 1;

    /// <summary>The columns and rows of a quad.</summary>
    private const int QuadSide = 2;

    /// <summary>The columns of a quad pair.</summary>
    private const int PairWidth = 4;

    /// <summary>The samples of a quad.</summary>
    private const int QuadSamples = 4;

    /// <summary>The padding of the exponent lines: a zero column left, and room for the far north-east neighbour.</summary>
    private const int LinePadding = 4;

    /// <summary>The rho bits of a quad's left column.</summary>
    private const int LeftColumn = 0x03;

    /// <summary>The rho bits of a quad's right column.</summary>
    private const int RightColumn = 0x0C;

    /// <summary>The first-row context bits from the previous quad's right column.</summary>
    private const int FirstRowEastBits = 0x06;

    /// <summary>The context bit of the west neighbours.</summary>
    private const int WestContext = 2;

    /// <summary>The context bit of the east neighbours.</summary>
    private const int EastContext = 4;

    /// <summary>The exponent the context term starts above.</summary>
    private const int ContextBase = 2;

    /// <summary>The smallest residual of the long first-row coding.</summary>
    private const int LargeResidual = 3;

    /// <summary>The residual offset of the long first-row coding.</summary>
    private const int LargeOffset = 2;

    /// <summary>The prefix of residual 2, read as 0 then 1.</summary>
    private const int PrefixTwo = 0b10;

    /// <summary>The prefix of residuals 3 and 4, read as 0, 0, 1.</summary>
    private const int PrefixThree = 0b100;

    /// <summary>The bits of a long prefix.</summary>
    private const int LongPrefixBits = 3;

    /// <summary>The smallest residual with a five-bit suffix.</summary>
    private const int FiveBitBase = 5;

    /// <summary>The bits of the long suffix.</summary>
    private const int FiveBitSuffix = 5;

    /// <summary>The residual mode in which both quads have a u-offset.</summary>
    private const int BothOffsets = 3;

    /// <summary>The fill bit of MagSgn.</summary>
    private const int MagSgnPad = 1;

    /// <summary>The passes of the refinement segment.</summary>
    private const int RefinementPasses = 2;

    /// <summary>The magnitude of every coefficient.</summary>
    private readonly int[] _magnitude;

    /// <summary>Whether each coefficient is negative.</summary>
    private readonly bool[] _negative;

    /// <summary>The block width.</summary>
    private readonly int _width;

    /// <summary>The block height.</summary>
    private readonly int _height;

    /// <summary>The bit-plane of the cleanup pass: 0, or 1 when refinement passes code bit-plane 0.</summary>
    private readonly int _shift;

    /// <summary>The MEL stream.</summary>
    private readonly JpxTestHtMelWriter _mel = new();

    /// <summary>The VLC stream.</summary>
    private readonly JpxTestHtReverseWriter _vlc = JpxTestHtReverseWriter.ForVlc();

    /// <summary>The MagSgn stream.</summary>
    private readonly JpxTestHtForwardWriter _magSgn = new(MagSgnPad);

    /// <summary>Initializes a new instance of the <see cref="JpxTestHtBlockEncoder"/> class.</summary>
    /// <param name="coefficients">The coefficients, row by row.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="shift">The cleanup bit-plane.</param>
    private JpxTestHtBlockEncoder(int[] coefficients, int width, int height, int shift)
    {
        _width = width;
        _height = height;
        _shift = shift;
        _magnitude = new int[coefficients.Length];
        _negative = new bool[coefficients.Length];
        for (var i = 0; i < coefficients.Length; i++)
        {
            _magnitude[i] = Math.Abs(coefficients[i]);
            _negative[i] = coefficients[i] < 0;
        }
    }

    /// <summary>Codes a block.</summary>
    /// <param name="coefficients">The coefficients, row by row.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="refine">Whether to use refinement passes when they stay lossless.</param>
    /// <param name="causal">Whether the vertically causal mode is on.</param>
    /// <returns>The coded block: bit-planes p and one or two segments.</returns>
    internal static JpxTestBlockCode Encode(int[] coefficients, int width, int height, bool refine, bool causal)
    {
        if (Array.TrueForAll(coefficients, static value => value == 0))
        {
            return new(0, []);
        }

        if (refine && new JpxTestHtBlockEncoder(coefficients, width, height, 1).TryRefined(causal) is { } refined)
        {
            return refined;
        }

        var encoder = new JpxTestHtBlockEncoder(coefficients, width, height, 0);
        return new(1, [new(1, encoder.Cleanup())]);
    }

    /// <summary>Gets the bit length of a value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The bits needed.</returns>
    private static int BitLength(int value) => value == 0 ? 0 : BitOperations.Log2((uint)value) + 1;

    /// <summary>Gets a quad's context from the previous quad and the row above.</summary>
    /// <param name="firstRow">Whether the quad is in the first row.</param>
    /// <param name="previous">The previous quad's rho.</param>
    /// <param name="above">The exponents of the row above, column c at c + 1.</param>
    /// <param name="x">The quad's first column.</param>
    /// <returns>The context.</returns>
    private static int Context(bool firstRow, int previous, int[] above, int x)
    {
        if (firstRow)
        {
            return ((previous & LeftColumn) != 0 ? 1 : 0) | ((previous >> 1) & FirstRowEastBits);
        }

        var north = (above[x] | above[x + 1]) != 0 ? 1 : 0;
        var west = (previous & RightColumn) != 0 ? WestContext : 0;
        var east = (above[x + QuadSide] | above[x + QuadSide + 1]) != 0 ? EastContext : 0;
        return north | west | east;
    }

    /// <summary>Checks whether a codeword's EMB bits fit a quad's samples.</summary>
    /// <param name="code">The codeword.</param>
    /// <param name="exponents">The samples' exponents.</param>
    /// <param name="rho">The significance pattern.</param>
    /// <param name="bound">The exponent bound.</param>
    /// <returns><see langword="true"/> when the decoder would rebuild the samples.</returns>
    private static bool Fits(JpxTestHtCode code, ReadOnlySpan<int> exponents, int rho, int bound)
    {
        for (var n = 0; n < QuadSamples; n++)
        {
            if (((rho >> n) & 1) == 0)
            {
                continue;
            }

            var known = (code.Known >> n) & 1;
            var one = (code.Ones >> n) & 1;
            var expected = known == 1 && bound >= QuadSide && exponents[n] == bound ? 1 : 0;
            if ((known == 1 && bound < QuadSide) || one != expected)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Writes a residual's prefix.</summary>
    /// <param name="writer">The VLC stream.</param>
    /// <param name="residual">The residual, 1 to 36.</param>
    private static void WritePrefix(JpxTestHtReverseWriter writer, int residual)
    {
        switch (residual)
        {
            case 1:
            {
                writer.Write(1, 1);
                break;
            }

            case QuadSide:
            {
                writer.Write(PrefixTwo, QuadSide);
                break;
            }

            default:
            {
                writer.Write(residual < FiveBitBase ? PrefixThree : 0U, LongPrefixBits);
                break;
            }
        }
    }

    /// <summary>Writes a residual's suffix.</summary>
    /// <param name="writer">The VLC stream.</param>
    /// <param name="residual">The residual, 1 to 36.</param>
    private static void WriteSuffix(JpxTestHtReverseWriter writer, int residual)
    {
        if (residual >= FiveBitBase)
        {
            writer.Write((uint)(residual - FiveBitBase), FiveBitSuffix);
        }
        else if (residual >= LargeResidual)
        {
            writer.Write((uint)(residual - LargeResidual), 1);
        }
    }

    /// <summary>Writes two residuals as both prefixes, then both suffixes.</summary>
    /// <param name="writer">The VLC stream.</param>
    /// <param name="first">The first residual.</param>
    /// <param name="second">The second residual.</param>
    private static void WritePair(JpxTestHtReverseWriter writer, int first, int second)
    {
        WritePrefix(writer, first);
        WritePrefix(writer, second);
        WriteSuffix(writer, first);
        WriteSuffix(writer, second);
    }

    /// <summary>Codes the cleanup pass at bit-plane one with SigProp and MagRef passes, when that stays lossless.</summary>
    /// <param name="causal">Whether the vertically causal mode is on.</param>
    /// <returns>The coded block, or <see langword="null"/> when a coefficient of magnitude one would be missed.</returns>
    private JpxTestBlockCode? TryRefined(bool causal)
    {
        if (!Array.Exists(_magnitude, static value => value > 1))
        {
            return null;
        }

        var sigProp = new JpxTestHtForwardWriter(0);
        if (!new JpxTestHtRefiner(_magnitude, _negative, _width, _height).Encode(sigProp, causal, out var magRef))
        {
            return null;
        }

        byte[] refinement = [.. sigProp.Finish(), .. magRef];
        return new(1 + _shift, [new(1, Cleanup()), new(RefinementPasses, refinement)]);
    }

    /// <summary>Gets a coefficient's magnitude at the cleanup bit-plane, or zero outside the block.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The magnitude mu.</returns>
    private int Mu(int x, int y) => x < _width && y < _height ? _magnitude[(y * _width) + x] >> _shift : 0;

    /// <summary>Gets a sample's exponent: one more than the bit length of mu less one, or zero when insignificant.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The exponent.</returns>
    private int Exponent(int x, int y)
    {
        var mu = Mu(x, y);
        return mu == 0 ? 0 : BitLength(mu - 1) + 1;
    }

    /// <summary>Gets a quad's significance pattern.</summary>
    /// <param name="x">The quad's first column.</param>
    /// <param name="y">The quad's first row.</param>
    /// <returns>rho: bit 0 top-left, 1 bottom-left, 2 top-right, 3 bottom-right.</returns>
    private int Rho(int x, int y)
    {
        var rho = 0;
        for (var n = 0; n < QuadSamples; n++)
        {
            rho |= (Mu(x + (n >> 1), y + (n & 1)) != 0 ? 1 : 0) << n;
        }

        return rho;
    }

    /// <summary>Codes the cleanup pass.</summary>
    /// <returns>The cleanup segment.</returns>
    private byte[] Cleanup()
    {
        int[][] lines = [new int[_width + LinePadding], new int[_width + LinePadding]];
        var current = 0;
        for (var y = 0; y < _height; y += QuadSide)
        {
            Array.Clear(lines[current]);
            CleanupRow(y, lines[1 - current], lines[current]);
            current = 1 - current;
        }

        var magSgn = _magSgn.Finish();
        var mel = _mel.Finish();
        _vlc.Flush();
        var vlc = _vlc.FinishVlc(mel.Length + _vlc.Count);
        return [.. magSgn, .. mel, .. vlc];
    }

    /// <summary>Codes one row of quads.</summary>
    /// <param name="y">The row's first sample row.</param>
    /// <param name="above">The exponents of the row above.</param>
    /// <param name="below">Receives the exponents of this row's lower samples.</param>
    private void CleanupRow(int y, int[] above, int[] below)
    {
        var firstRow = y == 0;
        var previous = 0;
        for (var x = 0; x < _width; x += PairWidth)
        {
            var first = Plan(firstRow, Context(firstRow, previous, above, x), above, x, y);
            var hasSecond = x + QuadSide < _width;
            var second = hasSecond ? Plan(firstRow, Context(firstRow, first.Rho, above, x + QuadSide), above, x + QuadSide, y) : default;
            previous = second.Rho;
            WriteQuad(first);
            if (hasSecond)
            {
                WriteQuad(second);
            }

            WriteResiduals(first.Residual, second.Residual, firstRow);
            WriteSamples(first, x, y, below);
            if (hasSecond)
            {
                WriteSamples(second, x + QuadSide, y, below);
            }
        }
    }

    /// <summary>Chooses a quad's bound and codeword.</summary>
    /// <param name="firstRow">Whether the quad is in the first row.</param>
    /// <param name="context">The quad's context.</param>
    /// <param name="above">The exponents of the row above.</param>
    /// <param name="x">The quad's first column.</param>
    /// <param name="y">The quad's first row.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="InvalidOperationException">No codeword fits.</exception>
    private JpxTestHtQuad Plan(bool firstRow, int context, int[] above, int x, int y)
    {
        var rho = Rho(x, y);
        if (rho == 0 && context == 0)
        {
            return new(0, 0, 0, 1, default);
        }

        Span<int> exponents = stackalloc int[QuadSamples];
        var largest = 0;
        for (var n = 0; n < QuadSamples; n++)
        {
            exponents[n] = Exponent(x + (n >> 1), y + (n & 1));
            largest = Math.Max(largest, exponents[n]);
        }

        var kappa = 1;
        if (!firstRow && BitOperations.PopCount((uint)rho) >= QuadSide)
        {
            var near = Math.Max(Math.Max(above[x], above[x + 1]), Math.Max(above[x + QuadSide], above[x + QuadSide + 1]));
            kappa = Math.Max(1, near - 1);
        }

        var residual = Math.Max(0, largest - kappa);
        for (var attempt = 0; attempt < QuadSide; attempt++)
        {
            var bound = kappa + residual;
            foreach (var code in JpxTestHtCodebook.Find(firstRow, context, rho, residual > 0))
            {
                if (Fits(code, exponents, rho, bound))
                {
                    return new(rho, context, residual, bound, code);
                }
            }

            residual++;
        }

        throw new InvalidOperationException("No VLC codeword fits the quad.");
    }

    /// <summary>Writes a quad's MEL event and codeword.</summary>
    /// <param name="quad">The quad.</param>
    private void WriteQuad(in JpxTestHtQuad quad)
    {
        if (quad.Context == 0)
        {
            _mel.Encode(quad.Rho != 0);
            if (quad.Rho == 0)
            {
                return;
            }
        }

        _vlc.Write((uint)quad.Code.Bits, quad.Code.Length);
    }

    /// <summary>Writes a quad pair's residuals.</summary>
    /// <param name="first">The first quad's residual.</param>
    /// <param name="second">The second quad's residual.</param>
    /// <param name="firstRow">Whether the pair is in the first row.</param>
    private void WriteResiduals(int first, int second, bool firstRow)
    {
        var mode = (first > 0 ? 1 : 0) | (second > 0 ? QuadSide : 0);
        if (mode != BothOffsets)
        {
            if (mode != 0)
            {
                WritePrefix(_vlc, Math.Max(first, second));
                WriteSuffix(_vlc, Math.Max(first, second));
            }

            return;
        }

        if (!firstRow)
        {
            WritePair(_vlc, first, second);
            return;
        }

        WriteFirstRowPair(first, second);
    }

    /// <summary>Writes the residuals of a first-row pair in which both quads have a u-offset.</summary>
    /// <param name="first">The first quad's residual.</param>
    /// <param name="second">The second quad's residual.</param>
    private void WriteFirstRowPair(int first, int second)
    {
        var large = first >= LargeResidual && second >= LargeResidual;
        _mel.Encode(large);
        if (large)
        {
            WritePair(_vlc, first - LargeOffset, second - LargeOffset);
        }
        else if (first >= LargeResidual)
        {
            WritePrefix(_vlc, first);
            _vlc.Write((uint)(second - 1), 1);
            WriteSuffix(_vlc, first);
        }
        else
        {
            WritePair(_vlc, first, second);
        }
    }

    /// <summary>Writes the magnitude and sign bits of a quad's significant samples, and records the lower row's exponents.</summary>
    /// <param name="quad">The quad.</param>
    /// <param name="x">The quad's first column.</param>
    /// <param name="y">The quad's first row.</param>
    /// <param name="below">The exponents of this row's lower samples.</param>
    private void WriteSamples(in JpxTestHtQuad quad, int x, int y, int[] below)
    {
        for (var n = 0; n < QuadSamples; n++)
        {
            if (((quad.Rho >> n) & 1) == 0)
            {
                continue;
            }

            var sx = x + (n >> 1);
            var sy = y + (n & 1);
            var bits = quad.Bound - ((quad.Code.Known >> n) & 1);
            var value = (uint)((Mu(sx, sy) - 1) << 1) | (_negative[(sy * _width) + sx] ? 1U : 0U);
            _magSgn.Write(value, bits);
            if ((n & 1) != 0)
            {
                below[sx + 1] = Exponent(sx, sy);
            }
        }
    }
}
