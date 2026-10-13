// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// Follows the Type 2 charstrings of the glyphs a subset keeps (Adobe Technical Note #5177) and records which local and
/// global subroutines they call, and whether a glyph is built from two others by the accented-character form of
/// <c>endchar</c>. A charstring whose subroutine numbers are computed with arithmetic cannot be followed, so every
/// subroutine is kept then.
/// </summary>
[DebuggerDisplay("CffSubrWalker: keep all {KeepAll}")]
internal sealed class CffSubrWalker
{
    /// <summary>The deepest subroutine nesting Type 2 allows.</summary>
    private const int MaxDepth = 10;

    /// <summary>The room for operands; Type 2 allows 48.</summary>
    private const int StackSize = 96;

    /// <summary>The prefix of a 16-bit integer.</summary>
    private const int ShortInt = 28;

    /// <summary>The first byte that starts a number.</summary>
    private const int FirstNumber = 32;

    /// <summary>The last single-byte integer.</summary>
    private const int SmallEnd = 246;

    /// <summary>The bias of single-byte integers.</summary>
    private const int SmallBias = 139;

    /// <summary>The last positive two-byte prefix.</summary>
    private const int PositiveEnd = 250;

    /// <summary>The first positive two-byte prefix.</summary>
    private const int PositiveStart = 247;

    /// <summary>The first negative two-byte prefix.</summary>
    private const int NegativeStart = 251;

    /// <summary>The bias of two-byte integers.</summary>
    private const int TwoByteBias = 108;

    /// <summary>The last two-byte prefix.</summary>
    private const int LastTwoByte = 254;

    /// <summary>The subroutine count below which the bias is <see cref="SmallSubrBias"/>.</summary>
    private const int SmallSubrLimit = 1_240;

    /// <summary>The subroutine count below which the bias is <see cref="MediumSubrBias"/>.</summary>
    private const int MediumSubrLimit = 33_900;

    /// <summary>The bias for fewer than 1,240 subroutines.</summary>
    private const int SmallSubrBias = 107;

    /// <summary>The bias for fewer than 33,900 subroutines.</summary>
    private const int MediumSubrBias = 1_131;

    /// <summary>The bias for more subroutines.</summary>
    private const int LargeSubrBias = 32_768;

    /// <summary>The operand pairs a stem operator or hint mask declares.</summary>
    private const int PairSize = 2;

    /// <summary>The <c>hstem</c> operator.</summary>
    private const int HStem = 1;

    /// <summary>The <c>vstem</c> operator.</summary>
    private const int VStem = 3;

    /// <summary>The <c>hstemhm</c> operator.</summary>
    private const int HStemHm = 18;

    /// <summary>The <c>vstemhm</c> operator.</summary>
    private const int VStemHm = 23;

    /// <summary>The <c>hintmask</c> operator.</summary>
    private const int HintMask = 19;

    /// <summary>The <c>cntrmask</c> operator.</summary>
    private const int CntrMask = 20;

    /// <summary>The bytes of a 16-bit integer operand.</summary>
    private const int ShortLength = 3;

    /// <summary>The bytes of a two-byte integer operand.</summary>
    private const int TwoByteLength = 2;

    /// <summary>The bytes of a 16.16 fixed point operand.</summary>
    private const int FixedLength = 5;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits in a 16.16 number's fraction.</summary>
    private const int FractionBits = 16;

    /// <summary>The stems one hint mask byte covers.</summary>
    private const int StemsPerByte = 8;

    /// <summary>The operands of the accented-character form of <c>endchar</c>.</summary>
    private const int AccentOperands = 4;

    /// <summary>The operator that ends a charstring.</summary>
    private const int EndChar = 14;

    /// <summary>The operator that returns from a subroutine.</summary>
    private const int Return = 11;

    /// <summary>The operator that calls a local subroutine.</summary>
    private const int CallSubr = 10;

    /// <summary>The operator that calls a global subroutine.</summary>
    private const int CallGsubr = 29;

    /// <summary>The escape operator.</summary>
    private const int Escape = 12;

    /// <summary>The first flex operator after the escape.</summary>
    private const int FirstFlex = 34;

    /// <summary>The last flex operator after the escape.</summary>
    private const int LastFlex = 37;

    /// <summary>The face.</summary>
    private readonly CffFace _face;

    /// <summary>The global subroutines called.</summary>
    private readonly bool[] _global;

    /// <summary>The local subroutines called, by Font DICT.</summary>
    private readonly bool[][] _local;

    /// <summary>The operand stack.</summary>
    private readonly int[] _stack = new int[StackSize];

    /// <summary>The operands on the stack.</summary>
    private int _depthOfStack;

    /// <summary>The stems declared so far in the glyph.</summary>
    private int _stems;

    /// <summary>Whether the glyph's charstring has ended.</summary>
    private bool _ended;

    /// <summary>Initializes a new instance of the <see cref="CffSubrWalker"/> class.</summary>
    /// <param name="face">The face.</param>
    internal CffSubrWalker(CffFace face)
    {
        _face = face;
        _global = new bool[face.GlobalSubrs.Count];
        _local = new bool[face.Dicts.Length][];
        for (var i = 0; i < _local.Length; i++)
        {
            _local[i] = new bool[face.Dicts[i].Subrs.Count];
        }
    }

    /// <summary>Gets a value indicating whether a charstring could not be followed, so every subroutine is kept.</summary>
    internal bool KeepAll { get; private set; }

    /// <summary>Gets a value indicating whether the last glyph walked is built from a base and an accent glyph.</summary>
    internal bool HasAccent { get; private set; }

    /// <summary>Gets a value indicating whether any glyph walked is built from a base and an accent glyph.</summary>
    internal bool AnyAccent { get; private set; }

    /// <summary>Gets the StandardEncoding code of the base glyph of the last glyph walked.</summary>
    internal int BaseCode { get; private set; }

    /// <summary>Gets the StandardEncoding code of the accent glyph of the last glyph walked.</summary>
    internal int AccentCode { get; private set; }

    /// <summary>Gets whether a global subroutine is called.</summary>
    /// <param name="index">The subroutine.</param>
    /// <returns><see langword="true"/> when the subroutine must be kept.</returns>
    internal bool IsGlobalUsed(int index) => KeepAll || _global[index];

    /// <summary>Gets whether a local subroutine is called.</summary>
    /// <param name="dict">The Font DICT.</param>
    /// <param name="index">The subroutine.</param>
    /// <returns><see langword="true"/> when the subroutine must be kept.</returns>
    internal bool IsLocalUsed(int dict, int index) => KeepAll || _local[dict][index];

    /// <summary>Follows a glyph's charstring.</summary>
    /// <param name="glyph">The glyph.</param>
    internal void Walk(int glyph)
    {
        _depthOfStack = 0;
        _stems = 0;
        _ended = false;
        HasAccent = false;
        Run(_face.CharStrings.Get(_face.Data.Span, glyph), _face.DictOf(glyph), 0);
    }

    /// <summary>Gets the bias added to a subroutine number.</summary>
    /// <param name="count">The number of subroutines.</param>
    /// <returns>The bias.</returns>
    private static int Bias(int count)
    {
        if (count < SmallSubrLimit)
        {
            return SmallSubrBias;
        }

        return count < MediumSubrLimit ? MediumSubrBias : LargeSubrBias;
    }

    /// <summary>Runs a charstring or subroutine.</summary>
    /// <param name="code">The code.</param>
    /// <param name="dict">The Font DICT whose local subroutines are called.</param>
    /// <param name="depth">The subroutine nesting.</param>
    private void Run(ReadOnlySpan<byte> code, int dict, int depth)
    {
        var position = 0;
        while (position < code.Length && !_ended)
        {
            int b = code[position];
            if (b >= FirstNumber || b == ShortInt)
            {
                position += Push(code, position);
                continue;
            }

            position++;
            switch (b)
            {
                case CallSubr:
                {
                    Call(_face.Dicts[dict].Subrs, _local[dict], dict, depth);
                    break;
                }

                case CallGsubr:
                {
                    Call(_face.GlobalSubrs, _global, dict, depth);
                    break;
                }

                case Return:
                {
                    return;
                }

                case EndChar:
                {
                    End();
                    break;
                }

                case Escape:
                {
                    Flex(FontBytes.U8(code, position));
                    position++;
                    break;
                }

                default:
                {
                    position += Clear(b, code, position);
                    break;
                }
            }
        }
    }

    /// <summary>Handles an operator that clears the stack.</summary>
    /// <param name="op">The operator.</param>
    /// <param name="code">The code.</param>
    /// <param name="position">The offset after the operator.</param>
    /// <returns>The bytes after the operator that belong to it: the hint mask.</returns>
    private int Clear(int op, ReadOnlySpan<byte> code, int position)
    {
        var extra = 0;
        if (op is HStem or VStem or HStemHm or VStemHm)
        {
            _stems += _depthOfStack / PairSize;
        }
        else if (op is HintMask or CntrMask)
        {
            _stems += _depthOfStack / PairSize;
            extra = Math.Min((_stems + StemsPerByte - 1) / StemsPerByte, code.Length - position);
        }

        _depthOfStack = 0;
        return extra;
    }

    /// <summary>Handles <c>endchar</c>, which may be the accented-character form.</summary>
    private void End()
    {
        if (_depthOfStack >= AccentOperands)
        {
            HasAccent = true;
            AnyAccent = true;
            BaseCode = _stack[_depthOfStack - PairSize];
            AccentCode = _stack[_depthOfStack - 1];
        }

        _ended = true;
    }

    /// <summary>Handles an escaped operator.</summary>
    /// <param name="op">The byte after the escape.</param>
    private void Flex(int op)
    {
        if (op is >= FirstFlex and <= LastFlex)
        {
            _depthOfStack = 0;
            return;
        }

        // Arithmetic and storage operators can compute a subroutine number, so the calls cannot be followed.
        KeepAll = true;
        _ended = true;
    }

    /// <summary>Calls a subroutine.</summary>
    /// <param name="subrs">The subroutines.</param>
    /// <param name="used">Where calls are recorded.</param>
    /// <param name="dict">The Font DICT.</param>
    /// <param name="depth">The nesting of the caller.</param>
    private void Call(CffIndex subrs, bool[] used, int dict, int depth)
    {
        if (_depthOfStack == 0 || depth >= MaxDepth)
        {
            KeepAll = true;
            _ended = true;
            return;
        }

        _depthOfStack--;
        var number = _stack[_depthOfStack] + Bias(subrs.Count);
        if ((uint)number >= (uint)subrs.Count)
        {
            return;
        }

        used[number] = true;
        Run(subrs.Get(_face.Data.Span, number), dict, depth + 1);
    }

    /// <summary>Pushes the number at a position.</summary>
    /// <param name="code">The code.</param>
    /// <param name="position">The offset of the number.</param>
    /// <returns>The bytes the number takes.</returns>
    private int Push(ReadOnlySpan<byte> code, int position)
    {
        int b = code[position];
        int value;
        int length;
        if (b == ShortInt)
        {
            value = (short)((FontBytes.U8(code, position + 1) << ByteBits) | FontBytes.U8(code, position + TwoByteLength));
            length = ShortLength;
        }
        else if (b <= SmallEnd)
        {
            value = b - SmallBias;
            length = 1;
        }
        else if (b <= PositiveEnd)
        {
            value = ((b - PositiveStart) << ByteBits) + FontBytes.U8(code, position + 1) + TwoByteBias;
            length = TwoByteLength;
        }
        else if (b <= LastTwoByte)
        {
            value = -((b - NegativeStart) << ByteBits) - FontBytes.U8(code, position + 1) - TwoByteBias;
            length = TwoByteLength;
        }
        else
        {
            value = (int)FontBytes.U32(code, position + 1) >> FractionBits;
            length = FixedLength;
        }

        if (_depthOfStack < _stack.Length)
        {
            _stack[_depthOfStack] = value;
            _depthOfStack++;
        }

        return length;
    }
}
