// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Interchange;

/// <summary>
/// Writes an annotation's dictionary into an FDF file from a <see cref="PdfInterchangeAnnotation"/>, with the entries and
/// encodings <see cref="InterchangeAnnotationBuilder"/> gives a dictionary it builds. The page, the reply link and the
/// pop-up are written here as well.
/// </summary>
internal static class FdfAnnotationWriter
{
    /// <summary>The bit offset of red in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit offset of green in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>Writes the dictionary of an annotation.</summary>
    /// <param name="output">The output.</param>
    /// <param name="source">The annotation.</param>
    /// <param name="links">The objects it refers to.</param>
    internal static void Write(ref PdfObjectWriter output, PdfInterchangeAnnotation source, in FdfLinks links)
    {
        output.BeginDictionary();
        output.Key(KnownName.Type);
        output.Name(KnownName.Annot);
        output.Key(KnownName.Subtype);
        output.Name(source.Subtype);
        output.Key(KnownName.Rect);
        WriteRectangle(ref output, source.Rect ?? default);
        WriteBase(ref output, source);
        WriteBorder(ref output, source);
        WriteGeometry(ref output, source);
        WriteText(ref output, source);
        WriteLinks(ref output, source, links);
        output.EndDictionary();
    }

    /// <summary>Writes the dictionary of a pop-up window.</summary>
    /// <param name="output">The output.</param>
    /// <param name="popup">The pop-up.</param>
    /// <param name="parent">The number of the annotation the pop-up belongs to.</param>
    internal static void WritePopup(ref PdfObjectWriter output, PdfInterchangePopup popup, int parent)
    {
        output.BeginDictionary();
        output.Key(KnownName.Type);
        output.Name(KnownName.Annot);
        output.Key(KnownName.Subtype);
        output.Name(KnownName.Popup);
        output.Key(KnownName.Rect);
        WriteRectangle(ref output, popup.Rect);
        output.Key(KnownName.Open);
        output.Boolean(popup.IsOpen);
        output.Key(KnownName.Parent);
        output.Reference(parent);
        output.Key(KnownName.Page);
        output.Integer(popup.Page);
        output.EndDictionary();
    }

    /// <summary>Writes a rectangle as <c>[left bottom right top]</c>.</summary>
    /// <param name="output">The output.</param>
    /// <param name="rectangle">The rectangle.</param>
    private static void WriteRectangle(ref PdfObjectWriter output, PdfRectangle rectangle)
    {
        output.BeginArray();
        output.Number(rectangle.Left);
        output.Separate(1);
        output.Number(rectangle.Bottom);
        output.Separate(1);
        output.Number(rectangle.Right);
        output.Separate(1);
        output.Number(rectangle.Top);
        output.EndArray();
    }

    /// <summary>Writes flags, colours, dates and opacity.</summary>
    /// <param name="output">The output.</param>
    /// <param name="source">The annotation.</param>
    private static void WriteBase(ref PdfObjectWriter output, PdfInterchangeAnnotation source)
    {
        if (source.Flags != PdfAnnotationFlags.None)
        {
            output.Key(KnownName.F);
            output.Integer((int)source.Flags);
        }

        WriteColor(ref output, KnownName.C, source.Color);
        WriteColor(ref output, KnownName.IC, source.InteriorColor);
        WriteDate(ref output, KnownName.M, source.Date);
        WriteDate(ref output, KnownName.CreationDate, source.CreationDate);
        if (source.Opacity is { } opacity)
        {
            output.Key(KnownName.CA);
            output.Number(Math.Clamp(opacity, 0, 1));
        }

        if (source.Justification is { } justification)
        {
            output.Key(KnownName.Q);
            output.Integer(justification);
        }

        output.NameEntry(KnownName.Name, source.Icon);
        output.NameEntry(output.Names.Intern("Sy"), source.Symbol);
        if (source.IsOpen is not { } open)
        {
            return;
        }

        output.Key(KnownName.Open);
        output.Boolean(open);
    }

    /// <summary>Writes an RGB colour entry, each component rounded up at the sixth decimal as <see cref="PdfAnnotations.SetColor"/> does.</summary>
    /// <param name="output">The output.</param>
    /// <param name="key">The key.</param>
    /// <param name="color">The colour as 0xRRGGBB, or <see langword="null"/> for no entry.</param>
    private static void WriteColor(ref PdfObjectWriter output, PdfName key, uint? color)
    {
        if (color is not { } rgb)
        {
            return;
        }

        output.Key(key);
        output.BeginArray();
        output.Value(PdfAnnotations.Component(rgb >> RedShift));
        output.Separate(1);
        output.Value(PdfAnnotations.Component(rgb >> GreenShift));
        output.Separate(1);
        output.Value(PdfAnnotations.Component(rgb));
        output.EndArray();
    }

    /// <summary>Writes a date entry when there is a date.</summary>
    /// <param name="output">The output.</param>
    /// <param name="key">The key.</param>
    /// <param name="date">The date, or <see langword="null"/> for no entry.</param>
    private static void WriteDate(ref PdfObjectWriter output, PdfName key, DateTimeOffset? date)
    {
        if (date is not { } value)
        {
            return;
        }

        output.Key(key);
        output.Date(value);
    }

    /// <summary>Writes the border style, dashes and cloud intensity.</summary>
    /// <param name="output">The output.</param>
    /// <param name="source">The annotation.</param>
    private static void WriteBorder(ref PdfObjectWriter output, PdfInterchangeAnnotation source)
    {
        if (source.Width is not null || source.Style is not null || !source.Dashes.IsEmpty)
        {
            output.Key(KnownName.BS);
            output.BeginDictionary();
            if (source.Width is { } width)
            {
                output.Key(KnownName.W);
                output.Number(width);
            }

            output.NameEntry(KnownName.S, source.Style);
            output.NumbersEntry(KnownName.D, source.Dashes);
            output.EndDictionary();
        }

        if (source.Intensity is not { } intensity)
        {
            return;
        }

        output.Key(KnownName.BE);
        output.BeginDictionary();
        output.Key(KnownName.S);
        output.Name(KnownName.C);
        output.Key(KnownName.I);
        output.Number(intensity);
        output.EndDictionary();
    }

    /// <summary>Writes the point arrays and line endings.</summary>
    /// <param name="output">The output.</param>
    /// <param name="source">The annotation.</param>
    private static void WriteGeometry(ref PdfObjectWriter output, PdfInterchangeAnnotation source)
    {
        output.NumbersEntry(KnownName.QuadPoints, source.Coords);
        output.NumbersEntry(KnownName.Vertices, source.Vertices);
        output.NumbersEntry(KnownName.L, source.Line);
        output.NumbersEntry(KnownName.CL, source.Callout);
        output.NumbersEntry(KnownName.RD, source.Fringe);
        if (!source.Gestures.IsEmpty)
        {
            output.Key(KnownName.InkList);
            output.BeginArray();
            var strokes = source.Gestures.Span;
            for (var i = 0; i < strokes.Length; i++)
            {
                output.Separate(i);
                output.Numbers(strokes[i].Span);
            }

            output.EndArray();
        }

        if (source.Head is null && source.Tail is null)
        {
            return;
        }

        output.Key(KnownName.LE);
        output.BeginArray();
        output.Name(source.Head ?? "None");
        output.Separate(1);
        output.Name(source.Tail ?? "None");
        output.EndArray();
    }

    /// <summary>Writes the text entries.</summary>
    /// <param name="output">The output.</param>
    /// <param name="source">The annotation.</param>
    private static void WriteText(ref PdfObjectWriter output, PdfInterchangeAnnotation source)
    {
        output.TextEntry(KnownName.NM, source.Name);
        output.TextEntry(KnownName.T, source.Title);
        output.TextEntry(output.Names.Intern("Subj"), source.Subject);
        output.TextEntry(KnownName.Contents, source.Contents);
        output.TextEntry(KnownName.RC, source.RichContents);
        output.TextEntry(KnownName.DA, source.DefaultAppearance);
        output.TextEntry(KnownName.DS, source.DefaultStyle);
        output.TextEntry(KnownName.State, source.State);
        output.TextEntry(KnownName.StateModel, source.StateModel);
    }

    /// <summary>Writes the attached file, the page, the reply link and the pop-up.</summary>
    /// <param name="output">The output.</param>
    /// <param name="source">The annotation.</param>
    /// <param name="links">The objects it refers to.</param>
    private static void WriteLinks(ref PdfObjectWriter output, PdfInterchangeAnnotation source, in FdfLinks links)
    {
        if (source.Attachment is { } attachment)
        {
            WriteSpec(ref output, attachment, links.Stream);
        }

        output.Key(KnownName.Page);
        output.Integer(source.Page);
        if (links.Parent > 0)
        {
            output.Key(KnownName.IRT);
            output.Reference(links.Parent);
            output.Key(KnownName.RT);
            output.Name(string.IsNullOrEmpty(source.ReplyType) ? "R" : source.ReplyType);
        }

        if (links.Popup <= 0)
        {
            return;
        }

        output.Key(KnownName.Popup);
        output.Reference(links.Popup);
    }

    /// <summary>Writes the file specification of an attachment.</summary>
    /// <param name="output">The output.</param>
    /// <param name="attachment">The attachment.</param>
    /// <param name="stream">The number of the object holding the embedded file.</param>
    private static void WriteSpec(ref PdfObjectWriter output, PdfInterchangeAttachment attachment, int stream)
    {
        output.Key(output.Names.Intern("FS"));
        output.BeginDictionary();
        output.Key(KnownName.Type);
        output.Name(KnownName.Filespec);
        output.TextEntry(KnownName.F, attachment.FileName);
        output.TextEntry(KnownName.UF, attachment.FileName);
        output.TextEntry(KnownName.Desc, attachment.Description);
        output.Key(KnownName.EF);
        output.BeginDictionary();
        output.Key(KnownName.F);
        output.Reference(stream);
        output.EndDictionary();
        output.EndDictionary();
    }
}
