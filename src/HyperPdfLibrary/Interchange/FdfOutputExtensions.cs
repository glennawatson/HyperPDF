// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Interchange;

/// <summary>
/// The PDF syntax of an FDF file, written into a pooled <see cref="PdfObjectWriter"/> as the data is read, so no object
/// tree is built first. An entry is a space, the key and a space, then the value.
/// </summary>
internal static class FdfOutputExtensions
{
    /// <summary>The bytes of a formatted date.</summary>
    private const int DateBytes = 32;

    /// <summary>Extension members for <c>PdfObjectWriter</c>.</summary>
    /// <param name="writer">The output.</param>
    extension(ref PdfObjectWriter writer)
    {

        /// <summary>Starts an indirect object.</summary>
        /// <param name="number">The object number.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void BeginObject(int number) => writer.WriteObjectHeader(new(number, 0));

        /// <summary>Ends an indirect object.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EndObject() => writer.WriteRaw("\nendobj\n"u8);

        /// <summary>Writes an indirect stream object, whose data and dictionary are already built.</summary>
        /// <param name="number">The object number.</param>
        /// <param name="stream">The stream.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Stream(int number, PdfStream stream) =>
            writer.WriteIndirectObject(new(number, 0), PdfValue.FromStream(stream));

        /// <summary>Writes the key of a dictionary entry.</summary>
        /// <param name="key">The key.</param>
        internal void Key(PdfName key)
        {
            writer.Buffer.WriteByte((byte)' ');
            PdfSyntax.WriteName(ref writer.Buffer, writer.Names.GetSpelling(key));
            writer.Buffer.WriteByte((byte)' ');
        }

        /// <summary>Writes a name.</summary>
        /// <param name="name">The name.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Name(PdfName name) => writer.WriteValue(PdfValue.FromName(name));

        /// <summary>Writes a name from its spelling.</summary>
        /// <param name="spelling">The spelling.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Name(string spelling) => writer.WriteValue(PdfValue.FromName(writer.Names.Intern(spelling)));

        /// <summary>Writes an integer.</summary>
        /// <param name="number">The integer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Integer(long number) => writer.WriteValue(PdfValue.FromInteger(number));

        /// <summary>Writes a number, as an integer when it is whole.</summary>
        /// <param name="number">The number.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Number(double number) => writer.WriteValue(PdfNumber.ToValue(number));

        /// <summary>Writes a value that is already built, such as a colour component.</summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Value(PdfValue value) => writer.WriteValue(value);

        /// <summary>Writes <c>true</c> or <c>false</c>.</summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Boolean(bool value) => writer.WriteValue(PdfValue.FromBoolean(value));

        /// <summary>Writes a reference to an object with generation 0.</summary>
        /// <param name="number">The object number.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Reference(int number) => writer.WriteValue(PdfValue.FromReference(new(number, 0)));

        /// <summary>Writes the start of an array.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void BeginArray() => writer.Buffer.WriteByte((byte)'[');

        /// <summary>Writes the end of an array.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EndArray() => writer.Buffer.WriteByte((byte)']');

        /// <summary>Writes the separator before an array item.</summary>
        /// <param name="index">The item's position.</param>
        internal void Separate(int index)
        {
            if (index > 0)
            {
                writer.Buffer.WriteByte((byte)' ');
            }
        }

        /// <summary>Writes the start of a dictionary.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void BeginDictionary() => writer.WriteRaw("<<"u8);

        /// <summary>Writes the end of a dictionary.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EndDictionary() => writer.WriteRaw(">>"u8);

        /// <summary>Writes an array of numbers.</summary>
        /// <param name="numbers">The numbers.</param>
        internal void Numbers(ReadOnlySpan<float> numbers)
        {
            writer.BeginArray();
            for (var i = 0; i < numbers.Length; i++)
            {
                writer.Separate(i);
                writer.Number(numbers[i]);
            }

            writer.EndArray();
        }

        /// <summary>Writes a text string as a PDF text string: PDFDocEncoding when every character fits, UTF-16 otherwise.</summary>
        /// <param name="text">The text.</param>
        internal void Text(string text)
        {
            var rented = ArrayPool<byte>.Shared.Rent(PdfText.MaxEncodedLength(text.Length));
            var length = PdfText.Encode(text.AsSpan(), false, rented);
            PdfSyntax.WriteString(ref writer.Buffer, rented.AsSpan(0, length));
            ArrayPool<byte>.Shared.Return(rented);
        }

        /// <summary>Writes bytes as a string.</summary>
        /// <param name="bytes">The bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Bytes(ReadOnlySpan<byte> bytes) => PdfSyntax.WriteString(ref writer.Buffer, bytes);

        /// <summary>Writes a date as a PDF date string.</summary>
        /// <param name="date">The date.</param>
        internal void Date(DateTimeOffset date)
        {
            var rented = ArrayPool<byte>.Shared.Rent(DateBytes);
            var length = PdfDate.Format(date, rented);
            PdfSyntax.WriteString(ref writer.Buffer, rented.AsSpan(0, length));
            ArrayPool<byte>.Shared.Return(rented);
        }

        /// <summary>Writes an entry whose value is a text string, when there is text.</summary>
        /// <param name="key">The key.</param>
        /// <param name="text">The text, or <see langword="null"/> for no entry.</param>
        internal void TextEntry(PdfName key, string? text)
        {
            if (text is null)
            {
                return;
            }

            writer.Key(key);
            writer.Text(text);
        }

        /// <summary>Writes an entry whose value is a name, when there is a name.</summary>
        /// <param name="key">The key.</param>
        /// <param name="spelling">The name's spelling; no entry when <see langword="null"/> or empty.</param>
        internal void NameEntry(PdfName key, string? spelling)
        {
            if (string.IsNullOrEmpty(spelling))
            {
                return;
            }

            writer.Key(key);
            writer.Name(spelling);
        }

        /// <summary>Writes an entry whose value is an array of numbers, when there are some.</summary>
        /// <param name="key">The key.</param>
        /// <param name="numbers">The numbers; no entry when empty.</param>
        internal void NumbersEntry(PdfName key, ReadOnlyMemory<float> numbers)
        {
            if (numbers.IsEmpty)
            {
                return;
            }

            writer.Key(key);
            writer.Numbers(numbers.Span);
        }
    }
}
