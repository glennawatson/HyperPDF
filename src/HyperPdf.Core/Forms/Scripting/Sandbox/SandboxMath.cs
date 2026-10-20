// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>The <c>Math</c> object of a form script.</summary>
internal sealed class SandboxMath : SandboxObject
{
    /// <summary>Half, added before rounding down so that halves round up as JavaScript's <c>Math.round</c> does.</summary>
    private const double Half = 0.5;

    /// <inheritdoc/>
    internal override SandboxValue Get(string name) => name switch
    {
        "PI" => SandboxValue.Of(Math.PI),
        "E" => SandboxValue.Of(Math.E),
        _ => SandboxValue.Undefined,
    };

    /// <inheritdoc/>
    internal override SandboxValue Call(string name, SandboxValue[] arguments)
    {
        var first = Argument(arguments, 0);
        return name switch
        {
            "abs" => SandboxValue.Of(Math.Abs(first)),
            "floor" => SandboxValue.Of(Math.Floor(first)),
            "ceil" => SandboxValue.Of(Math.Ceiling(first)),
            "sqrt" => SandboxValue.Of(Math.Sqrt(first)),
            "trunc" => SandboxValue.Of(Math.Truncate(first)),
            "round" => SandboxValue.Of(Math.Floor(first + Half)),
            "pow" => SandboxValue.Of(Math.Pow(first, Argument(arguments, 1))),
            "min" => SandboxValue.Of(Fold(arguments, double.PositiveInfinity, Math.Min)),
            "max" => SandboxValue.Of(Fold(arguments, double.NegativeInfinity, Math.Max)),
            _ => base.Call($"Math.{name}", arguments),
        };
    }

    /// <summary>Gets an argument as a number.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="index">The position.</param>
    /// <returns>The number; not a number when missing.</returns>
    private static double Argument(SandboxValue[] arguments, int index) => index < arguments.Length ? arguments[index].ToNumber() : double.NaN;

    /// <summary>Combines all arguments with a function, giving not a number when any is not a number.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="start">The result for no arguments.</param>
    /// <param name="combine">Combines two numbers.</param>
    /// <returns>The result.</returns>
    private static double Fold(SandboxValue[] arguments, double start, Func<double, double, double> combine)
    {
        var result = start;
        foreach (var argument in arguments)
        {
            var number = argument.ToNumber();
            result = double.IsNaN(number) || double.IsNaN(result) ? double.NaN : combine(result, number);
        }

        return result;
    }
}
