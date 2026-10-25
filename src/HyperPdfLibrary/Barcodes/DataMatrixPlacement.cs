// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Barcodes;

/// <summary>Places codewords in a Data Matrix mapping area in the diagonal sweep of ISO/IEC 16022, with its four corner cases.</summary>
[DebuggerDisplay("DataMatrixPlacement: {_side}x{_side}")]
internal sealed class DataMatrixPlacement
{
    /// <summary>A module nothing has been placed in.</summary>
    private const byte Unset = 0;

    /// <summary>A light module.</summary>
    private const byte Light = 1;

    /// <summary>A dark module.</summary>
    private const byte Dark = 2;

    /// <summary>The bits in a codeword.</summary>
    private const int CodewordBits = 8;

    /// <summary>The row the sweep starts on.</summary>
    private const int StartRow = 4;

    /// <summary>The distance the sweep moves along a diagonal.</summary>
    private const int Diagonal = 2;

    /// <summary>The distance the sweep moves across to start the downward diagonal and down to start the next upward one.</summary>
    private const int Jump = 3;

    /// <summary>The rows added to wrap a position off the top or the left edge.</summary>
    private const int WrapBase = 4;

    /// <summary>The modulus of the wrapping.</summary>
    private const int WrapModulus = 8;

    /// <summary>The mapping sides that are multiples of four have no second corner case.</summary>
    private const int FourMultiple = 4;

    /// <summary>The corner case that has the cell row as the sweep row at the bottom.</summary>
    private const int CornerOne = 0;

    /// <summary>The second corner case.</summary>
    private const int CornerTwo = 1;

    /// <summary>The third corner case.</summary>
    private const int CornerThree = 2;

    /// <summary>The fourth corner case.</summary>
    private const int CornerFour = 3;

    /// <summary>The row offsets of the eight modules of an ordinary codeword from the lower right one.</summary>
    private static readonly int[] UtahRows = [-2, -2, -1, -1, -1, 0, 0, 0];

    /// <summary>The column offsets of the eight modules of an ordinary codeword from the lower right one.</summary>
    private static readonly int[] UtahColumns = [-2, -1, -2, -1, 0, -2, -1, 0];

    /// <summary>The rows of each corner shape's modules; a negative number counts back from the mapping area's side.</summary>
    private static readonly int[][] CornerRows =
    [
        [-1, -1, -1, 0, 0, 1, 2, 3],
        [-3, -2, -1, 0, 0, 0, 0, 1],
        [-3, -2, -1, 0, 0, 1, 2, 3],
        [-1, -1, 0, 0, 0, 1, 1, 1],
    ];

    /// <summary>The columns of each corner shape's modules; a negative number counts back from the mapping area's side.</summary>
    private static readonly int[][] CornerColumns =
    [
        [0, 1, 2, -2, -1, -1, -1, -1],
        [0, 0, 0, -4, -3, -2, -1, -1],
        [0, 0, 0, -2, -1, -1, -1, -1],
        [0, -1, -3, -2, -1, -3, -2, -1],
    ];

    /// <summary>The modules across the mapping area.</summary>
    private readonly int _side;

    /// <summary>The codewords to place.</summary>
    private readonly byte[] _codewords;

    /// <summary>What each module holds.</summary>
    private readonly byte[] _cells;

    /// <summary>The row the sweep is on.</summary>
    private int _row;

    /// <summary>The column the sweep is on.</summary>
    private int _column;

    /// <summary>The next codeword to place.</summary>
    private int _codeword;

    /// <summary>Initializes a new instance of the <see cref="DataMatrixPlacement"/> class.</summary>
    /// <param name="side">The modules across the mapping area.</param>
    /// <param name="codewords">The codewords to place.</param>
    private DataMatrixPlacement(int side, byte[] codewords)
    {
        _side = side;
        _codewords = codewords;
        _cells = new byte[side * side];
    }

    /// <summary>Places the codewords.</summary>
    /// <param name="side">The modules across the mapping area.</param>
    /// <param name="codewords">The data and error correction codewords.</param>
    /// <returns>The modules row by row; true is dark.</returns>
    internal static bool[] Place(int side, byte[] codewords)
    {
        var placement = new DataMatrixPlacement(side, codewords);
        placement.Sweep();
        var modules = new bool[side * side];
        for (var i = 0; i < modules.Length; i++)
        {
            modules[i] = placement._cells[i] == Dark;
        }

        return modules;
    }

    /// <summary>Walks the mapping area in diagonals, placing a codeword at each free spot and at the corners when they come up.</summary>
    private void Sweep()
    {
        _row = StartRow;
        _column = 0;
        _codeword = 0;
        do
        {
            PlaceCorner();
            SweepUp();
            _row++;
            _column += Jump;
            SweepDown();
            _row += Jump;
            _column++;
        }
        while (_row < _side || _column < _side);

        var last = (_side * _side) - 1;
        if (_cells[last] != Unset)
        {
            return;
        }

        _cells[last] = Dark;
        _cells[last - _side - 1] = Dark;
    }

    /// <summary>Sweeps up and to the right, placing a codeword at each free spot.</summary>
    private void SweepUp()
    {
        do
        {
            if (_row < _side && _column >= 0 && _cells[(_row * _side) + _column] == Unset)
            {
                PlaceShape(UtahRows, UtahColumns, _row, _column);
            }

            _row -= Diagonal;
            _column += Diagonal;
        }
        while (_row >= 0 && _column < _side);
    }

    /// <summary>Sweeps down and to the left, placing a codeword at each free spot.</summary>
    private void SweepDown()
    {
        do
        {
            if (_row >= 0 && _column < _side && _cells[(_row * _side) + _column] == Unset)
            {
                PlaceShape(UtahRows, UtahColumns, _row, _column);
            }

            _row += Diagonal;
            _column -= Diagonal;
        }
        while (_row < _side && _column >= 0);
    }

    /// <summary>Places a codeword in a corner pattern when the sweep has reached one.</summary>
    private void PlaceCorner()
    {
        var corner = GetCorner();
        if (corner < 0)
        {
            return;
        }

        var rows = CornerRows[corner];
        var columns = CornerColumns[corner];
        for (var bit = 0; bit < CodewordBits; bit++)
        {
            Store(rows[bit] < 0 ? rows[bit] + _side : rows[bit], columns[bit] < 0 ? columns[bit] + _side : columns[bit], bit);
        }

        _codeword++;
    }

    /// <summary>Works out which corner case the sweep is at, if any.</summary>
    /// <returns>The corner, or -1 when the sweep is not at one.</returns>
    private int GetCorner()
    {
        if (_row == _side && _column == 0)
        {
            return CornerOne;
        }

        if (_row == _side - Diagonal && _column == 0)
        {
            return (_side & (FourMultiple - 1)) != 0 ? CornerTwo : GetThirdCorner();
        }

        return _row == _side + WrapBase && _column == Diagonal && (_side & (WrapModulus - 1)) == 0 ? CornerFour : -1;
    }

    /// <summary>Tells whether the third corner case applies on the row where the second one does not.</summary>
    /// <returns>The third corner, or -1.</returns>
    private int GetThirdCorner() => (_side & (WrapModulus - 1)) == WrapBase ? CornerThree : -1;

    /// <summary>Places the eight bits of the next codeword in the ordinary shape around a spot.</summary>
    /// <param name="rows">The row offsets of the eight modules.</param>
    /// <param name="columns">The column offsets of the eight modules.</param>
    /// <param name="originRow">The row the offsets count from.</param>
    /// <param name="originColumn">The column the offsets count from.</param>
    private void PlaceShape(int[] rows, int[] columns, int originRow, int originColumn)
    {
        for (var bit = 0; bit < CodewordBits; bit++)
        {
            PlaceWrapped(originRow + rows[bit], originColumn + columns[bit], bit);
        }

        _codeword++;
    }

    /// <summary>Places one bit, wrapping a position that falls off the top or left edge to the opposite side.</summary>
    /// <param name="row">The row, which may be negative.</param>
    /// <param name="column">The column, which may be negative.</param>
    /// <param name="bit">The bit, 0 for the highest to 7 for the lowest.</param>
    private void PlaceWrapped(int row, int column, int bit)
    {
        if (row < 0)
        {
            row += _side;
            column += WrapBase - ((_side + WrapBase) % WrapModulus);
        }

        if (column < 0)
        {
            column += _side;
            row += WrapBase - ((_side + WrapBase) % WrapModulus);
        }

        Store(row, column, bit);
    }

    /// <summary>Stores one bit of the current codeword.</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The column.</param>
    /// <param name="bit">The bit, 0 for the highest to 7 for the lowest.</param>
    private void Store(int row, int column, int bit)
    {
        var value = _codeword < _codewords.Length && ((_codewords[_codeword] >> (CodewordBits - 1 - bit)) & 1) != 0;
        _cells[(row * _side) + column] = value ? Dark : Light;
    }
}
