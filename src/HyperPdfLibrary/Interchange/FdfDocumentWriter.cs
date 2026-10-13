// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Interchange;

/// <summary>
/// Writes the objects of an FDF file from a <see cref="PdfInterchangeData"/>: the catalog with its <c>/FDF</c>
/// dictionary as object 1, then the differences stream, one object for each annotation, and the streams and pop-ups the
/// annotations bring, numbered in that order. Everything is written as it is read, with no object tree built first.
/// </summary>
internal static class FdfDocumentWriter
{
    /// <summary>The number of the catalog.</summary>
    private const int CatalogNumber = 1;

    /// <summary>The number of the first object after the catalog.</summary>
    private const int FirstNumber = 2;

    /// <summary>Writes the objects.</summary>
    /// <param name="output">The output.</param>
    /// <param name="data">The data.</param>
    internal static void Write(ref PdfObjectWriter output, PdfInterchangeData data)
    {
        var differences = data.Differences.IsEmpty ? 0 : FirstNumber;
        var firstAnnotation = differences > 0 ? FirstNumber + 1 : FirstNumber;
        var numbers = new FdfNumbers(differences, firstAnnotation, firstAnnotation + data.Annotations.Count);
        output.BeginObject(CatalogNumber);
        output.BeginDictionary();
        output.Key(output.Names.Intern("FDF"));
        WriteFdf(ref output, data, numbers);
        output.EndDictionary();
        output.EndObject();
        if (differences > 0)
        {
            output.Stream(differences, new(new PdfDictionary(null), data.Differences.ToArray()));
        }

        WriteAnnotations(ref output, data.Annotations, numbers);
        WriteExtras(ref output, data.Annotations, numbers);
    }

    /// <summary>Writes the <c>/FDF</c> dictionary.</summary>
    /// <param name="output">The output.</param>
    /// <param name="data">The data.</param>
    /// <param name="numbers">The numbers of the objects it refers to.</param>
    private static void WriteFdf(ref PdfObjectWriter output, PdfInterchangeData data, in FdfNumbers numbers)
    {
        output.BeginDictionary();
        output.TextEntry(KnownName.F, data.FileHref);
        output.TextEntry(output.Names.Intern("Status"), data.Status);
        output.NameEntry(KnownName.Encoding, data.Encoding);
        WriteIds(ref output, data);
        WriteScripts(ref output, data.JavaScript);
        if (numbers.Differences > 0)
        {
            output.Key(KnownName.Differences);
            output.Reference(numbers.Differences);
        }

        if (data.Fields.Count > 0)
        {
            output.Key(KnownName.Fields);
            WriteFields(ref output, InterchangeFieldTree.Build(data.Fields));
        }

        if (data.Annotations.Count > 0)
        {
            output.Key(KnownName.Annots);
            output.BeginArray();
            for (var i = 0; i < data.Annotations.Count; i++)
            {
                output.Separate(i);
                output.Reference(numbers.FirstAnnotation + i);
            }

            output.EndArray();
        }

        output.EndDictionary();
    }

    /// <summary>Writes <c>/ID</c> from hexadecimal identifiers.</summary>
    /// <param name="output">The output.</param>
    /// <param name="data">The data.</param>
    private static void WriteIds(ref PdfObjectWriter output, PdfInterchangeData data)
    {
        if (data.OriginalId is null || InterchangeBytes.DecodeHex(data.OriginalId) is not { } first)
        {
            return;
        }

        var second = data.ModifiedId is null ? null : InterchangeBytes.DecodeHex(data.ModifiedId);
        output.Key(KnownName.ID);
        output.BeginArray();
        output.Bytes(first);
        output.Separate(1);
        output.Bytes(second ?? first);
        output.EndArray();
    }

    /// <summary>Writes <c>/JavaScript</c> from the scripts kept as data.</summary>
    /// <param name="output">The output.</param>
    /// <param name="scripts">The scripts, or <see langword="null"/>.</param>
    private static void WriteScripts(ref PdfObjectWriter output, PdfInterchangeScripts? scripts)
    {
        if (scripts is null)
        {
            return;
        }

        output.Key(KnownName.JavaScript);
        output.BeginDictionary();
        output.TextEntry(output.Names.Intern("Before"), scripts.Before);
        output.TextEntry(output.Names.Intern("After"), scripts.After);
        output.TextEntry(output.Names.Intern("AfterPermsReady"), scripts.AfterPermsReady);
        if (scripts.Document.Length > 0)
        {
            output.Key(output.Names.Intern("Doc"));
            output.BeginArray();
            for (var i = 0; i < scripts.Document.Length; i++)
            {
                output.Separate(i);
                output.Text(scripts.Document[i]);
            }

            output.EndArray();
        }

        output.EndDictionary();
    }

    /// <summary>Writes the array of field dictionaries for a node's children.</summary>
    /// <param name="output">The output.</param>
    /// <param name="node">The parent.</param>
    private static void WriteFields(ref PdfObjectWriter output, InterchangeFieldTree node)
    {
        output.BeginArray();
        for (var i = 0; i < node.Children.Count; i++)
        {
            output.Separate(i);
            WriteField(ref output, node.Children[i]);
        }

        output.EndArray();
    }

    /// <summary>Writes one field dictionary.</summary>
    /// <param name="output">The output.</param>
    /// <param name="node">The field.</param>
    private static void WriteField(ref PdfObjectWriter output, InterchangeFieldTree node)
    {
        output.BeginDictionary();
        output.Key(KnownName.T);
        output.Text(node.PartialName);
        if (node.Field is { } field)
        {
            WriteValue(ref output, field);
        }

        if (node.Children.Count > 0)
        {
            output.Key(KnownName.Kids);
            WriteFields(ref output, node);
        }

        output.EndDictionary();
    }

    /// <summary>Writes a field's value, rich value and flags.</summary>
    /// <param name="output">The output.</param>
    /// <param name="field">The field.</param>
    private static void WriteValue(ref PdfObjectWriter output, PdfInterchangeField field)
    {
        if (field.Values.Length == 1)
        {
            output.Key(KnownName.V);
            if (field.ValueIsName)
            {
                output.Name(field.Values[0]);
            }
            else
            {
                output.Text(field.Values[0]);
            }
        }
        else if (field.Values.Length > 1)
        {
            output.Key(KnownName.V);
            output.BeginArray();
            for (var i = 0; i < field.Values.Length; i++)
            {
                output.Separate(i);
                output.Text(field.Values[i]);
            }

            output.EndArray();
        }

        output.TextEntry(output.Names.Intern("RV"), field.RichText);
        WriteFlags(ref output, KnownName.Ff, field.Flags);
        WriteFlags(ref output, output.Names.Intern("SetFf"), field.SetFlags);
        WriteFlags(ref output, output.Names.Intern("ClrFf"), field.ClearFlags);
    }

    /// <summary>Writes a flags entry when there are flags.</summary>
    /// <param name="output">The output.</param>
    /// <param name="key">The key.</param>
    /// <param name="flags">The flags, or <see langword="null"/>.</param>
    private static void WriteFlags(ref PdfObjectWriter output, PdfName key, uint? flags)
    {
        if (flags is not { } value)
        {
            return;
        }

        output.Key(key);
        output.Integer(value);
    }

    /// <summary>Writes the object of every annotation.</summary>
    /// <param name="output">The output.</param>
    /// <param name="annotations">The annotations.</param>
    /// <param name="numbers">The numbers of the objects.</param>
    private static void WriteAnnotations(ref PdfObjectWriter output, List<PdfInterchangeAnnotation> annotations, in FdfNumbers numbers)
    {
        var parents = IndexParents(annotations, numbers.FirstAnnotation);
        var extra = numbers.FirstExtra;
        for (var i = 0; i < annotations.Count; i++)
        {
            var links = LinksOf(annotations[i], ref extra, parents);
            output.BeginObject(numbers.FirstAnnotation + i);
            FdfAnnotationWriter.Write(ref output, annotations[i], links);
            output.EndObject();
        }
    }

    /// <summary>Writes the embedded files and pop-ups the annotations bring, in the order their numbers were given.</summary>
    /// <param name="output">The output.</param>
    /// <param name="annotations">The annotations.</param>
    /// <param name="numbers">The numbers of the objects.</param>
    private static void WriteExtras(ref PdfObjectWriter output, List<PdfInterchangeAnnotation> annotations, in FdfNumbers numbers)
    {
        var extra = numbers.FirstExtra;
        for (var i = 0; i < annotations.Count; i++)
        {
            var links = LinksOf(annotations[i], ref extra, null);
            if (annotations[i].Attachment is { } attachment)
            {
                output.Stream(links.Stream, InterchangeAnnotationBuilder.BuildEmbeddedFile(attachment, null, output.Names));
            }

            if (annotations[i].Popup is not { } popup)
            {
                continue;
            }

            output.BeginObject(links.Popup);
            FdfAnnotationWriter.WritePopup(ref output, popup, numbers.FirstAnnotation + i);
            output.EndObject();
        }
    }

    /// <summary>Gets the numbers of the objects an annotation refers to, taking the next numbers for the extras it brings.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="extra">The next number for an extra object; advanced past those taken.</param>
    /// <param name="parents">The number of each named annotation, or <see langword="null"/> when no annotation is a reply.</param>
    /// <returns>The numbers.</returns>
    private static FdfLinks LinksOf(PdfInterchangeAnnotation annotation, ref int extra, Dictionary<string, int>? parents)
    {
        var stream = 0;
        if (annotation.Attachment is not null)
        {
            stream = extra;
            extra++;
        }

        var popup = 0;
        if (annotation.Popup is not null)
        {
            popup = extra;
            extra++;
        }

        var parent = annotation.InReplyTo is { } name && parents is not null && parents.TryGetValue(name, out var number) ? number : 0;
        return new(parent, stream, popup);
    }

    /// <summary>Gets the number of each named annotation, when some annotation is a reply and needs one.</summary>
    /// <param name="annotations">The annotations.</param>
    /// <param name="first">The number of the first annotation.</param>
    /// <returns>The numbers by name, or <see langword="null"/> when no annotation is a reply.</returns>
    private static Dictionary<string, int>? IndexParents(List<PdfInterchangeAnnotation> annotations, int first)
    {
        var any = false;
        foreach (var annotation in annotations)
        {
            any |= annotation.InReplyTo is not null;
        }

        if (!any)
        {
            return null;
        }

        Dictionary<string, int> numbers = [with(annotations.Count, StringComparer.Ordinal)];
        for (var i = 0; i < annotations.Count; i++)
        {
            var name = annotations[i].Name;
            if (!string.IsNullOrEmpty(name))
            {
                numbers[name] = first + i;
            }
        }

        return numbers;
    }
}
