// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Forms;

/// <summary>Wraps widget content in a Form XObject with its font resources.</summary>
internal static class FormAppearanceResources
{
    /// <summary>Gets the name an icon is drawn by in the appearance's <c>/XObject</c> resources.</summary>
    internal static ReadOnlySpan<byte> IconName => "Icon"u8;

    /// <summary>Wraps the drawn content in a Form XObject whose resources hold the font.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The widget.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="font">The font, or <see langword="null"/> when the content uses none.</param>
    /// <returns>The Form XObject.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfStream Finish(ref PdfContentBuilder builder, in AppearanceContext context, in FormFrame frame, FormFont? font) =>
        Finish(ref builder, context, frame, font is null ? [] : [font], default);

    /// <summary>Wraps the drawn content in a Form XObject whose resources hold the fonts and an icon.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The widget.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="fonts">The fonts the content uses.</param>
    /// <param name="icon">The icon form to draw as <see cref="IconName"/>, or <see langword="default"/> when the content draws none.</param>
    /// <returns>The Form XObject.</returns>
    internal static PdfStream Finish(ref PdfContentBuilder builder, in AppearanceContext context, in FormFrame frame, List<FormFont> fonts, PdfValue icon)
    {
        PdfDictionary? resources = null;
        if (fonts.Count > 0 || !icon.IsNull)
        {
            resources = new(context.Store);
        }

        if (fonts.Count > 0)
        {
            PdfDictionary fontResources = new(context.Store);
            foreach (var font in fonts)
            {
                fontResources.Set(context.Store.Names.Intern(font.Name), font.Resource);
            }

            resources!.Set(KnownName.Font, PdfValue.FromDictionary(fontResources));
        }

        if (!icon.IsNull)
        {
            PdfDictionary iconResources = new(context.Store);
            iconResources.Set(context.Store.Names.Intern(IconName), icon);
            resources!.Set(KnownName.XObject, PdfValue.FromDictionary(iconResources));
        }

        return builder.ToFormXObject(context.Store, new(0, 0, frame.Width, frame.Height), frame.GetMatrix(), resources);
    }
}
