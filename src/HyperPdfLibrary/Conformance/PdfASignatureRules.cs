// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// PDF/A rules on permissions and signatures (ISO 19005-2, 6.1.12 and 6.4.3; ISO 19005-4, 6.1.11): only /DocMDP and /UR3
/// permissions, no digest entries beside /DocMDP, a byte range over the whole file, and one signer with its certificate.
/// </summary>
internal static class PdfASignatureRules
{
    /// <summary>The context of failures in signatures.</summary>
    private const string Where = "signature";

    /// <summary>The deepest field tree walked.</summary>
    private const int MaxFieldDepth = 64;

    /// <summary>Records the rules the permissions and signatures break.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="layout">The cross-reference sections.</param>
    internal static void Check(PdfAFileScan scan, PdfAXrefLayout layout)
    {
        var context = scan.Context;
        var names = scan.Store.Names;
        var catalog = context.Document.Catalog;
        var permissions = catalog.GetDictionary(KnownName.Perms);
        if (permissions is not null)
        {
            var ur3 = names.Intern("UR3"u8);
            context.Require(OnlyKeys(permissions, KnownName.DocMDP, ur3), PdfARule.Permissions, PdfAContexts.Catalog);
            context.Require(OnlyKeys(permissions, KnownName.DocMDP, KnownName.DocMDP), PdfARule.PermissionsPdfA4, PdfAContexts.Catalog);
        }

        var hasDocMdp = permissions is not null && !permissions.GetRaw(KnownName.DocMDP).IsNull;
        var fields = catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields);
        for (var i = 0; fields is not null && i < fields.Count; i++)
        {
            if (fields.GetDictionary(i) is { } field)
            {
                CheckField(scan, layout, field, hasDocMdp, 0);
            }
        }
    }

    /// <summary>Determines whether a dictionary's keys with values other than null are among the allowed ones.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="first">An allowed key.</param>
    /// <param name="second">Another allowed key.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    private static bool OnlyKeys(PdfDictionary dictionary, PdfName first, PdfName second)
    {
        for (var i = 0; i < dictionary.Count; i++)
        {
            var key = dictionary.GetKeyAt(i);
            if (key != first && key != second && !dictionary.GetValueAt(i).IsNull)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Checks a field's signature and its child fields.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="layout">The cross-reference sections.</param>
    /// <param name="field">The field.</param>
    /// <param name="hasDocMdp">Whether the permissions hold /DocMDP.</param>
    /// <param name="depth">The field's depth.</param>
    private static void CheckField(PdfAFileScan scan, PdfAXrefLayout layout, PdfDictionary field, bool hasDocMdp, int depth)
    {
        if (depth > MaxFieldDepth)
        {
            return;
        }

        if (IsSignatureField(field) && field.GetRaw(KnownName.V) is var reference && StoreReading.Resolve(scan.Store, reference) is { Kind: PdfKind.Dictionary } value)
        {
            CheckSignature(scan, layout, value.AsDictionary()!, reference, hasDocMdp);
        }

        var kids = field.GetArray(KnownName.Kids);
        for (var i = 0; kids is not null && i < kids.Count; i++)
        {
            if (kids.GetDictionary(i) is { } kid && !kid.GetRaw(KnownName.T).IsNull)
            {
                CheckField(scan, layout, kid, hasDocMdp, depth + 1);
            }
        }
    }

    /// <summary>Determines whether a field's type, inherited through /Parent, is /Sig.</summary>
    /// <param name="field">The field.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsSignatureField(PdfDictionary field)
    {
        var node = field;
        for (var depth = 0; node is not null && depth <= MaxFieldDepth; depth++)
        {
            if (node.Get(KnownName.FT).TryGetName(out var type))
            {
                return type.ToKnownName() == KnownName.Sig;
            }

            node = node.GetDictionary(KnownName.Parent);
        }

        return false;
    }

    /// <summary>Checks one signature dictionary.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="layout">The cross-reference sections.</param>
    /// <param name="signature">The signature dictionary.</param>
    /// <param name="reference">The field's /V value, which locates the dictionary in the file.</param>
    /// <param name="hasDocMdp">Whether the permissions hold /DocMDP.</param>
    private static void CheckSignature(PdfAFileScan scan, PdfAXrefLayout layout, PdfDictionary signature, PdfValue reference, bool hasDocMdp)
    {
        var context = scan.Context;
        var references = signature.GetArray(KnownName.Reference);
        for (var i = 0; hasDocMdp && references is not null && i < references.Count; i++)
        {
            if (references.GetDictionary(i) is { } sigRef)
            {
                context.Require(!HasDigestEntries(scan.Store, sigRef), PdfARule.SignatureReferenceDigest, Where);
            }
        }

        if (signature.Get(KnownName.Contents) is { Kind: PdfKind.String } contents)
        {
            CheckContents(context, contents.AsStringBytes());
        }

        context.Require(PdfASignatureByteRange.CoversFile(scan, layout, signature, reference), PdfARule.SignatureByteRange, Where);
    }

    /// <summary>Determines whether a signature reference has /DigestLocation, /DigestMethod or /DigestValue.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="sigRef">The signature reference dictionary.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasDigestEntries(PdfObjectStore store, PdfDictionary sigRef)
    {
        var names = store.Names;
        return !sigRef.GetRaw(names.Intern("DigestLocation"u8)).IsNull
            || !sigRef.GetRaw(names.Intern("DigestMethod"u8)).IsNull
            || !sigRef.GetRaw(names.Intern("DigestValue"u8)).IsNull;
    }

    /// <summary>Checks the PKCS#7 container: exactly one signer, and certificates.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="contents">The /Contents bytes, which may be padded with zeros.</param>
    private static void CheckContents(PdfAValidationContext context, ReadOnlySpan<byte> contents)
    {
        var signers = 0;
        var certificates = 0;
        try
        {
            var length = PdfCmsVerifier.TryGetEncodedLength(contents, out var encoded) ? encoded : contents.Length;
            var cms = new SignedCms();
            cms.Decode(contents[..length]);
            signers = cms.SignerInfos.Count;
            certificates = cms.Certificates.Count;
        }
        catch (Exception ex) when (ex is CryptographicException or AsnContentException)
        {
            // veraPDF reads a container it cannot parse as one with no signers and no certificates.
        }

        context.Require(signers == 1, PdfARule.SignatureSignerCount, Where);
        context.Require(certificates > 0, PdfARule.SignatureCertificate, Where);
    }
}
