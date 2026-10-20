// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>The shapes of the text annotation icons other than the speech box, as tables for <see cref="IconShapes"/>.</summary>
internal static class NoteIcons
{
    /// <summary>The yellow every note icon is filled with.</summary>
    private static readonly IconFill Yellow = new(1, 1, 0);

    /// <summary>Gets the page icon's body: a page with its upper-right corner cut.</summary>
    private static ReadOnlySpan<byte> PageBody => "M15 5 L60 5 L85 30 L85 95 L15 95 Z"u8;

    /// <summary>Gets the page icon's details: the fold and three lines of text.</summary>
    private static ReadOnlySpan<byte> PageDetail => "M60 5 L60 30 L85 30 M30 50 L70 50 M30 65 L70 65 M30 80 L70 80"u8;

    /// <summary>Gets the key icon's body: the ring of the key.</summary>
    private static ReadOnlySpan<byte> KeyBody => "M50 50 C50 62 40 72 28 72 C16 72 6 62 6 50 C6 38 16 28 28 28 C40 28 50 38 50 50 Z"u8;

    /// <summary>Gets the key icon's details: the hole in the ring, the shaft and two teeth.</summary>
    private static ReadOnlySpan<byte> KeyDetail =>
        "M36 50 C36 54 32 58 28 58 C24 58 20 54 20 50 C20 46 24 42 28 42 C32 42 36 46 36 50 Z M50 50 L92 50 M72 50 L72 72 M88 50 L88 72"u8;

    /// <summary>Gets the help icon's body: a circle.</summary>
    private static ReadOnlySpan<byte> HelpBody => "M95 50 C95 75 75 95 50 95 C25 95 5 75 5 50 C5 25 25 5 50 5 C75 5 95 25 95 50 Z"u8;

    /// <summary>Gets the help icon's details: the question mark and its dot.</summary>
    private static ReadOnlySpan<byte> HelpDetail => "M35 38 C35 15 65 15 65 36 C65 50 50 50 50 64 M50 75 L50 80"u8;

    /// <summary>Gets the body of the paragraph icons: a square.</summary>
    private static ReadOnlySpan<byte> BoxBody => "M5 5 L95 5 L95 95 L5 95 Z"u8;

    /// <summary>Gets the details of the paragraph icon: the paragraph sign.</summary>
    private static ReadOnlySpan<byte> ParagraphDetail => "M40 22 L75 22 M55 22 L55 80 M70 22 L70 80 M55 22 C30 22 30 55 55 55"u8;

    /// <summary>Gets the details of the new paragraph icon: the paragraph sign and an insertion mark.</summary>
    private static ReadOnlySpan<byte> NewParagraphDetail => "M40 22 L75 22 M55 22 L55 70 M70 22 L70 70 M55 22 C30 22 30 55 55 55 M12 88 L22 62 L32 88 Z"u8;

    /// <summary>Gets the insert icon's body: a caret.</summary>
    private static ReadOnlySpan<byte> CaretBody => "M50 10 L15 90 L85 90 Z"u8;

    /// <summary>Gets the insert icon's details: a short stem.</summary>
    private static ReadOnlySpan<byte> CaretDetail => "M50 40 L50 72"u8;

    /// <summary>Reads the icon a text annotation names.</summary>
    /// <param name="name">The annotation's <c>/Name</c>, or <see langword="null"/> when it has none.</param>
    /// <returns>The icon; the speech box for a missing or unknown name.</returns>
    internal static NoteIcon Parse(string? name) => name switch
    {
        "Note" => NoteIcon.Note,
        "Key" => NoteIcon.Key,
        "Help" => NoteIcon.Help,
        "Paragraph" => NoteIcon.Paragraph,
        "NewParagraph" => NoteIcon.NewParagraph,
        "Insert" => NoteIcon.Insert,
        _ => NoteIcon.Comment,
    };

    /// <summary>Draws an icon other than the speech box.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="bounds">The icon's rectangle.</param>
    /// <param name="icon">The icon.</param>
    internal static void Emit(ref PdfContentBuilder builder, PdfRectangle bounds, NoteIcon icon)
    {
        switch (icon)
        {
            case NoteIcon.Note:
                {
                    IconShapes.Draw(ref builder, bounds, Yellow, PageBody, PageDetail);
                    break;
                }

            case NoteIcon.Key:
                {
                    IconShapes.Draw(ref builder, bounds, Yellow, KeyBody, KeyDetail);
                    break;
                }

            case NoteIcon.Help:
                {
                    IconShapes.Draw(ref builder, bounds, Yellow, HelpBody, HelpDetail);
                    break;
                }

            case NoteIcon.Paragraph:
                {
                    IconShapes.Draw(ref builder, bounds, Yellow, BoxBody, ParagraphDetail);
                    break;
                }

            case NoteIcon.NewParagraph:
                {
                    IconShapes.Draw(ref builder, bounds, Yellow, BoxBody, NewParagraphDetail);
                    break;
                }

            default:
                {
                    IconShapes.Draw(ref builder, bounds, Yellow, CaretBody, CaretDetail);
                    break;
                }
        }
    }
}
