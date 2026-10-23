// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Barcodes;

/// <summary>Lays out a QR Code symbol: the fixed patterns, the data in its zigzag, the best mask and the format bits.</summary>
[DebuggerDisplay("QrMatrix: {_size}x{_size}")]
internal sealed class QrMatrix
{
    /// <summary>The number of masks.</summary>
    private const int MaskCount = 8;

    /// <summary>The row and column of the timing patterns.</summary>
    private const int TimingLine = 6;

    /// <summary>The generator of the format bits' error correction code.</summary>
    private const int FormatGenerator = 0x537;

    /// <summary>The pattern the format bits are combined with so they are never all light.</summary>
    private const int FormatMask = 0x5412;

    /// <summary>The generator of the version bits' error correction code.</summary>
    private const int VersionGenerator = 0x1F25;

    /// <summary>The bits of error correction in the format bits.</summary>
    private const int FormatCheckBits = 10;

    /// <summary>The bits of error correction in the version bits.</summary>
    private const int VersionCheckBits = 12;

    /// <summary>The bits in the format information.</summary>
    private const int FormatBits = 15;

    /// <summary>The bits in the version information.</summary>
    private const int VersionBits = 18;

    /// <summary>The modules from a corner to the centre of a finder pattern.</summary>
    private const int FinderCentre = 3;

    /// <summary>The distance from a finder or alignment pattern's centre to its outermost ring that is light.</summary>
    private const int FinderLightRing = 4;

    /// <summary>The distance from a finder pattern's centre to its inner light ring.</summary>
    private const int FinderInnerRing = 2;

    /// <summary>The format bits of the quartile level.</summary>
    private const int QuartileBits = 3;

    /// <summary>The format bits of the high level.</summary>
    private const int HighBits = 2;

    /// <summary>The bits the mask number takes in the format information.</summary>
    private const int MaskBits = 3;

    /// <summary>The number of bits to a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Two, the modulus of the simplest masks.</summary>
    private const int Two = 2;

    /// <summary>Three, the modulus of the other masks.</summary>
    private const int Three = 3;

    /// <summary>The number of the fifth mask.</summary>
    private const int MaskFour = 4;

    /// <summary>The number of the sixth mask.</summary>
    private const int MaskFive = 5;

    /// <summary>The number of the seventh mask.</summary>
    private const int MaskSix = 6;

    /// <summary>The first version that has version information modules.</summary>
    private const int FirstVersionWithInfo = 7;

    /// <summary>The column of the format bits' vertical strip and the row of its horizontal one.</summary>
    private const int FormatStrip = 8;

    /// <summary>The modules from a finder's centre to the version information block.</summary>
    private const int VersionOffset = 11;

    /// <summary>The columns of the first copy of the format bits, by bit.</summary>
    private static readonly int[] FirstCopyX = [8, 8, 8, 8, 8, 8, 8, 8, 7, 5, 4, 3, 2, 1, 0];

    /// <summary>The rows of the first copy of the format bits, by bit.</summary>
    private static readonly int[] FirstCopyY = [0, 1, 2, 3, 4, 5, 7, 8, 8, 8, 8, 8, 8, 8, 8];

    /// <summary>The modules across.</summary>
    private readonly int _size;

    /// <summary>The modules row by row; true is dark.</summary>
    private readonly bool[] _modules;

    /// <summary>Which modules belong to a fixed pattern and so are neither data nor masked.</summary>
    private readonly bool[] _fixed;

    /// <summary>Initializes a new instance of the <see cref="QrMatrix"/> class.</summary>
    /// <param name="version">The version.</param>
    private QrMatrix(int version)
    {
        _size = QrTables.GetSize(version);
        _modules = new bool[_size * _size];
        _fixed = new bool[_size * _size];
    }

    /// <summary>Builds a symbol.</summary>
    /// <param name="version">The version.</param>
    /// <param name="level">The error correction level.</param>
    /// <param name="codewords">The data and error correction bytes, in the order they are placed.</param>
    /// <returns>The modules row by row; true is dark.</returns>
    internal static bool[] Build(int version, PdfQrErrorCorrection level, byte[] codewords)
    {
        var matrix = new QrMatrix(version);
        matrix.DrawFixedPatterns(version);
        matrix.PlaceData(codewords);
        var best = 0;
        var bestScore = int.MaxValue;

        // Each mask is tried on a copy, so the data is never un-masked again; the copy is exactly as long as the symbol, which
        // QrPenalty needs, so it is a plain array.
        var trial = new bool[matrix._modules.Length];
        for (var mask = 0; mask < MaskCount; mask++)
        {
            matrix.DrawFormat(level, mask);
            matrix._modules.CopyTo(trial, 0);
            matrix.ApplyMask(trial, mask);
            var score = QrPenalty.Score(trial, matrix._size, bestScore);
            if (score >= bestScore)
            {
                continue;
            }

            best = mask;
            bestScore = score;
        }

        matrix.ApplyMask(best);
        matrix.DrawFormat(level, best);
        return matrix._modules;
    }

    /// <summary>Gets the two format bits of an error correction level.</summary>
    /// <param name="level">The level.</param>
    /// <returns>The bits.</returns>
    private static int GetLevelBits(PdfQrErrorCorrection level) => level switch
    {
        PdfQrErrorCorrection.Low => 1,
        PdfQrErrorCorrection.Medium => 0,
        PdfQrErrorCorrection.Quartile => QuartileBits,
        _ => HighBits,
    };

    /// <summary>Determines whether a mask flips a module.</summary>
    /// <param name="mask">The mask, 0 to 7.</param>
    /// <param name="x">The module's column.</param>
    /// <param name="y">The module's row.</param>
    /// <returns><see langword="true"/> when the module is flipped.</returns>
    private static bool Flips(int mask, int x, int y) => mask switch
    {
        0 => (x + y) % Two == 0,
        1 => y % Two == 0,
        Two => x % Three == 0,
        Three => (x + y) % Three == 0,
        MaskFour => ((x / Three) + (y / Two)) % Two == 0,
        MaskFive => (x * y % Two) + (x * y % Three) == 0,
        MaskSix => ((x * y % Two) + (x * y % Three)) % Two == 0,
        _ => (((x + y) % Two) + (x * y % Three)) % Two == 0,
    };

    /// <summary>Gets a bit of a value.</summary>
    /// <param name="value">The value.</param>
    /// <param name="index">The bit, counted from the lowest.</param>
    /// <returns><see langword="true"/> for a one.</returns>
    private static bool GetBit(int value, int index) => ((value >> index) & 1) != 0;

    /// <summary>Sets a fixed module.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="dark">Whether it is dark.</param>
    private void SetFixed(int x, int y, bool dark)
    {
        _modules[(y * _size) + x] = dark;
        _fixed[(y * _size) + x] = true;
    }

    /// <summary>Draws the timing, finder and alignment patterns and reserves the format and version areas.</summary>
    /// <param name="version">The version.</param>
    private void DrawFixedPatterns(int version)
    {
        for (var i = 0; i < _size; i++)
        {
            SetFixed(TimingLine, i, (i & 1) == 0);
            SetFixed(i, TimingLine, (i & 1) == 0);
        }

        DrawFinder(FinderCentre, FinderCentre);
        DrawFinder(_size - FinderCentre - 1, FinderCentre);
        DrawFinder(FinderCentre, _size - FinderCentre - 1);
        DrawAlignments(version);
        DrawFormat(PdfQrErrorCorrection.Low, 0);
        DrawVersion(version);
    }

    /// <summary>Draws a finder pattern with its light border.</summary>
    /// <param name="centreX">The centre's column.</param>
    /// <param name="centreY">The centre's row.</param>
    private void DrawFinder(int centreX, int centreY)
    {
        for (var dy = -FinderLightRing; dy <= FinderLightRing; dy++)
        {
            for (var dx = -FinderLightRing; dx <= FinderLightRing; dx++)
            {
                var x = centreX + dx;
                var y = centreY + dy;
                if ((uint)x >= (uint)_size || (uint)y >= (uint)_size)
                {
                    continue;
                }

                var ring = Math.Max(Math.Abs(dx), Math.Abs(dy));
                SetFixed(x, y, ring is not (FinderInnerRing or FinderLightRing));
            }
        }
    }

    /// <summary>Draws the alignment patterns, which the larger versions have between the finders.</summary>
    /// <param name="version">The version.</param>
    private void DrawAlignments(int version)
    {
        var positions = QrTables.GetAlignmentPositions(version);
        var last = positions.Length - 1;
        for (var i = 0; i < positions.Length; i++)
        {
            for (var j = 0; j < positions.Length; j++)
            {
                var overlapsFinder = (i == 0 && j == 0) || (i == 0 && j == last) || (i == last && j == 0);
                if (!overlapsFinder)
                {
                    DrawAlignment(positions[i], positions[j]);
                }
            }
        }
    }

    /// <summary>Draws one alignment pattern.</summary>
    /// <param name="centreX">The centre's column.</param>
    /// <param name="centreY">The centre's row.</param>
    private void DrawAlignment(int centreX, int centreY)
    {
        for (var dy = -FinderInnerRing; dy <= FinderInnerRing; dy++)
        {
            for (var dx = -FinderInnerRing; dx <= FinderInnerRing; dx++)
            {
                SetFixed(centreX + dx, centreY + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
            }
        }
    }

    /// <summary>Draws both copies of the format bits and the one module that is always dark.</summary>
    /// <param name="level">The error correction level.</param>
    /// <param name="mask">The mask in use.</param>
    private void DrawFormat(PdfQrErrorCorrection level, int mask)
    {
        var data = (GetLevelBits(level) << MaskBits) | mask;
        var remainder = data;
        for (var i = 0; i < FormatCheckBits; i++)
        {
            remainder = (remainder << 1) ^ ((remainder >> (FormatCheckBits - 1)) * FormatGenerator);
        }

        var bits = ((data << FormatCheckBits) | remainder) ^ FormatMask;
        for (var i = 0; i < FormatBits; i++)
        {
            var dark = GetBit(bits, i);
            SetFixed(FirstCopyX[i], FirstCopyY[i], dark);
            var second = GetSecondCopy(i);
            SetFixed(second.X, second.Y, dark);
        }

        SetFixed(FormatStrip, _size - FormatStrip, true);
    }

    /// <summary>Gets where a format bit goes in the copy split between the other two finders.</summary>
    /// <param name="bit">The bit's index.</param>
    /// <returns>The module.</returns>
    private ModulePosition GetSecondCopy(int bit) => bit < FormatStrip
        ? new ModulePosition(_size - 1 - bit, FormatStrip)
        : new ModulePosition(FormatStrip, _size - FormatBits + bit);

    /// <summary>Draws both copies of the version bits, which versions 7 and up have.</summary>
    /// <param name="version">The version.</param>
    private void DrawVersion(int version)
    {
        if (version < FirstVersionWithInfo)
        {
            return;
        }

        var remainder = version;
        for (var i = 0; i < VersionCheckBits; i++)
        {
            remainder = (remainder << 1) ^ ((remainder >> (VersionCheckBits - 1)) * VersionGenerator);
        }

        var bits = (version << VersionCheckBits) | remainder;
        for (var i = 0; i < VersionBits; i++)
        {
            var dark = GetBit(bits, i);
            var a = _size - VersionOffset + (i % Three);
            var b = i / Three;
            SetFixed(a, b, dark);
            SetFixed(b, a, dark);
        }
    }

    /// <summary>Places the codewords in the free modules, two columns at a time, zigzagging up and down.</summary>
    /// <param name="codewords">The codewords.</param>
    private void PlaceData(byte[] codewords)
    {
        var bit = 0;
        for (var right = _size - 1; right >= 1; right -= Two)
        {
            if (right == TimingLine)
            {
                right = TimingLine - 1;
            }

            var upward = ((right + 1) & Two) == 0;
            for (var step = 0; step < _size; step++)
            {
                bit = PlacePair(codewords, bit, ((upward ? _size - 1 - step : step) * _size) + right);
            }
        }
    }

    /// <summary>Places the bits of the codewords in the two modules of a row of a column pair, right one first.</summary>
    /// <param name="codewords">The codewords whose bits are placed.</param>
    /// <param name="bit">The index of the next bit to place.</param>
    /// <param name="rightAt">The index of the right module of the pair.</param>
    /// <returns>The index after the last bit placed.</returns>
    private int PlacePair(byte[] codewords, int bit, int rightAt)
    {
        for (var column = 0; column < Two; column++)
        {
            var at = rightAt - column;
            if (_fixed[at] || bit >= codewords.Length * ByteBits)
            {
                continue;
            }

            _modules[at] = GetBit(codewords[bit / ByteBits], ByteBits - 1 - (bit % ByteBits));
            bit++;
        }

        return bit;
    }

    /// <summary>Flips the data modules a mask covers; applying a mask twice removes it.</summary>
    /// <param name="mask">The mask, 0 to 7.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyMask(int mask) => ApplyMask(_modules, mask);

    /// <summary>Flips the data modules a mask covers in a copy of the modules.</summary>
    /// <param name="modules">The modules to flip.</param>
    /// <param name="mask">The mask, 0 to 7.</param>
    private void ApplyMask(Span<bool> modules, int mask)
    {
        for (var y = 0; y < _size; y++)
        {
            for (var x = 0; x < _size; x++)
            {
                var at = (y * _size) + x;
                if (!_fixed[at] && Flips(mask, x, y))
                {
                    modules[at] = !modules[at];
                }
            }
        }
    }
}
