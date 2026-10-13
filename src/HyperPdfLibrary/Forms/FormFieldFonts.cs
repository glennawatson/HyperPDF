// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Builds default appearance strings (<c>/DA</c>, ISO 32000-2 12.7.3.3) and finds the fonts they name in the form's default resources.</summary>
internal static class FormFieldFonts
{
    /// <summary>The size used for choice fields and multiline text, where fitting the text would make it unreadable.</summary>
    internal const float ReadableSize = 12;

    /// <summary>The default appearance of a field that draws no text: black.</summary>
    internal const string ColorOnly = "0 g";

    /// <summary>Builds the default appearance of a new field.</summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The string.</returns>
    internal static string Build(PdfFieldDefinition definition)
    {
        if (definition.DefaultAppearance is { Length: > 0 } given)
        {
            return given;
        }

        if (definition.Type is PdfFieldType.CheckBox or PdfFieldType.RadioButton or PdfFieldType.Signature)
        {
            return ColorOnly;
        }

        var multiline = (definition.Flags & PdfFieldFlags.Multiline) != 0;
        var fallback = multiline || definition.Type is PdfFieldType.ComboBox or PdfFieldType.ListBox ? ReadableSize : 0;
        return string.Create(CultureInfo.InvariantCulture, $"/{FormFont.FallbackName} {definition.FontSize ?? fallback:0.####} Tf {ColorOnly}");
    }

    /// <summary>Finds the font a default appearance names in the form's default resources.</summary>
    /// <param name="document">The document.</param>
    /// <param name="appearance">The default appearance string, or <see langword="null"/> for the one the library builds.</param>
    /// <returns>The font dictionary; <see langword="null"/> when the string names no font or the resources lack it.</returns>
    internal static PdfDictionary? FindInForm(PdfDocument document, string? appearance)
    {
        var name = appearance is null ? FormFont.FallbackName : DefaultAppearance.Parse(appearance).FontName;
        if (name.Length == 0)
        {
            return null;
        }

        var fonts = document.Catalog.GetDictionary(KnownName.AcroForm)?.GetDictionary(KnownName.DR)?.GetDictionary(KnownName.Font);
        return fonts?.GetDictionary(document.Objects.Names.Intern(name));
    }

    /// <summary>Determines whether a default appearance names a font that is embedded in the file.</summary>
    /// <param name="document">The document.</param>
    /// <param name="appearance">The default appearance string, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the form's resources hold the font and it carries its program.</returns>
    internal static bool IsEmbeddedInForm(PdfDocument document, string? appearance) =>
        appearance is not null && FindInForm(document, appearance) is { } font && IsEmbedded(font);

    /// <summary>Determines whether a font dictionary carries its font program.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <returns><see langword="true"/> when its descriptor holds a font file.</returns>
    internal static bool IsEmbedded(PdfDictionary font)
    {
        var target = font.IsName(KnownName.Subtype, KnownName.Type0) ? font.GetArray(KnownName.DescendantFonts)?.GetDictionary(0) : font;
        var descriptor = target?.GetDictionary(KnownName.FontDescriptor);
        return descriptor is not null
            && (descriptor.ContainsKey(KnownName.FontFile) || descriptor.ContainsKey(KnownName.FontFile2) || descriptor.ContainsKey(KnownName.FontFile3));
    }

    /// <summary>Checks that a default appearance given by the caller names a font the form has.</summary>
    /// <param name="document">The document.</param>
    /// <param name="appearance">The default appearance string.</param>
    /// <exception cref="ArgumentException">The string names a font that is not in the form's default resources.</exception>
    internal static void RequireFont(PdfDocument document, string appearance)
    {
        var name = DefaultAppearance.Parse(appearance).FontName;
        if (name.Length > 0 && name != FormFont.FallbackName && FindInForm(document, appearance) is null)
        {
            throw new ArgumentException($"The default appearance names the font '{name}', which is not in the form's default resources (/DR).", nameof(appearance));
        }
    }
}
