// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Json;

/// <summary>
/// Writes a document's objects as JSON in the shape of qpdf's version 2 file: every object by number, strings as <c>u:</c> text
/// or <c>b:</c> hexadecimal, names as <c>/Name</c> and references as <c>12 0 R</c>. <see cref="PdfJsonUpdater"/> reads it back.
/// </summary>
public static class PdfJsonExporter
{
    /// <summary>The version of the JSON layout.</summary>
    private const int JsonVersion = 2;

    /// <summary>The keys of a stream dictionary that describe how the stored data is filtered, which decoded data no longer needs.</summary>
    private static readonly KnownName[] StreamKeys = [KnownName.Filter, KnownName.DecodeParms, KnownName.Length];

    /// <summary>Writes a document as JSON to a stream.</summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">The stream.</param>
    /// <param name="options">How to export.</param>
    /// <param name="cancellationToken">Cancels the export between objects.</param>
    /// <returns>A task.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask ExportAsync(PdfDocument document, Stream destination, PdfJsonOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        var writer = new Utf8JsonWriter(destination, new JsonWriterOptions { Indented = options.Indented });
        await using (writer.ConfigureAwait(false))
        {
            Write(document, writer, options, cancellationToken);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Writes a document as JSON.</summary>
    /// <param name="document">The document.</param>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="options">How to export.</param>
    /// <param name="cancellationToken">Cancels the export between objects.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static void Write(PdfDocument document, Utf8JsonWriter writer, PdfJsonOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(options);
        var store = document.Objects;
        writer.WriteStartObject();
        writer.WriteNumber("version", JsonVersion);
        writer.WriteStartArray(PdfJsonKeys.Qpdf);
        writer.WriteStartObject();
        writer.WriteNumber("jsonversion", JsonVersion);
        writer.WriteString("pdfversion", store.Version);
        writer.WriteNumber("maxobjectid", Math.Max(0, store.Size - 1));
        writer.WriteEndObject();
        writer.WriteStartObject();
        for (var number = 1; number < store.Size; number++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = new PdfObjectId(number, StoreEditing.GetGeneration(store, number));
            var value = StoreReading.GetObject(store, id);
            if (value.IsNull)
            {
                continue;
            }

            writer.WritePropertyName(string.Create(CultureInfo.InvariantCulture, $"obj:{id.Number} {id.Generation} R"));
            WriteObject(store, value, writer, options, cancellationToken);
        }

        writer.WritePropertyName(PdfJsonKeys.Trailer);
        writer.WriteStartObject();
        writer.WritePropertyName(PdfJsonKeys.Value);
        WriteDictionary(store, store.Trailer, writer, null);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>Writes one object: a value, or a stream with its dictionary and data.</summary>
    /// <param name="store">The object store.</param>
    /// <param name="value">The object.</param>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="options">How to export.</param>
    /// <param name="cancellationToken">Cancels the decode of a stream's data.</param>
    private static void WriteObject(PdfObjectStore store, PdfValue value, Utf8JsonWriter writer, PdfJsonOptions options, CancellationToken cancellationToken)
    {
        writer.WriteStartObject();
        if (value.AsStream() is { } stream)
        {
            writer.WritePropertyName(PdfJsonKeys.Stream);
            WriteStream(store, stream, writer, options, cancellationToken);
        }
        else
        {
            writer.WritePropertyName(PdfJsonKeys.Value);
            WriteValue(store, value, writer);
        }

        writer.WriteEndObject();
    }

    /// <summary>Writes a stream as its dictionary and, as base64, its data.</summary>
    /// <param name="store">The object store.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="options">How to export.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    private static void WriteStream(PdfObjectStore store, PdfStream stream, Utf8JsonWriter writer, PdfJsonOptions options, CancellationToken cancellationToken)
    {
        writer.WriteStartObject();
        var data = default(PooledBuffer);
        try
        {
            var decoded = options.StreamData == PdfJsonStreamData.Decoded || (options.StreamData == PdfJsonStreamData.Stored && stream.IsEncrypted);
            var wholly = decoded && stream.Decode(ref data, cancellationToken) == PdfImageCodec.None;
            writer.WritePropertyName(PdfJsonKeys.Dictionary);
            WriteDictionary(store, stream.Dictionary, writer, wholly ? StreamKeys : null);
            if (options.StreamData == PdfJsonStreamData.None)
            {
                return;
            }

            if (wholly)
            {
                writer.WriteBase64String(PdfJsonKeys.Data, data.WrittenSpan);
            }
            else if (!stream.IsEncrypted)
            {
                writer.WriteBase64String(PdfJsonKeys.Data, stream.CopyRawData());
            }
        }
        finally
        {
            data.Dispose();
            writer.WriteEndObject();
        }
    }

    /// <summary>Writes a dictionary, leaving out some keys.</summary>
    /// <param name="store">The object store.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="skipped">The keys to leave out, or <see langword="null"/> for none.</param>
    private static void WriteDictionary(PdfObjectStore store, PdfDictionary dictionary, Utf8JsonWriter writer, KnownName[]? skipped)
    {
        writer.WriteStartObject();
        for (var i = 0; i < dictionary.Count; i++)
        {
            var key = dictionary.GetKeyAt(i);
            if (dictionary.IsShadowed(i) || (skipped is not null && Array.IndexOf(skipped, key.ToKnownName()) >= 0))
            {
                continue;
            }

            writer.WritePropertyName(PdfJsonStrings.EncodeName(store.Names.GetSpelling(key)));
            WriteValue(store, dictionary.GetValueAt(i), writer);
        }

        writer.WriteEndObject();
    }

    /// <summary>Writes one value.</summary>
    /// <param name="store">The object store.</param>
    /// <param name="value">The value.</param>
    /// <param name="writer">The JSON writer.</param>
    private static void WriteValue(PdfObjectStore store, PdfValue value, Utf8JsonWriter writer)
    {
        switch (value.Kind)
        {
            case PdfKind.Boolean:
            {
                writer.WriteBooleanValue(value.AsBoolean());
                return;
            }

            case PdfKind.Integer:
            {
                writer.WriteNumberValue(value.AsInteger());
                return;
            }

            case PdfKind.Real:
            {
                writer.WriteNumberValue(value.AsNumber());
                return;
            }

            case PdfKind.Name:
            {
                writer.WriteStringValue(PdfJsonStrings.EncodeName(store.Names.GetSpelling(value.AsName())));
                return;
            }

            case PdfKind.String:
            {
                writer.WriteStringValue(PdfJsonStrings.EncodeString(value.AsStringBytes()));
                return;
            }

            case PdfKind.Reference:
            {
                var id = value.AsReference();
                writer.WriteStringValue(string.Create(CultureInfo.InvariantCulture, $"{id.Number} {id.Generation} R"));
                return;
            }

            default:
            {
                WriteContainer(store, value, writer);
                return;
            }
        }
    }

    /// <summary>Writes an array or a dictionary; anything else is written as null.</summary>
    /// <param name="store">The object store.</param>
    /// <param name="value">The value.</param>
    /// <param name="writer">The JSON writer.</param>
    private static void WriteContainer(PdfObjectStore store, PdfValue value, Utf8JsonWriter writer)
    {
        if (value.AsArray() is { } array)
        {
            writer.WriteStartArray();
            foreach (var item in array.Items)
            {
                WriteValue(store, item, writer);
            }

            writer.WriteEndArray();
            return;
        }

        if (value.Kind == PdfKind.Dictionary)
        {
            WriteDictionary(store, value.AsDictionary()!, writer, null);
            return;
        }

        writer.WriteNullValue();
    }
}
