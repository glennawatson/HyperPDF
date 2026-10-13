// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>What is needed to draw one widget's appearance.</summary>
/// <param name="Store">The document's objects.</param>
/// <param name="Widget">The widget dictionary.</param>
/// <param name="Field">The field dictionary the widget belongs to.</param>
/// <param name="Type">The field type.</param>
/// <param name="Flags">The field flags.</param>
/// <param name="Form">The AcroForm dictionary, or <see langword="null"/>.</param>
/// <param name="PageResources">The resources of the widget's page, or <see langword="null"/>.</param>
[DebuggerDisplay("AppearanceContext: {Type}")]
internal readonly record struct AppearanceContext(
    PdfObjectStore Store,
    PdfDictionary Widget,
    PdfDictionary Field,
    PdfFieldType Type,
    PdfFieldFlags Flags,
    PdfDictionary? Form,
    PdfDictionary? PageResources)
{
    /// <summary>Gets the document's loaded fonts, which encode and measure text in the field's own font; <see langword="null"/> measures with the font's widths and WinAnsi.</summary>
    internal PdfFontCache? Fonts { get; init; }

    /// <summary>Gets the token that cancels drawing; drawing loops check it for every row, line and word.</summary>
    internal CancellationToken CancellationToken { get; init; }

    /// <summary>Gets the widget's <c>/Rect</c>.</summary>
    internal PdfRectangle Rect => Widget.TryGetRectangle(KnownName.Rect, out var rect) ? rect : default;

    /// <summary>Gets the widget's appearance characteristics (<c>/MK</c>).</summary>
    internal PdfDictionary? Characteristics => Widget.GetDictionary(KnownName.MK);

    /// <summary>Gets the widget's default appearance.</summary>
    internal DefaultAppearance DefaultAppearance => DefaultAppearance.Find(Widget, Field, Form);

    /// <summary>Gets the text alignment: 0 left, 1 centred, 2 right.</summary>
    internal int Alignment
    {
        get
        {
            if (Widget.ContainsKey(KnownName.Q))
            {
                return Widget.GetInt32(KnownName.Q);
            }

            var inherited = FieldAttributes.Find(Field, KnownName.Q);
            return !inherited.IsNull ? inherited.AsInt32() : Form?.GetInt32(KnownName.Q) ?? 0;
        }
    }

    /// <summary>Gets the most characters the field takes, or 0.</summary>
    internal int MaxLength => Math.Max(0, FieldAttributes.Find(Field, KnownName.MaxLen).AsInt32());
}
