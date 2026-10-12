// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads PDF values with a <see cref="PdfASyntaxReader"/> and checks each string, name, number, array and dictionary
/// against the PDF/A implementation limits. It follows veraPDF's reading: a dictionary entry whose value is null does not
/// count, and syntax it cannot recover from makes the whole object unreadable.
/// </summary>
internal static class PdfASyntaxValues
{
    /// <summary>The deepest nesting of arrays and dictionaries read.</summary>
    private const int MaxDepth = 256;

    /// <summary>Gets the spelling of the reference keyword.</summary>
    private static ReadOnlySpan<byte> ReferenceWord => "R"u8;

    /// <summary>Reads and checks the next value.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="findings">Receives the broken rules.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The kind of value read.</returns>
    internal static PdfASyntaxValue Read(ref PdfASyntaxReader reader, PdfAObjectFindings findings, int depth)
    {
        reader.Next();
        return FromToken(ref reader, findings, depth);
    }

    /// <summary>Checks the value that starts with the token just read.</summary>
    /// <param name="reader">The reader, on the value's first token.</param>
    /// <param name="findings">Receives the broken rules.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The kind of value read.</returns>
    internal static PdfASyntaxValue FromToken(ref PdfASyntaxReader reader, PdfAObjectFindings findings, int depth)
    {
        switch (reader.Kind)
        {
            case PdfASyntaxToken.Integer:
            {
                return ReadIntegerOrReference(ref reader, findings);
            }

            case PdfASyntaxToken.Real:
            {
                findings.CheckReal(reader.Real);
                return PdfASyntaxValue.Real;
            }

            case PdfASyntaxToken.LiteralString or PdfASyntaxToken.HexString:
            {
                CheckString(ref reader, findings);
                return PdfASyntaxValue.String;
            }

            case PdfASyntaxToken.Name:
            {
                findings.CheckName(reader.Value.Length);
                return PdfASyntaxValue.Name;
            }

            case PdfASyntaxToken.ArrayStart:
            {
                return depth < MaxDepth ? ReadArray(ref reader, findings, depth + 1) : PdfASyntaxValue.Broken;
            }

            case PdfASyntaxToken.DictionaryStart:
            {
                return depth < MaxDepth ? ReadDictionary(ref reader, findings, depth + 1) : PdfASyntaxValue.Broken;
            }

            default:
            {
                return FromWord(ref reader);
            }
        }
    }

    /// <summary>Checks the string just read.</summary>
    /// <param name="reader">The reader, on the string.</param>
    /// <param name="findings">Receives the broken rules.</param>
    internal static void CheckString(ref PdfASyntaxReader reader, PdfAObjectFindings findings)
    {
        findings.CheckString(reader.Value);
        if (reader.Kind == PdfASyntaxToken.HexString)
        {
            findings.CheckHex(reader.OnlyHex, reader.HexCount);
        }
    }

    /// <summary>Reads the entries of a dictionary after its opening brackets.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="findings">Receives the broken rules.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The kind of value read.</returns>
    internal static PdfASyntaxValue ReadDictionary(ref PdfASyntaxReader reader, PdfAObjectFindings findings, int depth)
    {
        var count = 0;
        reader.Next();
        while (reader.Kind == PdfASyntaxToken.Name)
        {
            var keyLength = reader.Value.Length;
            var value = Read(ref reader, findings, depth);
            if (value == PdfASyntaxValue.Broken)
            {
                return value;
            }

            // veraPDF drops entries whose value is null, so neither the entry nor its key is checked.
            if (value is not (PdfASyntaxValue.Empty or PdfASyntaxValue.Null))
            {
                count++;
                findings.CheckName(keyLength);
            }

            reader.Next();
        }

        if (reader.Kind != PdfASyntaxToken.DictionaryEnd)
        {
            return PdfASyntaxValue.Broken;
        }

        findings.CheckDictionary(count);
        return PdfASyntaxValue.Dictionary;
    }

    /// <summary>Reads an integer, or an indirect reference when two integers and R follow.</summary>
    /// <param name="reader">The reader, on the integer.</param>
    /// <param name="findings">Receives the broken rules.</param>
    /// <returns>The kind of value read.</returns>
    private static PdfASyntaxValue ReadIntegerOrReference(ref PdfASyntaxReader reader, PdfAObjectFindings findings)
    {
        var value = reader.Integer;
        var after = reader.Position;
        reader.Next();
        if (reader.Kind == PdfASyntaxToken.Integer)
        {
            reader.Next();
            if (reader.IsWord(ReferenceWord))
            {
                return PdfASyntaxValue.Reference;
            }
        }

        reader.Position = after;
        findings.CheckInteger(value);
        return PdfASyntaxValue.Integer;
    }

    /// <summary>Reads the elements of an array after its opening bracket.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="findings">Receives the broken rules.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The kind of value read.</returns>
    private static PdfASyntaxValue ReadArray(ref PdfASyntaxReader reader, PdfAObjectFindings findings, int depth)
    {
        var count = 0;
        while (true)
        {
            var value = Read(ref reader, findings, depth);
            if (value == PdfASyntaxValue.Broken)
            {
                return value;
            }

            if (value == PdfASyntaxValue.Empty)
            {
                break;
            }

            count++;
        }

        if (reader.Kind != PdfASyntaxToken.ArrayEnd)
        {
            return PdfASyntaxValue.Broken;
        }

        findings.CheckArray(count);
        return PdfASyntaxValue.Array;
    }

    /// <summary>Reads a word as null, a boolean or nothing.</summary>
    /// <param name="reader">The reader, on the token.</param>
    /// <returns>The kind of value read.</returns>
    private static PdfASyntaxValue FromWord(ref PdfASyntaxReader reader)
    {
        if (reader.Kind == PdfASyntaxToken.Broken)
        {
            return PdfASyntaxValue.Broken;
        }

        if (reader.IsWord("null"u8))
        {
            return PdfASyntaxValue.Null;
        }

        return reader.IsWord("true"u8) || reader.IsWord("false"u8) ? PdfASyntaxValue.Boolean : PdfASyntaxValue.Empty;
    }
}
