// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads the trailer entries the structure rules need straight from the bytes: /Prev, /XRefStm and the /ID strings. They
/// are read as written, because the trailer of an encrypted file is not encrypted.
/// </summary>
internal static class PdfATrailerEntries
{
    /// <summary>Gets the /Prev key.</summary>
    private static ReadOnlySpan<byte> PrevKey => "Prev"u8;

    /// <summary>Gets the /XRefStm key.</summary>
    private static ReadOnlySpan<byte> HybridKey => "XRefStm"u8;

    /// <summary>Gets the /ID key.</summary>
    private static ReadOnlySpan<byte> IdKey => "ID"u8;

    /// <summary>Gets the reference keyword.</summary>
    private static ReadOnlySpan<byte> ReferenceWord => "R"u8;

    /// <summary>Reads the trailer dictionary that starts at the reader's next token.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="offset">The section's offset.</param>
    /// <param name="isStream">Whether the section is a cross-reference stream.</param>
    /// <param name="scratch">Receives findings while values are skipped; they are dropped.</param>
    /// <returns>The section.</returns>
    internal static PdfAXrefSection Read(ref PdfASyntaxReader reader, long offset, bool isStream, PdfAObjectFindings scratch)
    {
        long? previous = null;
        long? hybrid = null;
        byte[]? identifier = null;
        reader.Next();
        if (reader.Kind == PdfASyntaxToken.DictionaryStart)
        {
            reader.Next();
            while (reader.Kind == PdfASyntaxToken.Name)
            {
                if (reader.Value.SequenceEqual(PrevKey))
                {
                    previous = ReadInteger(ref reader, scratch);
                }
                else if (reader.Value.SequenceEqual(HybridKey))
                {
                    hybrid = ReadInteger(ref reader, scratch);
                }
                else if (reader.Value.SequenceEqual(IdKey))
                {
                    identifier = ReadIdentifier(ref reader, scratch);
                }
                else if (PdfASyntaxValues.Read(ref reader, scratch, 1) == PdfASyntaxValue.Broken)
                {
                    break;
                }

                reader.Next();
            }
        }

        scratch.Discard();
        return new(offset, isStream, previous, hybrid, identifier);
    }

    /// <summary>Reads an integer value; a reference or any other value reads as missing.</summary>
    /// <param name="reader">The reader, on the key.</param>
    /// <param name="scratch">Receives findings while values are skipped.</param>
    /// <returns>The integer, or <see langword="null"/>.</returns>
    private static long? ReadInteger(ref PdfASyntaxReader reader, PdfAObjectFindings scratch)
    {
        reader.Next();
        if (reader.Kind != PdfASyntaxToken.Integer)
        {
            _ = PdfASyntaxValues.FromToken(ref reader, scratch, 1);
            return null;
        }

        var value = reader.Integer;
        var after = reader.Position;
        reader.Next();
        if (reader.Kind == PdfASyntaxToken.Integer)
        {
            reader.Next();
            if (reader.IsWord(ReferenceWord))
            {
                return null;
            }
        }

        reader.Position = after;
        return value;
    }

    /// <summary>Reads /ID, joining the bytes of the strings in the array.</summary>
    /// <param name="reader">The reader, on the key.</param>
    /// <param name="scratch">Receives findings while values are skipped.</param>
    /// <returns>The bytes, or <see langword="null"/> when the value is not an array.</returns>
    private static byte[]? ReadIdentifier(ref PdfASyntaxReader reader, PdfAObjectFindings scratch)
    {
        reader.Next();
        if (reader.Kind != PdfASyntaxToken.ArrayStart)
        {
            _ = PdfASyntaxValues.FromToken(ref reader, scratch, 1);
            return null;
        }

        var bytes = new List<byte>();
        while (true)
        {
            reader.Next();
            if (reader.Kind is PdfASyntaxToken.LiteralString or PdfASyntaxToken.HexString)
            {
                bytes.AddRange(reader.Value);
            }
            else if (reader.Kind == PdfASyntaxToken.ArrayEnd || PdfASyntaxValues.FromToken(ref reader, scratch, 1) is PdfASyntaxValue.Broken or PdfASyntaxValue.Empty)
            {
                break;
            }
        }

        return [.. bytes];
    }
}
