// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Collects the lexical rules one object breaks while it is read. They count only once the whole object reads, because
/// veraPDF skips an object it cannot parse.
/// </summary>
[DebuggerDisplay("PdfAObjectFindings: {_pending.Count} pending")]
internal sealed class PdfAObjectFindings
{
    /// <summary>The largest integer: 2,147,483,647 (ISO 32000-1, Annex C).</summary>
    private const long MaxInteger = int.MaxValue;

    /// <summary>The smallest integer: -2,147,483,648.</summary>
    private const long MinInteger = int.MinValue;

    /// <summary>The largest real magnitude in PDF/A-2 and later: 3.403 x 10^38.</summary>
    private const double MaxReal = 3.403e+38;

    /// <summary>The largest real magnitude in PDF/A-1: 32,767.</summary>
    private const double MaxRealPdfA1 = 32_767.0;

    /// <summary>The smallest non-zero real magnitude: 1.175 x 10^-38.</summary>
    private const double MinRealMagnitude = 1.175e-38;

    /// <summary>The longest name in bytes.</summary>
    private const int MaxNameBytes = 127;

    /// <summary>The longest string in PDF/A-2 and later: 32,767.</summary>
    private const int MaxString = 32_767;

    /// <summary>The longest string in PDF/A-1: 65,535.</summary>
    private const int MaxStringPdfA1 = 65_535;

    /// <summary>The most array elements in PDF/A-1.</summary>
    private const int MaxArray = 8191;

    /// <summary>The most dictionary entries in PDF/A-1.</summary>
    private const int MaxDictionary = 4095;

    /// <summary>The digits in each byte of a hexadecimal string.</summary>
    private const int HexPair = 2;

    /// <summary>The rules broken so far.</summary>
    private readonly List<PdfARule> _pending = [];

    /// <summary>Gets the number of rules pending.</summary>
    internal int Count => _pending.Count;

    /// <summary>Copies some pending rules to another collector.</summary>
    /// <param name="target">The collector.</param>
    /// <param name="start">The first rule.</param>
    /// <param name="count">The number of rules.</param>
    /// <param name="numbersOnly">Whether to copy only the number limits, for operands read as numbers.</param>
    internal void CopyTo(PdfAObjectFindings target, int start, int count, bool numbersOnly)
    {
        for (var i = start; i < start + count; i++)
        {
            var rule = _pending[i];
            if (!numbersOnly || rule is PdfARule.IntegerRange or PdfARule.RealRange or PdfARule.RealRangePdfA1 or PdfARule.RealMagnitude)
            {
                target._pending.Add(rule);
            }
        }
    }

    /// <summary>Records a broken rule when a condition does not hold.</summary>
    /// <param name="holds">Whether the rule holds.</param>
    /// <param name="rule">The rule.</param>
    internal void Require(bool holds, PdfARule rule)
    {
        if (!holds)
        {
            _pending.Add(rule);
        }
    }

    /// <summary>Checks an integer against the implementation limits.</summary>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CheckInteger(long value) => Require(value is >= MinInteger and <= MaxInteger, PdfARule.IntegerRange);

    /// <summary>Checks a real number against the implementation limits.</summary>
    /// <param name="value">The value.</param>
    internal void CheckReal(double value)
    {
        Require(value is >= -MaxReal and <= MaxReal, PdfARule.RealRange);
        Require(value is >= -MaxRealPdfA1 and <= MaxRealPdfA1, PdfARule.RealRangePdfA1);
        Require(value == 0 || value <= -MinRealMagnitude || value >= MinRealMagnitude, PdfARule.RealMagnitude);
    }

    /// <summary>Checks a name's decoded length.</summary>
    /// <param name="length">The length in bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CheckName(int length) => Require(length <= MaxNameBytes, PdfARule.NameLength);

    /// <summary>Checks a string's length as veraPDF measures it: UTF-16 characters of the bytes read as UTF-8.</summary>
    /// <param name="value">The decoded bytes.</param>
    internal void CheckString(ReadOnlySpan<byte> value)
    {
        // Fewer bytes than the limit always decode to fewer characters, so only long strings are decoded.
        var length = value.Length > MaxString ? Encoding.UTF8.GetCharCount(value) : value.Length;
        Require(length <= MaxString, PdfARule.StringLength);
        Require(length <= MaxStringPdfA1, PdfARule.StringLengthPdfA1);
    }

    /// <summary>Checks a hexadecimal string's digits.</summary>
    /// <param name="onlyHex">Whether every byte other than white space is a digit.</param>
    /// <param name="count">The number of bytes other than white space.</param>
    internal void CheckHex(bool onlyHex, long count)
    {
        Require(onlyHex, PdfARule.HexStringCharacters);
        Require(count % HexPair == 0, PdfARule.HexStringEvenDigits);
    }

    /// <summary>Checks an array's size.</summary>
    /// <param name="count">The number of elements.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CheckArray(int count) => Require(count <= MaxArray, PdfARule.ArraySizePdfA1);

    /// <summary>Checks a dictionary's size.</summary>
    /// <param name="count">The number of entries with a value other than null.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CheckDictionary(int count) => Require(count <= MaxDictionary, PdfARule.DictionarySizePdfA1);

    /// <summary>Records the pending rules as failures of the validation.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="where">Where the object is.</param>
    internal void Commit(PdfAValidationContext context, string? where)
    {
        foreach (var rule in _pending)
        {
            context.Fail(rule, where);
        }

        _pending.Clear();
    }

    /// <summary>Records the pending rules of an indirect object as failures of the validation.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="number">The object number.</param>
    internal void CommitObject(PdfAValidationContext context, int number)
    {
        if (_pending.Count > 0)
        {
            Commit(context, PdfAContexts.Object(number));
        }
    }

    /// <summary>Drops the pending rules, as the object could not be read.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Discard() => _pending.Clear();
}
