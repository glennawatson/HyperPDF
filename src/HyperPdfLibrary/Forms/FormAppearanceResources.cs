// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Forms;

/// <summary>Wraps widget content in a Form XObject with its font resources.</summary>
internal static class FormAppearanceResources
{
    /// <summary>The entries in a matrix.</summary>
    private const int MatrixLength = 6;

    /// <summary>Gets the name an icon is drawn by in the appearance's <c>/XObject</c> resources.</summary>
    internal static ReadOnlySpan<byte> IconName => "Icon"u8;

    /// <summary>Wraps the drawn content in a Form XObject whose resources hold the font.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The widget.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="font">The font, or <see langword="null"/> when the content uses none.</param>
    /// <returns>The Form XObject.</returns>
    internal static PdfStream Finish(ref PdfContentBuilder builder, in AppearanceContext context, in FormFrame frame, FormFont? font)
    {
        Span<float> matrix = stackalloc float[MatrixLength];
        return builder.ToFormXObject(context.Store, new(0, 0, frame.Width, frame.Height), frame.GetMatrix(matrix), font?.GetResources(context.Store));
    }

    /// <summary>Wraps the drawn content in a Form XObject whose resources hold a font and an icon.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The widget.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="font">The font, or <see langword="null"/> when the content uses none.</param>
    /// <param name="icon">The icon form to draw as <see cref="IconName"/>, or <see langword="default"/> when the content draws none.</param>
    /// <returns>The Form XObject.</returns>
    internal static PdfStream Finish(ref PdfContentBuilder builder, in AppearanceContext context, in FormFrame frame, FormFont? font, PdfValue icon)
    {
        if (icon.IsNull)
        {
            return Finish(ref builder, context, frame, font);
        }

        ReadOnlySpan<FormFont> fonts = font is null ? [] : [font];
        var resources = context.Fonts?.GetAppearanceResources(context.Store, fonts, icon) ?? CreateResources(context.Store, fonts, icon);
        return FinishWith(ref builder, context, frame, resources);
    }

    /// <summary>Wraps the drawn content in a Form XObject whose resources hold the fonts.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The widget.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="fonts">The fonts the content uses.</param>
    /// <returns>The Form XObject.</returns>
    internal static PdfStream Finish(ref PdfContentBuilder builder, in AppearanceContext context, in FormFrame frame, ReadOnlySpan<FormFont> fonts)
    {
        if (fonts.IsEmpty)
        {
            return Finish(ref builder, context, frame, (FormFont?)null);
        }

        if (fonts.Length == 1)
        {
            return Finish(ref builder, context, frame, fonts[0]);
        }

        var resources = context.Fonts?.GetAppearanceResources(context.Store, fonts, default) ?? CreateResources(context.Store, fonts, default);
        return FinishWith(ref builder, context, frame, resources);
    }

    /// <summary>Creates resources for a set of fonts and an optional icon.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="fonts">The fonts.</param>
    /// <param name="icon">The icon, or no value.</param>
    /// <returns>The dictionary.</returns>
    internal static PdfDictionary CreateResources(PdfObjectStore store, ReadOnlySpan<FormFont> fonts, PdfValue icon)
    {
        var resources = new PdfDictionary(store);
        if (!fonts.IsEmpty)
        {
            var fontResources = new PdfDictionary(store, fonts.Length);
            foreach (var font in fonts)
            {
                fontResources.Set(font.Key, font.Resource);
            }

            resources.Set(KnownName.Font, PdfValue.FromDictionary(fontResources));
        }

        if (!icon.IsNull)
        {
            var icons = new PdfDictionary(store, 1);
            icons.Set(store.Names.Intern(IconName), icon);
            resources.Set(KnownName.XObject, PdfValue.FromDictionary(icons));
        }

        return resources;
    }

    /// <summary>Wraps the content with its prepared resources.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The widget.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="resources">The resources so far.</param>
    /// <returns>The Form XObject.</returns>
    private static PdfStream FinishWith(ref PdfContentBuilder builder, in AppearanceContext context, in FormFrame frame, PdfDictionary resources)
    {
        Span<float> matrix = stackalloc float[MatrixLength];
        return builder.ToFormXObject(context.Store, new(0, 0, frame.Width, frame.Height), frame.GetMatrix(matrix), resources);
    }
}
