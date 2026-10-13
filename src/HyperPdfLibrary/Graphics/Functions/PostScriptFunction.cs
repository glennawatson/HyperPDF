// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Functions;

/// <summary>
/// A type 4 PostScript calculator function. The program is compiled once; each evaluation runs the instructions on a
/// fixed stack of <see cref="StackLimit"/> operands held on the call stack. A program that raises a PostScript error
/// (ISO 32000-2 7.10.5: stack underflow or overflow, an index out of range, an undefined result) or that leaves fewer
/// results than the function has outputs fails. The standard does not say what a failed function produces, so each output
/// then takes the low end of its range.
/// </summary>
internal sealed class PostScriptFunction : PdfFunction
{
    /// <summary>The operand stack limit, from PDF 32000 Annex C.</summary>
    private const int StackLimit = 100;

    /// <summary>The compiled program.</summary>
    private readonly PostScriptInstruction[] _code;

    /// <summary>Initializes a new instance of the <see cref="PostScriptFunction"/> class.</summary>
    /// <param name="domain">The input domain.</param>
    /// <param name="range">The output range.</param>
    /// <param name="code">The compiled program.</param>
    private PostScriptFunction(float[] domain, float[] range, PostScriptInstruction[] code)
        : base(domain, range, range.Length / FunctionReader.PairSize) => _code = code;

    /// <summary>Parses a PostScript calculator function.</summary>
    /// <param name="stream">The function stream holding the program.</param>
    /// <param name="domain">The input domain.</param>
    /// <param name="range">The output range, required.</param>
    /// <returns>The function, or <see langword="null"/> when invalid.</returns>
    internal static PostScriptFunction? Parse(PdfStream? stream, float[] domain, float[]? range)
    {
        if (stream is null || range is null)
        {
            return null;
        }

        var data = default(PooledBuffer);
        try
        {
            _ = stream.Decode(ref data);
            var code = PostScriptCompiler.Compile(data.WrittenSpan);
            return code is null ? null : new(domain, range, code);
        }
        finally
        {
            data.Dispose();
        }
    }

    /// <inheritdoc/>
    [SkipLocalsInit]
    private protected override void EvaluateCore(ReadOnlySpan<float> input, Span<float> output)
    {
        Span<PostScriptValue> storage = stackalloc PostScriptValue[StackLimit];
        var stack = new PostScriptStack(storage);
        foreach (var value in input)
        {
            stack.Push(PostScriptValue.Real(value));
        }

        Run(_code, ref stack);
        var results = stack.AsSpan();
        if (stack.Failed || results.Length < output.Length)
        {
            // Clamping minus infinity to the range gives the low end of each output.
            output.Fill(float.NegativeInfinity);
            return;
        }

        results = results[^output.Length..];
        for (var i = 0; i < output.Length; i++)
        {
            output[i] = (float)results[i].Number;
        }
    }

    /// <summary>Runs a program until it ends or fails.</summary>
    /// <param name="code">The compiled program.</param>
    /// <param name="stack">The operand stack.</param>
    private static void Run(PostScriptInstruction[] code, ref PostScriptStack stack)
    {
        var counter = 0;
        while ((uint)counter < (uint)code.Length && !stack.Failed)
        {
            var instruction = code[counter];
            counter = instruction.Code switch
            {
                PostScriptOpCode.Push => Push(ref stack, instruction.Operand, counter),
                PostScriptOpCode.Jump => instruction.Target,
                PostScriptOpCode.JumpIfFalse => stack.Pop().Number is 0 ? instruction.Target : counter + 1,
                _ => Operate(ref stack, instruction.Code, counter),
            };
        }
    }

    /// <summary>Pushes an operand.</summary>
    /// <param name="stack">The operand stack.</param>
    /// <param name="value">The operand.</param>
    /// <param name="counter">The current instruction.</param>
    /// <returns>The next instruction.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Push(ref PostScriptStack stack, PostScriptValue value, int counter)
    {
        stack.Push(value);
        return counter + 1;
    }

    /// <summary>Runs an operator.</summary>
    /// <param name="stack">The operand stack.</param>
    /// <param name="code">The operator.</param>
    /// <param name="counter">The current instruction.</param>
    /// <returns>The next instruction.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Operate(ref PostScriptStack stack, PostScriptOpCode code, int counter)
    {
        PostScriptOperators.Run(code, ref stack);
        return counter + 1;
    }
}
