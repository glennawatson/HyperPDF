// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.AssociatedFiles;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads and manages document attachments.</summary>
public static class PdfDocumentAttachments
{
    /// <summary>Gets the files embedded in the document, in /EmbeddedFiles name tree order.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The attachments.</returns>
    public static IReadOnlyList<PdfAttachment> GetAttachments(PdfDocument document)
    {
        // Read before the objects are, so an edit that lands while the list is built keeps it out of the cache.
        var revision = PdfDocumentCaches.Revision(document);
        if (Volatile.Read(ref document.State.Attachments) is { } cached)
        {
            return cached;
        }

        var entries = new List<NameTreeEntry>();
        NameTree.EnumerateRaw(document.Catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.EmbeddedFiles), entries);
        var attachments = new PdfAttachment[entries.Count];
        for (var i = 0; i < attachments.Length; i++)
        {
            attachments[i] = PdfDocumentAttachments.ReadAttachment(document, i, entries[i]);
        }

        return PdfDocumentCaches.Publish(document, ref document.State.Attachments, attachments, revision);
    }

    /// <summary>Gets the document's top-level signature fields: those listed in the /AcroForm /Fields array.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The signatures.</returns>
    public static IReadOnlyList<PdfSignatureField> GetSignatures(PdfDocument document)
    {
        var revision = PdfDocumentCaches.Revision(document);
        if (Volatile.Read(ref document.State.Signatures) is { } cached)
        {
            return cached;
        }

        var fields = document.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields);
        var signatures = new List<PdfSignatureField>();
        for (var i = 0; fields is not null && i < fields.Count; i++)
        {
            var field = fields.GetDictionary(i);
            if (field?.IsName(KnownName.FT, KnownName.Sig) == true)
            {
                signatures.Add(PdfDocumentAttachments.ReadSignature(document, signatures.Count, field.GetDictionary(KnownName.V)));
            }
        }

        var result = signatures.ToArray();
        return PdfDocumentCaches.Publish(document, ref document.State.Signatures, result, revision);
    }

    /// <summary>Reads one entry of the <c>/EmbeddedFiles</c> name tree.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The entry's position in the tree.</param>
    /// <param name="entry">The name and the file specification as the tree stores it.</param>
    /// <returns>The attachment.</returns>
    private static PdfAttachment ReadAttachment(PdfDocument document, int index, in NameTreeEntry entry)
    {
        var value = StoreReading.Resolve(document.Objects, entry.Value);
        var spec = value.AsDictionary();
        var files = spec?.GetDictionary(KnownName.EF);
        var data = files?.GetStream(KnownName.UF) ?? files?.GetStream(KnownName.F);
        var name = PdfDocumentFileSpecs.ReadFileSpec(document, value);
        var attachment = new PdfAttachment(index, string.IsNullOrEmpty(name) ? string.Create(CultureInfo.InvariantCulture, $"Attachment {index + 1}") : name, data)
        {
            Key = PdfText.Decode(entry.Key.AsStringBytes()),
            Id = entry.Value.AsReference(),
            Description = spec?.GetText(KnownName.Desc),
            MimeType = data?.Dictionary.NameText("Subtype"),
            Relationship = PdfDocumentAttachments.ReadRelationship(spec),
        };
        return data?.Dictionary.GetDictionary(KnownName.Params) is { } parameters ? PdfDocumentAttachments.WithParameters(attachment, parameters) : attachment;
    }

    /// <summary>Adds what an embedded file's <c>/Params</c> records: the dates, size and checksum.</summary>
    /// <param name="attachment">The attachment.</param>
    /// <param name="parameters">The parameters.</param>
    /// <returns>The attachment with the values.</returns>
    private static PdfAttachment WithParameters(PdfAttachment attachment, PdfDictionary parameters) => attachment with
    {
        Created = PdfDocumentAttachments.ReadDate(parameters, KnownName.CreationDate),
        Modified = PdfDocumentAttachments.ReadDate(parameters, KnownName.ModDate),
        Size = parameters.ContainsKey(KnownName.Size) ? parameters.GetInteger(KnownName.Size) : null,
        CheckSum = parameters.ContainsKey(KnownName.CheckSum) ? parameters.GetStringBytes(KnownName.CheckSum).ToArray() : null,
    };

    /// <summary>Reads a file specification's <c>/AFRelationship</c>.</summary>
    /// <param name="specification">The file specification, or <see langword="null"/>.</param>
    /// <returns>The relationship, or <see langword="null"/> when the specification states none or a name this library does not define.</returns>
    private static PdfFileRelationship? ReadRelationship(PdfDictionary? specification) =>
        specification?.NameText("AFRelationship") is { } name && Enum.TryParse<PdfFileRelationship>(name, false, out var relationship) ? relationship : null;

    /// <summary>Reads a date from the <c>/Params</c> of an embedded file.</summary>
    /// <param name="parameters">The parameters, or <see langword="null"/>.</param>
    /// <param name="key">/CreationDate or /ModDate.</param>
    /// <returns>The date, or <see langword="null"/> when there is none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DateTimeOffset? ReadDate(PdfDictionary parameters, KnownName key) => PdfDate.Parse(parameters.GetStringBytes(key));

    /// <summary>Reads a signature dictionary.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The signature's index.</param>
    /// <param name="value">The field's /V signature dictionary, if signed.</param>
    /// <returns>The signature.</returns>
    private static PdfSignatureField ReadSignature(PdfDocument document, int index, PdfDictionary? value)
    {
        if (value is null)
        {
            return new(index, [], [], string.Empty, string.Empty, null);
        }

        var range = value.GetArray(KnownName.ByteRange);
        var byteRange = new long[range?.Count ?? 0];
        for (var i = 0; i < byteRange.Length; i++)
        {
            byteRange[i] = range!.Get(i).AsInteger();
        }

        var subFilter = value.GetName(KnownName.SubFilter);
        return new(
index,
value.GetStringBytes(KnownName.Contents).ToArray(),
byteRange,
subFilter.IsNone ? string.Empty : Encoding.ASCII.GetString(document.Objects.Names.GetSpelling(subFilter)),
value.GetText(KnownName.Reason) ?? string.Empty,
PdfDate.Parse(value.GetStringBytes(KnownName.M)));
    }
}
