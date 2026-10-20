// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>The binary operators of a form script, with JavaScript's rules for mixing numbers and text.</summary>
internal static class SandboxOperators
{
    /// <summary>Applies an operator.</summary>
    /// <param name="symbol">The operator's symbol.</param>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The result.</returns>
    /// <exception cref="SandboxException">The operator is not one the sandbox has.</exception>
    internal static SandboxValue Apply(string symbol, SandboxValue left, SandboxValue right) => symbol switch
    {
        "+" => Add(left, right),
        "-" => SandboxValue.Of(left.ToNumber() - right.ToNumber()),
        "*" => SandboxValue.Of(left.ToNumber() * right.ToNumber()),
        "/" => SandboxValue.Of(left.ToNumber() / right.ToNumber()),
        "%" => SandboxValue.Of(left.ToNumber() % right.ToNumber()),
        "<" => SandboxValue.Of(Order(left, right) is { } order && order < 0),
        ">" => SandboxValue.Of(Order(left, right) is { } order && order > 0),
        "<=" => SandboxValue.Of(Order(left, right) is { } order && order <= 0),
        ">=" => SandboxValue.Of(Order(left, right) is { } order && order >= 0),
        "==" => SandboxValue.Of(LooseEquals(left, right)),
        "!=" => SandboxValue.Of(!LooseEquals(left, right)),
        "===" => SandboxValue.Of(StrictEquals(left, right)),
        "!==" => SandboxValue.Of(!StrictEquals(left, right)),
        _ => throw new SandboxException($"The script uses the operator '{symbol}', which form scripts cannot use."),
    };

    /// <summary>Compares two values for strict equality: same kind and same value.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when they are strictly equal.</returns>
    internal static bool StrictEquals(SandboxValue left, SandboxValue right) =>
        left.Kind == right.Kind && left.Kind switch
        {
            SandboxValueKind.Number or SandboxValueKind.Boolean => SandboxValue.SameNumber(left.Number, right.Number),
            SandboxValueKind.String => string.Equals((string)left.Reference!, (string)right.Reference!, StringComparison.Ordinal),
            SandboxValueKind.Object => ReferenceEquals(left.Reference, right.Reference),
            _ => true,
        };

    /// <summary>Compares two values for loose equality: null equals undefined, and other mixes compare as numbers.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when they are loosely equal.</returns>
    internal static bool LooseEquals(SandboxValue left, SandboxValue right)
    {
        var leftEmpty = left.Kind is SandboxValueKind.Null or SandboxValueKind.Undefined;
        var rightEmpty = right.Kind is SandboxValueKind.Null or SandboxValueKind.Undefined;
        return left.Kind == right.Kind || leftEmpty || rightEmpty
            ? (leftEmpty && rightEmpty) || StrictEquals(left, right)
            : left.Kind != SandboxValueKind.Object && right.Kind != SandboxValueKind.Object && SandboxValue.SameNumber(left.ToNumber(), right.ToNumber());
    }

    /// <summary>Adds numbers, or joins text when either side is text.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The sum or the joined text.</returns>
    private static SandboxValue Add(SandboxValue left, SandboxValue right) =>
        left.Kind is SandboxValueKind.String or SandboxValueKind.Object || right.Kind is SandboxValueKind.String or SandboxValueKind.Object
            ? SandboxValue.Of(string.Concat(left.ToText(), right.ToText()))
            : SandboxValue.Of(left.ToNumber() + right.ToNumber());

    /// <summary>Orders two values: as text when both are text, otherwise as numbers.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>Negative, zero or positive; <see langword="null"/> when a number is not a number, which never compares true.</returns>
    private static int? Order(SandboxValue left, SandboxValue right)
    {
        if (left.IsText && right.IsText)
        {
            return string.CompareOrdinal((string)left.Reference!, (string)right.Reference!);
        }

        var a = left.ToNumber();
        var b = right.ToNumber();
        return double.IsNaN(a) || double.IsNaN(b) ? null : a.CompareTo(b);
    }
}
