// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// Codes the HT SigProp and MagRef passes for bit-plane zero after a cleanup pass at bit-plane one. MagRef sends the
/// low bit of each sample the cleanup pass made significant; SigProp sends, stripe by stripe and four columns at a time,
/// the low bit of each other sample next to a significant one, then the signs of those that became significant.
/// </summary>
internal sealed class JpxTestHtRefiner
{
    /// <summary>The rows of a stripe.</summary>
    private const int StripeRows = 4;

    /// <summary>The last row of a stripe.</summary>
    private const int LastRow = StripeRows - 1;

    /// <summary>The state of a sample significant after the cleanup pass.</summary>
    private const int CleanupSignificant = 1;

    /// <summary>The state of a sample made significant by SigProp.</summary>
    private const int NewlySignificant = 2;

    /// <summary>The magnitude of every coefficient.</summary>
    private readonly int[] _magnitude;

    /// <summary>Whether each coefficient is negative.</summary>
    private readonly bool[] _negative;

    /// <summary>Each sample's significance state.</summary>
    private readonly int[] _state;

    /// <summary>The block width.</summary>
    private readonly int _width;

    /// <summary>The block height.</summary>
    private readonly int _height;

    /// <summary>Initializes a new instance of the <see cref="JpxTestHtRefiner"/> class.</summary>
    /// <param name="magnitude">The coefficient magnitudes, row by row.</param>
    /// <param name="negative">Whether each coefficient is negative.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    internal JpxTestHtRefiner(int[] magnitude, bool[] negative, int width, int height)
    {
        _magnitude = magnitude;
        _negative = negative;
        _width = width;
        _height = height;
        _state = new int[magnitude.Length];
        for (var i = 0; i < magnitude.Length; i++)
        {
            _state[i] = magnitude[i] > 1 ? CleanupSignificant : 0;
        }
    }

    /// <summary>Codes both passes.</summary>
    /// <param name="sigProp">Receives the SigProp bits.</param>
    /// <param name="causal">Whether the vertically causal mode is on.</param>
    /// <param name="magRef">Receives the MagRef bytes.</param>
    /// <returns><see langword="false"/> when a coefficient of magnitude one is not reached, so the coding is not lossless.</returns>
    internal bool Encode(JpxTestHtForwardWriter sigProp, bool causal, out byte[] magRef)
    {
        var reverse = JpxTestHtReverseWriter.ForMagRef();
        for (var top = 0; top < _height; top += StripeRows)
        {
            for (var x = 0; x < _width; x++)
            {
                MagRefColumn(reverse, x, top);
            }
        }

        magRef = reverse.Finish();
        for (var top = 0; top < _height; top += StripeRows)
        {
            for (var x = 0; x < _width; x += StripeRows)
            {
                SigPropGroup(sigProp, x, top, causal);
            }
        }

        for (var i = 0; i < _magnitude.Length; i++)
        {
            if (_magnitude[i] == 1 && _state[i] != NewlySignificant)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Sends the low bits of one stripe column's cleanup-significant samples.</summary>
    /// <param name="writer">The MagRef stream.</param>
    /// <param name="x">The column.</param>
    /// <param name="top">The stripe's first row.</param>
    private void MagRefColumn(JpxTestHtReverseWriter writer, int x, int top)
    {
        for (var y = top; y < Math.Min(top + StripeRows, _height); y++)
        {
            var i = (y * _width) + x;
            if (_state[i] == CleanupSignificant)
            {
                writer.Write((uint)(_magnitude[i] & 1), 1);
            }
        }
    }

    /// <summary>Codes one group of four columns of a stripe.</summary>
    /// <param name="writer">The SigProp stream.</param>
    /// <param name="left">The group's first column.</param>
    /// <param name="top">The stripe's first row.</param>
    /// <param name="causal">Whether the vertically causal mode is on.</param>
    private void SigPropGroup(JpxTestHtForwardWriter writer, int left, int top, bool causal)
    {
        var found = new List<int>();
        for (var x = left; x < Math.Min(left + StripeRows, _width); x++)
        {
            for (var y = top; y < Math.Min(top + StripeRows, _height); y++)
            {
                var i = (y * _width) + x;
                if (_state[i] != 0 || !HasSignificantNeighbour(x, y, causal))
                {
                    continue;
                }

                writer.Write((uint)_magnitude[i], 1);
                if (_magnitude[i] != 1)
                {
                    continue;
                }

                _state[i] = NewlySignificant;
                found.Add(i);
            }
        }

        foreach (var i in found)
        {
            writer.Write(_negative[i] ? 1U : 0U, 1);
        }
    }

    /// <summary>Determines whether any of a sample's eight neighbours is significant now.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="causal">Whether the row below the stripe is ignored.</param>
    /// <returns><see langword="true"/> when the sample is coded.</returns>
    private bool HasSignificantNeighbour(int x, int y, bool causal)
    {
        var lowest = causal && (y & LastRow) == LastRow ? 0 : 1;
        for (var dy = -1; dy <= lowest; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if ((dx != 0 || dy != 0) && IsSignificant(x + dx, y + dy))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Determines whether a sample inside the block is significant now.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns><see langword="false"/> outside the block.</returns>
    private bool IsSignificant(int x, int y) => x >= 0 && y >= 0 && x < _width && y < _height && _state[(y * _width) + x] != 0;
}
