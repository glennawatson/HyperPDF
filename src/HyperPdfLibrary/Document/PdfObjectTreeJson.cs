// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Text.Unicode;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Writes a read-only JSON view of a document's objects, to look at a file's structure while debugging. Nothing in the
/// document changes. It is built with <see cref="Utf8JsonWriter"/>, so it needs no reflection and is safe to trim and
/// compile ahead of time.
/// </summary>
/// <remarks>
/// The document is an object with <c>version</c>, <c>trailer</c> and <c>objects</c>. Each object has its number,
/// generation and value. A name is a string that starts with a slash, such as <c>"/Page"</c>; a PDF string is
/// <c>{"string":"text"}</c> when it is printable ASCII, otherwise <c>{"hex":"..."}</c>; a reference is
/// <c>{"ref":"12 0"}</c>, so a reference loop never repeats an object. A stream is
/// <c>{"stream":{"dictionary":...,"rawLength":n,"filters":[...]}}</c>, with <c>decoded</c> (Base64) and
/// <c>decodedLength</c> added when <see cref="PdfObjectTreeJsonOptions.MaxDecodedBytes"/> is above zero. Values nested
/// deeper than the reader allows are written as <c>{"truncated":true}</c>.
/// </remarks>
public static class PdfObjectTreeJson
{
    /// <summary>The longest name written without renting a buffer.</summary>
    private const int StackLimit = 256;

    /// <summary>The longest text of a reference: two numbers up to 11 characters each and a space.</summary>
    private const int ReferenceLength = 24;

    /// <summary>The characters of a printable ASCII string: space to tilde.</summary>
    private const byte FirstPrintable = 0x20;

    /// <summary>The last printable ASCII byte.</summary>
    private const byte LastPrintable = 0x7E;

    /// <summary>Writes the JSON view to a stream.</summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">The stream.</param>
    /// <param name="options">What to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static void Write(PdfDocument document, Stream destination, PdfObjectTreeJsonOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        using var writer = new Utf8JsonWriter(destination, new() { Indented = options.Indented });
        Begin(writer, document.Objects, options, cancellationToken);
        var store = document.Objects;
        for (var number = 1; number < store.Size; number++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteObject(writer, store, number, options, cancellationToken);
        }

        End(writer);
        writer.Flush();
    }

    /// <summary>Writes the JSON view to a stream asynchronously, flushing after each object.</summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">The stream.</param>
    /// <param name="options">What to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the JSON is written.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task WriteAsync(PdfDocument document, Stream destination, PdfObjectTreeJsonOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        var writer = new Utf8JsonWriter(destination, new() { Indented = options.Indented });
        await using (writer.ConfigureAwait(false))
        {
            var store = document.Objects;
            Begin(writer, store, options, cancellationToken);
            for (var number = 1; number < store.Size; number++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteObject(writer, store, number, options, cancellationToken);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            End(writer);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Writes the start of the document: its version and trailer, and the opening of the object list.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="options">What to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    private static void Begin(Utf8JsonWriter writer, PdfObjectStore store, PdfObjectTreeJsonOptions options, CancellationToken cancellationToken)
    {
        writer.WriteStartObject();
        writer.WriteString("version"u8, store.Version);
        writer.WritePropertyName("trailer"u8);
        WriteValue(writer, store, PdfValue.FromDictionary(store.Trailer), options, 0, cancellationToken);
        writer.WriteStartArray("objects"u8);
    }

    /// <summary>Writes the end of the object list and the document.</summary>
    /// <param name="writer">The JSON writer.</param>
    private static void End(Utf8JsonWriter writer)
    {
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>Writes one object, when the number names one.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="number">The object number.</param>
    /// <param name="options">What to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    private static void WriteObject(Utf8JsonWriter writer, PdfObjectStore store, int number, PdfObjectTreeJsonOptions options, CancellationToken cancellationToken)
    {
        var value = StoreReading.GetObject(store, new(number, 0));
        if (value.IsNull)
        {
            return;
        }

        writer.WriteStartObject();
        writer.WriteNumber("number"u8, number);
        writer.WriteNumber("generation"u8, StoreEditing.GetGeneration(store, number));
        writer.WritePropertyName("value"u8);
        WriteValue(writer, store, value, options, 0, cancellationToken);
        writer.WriteEndObject();
    }

    /// <summary>Writes a value.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="value">The value.</param>
    /// <param name="options">What to write.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    private static void WriteValue(Utf8JsonWriter writer, PdfObjectStore store, PdfValue value, PdfObjectTreeJsonOptions options, int depth, CancellationToken cancellationToken)
    {
        if (depth > PdfLimits.MaxNesting)
        {
            writer.WriteStartObject();
            writer.WriteBoolean("truncated"u8, true);
            writer.WriteEndObject();
            return;
        }

        switch (value.Kind)
        {
            case PdfKind.Array:
                {
                    WriteArray(writer, store, value.AsArray()!, options, depth, cancellationToken);
                    break;
                }

            case PdfKind.Dictionary:
                {
                    WriteDictionary(writer, store, value.AsDictionary()!, options, depth, cancellationToken);
                    break;
                }

            case PdfKind.Stream:
                {
                    WriteStream(writer, store, value.AsStream()!, options, depth, cancellationToken);
                    break;
                }

            default:
                {
                    WriteScalar(writer, store, value);
                    break;
                }
        }
    }

    /// <summary>Writes a value that holds no other values.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="value">The value: null, a boolean, a number, a name, a string or a reference.</param>
    private static void WriteScalar(Utf8JsonWriter writer, PdfObjectStore store, PdfValue value)
    {
        switch (value.Kind)
        {
            case PdfKind.Boolean:
                {
                    writer.WriteBooleanValue(value.AsBoolean());
                    break;
                }

            case PdfKind.Integer:
                {
                    writer.WriteNumberValue(value.AsInteger());
                    break;
                }

            case PdfKind.Real:
                {
                    writer.WriteNumberValue(value.AsNumber());
                    break;
                }

            case PdfKind.Name:
                {
                    WriteNameValue(writer, store, value.AsName(), true);
                    break;
                }

            case PdfKind.String:
                {
                    WriteString(writer, value.AsStringBytes());
                    break;
                }

            case PdfKind.Reference:
                {
                    WriteReference(writer, value.AsReference());
                    break;
                }

            default:
                {
                    writer.WriteNullValue();
                    break;
                }
        }
    }

    /// <summary>Writes a string: its text when it is printable ASCII, otherwise its bytes in hex.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="bytes">The string's bytes.</param>
    private static void WriteString(Utf8JsonWriter writer, ReadOnlySpan<byte> bytes)
    {
        writer.WriteStartObject();
        if (bytes.IndexOfAnyExceptInRange(FirstPrintable, LastPrintable) < 0)
        {
            writer.WriteString("string"u8, bytes);
        }
        else
        {
            writer.WriteString("hex"u8, Convert.ToHexString(bytes));
        }

        writer.WriteEndObject();
    }

    /// <summary>Writes an array.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="array">The array.</param>
    /// <param name="options">What to write.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    private static void WriteArray(Utf8JsonWriter writer, PdfObjectStore store, PdfArray array, PdfObjectTreeJsonOptions options, int depth, CancellationToken cancellationToken)
    {
        writer.WriteStartArray();
        for (var i = 0; i < array.Count; i++)
        {
            WriteValue(writer, store, array.GetRaw(i), options, depth + 1, cancellationToken);
        }

        writer.WriteEndArray();
    }

    /// <summary>Writes a dictionary as a JSON object keyed by key name, without the slash.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="options">What to write.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    private static void WriteDictionary(Utf8JsonWriter writer, PdfObjectStore store, PdfDictionary dictionary, PdfObjectTreeJsonOptions options, int depth, CancellationToken cancellationToken)
    {
        writer.WriteStartObject();
        for (var i = 0; i < dictionary.Count; i++)
        {
            WriteKey(writer, store, dictionary.GetKeyAt(i));
            WriteValue(writer, store, dictionary.GetValueAt(i), options, depth + 1, cancellationToken);
        }

        writer.WriteEndObject();
    }

    /// <summary>Writes a stream: its dictionary, its length, its filters and, when asked for, its decoded bytes.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="options">What to write.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    private static void WriteStream(Utf8JsonWriter writer, PdfObjectStore store, PdfStream stream, PdfObjectTreeJsonOptions options, int depth, CancellationToken cancellationToken)
    {
        writer.WriteStartObject();
        writer.WriteStartObject("stream"u8);
        writer.WritePropertyName("dictionary"u8);
        WriteDictionary(writer, store, stream.Dictionary, options, depth + 1, cancellationToken);
        writer.WriteNumber("rawLength"u8, stream.RawLength);
        writer.WriteStartArray("filters"u8);
        WriteFilters(writer, store, stream.Dictionary.Get(KnownName.Filter));
        writer.WriteEndArray();
        if (options.MaxDecodedBytes > 0)
        {
            WriteDecoded(writer, stream, options.MaxDecodedBytes, cancellationToken);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    /// <summary>Writes the filter names of a stream.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="filter">The stream's /Filter value.</param>
    private static void WriteFilters(Utf8JsonWriter writer, PdfObjectStore store, PdfValue filter)
    {
        if (filter.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                WriteNameValue(writer, store, array.GetName(i), false);
            }

            return;
        }

        if (filter.AsName() is { IsNone: false } name)
        {
            WriteNameValue(writer, store, name, false);
        }
    }

    /// <summary>Writes a name as a JSON string, with a leading slash when asked. A name that is not UTF-8 text is written with replacement characters.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="name">The name.</param>
    /// <param name="slash">Whether to start the text with a slash.</param>
    private static void WriteNameValue(Utf8JsonWriter writer, PdfObjectStore store, PdfName name, bool slash)
    {
        var spelling = store.Names.GetSpelling(name);
        if (!Utf8.IsValid(spelling))
        {
            writer.WriteStringValue(slash ? $"/{store.Names.GetString(name)}" : store.Names.GetString(name));
            return;
        }

        var length = spelling.Length + (slash ? 1 : 0);
        byte[]? rented = null;
        var buffer = length <= StackLimit ? stackalloc byte[StackLimit] : rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            if (slash)
            {
                buffer[0] = (byte)'/';
            }

            spelling.CopyTo(buffer[(length - spelling.Length)..]);
            writer.WriteStringValue(buffer[..length]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Writes a dictionary key as a property name.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="key">The key.</param>
    private static void WriteKey(Utf8JsonWriter writer, PdfObjectStore store, PdfName key)
    {
        var spelling = store.Names.GetSpelling(key);
        if (Utf8.IsValid(spelling))
        {
            writer.WritePropertyName(spelling);
        }
        else
        {
            writer.WritePropertyName(store.Names.GetString(key));
        }
    }

    /// <summary>Writes a reference as <c>{"ref":"12 0"}</c>.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="id">The reference.</param>
    private static void WriteReference(Utf8JsonWriter writer, PdfObjectId id)
    {
        Span<byte> text = stackalloc byte[ReferenceLength];
        _ = Utf8.TryWrite(text, CultureInfo.InvariantCulture, $"{id.Number} {id.Generation}", out var written);
        writer.WriteStartObject();
        writer.WriteString("ref"u8, text[..written]);
        writer.WriteEndObject();
    }

    /// <summary>Writes the first decoded bytes of a stream as Base64.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="limit">The most bytes to write.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    private static void WriteDecoded(Utf8JsonWriter writer, PdfStream stream, int limit, CancellationToken cancellationToken)
    {
        var decoded = default(PooledBuffer);
        try
        {
            _ = stream.Decode(ref decoded, cancellationToken);
            var span = decoded.WrittenSpan;
            writer.WriteNumber("decodedLength"u8, span.Length);
            writer.WriteBase64String("decoded"u8, span[..Math.Min(limit, span.Length)]);
        }
        finally
        {
            decoded.Dispose();
        }
    }
}
