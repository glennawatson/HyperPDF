// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Media;
using HyperPdf.Core.Text;
using HyperPdf.Core.Text.Fonts;

namespace HyperPdf.App.Views;

/// <summary>
/// The screen fonts that show text as it will be written. The built in PDF families are drawn with the installed fonts
/// that share their letter widths, so typed text lines up with how it is saved.
/// </summary>
internal static class PageFonts
{
    /// <summary>The fonts with Helvetica's widths, most likely first.</summary>
    private const string SansStack = "Helvetica, Arial, Liberation Sans, Arimo, Nimbus Sans, Helvetica Neue, DejaVu Sans";

    /// <summary>The fonts with Times' widths.</summary>
    private const string SerifStack = "Times New Roman, Times, Liberation Serif, Tinos, Nimbus Roman, DejaVu Serif";

    /// <summary>The fonts with Courier's widths.</summary>
    private const string MonoStack = "Courier New, Courier, Liberation Mono, Cousine, Nimbus Mono PS, DejaVu Sans Mono";

    /// <summary>The families made so far, by name.</summary>
    private static readonly Dictionary<string, FontFamily> Families = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Gets the screen font for a family. Call on the UI thread.</summary>
    /// <param name="family">The family, built in or installed.</param>
    /// <returns>The font family.</returns>
    internal static FontFamily Get(string? family)
    {
        var name = string.IsNullOrWhiteSpace(family) ? StandardFontFamilies.Sans : family;
        if (Families.TryGetValue(name, out var font))
        {
            return font;
        }

        font = new(Stack(name));
        Families[name] = font;
        return font;
    }

    /// <summary>Gets the name a family is listed by: a font that cannot be saved says which built in font it is saved as.</summary>
    /// <param name="family">The family.</param>
    /// <returns>The label.</returns>
    internal static string Label(string? family) =>
        string.IsNullOrEmpty(family) || !FontCatalog.IsSystemLoaded || FontCatalog.System.PreviewFace(family) is not { } face
            ? family ?? string.Empty
            : $"{family} (saved as {StandardFontFamilies.Closest(family, face.IsSerif, face.IsMonospace)})";

    /// <summary>Gets the font names tried for a family, in order.</summary>
    /// <param name="family">The family.</param>
    /// <returns>The names, comma separated.</returns>
    private static string Stack(string family)
    {
        if (string.Equals(family, StandardFontFamilies.Sans, StringComparison.OrdinalIgnoreCase))
        {
            return SansStack;
        }

        if (string.Equals(family, StandardFontFamilies.Serif, StringComparison.OrdinalIgnoreCase))
        {
            return SerifStack;
        }

        return string.Equals(family, StandardFontFamilies.Mono, StringComparison.OrdinalIgnoreCase) ? MonoStack : family;
    }
}
