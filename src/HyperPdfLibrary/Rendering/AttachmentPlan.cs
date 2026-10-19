// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// A file attachment annotation drawn as an icon (ISO 32000-2 12.5.6.15): a push pin, graph, paper clip or tag named by
/// <c>/Name</c>. Like a note, the icon is at most <see cref="NotePlan.MaxSide"/> points square and anchored at the upper-left
/// corner of <c>/Rect</c>, shrinking to fit a smaller rectangle.
/// </summary>
/// <param name="Bounds">The icon's rectangle.</param>
/// <param name="Icon">The icon.</param>
[DebuggerDisplay("AttachmentPlan: {Bounds} {Icon}")]
internal readonly record struct AttachmentPlan(PdfRectangle Bounds, AttachmentIcon Icon) : IAppearancePlan<AttachmentPlan>
{
    /// <summary>The red of the push pin's head.</summary>
    private static readonly IconFill Red = new(.85F, .1F, .1F);

    /// <summary>The blue of the graph's bars.</summary>
    private static readonly IconFill Blue = new(.25F, .45F, .85F);

    /// <summary>The paper colour of the tag.</summary>
    private static readonly IconFill Manila = new(1, .9F, .4F);

    /// <summary>The grey of the paper clip, which has no body to fill.</summary>
    private static readonly IconFill Grey = new(.4F, .4F, .4F);

    /// <inheritdoc/>
    public KnownName Blend => KnownName.Normal;

    /// <summary>Gets the push pin's body: its head.</summary>
    private static ReadOnlySpan<byte> PinBody => "M30 8 L70 8 L62 50 L38 50 Z"u8;

    /// <summary>Gets the push pin's details: the flange and the needle.</summary>
    private static ReadOnlySpan<byte> PinDetail => "M22 50 L78 50 M50 50 L50 95"u8;

    /// <summary>Gets the graph's body: three bars.</summary>
    private static ReadOnlySpan<byte> GraphBody => "M12 90 L12 55 L30 55 L30 90 Z M38 90 L38 20 L56 20 L56 90 Z M64 90 L64 40 L82 40 L82 90 Z"u8;

    /// <summary>Gets the graph's details: its two axes.</summary>
    private static ReadOnlySpan<byte> GraphDetail => "M5 95 L95 95 M5 5 L5 95"u8;

    /// <summary>Gets the paper clip's details: the wire.</summary>
    private static ReadOnlySpan<byte> ClipDetail => "M35 70 L35 25 C35 5 70 5 70 25 L70 80 C70 95 45 95 45 80 L45 35"u8;

    /// <summary>Gets the tag's body: the tag with a hole for the string.</summary>
    private static ReadOnlySpan<byte> TagBody =>
        "M15 50 L40 15 L85 15 L85 85 L40 85 Z M47 50 C47 54 44 57 40 57 C36 57 33 54 33 50 C33 46 36 43 40 43 C44 43 47 46 47 50 Z"u8;

    /// <inheritdoc/>
    public static bool TryRead(PdfDictionary annotation, out AttachmentPlan plan)
    {
        var rect = AppearanceRects.Read(annotation);
        var side = rect.Width > 0 ? Math.Min(NotePlan.MaxSide, rect.Width) : NotePlan.MaxSide;
        side = rect.Height > 0 ? Math.Min(side, rect.Height) : side;
        plan = new(new(rect.Left, rect.Top - side, rect.Left + side, rect.Top), Parse(annotation.NameText("Name")));
        return true;
    }

    /// <inheritdoc/>
    public void Emit(ref PdfContentBuilder builder)
    {
        switch (Icon)
        {
            case AttachmentIcon.Graph:
                {
                    IconShapes.Draw(ref builder, Bounds, Blue, GraphBody, GraphDetail);
                    break;
                }

            case AttachmentIcon.Paperclip:
                {
                    IconShapes.Draw(ref builder, Bounds, Grey, default, ClipDetail);
                    break;
                }

            case AttachmentIcon.Tag:
                {
                    IconShapes.Draw(ref builder, Bounds, Manila, TagBody, default);
                    break;
                }

            default:
                {
                    IconShapes.Draw(ref builder, Bounds, Red, PinBody, PinDetail);
                    break;
                }
        }
    }

    /// <summary>Reads the icon an attachment annotation names.</summary>
    /// <param name="name">The annotation's <c>/Name</c>, or <see langword="null"/> when it has none.</param>
    /// <returns>The icon; the push pin for a missing or unknown name.</returns>
    private static AttachmentIcon Parse(string? name) => name switch
    {
        "Graph" => AttachmentIcon.Graph,
        "Paperclip" => AttachmentIcon.Paperclip,
        "Tag" => AttachmentIcon.Tag,
        _ => AttachmentIcon.PushPin,
    };
}
