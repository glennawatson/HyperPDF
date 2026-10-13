// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Text;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// A free text annotation's frame (ISO 32000-2 12.5.6.6): the <c>/C</c> background and a border in the text colour of
/// <c>/DA</c>. <see cref="Create"/> adds the <c>/Contents</c> laid out with the <c>/DA</c> font and size by the form text layout.
/// </summary>
/// <param name="Rect">The annotation rectangle, which the appearance is fitted to.</param>
/// <param name="Frame">The rectangle the background and border are drawn in: <c>/Rect</c> pulled in by half the border.</param>
/// <param name="Background">The background colour, or none.</param>
/// <param name="Border">The border width and dash.</param>
/// <param name="StrokeColor">The text colour as a stroking colour operator, such as "0 G".</param>
[DebuggerDisplay("FreeTextAppearance: {Rect}")]
internal readonly record struct FreeTextAppearance(PdfRectangle Rect, PdfRectangle Frame, PaintColor Background, PaintStroke Border, string StrokeColor) : IAppearancePlan<FreeTextAppearance>
{
    /// <inheritdoc/>
    public PdfRectangle Bounds => Rect;

    /// <inheritdoc/>
    public KnownName Blend => KnownName.Normal;

    /// <inheritdoc/>
    public static bool TryRead(PdfDictionary annotation, out FreeTextAppearance plan)
    {
        var rect = AppearanceRects.Read(annotation);
        var border = PaintStroke.Read(annotation);
        var form = annotation.Owner is { } owner ? owner.Catalog.GetDictionary(KnownName.AcroForm) : null;
        var text = DefaultAppearance.Find(annotation, annotation, form);
        plan = new(rect, AppearanceRects.Inset(rect, border.Width * AppearanceRects.Half), PaintColor.Read(annotation.GetArray(KnownName.C), PaintColor.None), border, text.StrokeColor);
        return true;
    }

    /// <inheritdoc/>
    public void Emit(ref PdfContentBuilder builder)
    {
        if (Background.IsSet)
        {
            Background.SetFill(ref builder);
            builder.Rectangle(Frame.Left, Frame.Bottom, Frame.Width, Frame.Height);
            builder.Fill();
        }

        if (Border.Width <= 0)
        {
            return;
        }

        WriteOperator(ref builder, StrokeColor);
        Border.Apply(ref builder);
        builder.Rectangle(Frame.Left, Frame.Bottom, Frame.Width, Frame.Height);
        builder.Stroke();
    }

    /// <summary>Generates the frame and the text laid out over it.</summary>
    /// <param name="context">The annotation.</param>
    /// <returns>The appearance.</returns>
    internal static GeneratedAppearance? Create(in AnnotationContext context)
    {
        var frame = AnnotationAppearances.Build<FreeTextAppearance>(context);
        if (frame is null)
        {
            return null;
        }

        var annotation = context.Annotation;
        var form = context.Store.Catalog.GetDictionary(KnownName.AcroForm);
        var widget = new AppearanceContext(context.Store, annotation, annotation, PdfFieldType.Text, PdfFieldFlags.Multiline, form, context.Page.Resources) { Fonts = context.Cache.Fonts };
        var text = FormTextAppearance.CreateText(widget, annotation.GetText(KnownName.Contents) ?? string.Empty);
        return frame.Value with { Overlay = text };
    }

    /// <summary>Writes an operator the default appearance string holds, followed by a line break.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="text">The operator, in Latin-1 characters.</param>
    private static void WriteOperator(ref PdfContentBuilder builder, string text)
    {
        // The builder may keep the span it is given, so the bytes live in a pooled array rather than on the stack.
        var rented = ArrayPool<byte>.Shared.Rent(text.Length + 1);
        try
        {
            var written = Encoding.Latin1.GetBytes(text, rented);
            rented[written] = (byte)'\n';
            builder.WriteRaw(rented.AsSpan(0, written + 1));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
