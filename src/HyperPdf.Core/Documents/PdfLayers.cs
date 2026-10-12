// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.Core.Signatures.Signing;

namespace HyperPdf.Core.Documents;

/// <summary>
/// Reads a PDF's layers (optional content groups) and writes a copy whose default visibility shows or hides them, so a
/// renderer without a layer API draws the chosen layers. The copy is an incremental update; the original bytes are kept.
/// </summary>
public static class PdfLayers
{
    /// <summary>Gets the catalog key holding the optional content properties.</summary>
    private static ReadOnlySpan<byte> OCPropertiesKey => "OCProperties"u8;

    /// <summary>Reads the layers and whether each is shown by default.</summary>
    /// <param name="file">The PDF.</param>
    /// <returns>The layers; empty when there are none or the structure cannot be read.</returns>
    public static IReadOnlyList<DocumentLayer> Read(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        try
        {
            var structure = PdfReader.Read(file);
            var catalog = PdfReader.GetObject(structure, RootNumber(structure));
            var propertiesAt = PdfSyntax.FindKey(catalog.Span, 0, OCPropertiesKey);
            if (propertiesAt < 0)
            {
                return [];
            }

            var properties = PdfReader.Resolve(structure, catalog, propertiesAt);
            var groupsAt = PdfSyntax.FindKey(properties.Span, 0, "OCGs"u8);
            if (groupsAt < 0)
            {
                return [];
            }

            var configuration = DefaultConfiguration(structure, properties);
            var baseOff = !configuration.IsEmpty && ReadName(configuration.Span, "BaseState"u8) == "OFF";
            var on = ReadReferences(structure, configuration, "ON"u8);
            var off = ReadReferences(structure, configuration, "OFF"u8);
            var layers = new List<DocumentLayer>();
            foreach (var group in ReadReferences(structure, properties, "OCGs"u8))
            {
                var dictionary = PdfReader.GetObject(structure, group);
                var nameAt = PdfSyntax.FindKey(dictionary.Span, 0, "Name"u8);
                var name = nameAt < 0 ? string.Empty : PdfText.Decode(PdfReader.Resolve(structure, dictionary, nameAt).Span);
                var visible = baseOff ? on.Contains(group) : !off.Contains(group);
                layers.Add(new(group, string.IsNullOrWhiteSpace(name) ? string.Create(CultureInfo.CurrentCulture, $"Layer {layers.Count + 1}") : name, visible));
            }

            return layers;
        }
        catch (InvalidDataException)
        {
            return [];
        }
        catch (NotSupportedException)
        {
            return [];
        }
    }

    /// <summary>Writes a copy that shows the given layers and hides the rest.</summary>
    /// <param name="file">The PDF.</param>
    /// <param name="hidden">The ids of the layers to hide.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="InvalidDataException">The document has no layers or its structure cannot be read.</exception>
    public static byte[] WithHidden(byte[] file, IReadOnlyCollection<int> hidden)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(hidden);
        var structure = PdfReader.Read(file);
        var root = RootNumber(structure);
        var catalog = PdfReader.GetObject(structure, root);
        var propertiesAt = PdfSyntax.FindKey(catalog.Span, 0, OCPropertiesKey);
        if (propertiesAt < 0)
        {
            throw new InvalidDataException("The document has no layers.");
        }

        var properties = PdfReader.Resolve(structure, catalog, propertiesAt);
        var configurationAt = PdfSyntax.FindKey(properties.Span, 0, "D"u8);
        var configuration = configurationAt < 0 ? "<< >>"u8.ToArray() : PdfReader.Resolve(structure, properties, configurationAt).ToArray();
        var offList = new StringBuilder("[");
        foreach (var id in hidden)
        {
            _ = offList.Append(CultureInfo.InvariantCulture, $"{id} 0 R ");
        }

        var withoutOn = Encoding.Latin1.GetBytes(PdfEditing.RemoveKey(configuration, "ON"u8));
        var withoutOff = Encoding.Latin1.GetBytes(PdfEditing.RemoveKey(withoutOn, "OFF"u8));
        var cleaned = PdfEditing.RemoveKey(withoutOff, "BaseState"u8);
        var updated = PdfEditing.AddEntry(cleaned, $"/BaseState /ON /OFF {offList.ToString().TrimEnd()}]");
        var objects = new SortedDictionary<int, string>();
        if (configurationAt >= 0 && PdfSyntax.TryReadReference(properties.Span, configurationAt, out var configurationNumber))
        {
            objects[configurationNumber] = updated;
        }
        else
        {
            var newProperties = PdfEditing.AddEntry(PdfEditing.RemoveKey(properties.Span, "D"u8), $"/D {updated}");
            if (PdfSyntax.TryReadReference(catalog.Span, propertiesAt, out var propertiesNumber))
            {
                objects[propertiesNumber] = newProperties;
            }
            else
            {
                objects[root] = PdfEditing.AddEntry(PdfEditing.RemoveKey(catalog.Span, OCPropertiesKey), $"/OCProperties {newProperties}");
            }
        }

        _ = PdfSyntax.ReadLong(structure.Trailer, PdfSyntax.FindKey(structure.Trailer, 0, "Size"u8), out var size);
        var update = PdfUpdateWriter.Serialize(structure, objects, root, (int)size);
        var copy = new byte[file.Length + update.Length];
        file.CopyTo(copy, 0);
        update.CopyTo(copy, file.Length);
        return copy;
    }

    /// <summary>Gets the catalog's object number.</summary>
    /// <param name="structure">The structure.</param>
    /// <returns>The number.</returns>
    /// <exception cref="InvalidDataException">The trailer has no catalog.</exception>
    private static int RootNumber(PdfStructure structure)
    {
        var at = PdfSyntax.FindKey(structure.Trailer, 0, "Root"u8);
        return at >= 0 && PdfSyntax.TryReadReference(structure.Trailer, at, out var root) ? root : throw new InvalidDataException("The document has no catalog.");
    }

    /// <summary>Gets the default optional content configuration, or empty.</summary>
    /// <param name="structure">The structure.</param>
    /// <param name="properties">The optional content properties.</param>
    /// <returns>The configuration dictionary.</returns>
    private static ReadOnlyMemory<byte> DefaultConfiguration(PdfStructure structure, ReadOnlyMemory<byte> properties)
    {
        var at = PdfSyntax.FindKey(properties.Span, 0, "D"u8);
        return at < 0 ? default : PdfReader.Resolve(structure, properties, at);
    }

    /// <summary>Reads a name value, such as <c>/OFF</c>.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The name without its slash, or an empty string.</returns>
    private static string ReadName(ReadOnlySpan<byte> dictionary, ReadOnlySpan<byte> key)
    {
        var at = PdfSyntax.FindKey(dictionary, 0, key);
        return at < 0 || dictionary[at] != (byte)'/' ? string.Empty : Encoding.ASCII.GetString(dictionary[(at + 1)..PdfSyntax.TokenEnd(dictionary, at + 1)]);
    }

    /// <summary>Reads the references in an array held under a key.</summary>
    /// <param name="structure">The structure.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The referenced object numbers.</returns>
    private static HashSet<int> ReadReferences(PdfStructure structure, ReadOnlyMemory<byte> dictionary, ReadOnlySpan<byte> key)
    {
        var numbers = new HashSet<int>();
        var at = dictionary.IsEmpty ? -1 : PdfSyntax.FindKey(dictionary.Span, 0, key);
        if (at < 0)
        {
            return numbers;
        }

        var array = PdfReader.Resolve(structure, dictionary, at).Span;
        var index = PdfSyntax.SkipSpace(array, 1);
        while (index < array.Length && array[index] != (byte)']')
        {
            if (PdfSyntax.TryReadReference(array, index, out var number))
            {
                _ = numbers.Add(number);
            }

            var end = PdfSyntax.ValueEnd(array, index);
            index = PdfSyntax.SkipSpace(array, end > index ? end : index + 1);
        }

        return numbers;
    }
}
