// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Metadata;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Projects parsed XMP properties into the report's small metadata value.</summary>
internal static class AccessibilityMetadataReader
{
    /// <summary>The declared PDF/UA part's expanded XMP key.</summary>
    private const string PartKey = "{http://www.aiim.org/pdfua/ns/id/}part";

    /// <summary>The declared PDF/UA revision's expanded XMP key.</summary>
    private const string RevisionKey = "{http://www.aiim.org/pdfua/ns/id/}rev";

    /// <summary>The declared PDF/UA amendment's expanded XMP key (ISO 14289-1, clause 5).</summary>
    private const string AmendmentKey = "{http://www.aiim.org/pdfua/ns/id/}amd";

    /// <summary>The declared PDF/UA technical corrigendum's expanded XMP key (ISO 14289-1, clause 5).</summary>
    private const string CorrigendumKey = "{http://www.aiim.org/pdfua/ns/id/}corr";

    /// <summary>The Dublin Core title's expanded XMP key.</summary>
    private const string TitleKey = "{http://purl.org/dc/elements/1.1/}title";

    /// <summary>Reads already parsed values without building property keys or a retained object.</summary>
    /// <param name="xmp">The parsed packet, or null when none is readable.</param>
    /// <returns>The report's metadata values.</returns>
    internal static AccessibilityMetadata Read(XmpMetadata? xmp)
    {
        if (xmp is null)
        {
            return default;
        }

        int? part = int.TryParse(Value(xmp, PartKey), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
        return new(true, !string.IsNullOrWhiteSpace(Value(xmp, TitleKey)), part, NonEmpty(Value(xmp, RevisionKey)), NonEmpty(Value(xmp, AmendmentKey)), NonEmpty(Value(xmp, CorrigendumKey)));
    }

    /// <summary>Treats an empty value as missing.</summary>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    /// <returns>The value, or <see langword="null"/> when it is missing or empty.</returns>
    private static string? NonEmpty(string? value) => value is { Length: > 0 } ? value : null;

    /// <summary>Gets a property's first value using its constant expanded key.</summary>
    /// <param name="xmp">The parsed packet.</param>
    /// <param name="key">The expanded property key.</param>
    /// <returns>The first value, or null when absent or empty.</returns>
    private static string? Value(XmpMetadata xmp, string key) =>
        xmp.Properties.TryGetValue(key, out var values) && values.Length != 0 ? values[0] : null;
}
