// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Draws the placeholder of an unsigned signature field: its border and background, a line to sign on and a cross at the
/// start of the line. It uses no font, so it is valid in a document that claims PDF/A.
/// </summary>
internal static class FormSignatureAppearance
{
    /// <summary>The height of the signing line as a fraction of the box.</summary>
    private const float LineHeight = 0.3F;

    /// <summary>The width of the line and cross strokes.</summary>
    private const float StrokeWidth = 1;

    /// <summary>The cross's size as a fraction of the box height.</summary>
    private const float CrossFraction = 0.25F;

    /// <summary>The largest size of the cross in points.</summary>
    private const float MaxCross = 10;

    /// <summary>Creates the placeholder appearance.</summary>
    /// <param name="context">The widget.</param>
    /// <returns>The Form XObject.</returns>
    internal static PdfStream Create(in AppearanceContext context)
    {
        var frame = FormFrame.Create(context);
        var builder = default(PdfContentBuilder);
        try
        {
            FormBorderAppearance.Paint(ref builder, frame, context.Characteristics, false);
            var pad = frame.Padding + frame.Padding;
            var baseline = frame.Height * LineHeight;
            var cross = Math.Min(MaxCross, frame.Height * CrossFraction);
            if (frame.Width > pad + pad && frame.Height > pad + pad)
            {
                builder.SaveState();
                builder.WriteRaw("0.4 G\n"u8);
                builder.SetLineWidth(StrokeWidth);
                builder.MoveTo(pad, baseline);
                builder.LineTo(frame.Width - pad, baseline);
                builder.MoveTo(pad, baseline + cross);
                builder.LineTo(pad + cross, baseline + cross + cross);
                builder.MoveTo(pad, baseline + cross + cross);
                builder.LineTo(pad + cross, baseline + cross);
                builder.Stroke();
                builder.RestoreState();
            }

            return FormAppearanceResources.Finish(ref builder, context, frame, null);
        }
        finally
        {
            builder.Dispose();
        }
    }
}
