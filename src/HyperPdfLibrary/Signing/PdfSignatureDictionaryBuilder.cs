// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Signing;

/// <summary>Builds the signature dictionary (ISO 32000-2, Table 255) with room for the byte range and the signature value.</summary>
internal static class PdfSignatureDictionaryBuilder
{
    /// <summary>The first value of the placeholder byte range; the real offsets are written over the placeholder numbers, which are all ten digits wide.</summary>
    internal const long PlaceholderFirst = 1_111_111_111;

    /// <summary>The second value of the placeholder byte range.</summary>
    internal const long PlaceholderSecond = 2_222_222_222;

    /// <summary>The third value of the placeholder byte range.</summary>
    internal const long PlaceholderThird = 3_333_333_333;

    /// <summary>The number of digits each byte range number is written with.</summary>
    internal const int ByteRangeWidth = 10;

    /// <summary>The numbers in a byte range with one gap: two offsets and two lengths, here with the leading zero offset.</summary>
    private const int ByteRangeNumbers = 4;

    /// <summary>The bytes reserved to format the signing time, more than any PDF date takes.</summary>
    private const int DateBufferLength = 32;

    /// <summary>The entries every signature dictionary has: type, filter, sub-filter, byte range and contents.</summary>
    private const int BaseEntries = 5;

    /// <summary>The most signature reference dictionaries a signature carries: DocMDP and FieldMDP.</summary>
    private const int MaxReferences = 2;

    /// <summary>The most recent placeholder for the signature value: the same room is asked for again by the next signature of the same signer.</summary>
    private static byte[]? _zeroContents;

    /// <summary>Gets the name of the signature handler (ISO 32000-2, Table 255).</summary>
    private static ReadOnlySpan<byte> Handler => "Adobe.PPKLite"u8;

    /// <summary>Gets the version of the transform parameters (ISO 32000-2, Tables 257 and 259).</summary>
    private static ReadOnlySpan<byte> TransformVersion => "1.2"u8;

    /// <summary>Builds the dictionary of a signature or a document timestamp.</summary>
    /// <param name="document">The document.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="capacity">The room reserved for the signature value, in bytes.</param>
    /// <returns>The dictionary; every value in it is direct (ISO 32000-2, Table 255, /ByteRange).</returns>
    internal static PdfDictionary Build(PdfDocument document, PdfSigningPlan plan, int capacity)
    {
        var store = document.Objects;
        var names = store.Names;
        var timestamp = plan.Format == PdfSignatureFormat.Rfc3161;
        var dictionary = new PdfDictionary(store, EntryCount(plan, timestamp));
        dictionary.Set(KnownName.Type, PdfValue.FromName(timestamp ? names.Intern("DocTimeStamp"u8) : KnownName.Sig));
        dictionary.Set(KnownName.Filter, PdfValue.FromName(names.Intern(Handler)));
        dictionary.Set(KnownName.SubFilter, PdfValue.FromName(names.Intern(PdfSigningChoices.FormatSpelling(plan.Format))));
        if (!timestamp)
        {
            AddIdentity(document, dictionary, plan);
            AddReferences(document, dictionary, plan);
        }

        var range = new PdfArray(store, ByteRangeNumbers);
        range.Add(PdfValue.FromInteger(0));
        range.Add(PdfValue.FromInteger(PlaceholderFirst));
        range.Add(PdfValue.FromInteger(PlaceholderSecond));
        range.Add(PdfValue.FromInteger(PlaceholderThird));
        dictionary.Set(KnownName.ByteRange, PdfValue.FromArray(range));
        dictionary.Set(KnownName.Contents, PdfValue.FromString(ZeroContents(capacity)));
        return dictionary;
    }

    /// <summary>Gets the placeholder for the signature value: zeros that the writer writes out and the signer later writes over in the file, so the array itself is never changed.</summary>
    /// <param name="capacity">The room reserved, in bytes.</param>
    /// <returns>The zeros; the array of the last signature is reused when the room is the same.</returns>
    private static byte[] ZeroContents(int capacity)
    {
        if (Volatile.Read(ref _zeroContents) is { } cached && cached.Length == capacity)
        {
            return cached;
        }

        var zeros = new byte[capacity];
        Volatile.Write(ref _zeroContents, zeros);
        return zeros;
    }

    /// <summary>Counts the entries the dictionary will hold, so it is made at its final size.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="timestamp">Whether this is a document timestamp.</param>
    /// <returns>The entries.</returns>
    private static int EntryCount(PdfSigningPlan plan, bool timestamp)
    {
        var count = BaseEntries;
        if (timestamp)
        {
            return count;
        }

        count += 1 + (plan.SignerName.Length > 0 ? 1 : 0) + (plan.Reason is null ? 0 : 1) + (plan.Location is null ? 0 : 1) + (plan.ContactInfo is null ? 0 : 1);
        return plan.IsCertification || plan.HasFieldLock ? count + 1 : count;
    }

    /// <summary>Adds the name, time, reason, location and contact entries.</summary>
    /// <param name="document">The document.</param>
    /// <param name="dictionary">The signature dictionary.</param>
    /// <param name="plan">The plan.</param>
    private static void AddIdentity(PdfDocument document, PdfDictionary dictionary, PdfSigningPlan plan)
    {
        var allowUtf8 = PdfDocumentVersioning.GetVersion(document) >= PdfVersion.Pdf20;
        if (plan.SignerName.Length > 0)
        {
            dictionary.Set(KnownName.Name, PdfValue.FromString(PdfText.Encode(plan.SignerName, allowUtf8)));
        }

        Span<byte> date = stackalloc byte[DateBufferLength];
        var length = PdfDate.Format(plan.Time, date);
        dictionary.Set(KnownName.M, PdfValue.FromString(date[..length].ToArray()));
        SetText(dictionary, KnownName.Reason, plan.Reason, allowUtf8);
        SetText(dictionary, KnownName.Location, plan.Location, allowUtf8);
        SetText(dictionary, KnownName.ContactInfo, plan.ContactInfo, allowUtf8);
    }

    /// <summary>Sets a text string entry when there is text.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    /// <param name="allowUtf8">Whether the document is PDF 2.0 or higher, so text may be UTF-8.</param>
    private static void SetText(PdfDictionary dictionary, PdfName key, string? text, bool allowUtf8)
    {
        if (text is not null)
        {
            dictionary.Set(key, PdfValue.FromString(PdfText.Encode(text, allowUtf8)));
        }
    }

    /// <summary>Adds the signature reference dictionaries: DocMDP for a certification and FieldMDP for a lock.</summary>
    /// <param name="document">The document.</param>
    /// <param name="dictionary">The signature dictionary.</param>
    /// <param name="plan">The plan.</param>
    private static void AddReferences(PdfDocument document, PdfDictionary dictionary, PdfSigningPlan plan)
    {
        if (!plan.IsCertification && !plan.HasFieldLock)
        {
            return;
        }

        var store = document.Objects;
        var references = new PdfArray(store, MaxReferences);
        var catalog = store.Trailer.GetRaw(KnownName.Root);
        if (plan.IsCertification)
        {
            var parameters = TransformParameters(store);
            parameters.Set(KnownName.P, PdfValue.FromInteger((int)plan.Certification));
            references.Add(PdfValue.FromDictionary(Reference(store, KnownName.DocMDP, parameters, catalog, plan.Digest)));
        }

        if (plan.HasFieldLock)
        {
            references.Add(PdfValue.FromDictionary(Reference(store, store.Names.Intern("FieldMDP"u8), FieldLockParameters(store, plan.FieldLock!), catalog, plan.Digest)));
        }

        dictionary.Set(KnownName.Reference, PdfValue.FromArray(references));
    }

    /// <summary>Gets the /DigestMethod a reference dictionary names: the signature's own digest where a reference may name it, otherwise SHA-256.</summary>
    /// <param name="digest">The signature's digest.</param>
    /// <returns>The name.</returns>
    private static ReadOnlySpan<byte> DigestName(PdfDigestMethod digest) => digest switch
    {
        PdfDigestMethod.Sha384 => "SHA384"u8,
        PdfDigestMethod.Sha512 => "SHA512"u8,
        _ => "SHA256"u8,
    };

    /// <summary>Builds a transform parameters dictionary with the type and version.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary TransformParameters(PdfObjectStore store)
    {
        var parameters = new PdfDictionary(store);
        parameters.Set(KnownName.Type, PdfValue.FromName(store.Names.Intern("TransformParams"u8)));
        parameters.Set(KnownName.V, PdfValue.FromName(store.Names.Intern(TransformVersion)));
        return parameters;
    }

    /// <summary>Builds the FieldMDP transform parameters of a lock: the action and fields, copied from the lock (ISO 32000-2, 12.8.2.4).</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="fieldLock">The lock.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary FieldLockParameters(PdfObjectStore store, PdfFieldLock fieldLock)
    {
        var parameters = TransformParameters(store);
        parameters.Set(store.Names.Intern("Action"u8), PdfValue.FromName(fieldLock.Action switch
        {
            PdfFieldLockAction.Include => store.Names.Intern("Include"u8),
            PdfFieldLockAction.Exclude => store.Names.Intern("Exclude"u8),
            _ => KnownName.All,
        }));
        if (fieldLock.Action is PdfFieldLockAction.Include or PdfFieldLockAction.Exclude)
        {
            var fields = new PdfArray(store, fieldLock.Fields.Length);
            foreach (var name in fieldLock.Fields)
            {
                fields.Add(PdfValue.FromString(PdfText.Encode(name)));
            }

            parameters.Set(KnownName.Fields, PdfValue.FromArray(fields));
        }

        if (fieldLock.Permission != PdfMdpPermission.None)
        {
            parameters.Set(KnownName.P, PdfValue.FromInteger((int)fieldLock.Permission));
        }

        return parameters;
    }

    /// <summary>Builds a signature reference dictionary (ISO 32000-2, Table 256).</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="method">The transform method.</param>
    /// <param name="parameters">The transform parameters.</param>
    /// <param name="catalog">The reference to the catalog, which is the object the transform applies to.</param>
    /// <param name="digest">The signature's digest, which decides the digest method name.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Reference(PdfObjectStore store, PdfName method, PdfDictionary parameters, PdfValue catalog, PdfDigestMethod digest)
    {
        var reference = new PdfDictionary(store);
        reference.Set(KnownName.Type, PdfValue.FromName(store.Names.Intern("SigRef"u8)));
        reference.Set(KnownName.TransformMethod, PdfValue.FromName(method));
        reference.Set(KnownName.TransformParams, PdfValue.FromDictionary(parameters));
        reference.Set(store.Names.Intern("Data"u8), catalog);
        reference.Set(store.Names.Intern("DigestMethod"u8), PdfValue.FromName(store.Names.Intern(DigestName(digest))));
        return reference;
    }
}
