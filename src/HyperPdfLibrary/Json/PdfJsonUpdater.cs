// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text.Json;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Json;

/// <summary>
/// Applies a JSON file in the shape <see cref="PdfJsonExporter"/> writes to a document: each object in the file replaces the
/// document's object with the same number, a <c>null</c> value deletes it, and an object the file leaves out is not touched.
/// The trailer is read-only; the file's <c>trailer</c> entry is ignored. Save the document afterwards to keep the changes.
/// </summary>
public static class PdfJsonUpdater
{
    /// <summary>The prefix of an object's key.</summary>
    private const string ObjectPrefix = "obj:";

    /// <summary>The most levels of arrays and dictionaries a value may nest.</summary>
    private const int MaximumDepth = 64;

    /// <summary>The entries of the qpdf array: a header, then the object table.</summary>
    private const int QpdfEntries = 2;

    /// <summary>The index of the object table in the qpdf array.</summary>
    private const int ObjectTable = 1;

    /// <summary>Applies JSON read from a stream.</summary>
    /// <param name="document">The document to change.</param>
    /// <param name="source">The stream holding the JSON.</param>
    /// <param name="cancellationToken">Cancels the read and the changes between objects.</param>
    /// <returns>How many objects were replaced or deleted.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The JSON is not valid, or an object in it cannot be turned into a PDF object.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<int> ApplyAsync(PdfDocument document, Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            using var parsed = await JsonDocument.ParseAsync(source, default, cancellationToken).ConfigureAwait(false);
            return Apply(document, parsed.RootElement, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The JSON is not valid.", ex);
        }
    }

    /// <summary>Applies JSON held in memory.</summary>
    /// <param name="document">The document to change.</param>
    /// <param name="json">The JSON as UTF-8.</param>
    /// <param name="cancellationToken">Cancels the changes between objects.</param>
    /// <returns>How many objects were replaced or deleted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The JSON is not valid, or an object in it cannot be turned into a PDF object.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static int Apply(PdfDocument document, ReadOnlyMemory<byte> json, CancellationToken cancellationToken)
    {
        try
        {
            using var parsed = JsonDocument.Parse(json);
            return Apply(document, parsed.RootElement, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The JSON is not valid.", ex);
        }
    }

    /// <summary>Applies a parsed file.</summary>
    /// <param name="document">The document to change.</param>
    /// <param name="root">The file's top-level value.</param>
    /// <param name="cancellationToken">Cancels between objects.</param>
    /// <returns>How many objects were replaced or deleted.</returns>
    /// <exception cref="InvalidDataException">The JSON has no object table, or an object in it cannot be turned into a PDF object.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    private static int Apply(PdfDocument document, JsonElement root, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!root.TryGetProperty(PdfJsonKeys.Qpdf, out var parts) || parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() < QpdfEntries)
        {
            throw new InvalidDataException("The JSON has no qpdf array with an object table.");
        }

        var store = document.Objects;
        var changed = 0;
        foreach (var entry in parts[ObjectTable].EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryParseKey(entry.Name, out var id))
            {
                continue;
            }

            changed += ApplyObject(store, id, entry.Value);
        }

        return changed;
    }

    /// <summary>Replaces or deletes one object.</summary>
    /// <param name="store">The object store.</param>
    /// <param name="id">The object.</param>
    /// <param name="body">The object's JSON: a value or a stream.</param>
    /// <returns>1 when the object changed.</returns>
    /// <exception cref="InvalidDataException">The object cannot be turned into a PDF object.</exception>
    private static int ApplyObject(PdfObjectStore store, PdfObjectId id, JsonElement body)
    {
        if (body.TryGetProperty(PdfJsonKeys.Stream, out var streamJson))
        {
            StoreEditing.Replace(store, id, PdfValue.FromStream(ReadStream(store, id, streamJson)));
            return 1;
        }

        if (!body.TryGetProperty(PdfJsonKeys.Value, out var value))
        {
            throw new InvalidDataException($"Object {id.Number} has neither a value nor a stream.");
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            StoreEditing.Delete(store, id);
            return 1;
        }

        StoreEditing.Replace(store, id, ReadValue(store, value, 0));
        return 1;
    }

    /// <summary>Reads a stream: its dictionary, and its data, or the data it already has.</summary>
    /// <param name="store">The object store.</param>
    /// <param name="id">The object.</param>
    /// <param name="streamJson">The stream's JSON.</param>
    /// <returns>The stream.</returns>
    /// <exception cref="InvalidDataException">The stream has no dictionary, or no data and none stored to keep.</exception>
    private static PdfStream ReadStream(PdfObjectStore store, PdfObjectId id, JsonElement streamJson)
    {
        if (!streamJson.TryGetProperty(PdfJsonKeys.Dictionary, out var dictionaryJson) || dictionaryJson.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"The stream in object {id.Number} has no dictionary.");
        }

        var dictionary = ReadDictionary(store, dictionaryJson, 1);
        if (streamJson.TryGetProperty(PdfJsonKeys.Data, out var dataJson) && dataJson.ValueKind == JsonValueKind.String)
        {
            return new(dictionary, dataJson.GetBytesFromBase64());
        }

        if (StoreReading.GetObject(store, id).AsStream() is { IsEncrypted: false } existing)
        {
            return new(dictionary, existing.CopyRawData());
        }

        throw new InvalidDataException($"The stream in object {id.Number} has no data and no stored data to keep.");
    }

    /// <summary>Reads a value.</summary>
    /// <param name="store">The object store.</param>
    /// <param name="element">The JSON.</param>
    /// <param name="depth">How many arrays and dictionaries enclose it.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">The JSON nests too deeply, or a string is not valid.</exception>
    private static PdfValue ReadValue(PdfObjectStore store, JsonElement element, int depth)
    {
        if (depth > MaximumDepth)
        {
            throw new InvalidDataException("The JSON nests values too deeply.");
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.True or JsonValueKind.False:
            {
                return PdfValue.FromBoolean(element.GetBoolean());
            }

            case JsonValueKind.Number:
            {
                return element.TryGetInt64(out var integer) ? PdfValue.FromInteger(integer) : PdfValue.FromReal(element.GetDouble());
            }

            case JsonValueKind.String:
            {
                return ReadString(store, element.GetString()!);
            }

            case JsonValueKind.Array:
            {
                return PdfValue.FromArray(ReadArray(store, element, depth + 1));
            }

            case JsonValueKind.Object:
            {
                return PdfValue.FromDictionary(ReadDictionary(store, element, depth + 1));
            }

            default:
            {
                return default;
            }
        }
    }

    /// <summary>Reads a string value: a name, a reference, or a PDF string.</summary>
    /// <param name="store">The object store.</param>
    /// <param name="text">The JSON string.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">A <c>b:</c> string is not hexadecimal.</exception>
    private static PdfValue ReadString(PdfObjectStore store, string text)
    {
        if ((text.Length > 0 && text[0] == PdfJsonStrings.NameSlash) || text.StartsWith(PdfJsonStrings.RawNamePrefix + PdfJsonStrings.NameSlash, StringComparison.Ordinal))
        {
            return PdfValue.FromName(InternName(store, text));
        }

        if (PdfJsonStrings.TryParseReference(text, out var id))
        {
            return PdfValue.FromReference(id);
        }

        try
        {
            return PdfValue.FromString(PdfJsonStrings.DecodeString(text));
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("A b: string is not hexadecimal.", ex);
        }
    }

    /// <summary>Makes a name from its JSON string.</summary>
    /// <param name="store">The object store.</param>
    /// <param name="text">The JSON string, <c>/Name</c> or <c>n:/Name</c>.</param>
    /// <returns>The name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfName InternName(PdfObjectStore store, string text) => store.Names.Intern(PdfJsonStrings.DecodeName(text));

    /// <summary>Reads an array.</summary>
    /// <param name="store">The object store.</param>
    /// <param name="element">The JSON array.</param>
    /// <param name="depth">How many arrays and dictionaries enclose it.</param>
    /// <returns>The array.</returns>
    private static PdfArray ReadArray(PdfObjectStore store, JsonElement element, int depth)
    {
        var array = new PdfArray(store, element.GetArrayLength());
        foreach (var item in element.EnumerateArray())
        {
            array.Add(ReadValue(store, item, depth));
        }

        return array;
    }

    /// <summary>Reads a dictionary.</summary>
    /// <param name="store">The object store.</param>
    /// <param name="element">The JSON object.</param>
    /// <param name="depth">How many arrays and dictionaries enclose it.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary ReadDictionary(PdfObjectStore store, JsonElement element, int depth)
    {
        var dictionary = new PdfDictionary(store);
        foreach (var property in element.EnumerateObject())
        {
            dictionary.Set(InternName(store, property.Name), ReadValue(store, property.Value, depth));
        }

        return dictionary;
    }

    /// <summary>Reads an object key such as <c>obj:12 0 R</c>.</summary>
    /// <param name="key">The key.</param>
    /// <param name="id">The object.</param>
    /// <returns><see langword="true"/> when the key names an object.</returns>
    private static bool TryParseKey(string key, out PdfObjectId id)
    {
        id = default;
        return key.StartsWith(ObjectPrefix, StringComparison.Ordinal) && PdfJsonStrings.TryParseReference(key[ObjectPrefix.Length..], out id);
    }
}
