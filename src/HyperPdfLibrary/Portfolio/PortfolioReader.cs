// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Portfolio;

/// <summary>Reads the parts of a collection dictionary: schema, sort, colours, the folder tree and item values.</summary>
internal static class PortfolioReader
{
    /// <summary>The most folders read.</summary>
    private const int MaxFolders = 10_000;

    /// <summary>The shortest folder prefix, <c>&lt;n&gt;</c>.</summary>
    private const int MinPrefixLength = 3;

    /// <summary>The number of integers in one free range.</summary>
    private const int RangeLength = 2;

    /// <summary>The number of components in a colour.</summary>
    private const int ColorComponents = 3;

    /// <summary>Reads the schema, ordered by each field's <c>/O</c> value.</summary>
    /// <param name="objects">The document's objects, which resolve references and name the keys.</param>
    /// <param name="schema">The <c>/Schema</c> dictionary, or null.</param>
    /// <returns>The fields; entries that are not dictionaries are skipped.</returns>
    internal static PdfPortfolioField[] ReadSchema(PdfObjectStore objects, PdfDictionary? schema)
    {
        if (schema is null)
        {
            return [];
        }

        var fields = new List<PdfPortfolioField>(schema.Count);
        for (var i = 0; i < schema.Count; i++)
        {
            if (StoreReading.Resolve(objects, schema.GetValueAt(i)).AsDictionary() is not { } field)
            {
                continue;
            }

            var key = objects.Names.GetString(schema.GetKeyAt(i));
            fields.Add(new(key, field.NameText("Subtype") ?? string.Empty, field.Text("N") ?? string.Empty, field.Int("O", 0), field.Flag("V", true), field.Flag("E", false)));
        }

        fields.Sort(static (left, right) => left.Order.CompareTo(right.Order));
        return [.. fields];
    }

    /// <summary>Reads the sort keys; <c>/S</c> and <c>/A</c> may each be one value or an array.</summary>
    /// <param name="sort">The <c>/Sort</c> dictionary, or null.</param>
    /// <returns>The keys.</returns>
    internal static PdfPortfolioSort[] ReadSort(PdfDictionary? sort)
    {
        if (sort is null)
        {
            return [];
        }

        var keys = sort.NameText("S") is { } single
            ? new[]
        {
            single,
        }
            : sort.Array("S").NameTexts();
        var directions = sort.Array("A");
        var result = new PdfPortfolioSort[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            var direction = directions is null ? sort.Value("A") : directions.Get(i);
            result[i] = new(keys[i], direction.AsBoolean(true));
        }

        return result;
    }

    /// <summary>Reads the <c>/Colors</c> dictionary.</summary>
    /// <param name="colors">The dictionary, or null.</param>
    /// <returns>The colours, or null.</returns>
    internal static PdfPortfolioColors? ReadColors(PdfDictionary? colors) =>
        colors is null ? null : new(
        Color(
        colors,
        "Background"),
        Color(
        colors,
        "CardBackground"),
        Color(
        colors,
        "CardBorder"),
        Color(
        colors,
        "PrimaryText"),
        Color(
        colors,
        "SecondaryText"));

    /// <summary>Reads a folder and the folders below it.</summary>
    /// <param name="folder">The folder dictionary.</param>
    /// <param name="depth">The folder's depth.</param>
    /// <param name="visited">The folders already read, to stop loops.</param>
    /// <returns>The folder, or null when it was read already or the limits are reached.</returns>
    internal static PdfPortfolioFolder? ReadFolder(PdfDictionary folder, int depth, HashSet<PdfDictionary> visited)
    {
        if (depth >= PdfLimits.MaxNesting || visited.Count >= MaxFolders || !visited.Add(folder))
        {
            return null;
        }

        var children = new List<PdfPortfolioFolder>();
        for (var child = folder.Dict("Child"); child is not null; child = child.GetDictionary(KnownName.Next))
        {
            if (ReadFolder(child, depth + 1, visited) is not { } read)
            {
                break;
            }

            children.Add(read);
        }

        return new(
        folder.Int(
        "ID",
        0),
        folder.GetText(KnownName.Name),
        folder.GetText(KnownName.Desc),
        PdfDate.Parse(folder.GetStringBytes(KnownName.CreationDate)),
        PdfDate.Parse(folder.GetStringBytes(KnownName.ModDate)),
        ReadFree(folder.Array("Free")),
        [.. children]);
    }

    /// <summary>Reads the folder id from an <c>/EmbeddedFiles</c> name-tree key. A file in a folder has a key that starts with the folder's <c>/ID</c> in angle brackets.</summary>
    /// <param name="key">The name-tree key bytes.</param>
    /// <returns>The folder id, or null when the key has no folder prefix (the file is in the root folder).</returns>
    internal static int? ReadFolderId(ReadOnlySpan<byte> key)
    {
        if (key.Length < MinPrefixLength || key[0] != (byte)'<')
        {
            return null;
        }

        var close = key.IndexOf((byte)'>');
        if (close < MinPrefixLength - 1)
        {
            return null;
        }

        return int.TryParse(key[1..close], NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    /// <summary>Reads a collection item dictionary as text values.</summary>
    /// <param name="objects">The document's objects, which resolve references and name the keys.</param>
    /// <param name="item">The <c>/CI</c> dictionary, or null.</param>
    /// <returns>The values by key.</returns>
    internal static Dictionary<string, string> ReadItemValues(PdfObjectStore objects, PdfDictionary? item)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; item is not null && i < item.Count; i++)
        {
            var value = item.GetValueAt(i);
            var resolved = StoreReading.Resolve(objects, value);
            var text = resolved.AsDictionary() is { } sub ? sub.Value("D").ScalarText(objects) : resolved.ScalarText(objects);
            if (text is not null)
            {
                values[objects.Names.GetString(item.GetKeyAt(i))] = text;
            }
        }

        _ = values.Remove("Type");
        return values;
    }

    /// <summary>Reads a colour as three numbers.</summary>
    /// <param name="colors">The colours dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The components, or null when the entry is not three numbers.</returns>
    private static double[]? Color(PdfDictionary colors, string key) => colors.Array(key).Numbers() is { Length: ColorComponents } components ? components : null;

    /// <summary>Reads the <c>/Free</c> array of id ranges.</summary>
    /// <param name="free">The array, or null.</param>
    /// <returns>The ranges.</returns>
    private static PdfFolderIdRange[] ReadFree(PdfArray? free)
    {
        var ranges = new List<PdfFolderIdRange>();
        for (var i = 0; free is not null && i + 1 < free.Count; i += RangeLength)
        {
            ranges.Add(new(free.GetInt32(i), free.GetInt32(i + 1)));
        }

        return [.. ranges];
    }
}
