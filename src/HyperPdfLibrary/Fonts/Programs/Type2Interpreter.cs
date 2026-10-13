// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>
/// Runs Type 2 charstrings (Adobe Technical Note #5177) and sends the outline to a sink. The argument stack and
/// transient array live inline, so a run does not allocate.
/// </summary>
internal ref partial struct Type2Interpreter
{
    /// <summary>The first byte that starts an operand.</summary>
    private const int FirstOperandByte = 32;

    /// <summary>The prefix of a 16-bit integer.</summary>
    private const int ShortInt = 28;

    /// <summary>The prefix of a 16.16 fixed-point number.</summary>
    private const int Fixed = 255;

    /// <summary>The last single-byte integer.</summary>
    private const int SmallIntEnd = 246;

    /// <summary>The bias of single-byte integers.</summary>
    private const int SmallIntBias = 139;

    /// <summary>The first negative two-byte integer prefix.</summary>
    private const int NegativeStart = 251;

    /// <summary>The first positive two-byte integer prefix.</summary>
    private const int PositiveStart = 247;

    /// <summary>The bias of two-byte integers.</summary>
    private const int TwoByteBias = 108;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The scale of a 16.16 fixed-point number.</summary>
    private const float FixedScale = 65536F;

    /// <summary>The size of a 16.16 fixed-point number.</summary>
    private const int FixedSize = 4;

    /// <summary>The number of stems one hint mask byte covers.</summary>
    private const int StemsPerMaskByte = 8;

    /// <summary>The arguments of the accented-character form of endchar.</summary>
    private const int SeacArguments = 4;

    /// <summary>The subroutine bias for small subroutine counts.</summary>
    private const int SmallBias = 107;

    /// <summary>The subroutine bias for medium subroutine counts.</summary>
    private const int MediumBias = 1131;

    /// <summary>The subroutine bias for large subroutine counts.</summary>
    private const int LargeBias = 32_768;

    /// <summary>The largest count that uses the small bias.</summary>
    private const int SmallBiasLimit = 1240;

    /// <summary>The largest count that uses the medium bias.</summary>
    private const int MediumBiasLimit = 33_900;

    /// <summary>The CFF data the subroutine indexes refer to.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The global subroutines.</summary>
    private readonly CffIndex _globalSubrs;

    /// <summary>The local subroutines.</summary>
    private readonly CffIndex _localSubrs;

    /// <summary>The width added to an explicit width operand.</summary>
    private readonly float _nominalWidth;

    /// <summary>The width when a charstring gives none.</summary>
    private readonly float _defaultWidth;

    /// <summary>Whether the run stops once the width is known.</summary>
    private readonly bool _widthOnly;

    /// <summary>The argument stack.</summary>
    private readonly Span<float> _stack;

    /// <summary>The transient array.</summary>
    private readonly Span<float> _transient;

    /// <summary>The number of arguments on the stack.</summary>
    private int _count;

    /// <summary>The current x.</summary>
    private float _x;

    /// <summary>The current y.</summary>
    private float _y;

    /// <summary>The number of stem hints declared.</summary>
    private int _stems;

    /// <summary>Whether the width has been read.</summary>
    private bool _widthParsed;

    /// <summary>Whether a contour is open.</summary>
    private bool _open;

    /// <summary>Whether the run has finished.</summary>
    private bool _done;

    /// <summary>Initializes a new instance of the <see cref="Type2Interpreter"/> struct.</summary>
    /// <param name="data">The CFF data.</param>
    /// <param name="globalSubrs">The global subroutines.</param>
    /// <param name="widths">The local subroutines and the default and nominal widths.</param>
    /// <param name="widthOnly">Whether to stop once the width is known.</param>
    /// <param name="scratch">Space for the stack and transient array, at least <see cref="CharstringLimits.ScratchSize"/> values.</param>
    internal Type2Interpreter(ReadOnlySpan<byte> data, CffIndex globalSubrs, CffPrivate widths, bool widthOnly, Span<float> scratch)
    {
        _data = data;
        _globalSubrs = globalSubrs;
        _localSubrs = widths.Subrs;
        _stack = scratch[..CharstringLimits.StackDepth];
        _transient = scratch.Slice(CharstringLimits.StackDepth, CharstringLimits.TransientSize);

        // The scratch comes from a shared pool, and the get operator can read a slot no put wrote: that reads zero, not
        // whatever an earlier glyph or thread left.
        _transient.Clear();
        _nominalWidth = widths.NominalWidth;
        _defaultWidth = widths.DefaultWidth;
        _widthOnly = widthOnly;
        Outcome = new(widths.DefaultWidth, false, 0, 0, 0, 0);
    }

    /// <summary>The outcome of one operator.</summary>
    private enum Flow
    {
        /// <summary>Carry on with the next byte.</summary>
        Continue = 0,

        /// <summary>Return from the current subroutine.</summary>
        Return = 1,

        /// <summary>Stop the whole run.</summary>
        End = 2,
    }

    /// <summary>The operator families of the one-byte operators.</summary>
    private enum Family
    {
        /// <summary>A reserved operator, which clears the stack.</summary>
        Reserved = 0,

        /// <summary>A stem hint operator.</summary>
        Stem = 1,

        /// <summary>A hint or counter mask operator.</summary>
        Mask = 2,

        /// <summary>A moveto operator.</summary>
        Move = 3,

        /// <summary>A line operator.</summary>
        Line = 4,

        /// <summary>A curve operator.</summary>
        Curve = 5,

        /// <summary>A local subroutine call.</summary>
        CallSubr = 6,

        /// <summary>A global subroutine call.</summary>
        CallGlobalSubr = 7,

        /// <summary>A subroutine return.</summary>
        Return = 8,

        /// <summary>The end of the charstring.</summary>
        EndChar = 9,

        /// <summary>A two-byte operator.</summary>
        Escape = 10,
    }

    /// <summary>Gets the width and any accented-character request the run found.</summary>
    internal CharstringOutcome Outcome { get; private set; }

    /// <summary>Gets the family of each one-byte operator.</summary>
    private static ReadOnlySpan<byte> Families =>
    [
        0x00, 0x01, 0x00, 0x01, 0x03, 0x04, 0x04, 0x04, 0x05, 0x00, 0x06, 0x08, 0x0A, 0x00, 0x09, 0x00,
        0x00, 0x00, 0x01, 0x02, 0x02, 0x03, 0x03, 0x01, 0x05, 0x05, 0x05, 0x05, 0x00, 0x07, 0x05, 0x05,
    ];

    /// <summary>Gets the subroutine bias for a subroutine count.</summary>
    /// <param name="count">The count.</param>
    /// <returns>The bias.</returns>
    internal static int Bias(int count) => count switch
    {
        < SmallBiasLimit => SmallBias,
        < MediumBiasLimit => MediumBias,
        _ => LargeBias,
    };

    /// <summary>Runs a charstring from a starting point.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="charstring">The charstring.</param>
    /// <param name="startX">The starting x, used to place an accent.</param>
    /// <param name="startY">The starting y.</param>
    /// <param name="sink">The sink.</param>
    internal void Run<TSink>(ReadOnlySpan<byte> charstring, float startX, float startY, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        _x = startX;
        _y = startY;
        _ = Execute(charstring, 0, ref sink);
        ClosePath(ref sink);
    }

    /// <summary>Executes a charstring or subroutine.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="code">The bytes.</param>
    /// <param name="depth">The subroutine depth.</param>
    /// <param name="sink">The sink.</param>
    /// <returns>How the caller continues.</returns>
    private Flow Execute<TSink>(ReadOnlySpan<byte> code, int depth, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var position = 0;
        while (position < code.Length)
        {
            int b = code[position];
            if (b >= FirstOperandByte || b == ShortInt)
            {
                position = ReadNumber(code, position);
                continue;
            }

            position++;
            var family = (Family)Families[b];
            var flow = family <= Family.Curve
                ? RunPathOperator(family, b, code, ref position, ref sink)
                : RunFlowOperator(family, code, ref position, depth, ref sink);
            if (flow != Flow.Continue || _done)
            {
                return _done ? Flow.End : flow;
            }
        }

        return Flow.Continue;
    }

    /// <summary>Runs a hint or path operator.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="family">The operator family.</param>
    /// <param name="op">The operator.</param>
    /// <param name="code">The bytes.</param>
    /// <param name="position">The read position, after the operator.</param>
    /// <param name="sink">The sink.</param>
    /// <returns>Always <see cref="Flow.Continue"/>.</returns>
    private Flow RunPathOperator<TSink>(Family family, int op, ReadOnlySpan<byte> code, ref int position, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        switch (family)
        {
            case Family.Stem:
            {
                AddStems();
                break;
            }

            case Family.Mask:
            {
                AddStems();
                position = Math.Min(code.Length, position + ((_stems + StemsPerMaskByte - 1) / StemsPerMaskByte));
                break;
            }

            case Family.Move:
            {
                Move(op, ref sink);
                break;
            }

            case Family.Line:
            {
                Lines(op, ref sink);
                break;
            }

            case Family.Curve:
            {
                Curves(op, ref sink);
                break;
            }

            default:
            {
                _count = 0;
                break;
            }
        }

        return Flow.Continue;
    }

    /// <summary>Runs a subroutine, return, endchar or two-byte operator.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="family">The operator family.</param>
    /// <param name="code">The bytes.</param>
    /// <param name="position">The read position, after the operator.</param>
    /// <param name="depth">The subroutine depth.</param>
    /// <param name="sink">The sink.</param>
    /// <returns>How the caller continues.</returns>
    private Flow RunFlowOperator<TSink>(Family family, ReadOnlySpan<byte> code, ref int position, int depth, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        switch (family)
        {
            case Family.CallSubr:
            {
                return CallSubroutine(_localSubrs, depth, ref sink);
            }

            case Family.CallGlobalSubr:
            {
                return CallSubroutine(_globalSubrs, depth, ref sink);
            }

            case Family.Return:
            {
                return Flow.Return;
            }

            case Family.EndChar:
            {
                EndChar();
                return Flow.End;
            }

            default:
            {
                var op = FontBytes.U8(code, position);
                position++;
                RunEscape(op, ref sink);
                return Flow.Continue;
            }
        }
    }

    /// <summary>Calls a subroutine numbered by the top of the stack.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="subrs">The subroutines.</param>
    /// <param name="depth">The current depth.</param>
    /// <param name="sink">The sink.</param>
    /// <returns><see cref="Flow.End"/> when the subroutine ended the charstring.</returns>
    private Flow CallSubroutine<TSink>(CffIndex subrs, int depth, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        if (_count == 0 || depth >= CharstringLimits.MaxSubrDepth)
        {
            _count = 0;
            return Flow.Continue;
        }

        _count--;
        var index = (int)_stack[_count] + Bias(subrs.Count);
        var subr = subrs.Get(_data, index);
        if (subr.IsEmpty)
        {
            return Flow.Continue;
        }

        return Execute(subr, depth + 1, ref sink) == Flow.End ? Flow.End : Flow.Continue;
    }

    /// <summary>Handles endchar, including the accented-character form.</summary>
    private void EndChar()
    {
        TakeWidth(_count is 1 or SeacArguments + 1);
        if (_count >= SeacArguments)
        {
            Outcome = Outcome with
            {
                HasSeac = true,
                AccentX = _stack[0],
                AccentY = _stack[1],
                BaseCode = (int)_stack[Third],
                AccentCode = (int)_stack[SeacArguments - 1],
            };
        }

        _count = 0;
    }

    /// <summary>Reads an operand and pushes it.</summary>
    /// <param name="code">The bytes.</param>
    /// <param name="position">The operand's first byte.</param>
    /// <returns>The position after the operand.</returns>
    private int ReadNumber(ReadOnlySpan<byte> code, int position)
    {
        int b = code[position];
        float value;
        int size;
        if (b == ShortInt)
        {
            value = FontBytes.S16(code, position + 1);
            size = 1 + FontBytes.U16Size;
        }
        else if (b <= SmallIntEnd)
        {
            value = b - SmallIntBias;
            size = 1;
        }
        else if (b == Fixed)
        {
            value = (int)FontBytes.U32(code, position + 1) / FixedScale;
            size = 1 + FixedSize;
        }
        else
        {
            var next = FontBytes.U8(code, position + 1);
            value = b < NegativeStart ? ((b - PositiveStart) << ByteBits) + next + TwoByteBias : -((b - NegativeStart) << ByteBits) - next - TwoByteBias;
            size = FontBytes.U16Size;
        }

        Push(value);
        return position + size;
    }

    /// <summary>Pushes a value, dropping it when the stack is full.</summary>
    /// <param name="value">The value.</param>
    private void Push(float value)
    {
        if (_count >= CharstringLimits.StackDepth)
        {
            return;
        }

        _stack[_count] = value;
        _count++;
    }

    /// <summary>Reads the width the first stack-clearing operator may carry.</summary>
    /// <param name="hasExtra">Whether the stack has one more argument than the operator takes.</param>
    private void TakeWidth(bool hasExtra)
    {
        var parsed = _widthParsed;
        _widthParsed = true;
        if (parsed)
        {
            return;
        }

        if (hasExtra && _count > 0)
        {
            Outcome = Outcome with { Width = _nominalWidth + _stack[0] };
            _stack[1.._count].CopyTo(_stack);
            _count--;
        }
        else
        {
            Outcome = Outcome with { Width = _defaultWidth };
        }

        _done = _widthOnly;
    }

    /// <summary>Counts stem hints, including the implied vstems before a hint mask.</summary>
    private void AddStems()
    {
        TakeWidth((_count & 1) != 0);
        _stems += _count / LineArguments;
        _count = 0;
    }
}
