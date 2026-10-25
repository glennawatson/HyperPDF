// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Describes objects as canonical text, so two documents can be compared object by object or as a whole graph.</summary>
internal static class ObjectDescriber
{
    /// <summary>The deepest nesting described.</summary>
    private const int MaxDepth = 64;

    /// <summary>Describes each object by its number, keeping references as written.</summary>
    /// <param name="store">The document.</param>
    /// <returns>One line per object, in number order; missing objects give an empty line.</returns>
    internal static List<string> DescribeEachObject(PdfObjectStore store)
    {
        var lines = new List<string>();
        for (var number = 1; number < store.Size; number++)
        {
            var text = new StringBuilder();
            AppendValue(store, StoreReading.GetObject(store, new(number, 0)), text, null, 0);
            lines.Add(text.ToString());
        }

        return lines;
    }

    /// <summary>
    /// Describes everything reachable from the trailer's /Root and /Info, following references and naming each object by
    /// the order it is first reached, so the result does not depend on object numbers.
    /// </summary>
    /// <param name="store">The document.</param>
    /// <returns>The description.</returns>
    internal static string DescribeGraph(PdfObjectStore store)
    {
        var text = new StringBuilder();
        var order = new Dictionary<int, int>();
        AppendValue(store, store.Trailer.GetRaw(KnownName.Root), text, order, 0);
        AppendValue(store, store.Trailer.GetRaw(KnownName.Info), text, order, 0);
        return text.ToString();
    }

    /// <summary>Appends a value.</summary>
    /// <param name="store">The document.</param>
    /// <param name="value">The value.</param>
    /// <param name="text">The description.</param>
    /// <param name="order">The order objects were first reached, or <see langword="null"/> to keep references as written.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void AppendValue(PdfObjectStore store, PdfValue value, StringBuilder text, Dictionary<int, int>? order, int depth)
    {
        if (depth > MaxDepth)
        {
            _ = text.Append("...");
            return;
        }

        switch (value.Kind)
        {
            case PdfKind.Reference:
                {
                    AppendReference(store, value.AsReference(), text, order, depth);
                    break;
                }

            case PdfKind.Array:
                {
                    AppendArray(store, value.AsArray()!, text, order, depth);
                    break;
                }

            case PdfKind.Dictionary:
                {
                    AppendDictionary(store, value.AsDictionary()!, text, order, depth);
                    break;
                }

            case PdfKind.Stream:
                {
                    AppendStream(store, value.AsStream()!, text, order, depth);
                    break;
                }

            default:
                {
                    AppendSimple(store, value, text);
                    break;
                }
        }
    }

    /// <summary>Appends a reference, either as written or as the target the first time it is reached.</summary>
    /// <param name="store">The document.</param>
    /// <param name="id">The reference.</param>
    /// <param name="text">The description.</param>
    /// <param name="order">The reach order, or <see langword="null"/>.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void AppendReference(PdfObjectStore store, PdfObjectId id, StringBuilder text, Dictionary<int, int>? order, int depth)
    {
        if (order is null)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{id.Number} R");
            return;
        }

        if (order.TryGetValue(id.Number, out var seen))
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"@{seen}");
            return;
        }

        var index = order.Count;
        order[id.Number] = index;
        _ = text.Append(CultureInfo.InvariantCulture, $"@{index}=");
        AppendValue(store, StoreReading.GetObject(store, id), text, order, depth + 1);
    }

    /// <summary>Appends an array.</summary>
    /// <param name="store">The document.</param>
    /// <param name="array">The array.</param>
    /// <param name="text">The description.</param>
    /// <param name="order">The reach order, or <see langword="null"/>.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void AppendArray(PdfObjectStore store, PdfArray array, StringBuilder text, Dictionary<int, int>? order, int depth)
    {
        _ = text.Append('[');
        for (var i = 0; i < array.Count; i++)
        {
            AppendValue(store, array.GetRaw(i), text, order, depth + 1);
            _ = text.Append(' ');
        }

        _ = text.Append(']');
    }

    /// <summary>Appends a dictionary with its keys in alphabetical order.</summary>
    /// <param name="store">The document.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="text">The description.</param>
    /// <param name="order">The reach order, or <see langword="null"/>.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void AppendDictionary(PdfObjectStore store, PdfDictionary dictionary, StringBuilder text, Dictionary<int, int>? order, int depth)
    {
        var keys = new List<string>();
        for (var i = 0; i < dictionary.Count; i++)
        {
            keys.Add(store.Names.GetString(dictionary.GetKeyAt(i)));
        }

        keys.Sort(StringComparer.Ordinal);
        _ = text.Append("<<");
        foreach (var key in keys)
        {
            _ = text.Append('/').Append(key).Append(' ');
            AppendValue(store, dictionary.GetRaw(store.Names.Intern(key)), text, order, depth + 1);
            _ = text.Append(' ');
        }

        _ = text.Append(">>");
    }

    /// <summary>Appends a stream: its dictionary without the entries that depend on how it is encoded, then its decoded bytes.</summary>
    /// <param name="store">The document.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="text">The description.</param>
    /// <param name="order">The reach order, or <see langword="null"/>.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void AppendStream(PdfObjectStore store, PdfStream stream, StringBuilder text, Dictionary<int, int>? order, int depth)
    {
        var copy = stream.Dictionary.Clone();
        _ = copy.Remove(KnownName.Length);
        _ = copy.Remove(KnownName.Filter);
        _ = copy.Remove(KnownName.DecodeParms);
        AppendDictionary(store, copy, text, order, depth);
        _ = text.Append("stream:").Append(Convert.ToHexString(stream.DecodeToArray()));
    }

    /// <summary>Appends a null, boolean, number, name or string.</summary>
    /// <param name="store">The document.</param>
    /// <param name="value">The value.</param>
    /// <param name="text">The description.</param>
    private static void AppendSimple(PdfObjectStore store, PdfValue value, StringBuilder text)
    {
        switch (value.Kind)
        {
            case PdfKind.Boolean:
                {
                    _ = text.Append(value.AsBoolean() ? "true" : "false");
                    break;
                }

            case PdfKind.Integer:
                {
                    _ = text.Append(value.AsInteger());
                    break;
                }

            case PdfKind.Real:
                {
                    _ = text.Append(value.AsNumber().ToString("R", CultureInfo.InvariantCulture));
                    break;
                }

            case PdfKind.Name:
                {
                    _ = text.Append('/').Append(store.Names.GetString(value.AsName()));
                    break;
                }

            case PdfKind.String:
                {
                    _ = text.Append('<').Append(Convert.ToHexString(value.AsStringBytes())).Append('>');
                    break;
                }

            default:
                {
                    _ = text.Append("null");
                    break;
                }
        }
    }
}
