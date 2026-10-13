// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Graphics.Functions;

/// <summary>
/// The PostScript calculator operators of PDF 32000 section 7.10.5, looked up by table. A grouped switch was measured and
/// ran about 8% slower on an operator-heavy program than this delegate table, so the table stays. An operator that the
/// standard makes an error (stack underflow or overflow, an index out of range, or an undefined result such as a division
/// by zero) marks the stack as failed instead of throwing. Operands of any type convert to numbers: a boolean is 1 or 0.
/// Angles are in degrees, as in PostScript.
/// </summary>
internal static class PostScriptOperators
{
    /// <summary>The op code of the first operator in the tables.</summary>
    internal const int FirstOperator = (int)PostScriptOpCode.Abs;

    /// <summary>Degrees in a half turn.</summary>
    private const double HalfTurn = 180;

    /// <summary>Degrees in a full turn.</summary>
    private const double FullTurn = 360;

    /// <summary>The offset that turns rounding towards zero into rounding half up.</summary>
    private const double Half = 0.5;

    /// <summary>The operands <c>exch</c> swaps.</summary>
    private const int ExchangeCount = 2;

    /// <summary>The widest shift applied by <c>bitshift</c>.</summary>
    private const int MaxShift = 31;

    /// <summary>The operator names, in op code order from <see cref="FirstOperator"/>.</summary>
    private static readonly string[] Names =
    [
        "abs", "add", "atan", "ceiling", "cos", "cvi", "cvr", "div", "exp", "floor", "idiv", "ln", "log", "mod", "mul", "neg",
        "round", "sin", "sqrt", "sub", "truncate", "and", "bitshift", "eq", "false", "ge", "gt", "le", "lt", "ne", "not", "or",
        "true", "xor", "copy", "dup", "exch", "index", "pop", "roll",
    ];

    /// <summary>The operator implementations, in op code order from <see cref="FirstOperator"/>.</summary>
    private static readonly PostScriptOperator[] Handlers =
    [
        Abs, Add, Atan, Ceiling, Cos, Cvi, Cvr, Div, Exp, Floor, Idiv, Ln, Log, Mod, Mul, Neg,
        Round, Sin, Sqrt, Sub, Truncate, And, Bitshift, Eq, False, Ge, Gt, Le, Lt, Ne, Not, Or,
        True, Xor, Copy, Dup, Exch, Index, Pop, Roll,
    ];

    /// <summary>Finds an operator by name.</summary>
    /// <param name="name">The operator name.</param>
    /// <param name="code">The op code.</param>
    /// <returns><see langword="true"/> when the name is an operator.</returns>
    internal static bool TryGetOpCode(ReadOnlySpan<byte> name, out PostScriptOpCode code)
    {
        for (var i = 0; i < Names.Length; i++)
        {
            if (!Ascii.Equals(name, Names[i]))
            {
                continue;
            }

            code = (PostScriptOpCode)(i + FirstOperator);
            return true;
        }

        code = PostScriptOpCode.Push;
        return false;
    }

    /// <summary>Runs an operator.</summary>
    /// <param name="code">The op code; must be an operator.</param>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Run(PostScriptOpCode code, ref PostScriptStack stack) => Handlers[(int)code - FirstOperator](ref stack);

    /// <summary>Pushes the result of integer-preserving arithmetic.</summary>
    /// <param name="stack">The operand stack.</param>
    /// <param name="result">The result.</param>
    /// <param name="integers">Whether every operand was an integer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PushArithmetic(ref PostScriptStack stack, double result, bool integers) =>
        stack.Push(integers && result >= int.MinValue && result <= int.MaxValue ? PostScriptValue.Integer(result) : PostScriptValue.Real(result));

    /// <summary>Implements <c>abs</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Abs(ref PostScriptStack stack)
    {
        var a = stack.Pop();
        PushArithmetic(ref stack, Math.Abs(a.Number), a.IsInteger);
    }

    /// <summary>Implements <c>neg</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Neg(ref PostScriptStack stack)
    {
        var a = stack.Pop();
        PushArithmetic(ref stack, -a.Number, a.IsInteger);
    }

    /// <summary>Implements <c>add</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Add(ref PostScriptStack stack)
    {
        var b = stack.Pop();
        var a = stack.Pop();
        PushArithmetic(ref stack, a.Number + b.Number, a.IsInteger && b.IsInteger);
    }

    /// <summary>Implements <c>sub</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Sub(ref PostScriptStack stack)
    {
        var b = stack.Pop();
        var a = stack.Pop();
        PushArithmetic(ref stack, a.Number - b.Number, a.IsInteger && b.IsInteger);
    }

    /// <summary>Implements <c>mul</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Mul(ref PostScriptStack stack)
    {
        var b = stack.Pop();
        var a = stack.Pop();
        PushArithmetic(ref stack, a.Number * b.Number, a.IsInteger && b.IsInteger);
    }

    /// <summary>Pushes a real result; a result that is not a finite number is an undefinedresult error.</summary>
    /// <param name="stack">The operand stack.</param>
    /// <param name="result">The result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PushReal(ref PostScriptStack stack, double result)
    {
        if (!double.IsFinite(result))
        {
            stack.Fail();
            return;
        }

        stack.Push(PostScriptValue.Real(result));
    }

    /// <summary>Implements <c>div</c>; dividing by zero is an undefinedresult error.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Div(ref PostScriptStack stack)
    {
        var b = stack.Pop();
        var a = stack.Pop();
        PushReal(ref stack, b.Number == 0 ? double.NaN : a.Number / b.Number);
    }

    /// <summary>Implements <c>idiv</c>; a zero divisor or an overflow is an undefinedresult error.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Idiv(ref PostScriptStack stack)
    {
        var b = stack.PopInteger();
        var a = stack.PopInteger();
        if (b == 0 || (a == int.MinValue && b == -1))
        {
            stack.Fail();
            return;
        }

        stack.Push(PostScriptValue.Integer(Math.DivRem(a, b, out _)));
    }

    /// <summary>Implements <c>mod</c>; a zero divisor is an undefinedresult error.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Mod(ref PostScriptStack stack)
    {
        var b = stack.PopInteger();
        var a = stack.PopInteger();
        if (b == 0)
        {
            stack.Fail();
            return;
        }

        stack.Push(PostScriptValue.Integer(b == -1 ? 0 : a % b));
    }

    /// <summary>Implements <c>atan</c>: the angle of a vector in degrees, from 0 to 360; both operands zero is an undefinedresult error.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Atan(ref PostScriptStack stack)
    {
        var denominator = stack.Pop().Number;
        var numerator = stack.Pop().Number;
        if (numerator == 0 && denominator == 0)
        {
            stack.Fail();
            return;
        }

        var degrees = Math.Atan2(numerator, denominator) * HalfTurn / Math.PI;
        stack.Push(PostScriptValue.Real(degrees < 0 ? degrees + FullTurn : degrees));
    }

    /// <summary>Implements <c>cos</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Cos(ref PostScriptStack stack) => stack.Push(PostScriptValue.Real(Math.Cos(stack.Pop().Number * Math.PI / HalfTurn)));

    /// <summary>Implements <c>sin</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Sin(ref PostScriptStack stack) => stack.Push(PostScriptValue.Real(Math.Sin(stack.Pop().Number * Math.PI / HalfTurn)));

    /// <summary>Implements <c>ceiling</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Ceiling(ref PostScriptStack stack)
    {
        var a = stack.Pop();
        stack.Push(a with { Number = Math.Ceiling(a.Number) });
    }

    /// <summary>Implements <c>floor</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Floor(ref PostScriptStack stack)
    {
        var a = stack.Pop();
        stack.Push(a with { Number = Math.Floor(a.Number) });
    }

    /// <summary>Implements <c>round</c>, which rounds halves up.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Round(ref PostScriptStack stack)
    {
        var a = stack.Pop();
        stack.Push(a with { Number = Math.Floor(a.Number + Half) });
    }

    /// <summary>Implements <c>truncate</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Truncate(ref PostScriptStack stack)
    {
        var a = stack.Pop();
        stack.Push(a with { Number = Math.Truncate(a.Number) });
    }

    /// <summary>Implements <c>cvi</c>; out-of-range values saturate.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Cvi(ref PostScriptStack stack) => stack.Push(PostScriptValue.Integer(stack.PopInteger()));

    /// <summary>Implements <c>cvr</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Cvr(ref PostScriptStack stack) => stack.Push(PostScriptValue.Real(stack.Pop().Number));

    /// <summary>Implements <c>exp</c>; a result that is not a real number is an undefinedresult error.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Exp(ref PostScriptStack stack)
    {
        var exponent = stack.Pop().Number;
        var number = stack.Pop().Number;
        PushReal(ref stack, Math.Pow(number, exponent));
    }

    /// <summary>Implements <c>ln</c>; a value that is not positive is an undefinedresult error.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Ln(ref PostScriptStack stack) => PushReal(ref stack, Math.Log(stack.Pop().Number));

    /// <summary>Implements <c>log</c>; a value that is not positive is an undefinedresult error.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Log(ref PostScriptStack stack) => PushReal(ref stack, Math.Log10(stack.Pop().Number));

    /// <summary>Implements <c>sqrt</c>; a negative value is a rangecheck error.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Sqrt(ref PostScriptStack stack) => PushReal(ref stack, Math.Sqrt(stack.Pop().Number));

    /// <summary>Implements <c>and</c>: logical for booleans, bitwise for integers.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void And(ref PostScriptStack stack)
    {
        var b = stack.Pop();
        var a = stack.Pop();
        PushBitwise(ref stack, a, b, PostScriptStack.Saturate(a.Number) & PostScriptStack.Saturate(b.Number));
    }

    /// <summary>Implements <c>or</c>: logical for booleans, bitwise for integers.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Or(ref PostScriptStack stack)
    {
        var b = stack.Pop();
        var a = stack.Pop();
        PushBitwise(ref stack, a, b, PostScriptStack.Saturate(a.Number) | PostScriptStack.Saturate(b.Number));
    }

    /// <summary>Implements <c>xor</c>: logical for booleans, bitwise for integers.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Xor(ref PostScriptStack stack)
    {
        var b = stack.Pop();
        var a = stack.Pop();
        PushBitwise(ref stack, a, b, PostScriptStack.Saturate(a.Number) ^ PostScriptStack.Saturate(b.Number));
    }

    /// <summary>Pushes a bitwise result: a boolean when both operands are booleans, otherwise an integer.</summary>
    /// <param name="stack">The operand stack.</param>
    /// <param name="a">The first operand.</param>
    /// <param name="b">The second operand.</param>
    /// <param name="result">The bitwise result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PushBitwise(ref PostScriptStack stack, PostScriptValue a, PostScriptValue b, int result) =>
        stack.Push(a.IsBoolean && b.IsBoolean ? PostScriptValue.Boolean(result != 0) : PostScriptValue.Integer(result));

    /// <summary>Implements <c>not</c>: logical for booleans, bitwise for integers.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Not(ref PostScriptStack stack)
    {
        var a = stack.Pop();
        stack.Push(a.IsBoolean ? PostScriptValue.Boolean(a.Number == 0) : PostScriptValue.Integer(~PostScriptStack.Saturate(a.Number)));
    }

    /// <summary>Implements <c>bitshift</c>: left for a positive shift, right for a negative one.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Bitshift(ref PostScriptStack stack)
    {
        var shift = stack.PopInteger();
        var value = stack.PopInteger();
        var amount = (int)Math.Min(Math.Abs((long)shift), MaxShift);
        stack.Push(PostScriptValue.Integer(shift >= 0 ? value << amount : value >> amount));
    }

    /// <summary>Implements <c>eq</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Eq(ref PostScriptStack stack)
    {
        var b = stack.Pop();
        var a = stack.Pop();
        stack.Push(PostScriptValue.Boolean((a.Number - b.Number) is 0 && a.IsBoolean == b.IsBoolean));
    }

    /// <summary>Implements <c>ne</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Ne(ref PostScriptStack stack)
    {
        var b = stack.Pop();
        var a = stack.Pop();
        stack.Push(PostScriptValue.Boolean((a.Number - b.Number) is not 0 || a.IsBoolean != b.IsBoolean));
    }

    /// <summary>Implements <c>ge</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Ge(ref PostScriptStack stack)
    {
        var b = stack.Pop().Number;
        stack.Push(PostScriptValue.Boolean(stack.Pop().Number >= b));
    }

    /// <summary>Implements <c>gt</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Gt(ref PostScriptStack stack)
    {
        var b = stack.Pop().Number;
        stack.Push(PostScriptValue.Boolean(stack.Pop().Number > b));
    }

    /// <summary>Implements <c>le</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Le(ref PostScriptStack stack)
    {
        var b = stack.Pop().Number;
        stack.Push(PostScriptValue.Boolean(stack.Pop().Number <= b));
    }

    /// <summary>Implements <c>lt</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Lt(ref PostScriptStack stack)
    {
        var b = stack.Pop().Number;
        stack.Push(PostScriptValue.Boolean(stack.Pop().Number < b));
    }

    /// <summary>Implements <c>true</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void True(ref PostScriptStack stack) => stack.Push(PostScriptValue.Boolean(true));

    /// <summary>Implements <c>false</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void False(ref PostScriptStack stack) => stack.Push(PostScriptValue.Boolean(false));

    /// <summary>Implements <c>copy</c>; a count that does not fit is an error.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Copy(ref PostScriptStack stack) => stack.Copy(stack.PopInteger());

    /// <summary>Implements <c>pop</c>.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Pop(ref PostScriptStack stack) => _ = stack.Pop();

    /// <summary>Implements <c>roll</c>; a count that does not fit is an error.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Roll(ref PostScriptStack stack)
    {
        var shift = stack.PopInteger();
        stack.Roll(stack.PopInteger(), shift);
    }

    /// <summary>Implements <c>dup</c>; an empty stack is an error.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Dup(ref PostScriptStack stack) => stack.Copy(1);

    /// <summary>Implements <c>exch</c>; a stack with fewer than two operands is an error.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Exch(ref PostScriptStack stack) => stack.Roll(ExchangeCount, 1);

    /// <summary>Implements <c>index</c>; an index outside the stack is an error.</summary>
    /// <param name="stack">The operand stack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Index(ref PostScriptStack stack)
    {
        var depth = stack.PopInteger();
        if ((uint)depth >= (uint)stack.Count)
        {
            stack.Fail();
            return;
        }

        stack.Push(stack.Peek(depth));
    }
}
