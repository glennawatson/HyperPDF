// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// How a push button's icon is fitted to the space it has: the icon fit dictionary <c>/IF</c> of the widget's appearance
/// characteristics (ISO 32000-2 12.5.6.19, Table 190).
/// </summary>
/// <param name="When">When the icon is scaled (<c>/SW</c>).</param>
/// <param name="Proportional">Whether the icon keeps its proportions (<c>/S</c> is <c>P</c>, the default) or is stretched to fill the space (<c>A</c>).</param>
/// <param name="AlignX">How far across the leftover width the icon sits, from 0 (left edge) to 1 (right edge) (<c>/A</c>).</param>
/// <param name="AlignY">How far up the leftover height the icon sits, from 0 (bottom edge) to 1 (top edge).</param>
/// <param name="FitToBounds">Whether the border width is ignored when fitting the icon (<c>/FB</c>).</param>
[DebuggerDisplay("FormIconFit: {When} proportional={Proportional}")]
internal readonly record struct FormIconFit(FormIconScale When, bool Proportional, float AlignX, float AlignY, bool FitToBounds)
{
    /// <summary>The place of the vertical alignment in <c>/A</c>.</summary>
    private const int AlignYSlot = 1;

    /// <summary>The alignment that centres the icon.</summary>
    private const float Centered = 0.5F;

    /// <summary>Gets the fit a widget gets without an icon fit dictionary: always scaled, proportional and centred.</summary>
    internal static FormIconFit Default => new(FormIconScale.Always, true, Centered, Centered, false);

    /// <summary>Reads the icon fit of a widget.</summary>
    /// <param name="characteristics">The widget's <c>/MK</c>, or <see langword="null"/>.</param>
    /// <param name="names">The document's names.</param>
    /// <returns>The fit.</returns>
    internal static FormIconFit Read(PdfDictionary? characteristics, PdfNameTable names)
    {
        if (characteristics?.GetDictionary(names.Intern("IF"u8)) is not { } fit)
        {
            return Default;
        }

        var align = fit.GetArray(KnownName.A);
        return new(
            ReadScale(fit.GetName(names.Intern("SW"u8))),
            !fit.GetName(KnownName.S).Is(KnownName.A),
            ReadFraction(align, 0),
            ReadFraction(align, AlignYSlot),
            fit.GetBoolean(names.Intern("FB"u8)));
    }

    /// <summary>Works out how big the icon is drawn and where, in a space.</summary>
    /// <param name="iconWidth">The icon's width.</param>
    /// <param name="iconHeight">The icon's height.</param>
    /// <param name="spaceWidth">The space's width.</param>
    /// <param name="spaceHeight">The space's height.</param>
    /// <returns>The scale and the offset of the icon's lower left corner from the space's lower left corner.</returns>
    internal FormIconPlacement Place(float iconWidth, float iconHeight, float spaceWidth, float spaceHeight)
    {
        if (iconWidth <= 0 || iconHeight <= 0 || spaceWidth <= 0 || spaceHeight <= 0)
        {
            return new(1, 1, 0, 0);
        }

        var scaleX = spaceWidth / iconWidth;
        var scaleY = spaceHeight / iconHeight;
        if (!ShouldScale(iconWidth, iconHeight, spaceWidth, spaceHeight))
        {
            scaleX = 1;
            scaleY = 1;
        }
        else if (Proportional)
        {
            scaleX = Math.Min(scaleX, scaleY);
            scaleY = scaleX;
        }

        return new(scaleX, scaleY, (spaceWidth - (iconWidth * scaleX)) * AlignX, (spaceHeight - (iconHeight * scaleY)) * AlignY);
    }

    /// <summary>Reads the <c>/SW</c> name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>When to scale; always when missing.</returns>
    private static FormIconScale ReadScale(PdfName name)
    {
        if (name.Is(KnownName.B))
        {
            return FormIconScale.WhenTooBig;
        }

        if (name.Is(KnownName.S))
        {
            return FormIconScale.WhenTooSmall;
        }

        return name.Is(KnownName.N) ? FormIconScale.Never : FormIconScale.Always;
    }

    /// <summary>Reads one number of the <c>/A</c> array.</summary>
    /// <param name="align">The array, or <see langword="null"/>.</param>
    /// <param name="slot">The place of the number.</param>
    /// <returns>The fraction, from 0 to 1; centred when missing.</returns>
    private static float ReadFraction(PdfArray? align, int slot) => align is not null && slot < align.Count ? Math.Clamp(align.GetSingle(slot), 0, 1) : Centered;

    /// <summary>Determines whether the icon is scaled to the space.</summary>
    /// <param name="iconWidth">The icon's width.</param>
    /// <param name="iconHeight">The icon's height.</param>
    /// <param name="spaceWidth">The space's width.</param>
    /// <param name="spaceHeight">The space's height.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private bool ShouldScale(float iconWidth, float iconHeight, float spaceWidth, float spaceHeight) => When switch
    {
        FormIconScale.Always => true,
        FormIconScale.WhenTooBig => iconWidth > spaceWidth || iconHeight > spaceHeight,
        FormIconScale.WhenTooSmall => iconWidth < spaceWidth && iconHeight < spaceHeight,
        _ => false,
    };
}
