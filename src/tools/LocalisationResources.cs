// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using System.Xml;

namespace HyperPdf.Scripts;

/// <summary>Generates deterministic RESX files from the application's translation catalogs.</summary>
internal static class LocalisationResources
{
    /// <summary>Generates the English catalog and each available supported translation.</summary>
    /// <param name="catalogDirectory">The directory containing JSON catalogs and fragments.</param>
    /// <param name="resourcesDirectory">The existing resource output directory.</param>
    /// <param name="requireComplete">Whether every supported language and key must be translated.</param>
    /// <returns>The number of source resource keys.</returns>
    /// <exception cref="InvalidDataException">A catalog is incomplete or contains a non-string value.</exception>
    internal static int Generate(string catalogDirectory, string resourcesDirectory, bool requireComplete)
    {
        var files = Directory.GetFiles(catalogDirectory, "catalog-*.json");
        Array.Sort(files, StringComparer.Ordinal);
        var catalog = ReadCatalogs(files);
        var keys = new string[catalog.Count];
        catalog.Keys.CopyTo(keys, 0);
        Array.Sort(keys, StringComparer.Ordinal);
        var cultures = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal) { [string.Empty] = catalog };
        foreach (var culture in new[] { "fr", "de", "zh-Hans", "ar", "es", "pt" })
        {
            var translated = ReadTranslations(catalogDirectory, culture, keys, requireComplete);
            if (translated is not null)
            {
                cultures.Add(culture, translated);
            }
        }

        // Validate all catalogs before replacing any checked-in resource files.
        foreach (var (culture, translated) in cultures)
        {
            var suffix = culture.Length == 0 ? string.Empty : $".{culture}";
            WriteResources(Path.Combine(resourcesDirectory, $"Strings{suffix}.resx"), keys, catalog, translated);
        }

        return keys.Length;
    }

    /// <summary>Reads a language's base catalog followed by its ordered override fragments.</summary>
    /// <param name="directory">The catalog directory.</param>
    /// <param name="culture">The language identifier.</param>
    /// <param name="keys">The required source keys.</param>
    /// <param name="requireComplete">Whether missing catalogs or keys are errors.</param>
    /// <returns>The translations, or null when the base catalog is unavailable.</returns>
    /// <exception cref="InvalidDataException">A required catalog or key is absent.</exception>
    private static Dictionary<string, string>? ReadTranslations(string directory, string culture, string[] keys, bool requireComplete)
    {
        var path = Path.Combine(directory, $"translations.{culture}.json");
        if (!File.Exists(path))
        {
            return requireComplete ? throw new InvalidDataException($"Missing translation catalog for {culture}.") : null;
        }

        var fragments = Directory.GetFiles(directory, $"translations.{culture}.*.json");
        Array.Sort(fragments, StringComparer.CurrentCultureIgnoreCase);
        var paths = new List<string> { path };
        paths.AddRange(fragments);
        var translated = ReadCatalogs(paths);
        if (requireComplete)
        {
            foreach (var key in keys)
            {
                if (!translated.ContainsKey(key))
                {
                    throw new InvalidDataException($"Missing {culture} translation: {key}");
                }
            }
        }

        return translated;
    }

    /// <summary>Merges JSON objects using case-sensitive keys and last-file-wins overrides.</summary>
    /// <param name="paths">The ordered catalog paths.</param>
    /// <returns>The merged resource values.</returns>
    /// <exception cref="InvalidDataException">A resource value is not a string or null.</exception>
    private static Dictionary<string, string> ReadCatalogs(IEnumerable<string> paths)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                {
                    throw new InvalidDataException($"Non-string resource value in {path}: {property.Name}");
                }

                result[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return result;
    }

    /// <summary>Writes ordinal keys as BOM-free UTF-8 XML with preserved whitespace and LF endings.</summary>
    /// <param name="path">The destination RESX path.</param>
    /// <param name="keys">The ordinal source keys.</param>
    /// <param name="catalog">The English fallback values.</param>
    /// <param name="translated">The language's overrides.</param>
    private static void WriteResources(string path, string[] keys, Dictionary<string, string> catalog, Dictionary<string, string> translated)
    {
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false), NewLineChars = "\n" };
        using var writer = XmlWriter.Create(path, settings);
        writer.WriteStartDocument();
        writer.WriteStartElement("root");
        foreach (var key in keys)
        {
            writer.WriteStartElement("data");
            writer.WriteAttributeString("name", key);
            writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
            writer.WriteElementString("value", translated.GetValueOrDefault(key, catalog[key]));
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }
}
