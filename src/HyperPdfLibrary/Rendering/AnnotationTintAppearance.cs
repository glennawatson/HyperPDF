// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>Draws tint over fillable form widgets.</summary>
internal static class AnnotationTintAppearance
{
    /// <summary>The permission bits that let a reader fill in a form: modify contents, modify annotations and fill form fields.</summary>
    private const int FillingPermissions = 0x8 | 0x20 | 0x100;

    /// <summary>The bits to shift a colour channel down by to read the red.</summary>
    private const int RedShift = 16;

    /// <summary>The bits to shift a colour channel down by to read the green.</summary>
    private const int GreenShift = 8;

    /// <summary>The largest value of an 8-bit channel, as a float.</summary>
    private const float ChannelMaximum = 255;

    /// <summary>
    /// Determines whether the form field tint covers a widget: the field has a
    /// type, is not read-only, is not a push button, and the document's permissions (ISO 32000-2 table 22) allow filling or editing.
    /// </summary>
    /// <param name="context">The widget.</param>
    /// <returns><see langword="true"/> when the widget is tinted.</returns>
    internal static bool IsTinted(in AnnotationContext context)
    {
        var widget = context.Annotation;
        var field = !widget.ContainsKey(KnownName.T) && widget.GetDictionary(KnownName.Parent) is { } parent ? parent : widget;
        var source = FieldAttributes.Find(field, KnownName.FT).IsNull && widget.ContainsKey(KnownName.FT) ? widget : field;
        var flags = FieldAttributes.GetFlags(source);
        var type = FormValues.GetType(source, context.Store.Names, flags);
        return type is not (PdfFieldType.Unknown or PdfFieldType.PushButton)
            && (flags & PdfFieldFlags.ReadOnly) == 0
            && (context.Store.Security is not { } security || (security.Permissions & FillingPermissions) != 0);
    }

    /// <summary>Draws the tint of one widget: its rectangle filled with the colour at the opacity.</summary>
    /// <param name="context">The widget.</param>
    /// <param name="rect">The widget's rectangle.</param>
    /// <param name="tint">The tint.</param>
    /// <returns>The Form XObject.</returns>
    internal static PdfStream CreateTint(in AnnotationContext context, PdfRectangle rect, PdfFormHighlight tint)
    {
        var store = context.Store;
        var state = new PdfDictionary(store);
        state.Set(KnownName.Type, PdfValue.FromName(KnownName.ExtGState));
        state.Set(context.Cache.LowerCa, PdfValue.FromReal(tint.Alpha / ChannelMaximum));
        var states = new PdfDictionary(store);
        states.Set(context.Cache.GraphicsStateName, PdfValue.FromDictionary(state));
        var resources = new PdfDictionary(store);
        resources.Set(KnownName.ExtGState, PdfValue.FromDictionary(states));
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SetGraphicsState("GS"u8);
            builder.SetFillRgb(
                ((tint.Rgb >> RedShift) & byte.MaxValue) / ChannelMaximum,
                ((tint.Rgb >> GreenShift) & byte.MaxValue) / ChannelMaximum,
                (tint.Rgb & byte.MaxValue) / ChannelMaximum);
            builder.Rectangle(rect.Left, rect.Bottom, rect.Width, rect.Height);
            builder.Fill();
            return AnnotationAppearances.CreateForm(builder.ToArray(), context, rect, resources);
        }
        finally
        {
            builder.Dispose();
        }
    }
}
