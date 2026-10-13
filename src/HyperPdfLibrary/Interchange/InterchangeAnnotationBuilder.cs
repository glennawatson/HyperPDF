// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <summary>Builds an annotation dictionary from a <see cref="PdfInterchangeAnnotation"/>. Replies, pop-ups and the page are left to the caller.</summary>
internal static class InterchangeAnnotationBuilder
{
    /// <summary>The entries a border style dictionary has: width, style and dashes.</summary>
    private const int BorderEntries = 3;

    /// <summary>The entries of a cloud border effect: style and intensity.</summary>
    private const int EffectEntries = 2;

    /// <summary>The line endings of a line: start and end.</summary>
    private const int LineEndings = 2;

    /// <summary>The entries of an embedded file's parameters.</summary>
    private const int ParameterEntries = 1;

    /// <summary>The entries of a file specification.</summary>
    private const int SpecEntries = 5;

    /// <summary>Builds the dictionary.</summary>
    /// <param name="source">The annotation.</param>
    /// <param name="owner">The document the dictionary belongs to, or <see langword="null"/> for a free dictionary.</param>
    /// <param name="names">The name table the dictionary's names come from.</param>
    /// <param name="sink">Stores streams as objects.</param>
    /// <returns>The dictionary.</returns>
    internal static PdfDictionary Build(PdfInterchangeAnnotation source, PdfObjectStore? owner, PdfNameTable names, IInterchangeObjects sink)
    {
        var dictionary = PdfAnnotations.Create(owner, names.Intern(source.Subtype), source.Rect ?? default);
        ApplyBase(dictionary, source, names);
        ApplyBorder(dictionary, source, names);
        ApplyGeometry(dictionary, source, names);
        ApplyText(dictionary, source, names);
        if (source.Attachment is { } attachment)
        {
            dictionary.Set(names.Intern("FS"), PdfValue.FromDictionary(BuildSpec(attachment, owner, names, sink)));
        }

        return dictionary;
    }

    /// <summary>Builds the embedded file stream of an attachment.</summary>
    /// <param name="attachment">The attachment.</param>
    /// <param name="owner">The owning document, or <see langword="null"/>.</param>
    /// <param name="names">The names.</param>
    /// <returns>The stream.</returns>
    internal static PdfStream BuildEmbeddedFile(PdfInterchangeAttachment attachment, PdfObjectStore? owner, PdfNameTable names)
    {
        var parameters = new PdfDictionary(owner, ParameterEntries);
        parameters.Set(KnownName.Size, PdfValue.FromInteger(attachment.Data.Length));
        var file = new PdfDictionary(owner);
        file.Set(KnownName.Type, PdfValue.FromName(names.Intern("EmbeddedFile")));
        file.Set(KnownName.Params, PdfValue.FromDictionary(parameters));
        SetName(file, KnownName.Subtype, attachment.MimeType, names);
        return new(file, attachment.Data);
    }

    /// <summary>Makes an annotation a reply to another.</summary>
    /// <param name="dictionary">The reply.</param>
    /// <param name="parent">The annotation it answers, an indirect object.</param>
    /// <param name="replyType">The reply type, <c>R</c> or <c>Group</c>; reply when <see langword="null"/>.</param>
    /// <param name="names">The names.</param>
    internal static void LinkReply(PdfDictionary dictionary, PdfObjectId parent, string? replyType, PdfNameTable names)
    {
        PdfAnnotations.SetInReplyTo(dictionary, parent);
        SetName(dictionary, KnownName.RT, replyType, names);
    }

    /// <summary>Sets flags, colours, dates and opacity.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="source">The annotation.</param>
    /// <param name="names">The names.</param>
    private static void ApplyBase(PdfDictionary dictionary, PdfInterchangeAnnotation source, PdfNameTable names)
    {
        if (source.Flags != PdfAnnotationFlags.None)
        {
            PdfAnnotations.SetFlags(dictionary, source.Flags);
        }

        if (source.Color is { } color)
        {
            PdfAnnotations.SetColor(dictionary, KnownName.C, color);
        }

        if (source.InteriorColor is { } interior)
        {
            PdfAnnotations.SetColor(dictionary, KnownName.IC, interior);
        }

        if (source.Date is { } date)
        {
            PdfAnnotations.SetDate(dictionary, KnownName.M, date);
        }

        if (source.CreationDate is { } created)
        {
            PdfAnnotations.SetDate(dictionary, KnownName.CreationDate, created);
        }

        if (source.Opacity is { } opacity)
        {
            PdfAnnotations.SetOpacity(dictionary, opacity);
        }

        if (source.Justification is { } justification)
        {
            dictionary.Set(KnownName.Q, PdfValue.FromInteger(justification));
        }

        SetName(dictionary, KnownName.Name, source.Icon, names);
        SetName(dictionary, names.Intern("Sy"), source.Symbol, names);
        if (source.IsOpen is { } open)
        {
            dictionary.Set(KnownName.Open, PdfValue.FromBoolean(open));
        }
    }

    /// <summary>Sets the border style, dashes and cloud intensity.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="source">The annotation.</param>
    /// <param name="names">The names.</param>
    private static void ApplyBorder(PdfDictionary dictionary, PdfInterchangeAnnotation source, PdfNameTable names)
    {
        if (source.Width is not null || source.Style is not null || !source.Dashes.IsEmpty)
        {
            var border = new PdfDictionary(dictionary.Owner, BorderEntries);
            if (source.Width is { } width)
            {
                border.Set(KnownName.W, PdfNumber.ToValue(width));
            }

            SetName(border, KnownName.S, source.Style, names);
            SetNumbers(border, KnownName.D, source.Dashes);
            dictionary.Set(KnownName.BS, PdfValue.FromDictionary(border));
        }

        if (source.Intensity is not { } intensity)
        {
            return;
        }

        var effect = new PdfDictionary(dictionary.Owner, EffectEntries);
        effect.Set(KnownName.S, PdfValue.FromName(KnownName.C));
        effect.Set(KnownName.I, PdfNumber.ToValue(intensity));
        dictionary.Set(KnownName.BE, PdfValue.FromDictionary(effect));
    }

    /// <summary>Sets the point arrays and line endings.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="source">The annotation.</param>
    /// <param name="names">The names.</param>
    private static void ApplyGeometry(PdfDictionary dictionary, PdfInterchangeAnnotation source, PdfNameTable names)
    {
        SetNumbers(dictionary, KnownName.QuadPoints, source.Coords);
        SetNumbers(dictionary, KnownName.Vertices, source.Vertices);
        SetNumbers(dictionary, KnownName.L, source.Line);
        SetNumbers(dictionary, KnownName.CL, source.Callout);
        SetNumbers(dictionary, KnownName.RD, source.Fringe);
        if (!source.Gestures.IsEmpty)
        {
            var list = new PdfArray(dictionary.Owner, source.Gestures.Length);
            foreach (var stroke in source.Gestures.Span)
            {
                list.Add(PdfValue.FromArray(PdfArray.FromNumbers(dictionary.Owner, stroke.Span)));
            }

            dictionary.Set(KnownName.InkList, PdfValue.FromArray(list));
        }

        if (source.Head is null && source.Tail is null)
        {
            return;
        }

        var endings = new PdfArray(dictionary.Owner, LineEndings);
        endings.Add(PdfValue.FromName(names.Intern(source.Head ?? "None")));
        endings.Add(PdfValue.FromName(names.Intern(source.Tail ?? "None")));
        dictionary.Set(KnownName.LE, PdfValue.FromArray(endings));
    }

    /// <summary>Sets the text entries.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="source">The annotation.</param>
    /// <param name="names">The names.</param>
    private static void ApplyText(PdfDictionary dictionary, PdfInterchangeAnnotation source, PdfNameTable names)
    {
        SetText(dictionary, KnownName.NM, source.Name);
        SetText(dictionary, KnownName.T, source.Title);
        SetText(dictionary, names.Intern("Subj"), source.Subject);
        SetText(dictionary, KnownName.Contents, source.Contents);
        SetText(dictionary, KnownName.RC, source.RichContents);
        SetText(dictionary, KnownName.DA, source.DefaultAppearance);
        SetText(dictionary, KnownName.DS, source.DefaultStyle);
        SetText(dictionary, KnownName.State, source.State);
        SetText(dictionary, KnownName.StateModel, source.StateModel);
    }

    /// <summary>Builds the file specification of an attachment.</summary>
    /// <param name="attachment">The attachment.</param>
    /// <param name="owner">The owning document, or <see langword="null"/>.</param>
    /// <param name="names">The names.</param>
    /// <param name="sink">Stores the embedded file.</param>
    /// <returns>The file specification.</returns>
    private static PdfDictionary BuildSpec(PdfInterchangeAttachment attachment, PdfObjectStore? owner, PdfNameTable names, IInterchangeObjects sink)
    {
        var embedded = new PdfDictionary(owner, 1);
        embedded.Set(KnownName.F, sink.Add(BuildEmbeddedFile(attachment, owner, names)));
        var spec = new PdfDictionary(owner, SpecEntries);
        spec.Set(KnownName.Type, PdfValue.FromName(KnownName.Filespec));
        SetText(spec, KnownName.F, attachment.FileName);
        SetText(spec, KnownName.UF, attachment.FileName);
        SetText(spec, KnownName.Desc, attachment.Description);
        spec.Set(KnownName.EF, PdfValue.FromDictionary(embedded));
        return spec;
    }

    /// <summary>Sets a text string entry when there is text.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    private static void SetText(PdfDictionary dictionary, PdfName key, string? text)
    {
        if (text is not null)
        {
            PdfAnnotations.SetText(dictionary, key, text);
        }
    }

    /// <summary>Sets a name entry when there is a name.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="name">The name's spelling, or <see langword="null"/>.</param>
    /// <param name="names">The names.</param>
    private static void SetName(PdfDictionary dictionary, PdfName key, string? name, PdfNameTable names)
    {
        if (!string.IsNullOrEmpty(name))
        {
            dictionary.Set(key, PdfValue.FromName(names.Intern(name)));
        }
    }

    /// <summary>Sets an array of numbers when there are some.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="numbers">The numbers; nothing is set when empty.</param>
    private static void SetNumbers(PdfDictionary dictionary, PdfName key, ReadOnlyMemory<float> numbers)
    {
        if (!numbers.IsEmpty)
        {
            dictionary.Set(key, PdfValue.FromArray(PdfArray.FromNumbers(dictionary.Owner, numbers.Span)));
        }
    }
}
