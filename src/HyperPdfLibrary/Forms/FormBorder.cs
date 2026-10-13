// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>How a widget's border is drawn.</summary>
/// <param name="Width">The line width; 0 when the widget has no border colour, so nothing is drawn; a beveled or inset border needs no colour.</param>
/// <param name="Style">The line style.</param>
/// <param name="Dash">The dash length of a dashed line.</param>
[DebuggerDisplay("FormBorder: {Style} {Width}")]
internal readonly record struct FormBorder(float Width, FormBorderStyle Style, float Dash)
{
    /// <summary>The line width when a widget does not give one.</summary>
    private const float DefaultWidth = 1;

    /// <summary>The dash length when a widget does not give one.</summary>
    private const float DefaultDash = 3;

    /// <summary>The position of the width in a <c>/Border</c> array.</summary>
    private const int BorderWidthSlot = 2;

    /// <summary>Reads a widget's border.</summary>
    /// <param name="widget">The widget dictionary.</param>
    /// <param name="hasColor">Whether the widget's <c>/MK</c> has a border colour.</param>
    /// <returns>The border.</returns>
    internal static FormBorder Read(PdfDictionary widget, bool hasColor)
    {
        var style = widget.GetDictionary(KnownName.BS);
        var width = ReadWidth(widget, style);
        var dash = style?.GetArray(KnownName.D) is { Count: > 0 } dashes ? dashes.GetSingle(0) : DefaultDash;
        var kind = ReadStyle(style);

        // A beveled or inset border shades its own edge, so it needs no border colour; the other styles draw nothing without one.
        var raised = kind is FormBorderStyle.Beveled or FormBorderStyle.Inset;
        return new(width > 0 && (hasColor || raised) ? width : 0, kind, dash > 0 ? dash : DefaultDash);
    }

    /// <summary>Reads the line width: <c>/BS /W</c>, else the third entry of <c>/Border</c>, else 1.</summary>
    /// <param name="widget">The widget dictionary.</param>
    /// <param name="style">The <c>/BS</c> dictionary, or <see langword="null"/>.</param>
    /// <returns>The width.</returns>
    private static float ReadWidth(PdfDictionary widget, PdfDictionary? style)
    {
        if (style is not null && style.ContainsKey(KnownName.W))
        {
            return style.GetSingle(KnownName.W);
        }

        return widget.GetArray(KnownName.Border) is { } border && border.Count > BorderWidthSlot ? border.GetSingle(BorderWidthSlot) : DefaultWidth;
    }

    /// <summary>Reads the style name of a border style dictionary.</summary>
    /// <param name="style">The <c>/BS</c> dictionary, or <see langword="null"/>.</param>
    /// <returns>The style; solid when missing.</returns>
    private static FormBorderStyle ReadStyle(PdfDictionary? style)
    {
        if (style is null)
        {
            return FormBorderStyle.Solid;
        }

        var name = style.GetName(KnownName.S);
        if (name.Is(KnownName.D))
        {
            return FormBorderStyle.Dashed;
        }

        if (name.Is(KnownName.U))
        {
            return FormBorderStyle.Underline;
        }

        if (name.Is(KnownName.B))
        {
            return FormBorderStyle.Beveled;
        }

        return name.Is(KnownName.I) ? FormBorderStyle.Inset : FormBorderStyle.Solid;
    }
}
