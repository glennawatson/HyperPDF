// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Forms.Scripting;

namespace HyperPdf.Benchmarks;

/// <summary>Parses and formats the numbers typed into and shown by form fields, which happens on every keystroke of a number field.</summary>
public class HyperPdfFormNumbersBenchmarks
{
    /// <summary>The value formatted.</summary>
    private const double Amount = 1234567.891;

    /// <summary>The decimals shown.</summary>
    private const int Decimals = 2;

    /// <summary>The thousands separator style with a comma.</summary>
    private const int CommaStyle = 0;

    /// <summary>The negative style with a minus sign.</summary>
    private const int MinusStyle = 0;

    /// <summary>Parses a typed amount with a currency sign and thousands separators.</summary>
    /// <returns>The value.</returns>
    [Benchmark]
    public double ParseTypedAmount() => FormNumbers.TryParse("$1,234,567.89", out var value) ? value : 0;

    /// <summary>Formats an amount with separators and a currency sign.</summary>
    /// <returns>The length of the text.</returns>
    [Benchmark]
    public int FormatAmount() => FormNumbers.Format(Amount, Decimals, CommaStyle, MinusStyle, "$", true).Length;

    /// <summary>Writes a value as plain text.</summary>
    /// <returns>The length of the text.</returns>
    [Benchmark]
    public int PlainValue() => FormNumbers.Plain(Amount).Length;
}
