// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Finds the font a form field draws its text with, from its default appearance string (<c>/DA</c>, ISO 32000-2
/// §12.7.4.3) and the form's default resources, so the conversion can tell whether redrawing the field would need a font
/// it cannot embed.
/// </summary>
internal static class PdfAConversionFieldFonts
{
    /// <summary>The most parent fields followed when looking for an inherited <c>/DA</c>.</summary>
    private const int MaxParents = 32;

    /// <summary>Gets the operator that sets the font in a default appearance string.</summary>
    private static ReadOnlySpan<byte> SetFont => "Tf"u8;

    /// <summary>
    /// Determines whether a field's text font is neither embedded nor one the conversion has an open font for, so a
    /// redrawn appearance would break the font rules.
    /// </summary>
    /// <param name="session">The conversion.</param>
    /// <param name="form">The interactive form dictionary.</param>
    /// <param name="widget">The field's widget.</param>
    /// <returns><see langword="true"/> when the font cannot be embedded.</returns>
    internal static bool CannotEmbedFont(PdfAConversionSession session, PdfDictionary form, ResolvedWidget widget)
    {
        var store = session.Store;
        var appearance = FindAppearance(store, widget, form);
        if (appearance.Length == 0 || FontName(appearance) is not { } name)
        {
            return false;
        }

        var fonts = StoreReading.Resolve(store, form.GetRaw(KnownName.DR)).AsDictionary()?.GetRaw(KnownName.Font) is { } raw
            ? StoreReading.Resolve(store, raw).AsDictionary()
            : null;
        if (fonts is null || StoreReading.Resolve(store, fonts.GetRaw(store.Names.Intern(name))).AsDictionary() is not { } font)
        {
            return false;
        }

        return PdfAFontEmbedding.IsNotEmbedded(font) && PdfAFontEmbedding.Substitutable(store, font) == StandardFont.None;
    }

    /// <summary>Finds a widget's default appearance string: its own, its field's or an ancestor's, then the form's.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="widget">The widget.</param>
    /// <param name="form">The interactive form dictionary.</param>
    /// <returns>The string's bytes, or empty.</returns>
    private static byte[] FindAppearance(PdfObjectStore store, ResolvedWidget widget, PdfDictionary form)
    {
        var own = widget.Widget.GetStringBytes(KnownName.DA);
        if (!own.IsEmpty)
        {
            return own.ToArray();
        }

        var field = widget.Field;
        for (var depth = 0; field is not null && depth < MaxParents; depth++)
        {
            var inherited = field.GetStringBytes(KnownName.DA);
            if (!inherited.IsEmpty)
            {
                return inherited.ToArray();
            }

            field = StoreReading.Resolve(store, field.GetRaw(KnownName.Parent)).AsDictionary();
        }

        return form.GetStringBytes(KnownName.DA).ToArray();
    }

    /// <summary>Reads the font resource name set by the last <c>Tf</c> in a default appearance string.</summary>
    /// <param name="appearance">The string's bytes, such as <c>/Helv 0 Tf 0 g</c>.</param>
    /// <returns>The name without its slash, or <see langword="null"/> when there is none.</returns>
    private static byte[]? FontName(ReadOnlySpan<byte> appearance)
    {
        var tf = appearance.LastIndexOf(SetFont);
        if (tf < 0)
        {
            return null;
        }

        // Before Tf come the name and the size: "/Name size Tf".
        var slash = appearance[..tf].LastIndexOf((byte)'/');
        if (slash < 0)
        {
            return null;
        }

        var end = slash + 1;
        while (end < tf && !PdfCharacters.EndsToken(appearance[end]))
        {
            end++;
        }

        return end > slash + 1 ? appearance[(slash + 1)..end].ToArray() : null;
    }
}
