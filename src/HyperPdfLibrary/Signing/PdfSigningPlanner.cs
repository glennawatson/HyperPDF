// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Signing;

/// <summary>Works out what signing a document would do and whether it is allowed, without changing the document.</summary>
internal static class PdfSigningPlanner
{
    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The appearance a signature has when the options name none: invisible, every field of the record at its default.</summary>
    private static readonly PdfSignatureAppearance DefaultAppearance = new();

    /// <summary>Plans a signature.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The options.</param>
    /// <param name="identity">The signer, or <see langword="null"/> to plan without checking the certificate.</param>
    /// <returns>The plan with every reason it is refused.</returns>
    internal static PdfSigningPlanResult Plan(PdfDocument document, PdfSigningOptions options, PdfSignerIdentity? identity)
    {
        PdfSignerCertificate? signer = identity is { Certificates.Count: > 0 } ? PdfSignerCertificate.Read(identity.Certificates[0]) : null;
        var problems = PdfMessageLists.Rent();
        var notes = PdfMessageLists.Rent();
        try
        {
            var field = ResolveField(document, options, problems);
            var seed = PdfSeedValueReader.Read(field?.Dictionary.GetDictionary(KnownName.SV), document.Objects.Names);
            if (identity is not null && signer is null)
            {
                problems.Add("The signer has no certificate.");
            }

            var plan = Decide(document, options, new(field, seed, identity, signer), problems, notes);
            return new(plan, PdfMessageLists.Snapshot(problems), PdfMessageLists.Snapshot(notes), seed?.TimestampUrl, seed?.TimestampRequired == true);
        }
        finally
        {
            PdfMessageLists.Return(problems);
            PdfMessageLists.Return(notes);
        }
    }

    /// <summary>Makes the choices and checks them against the document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The options.</param>
    /// <param name="context">The field, seed values and signer.</param>
    /// <param name="problems">Receives the reasons signing is refused.</param>
    /// <param name="notes">Receives what the user should know.</param>
    /// <returns>The plan.</returns>
    private static PdfSigningPlan Decide(PdfDocument document, PdfSigningOptions options, in PlanContext context, List<string> problems, List<string> notes)
    {
        var (field, seed, identity, signer) = context;
        var store = document.Objects;
        var format = PdfSigningChoices.ChooseFormat(options, seed, problems);
        var digest = PdfSigningChoices.ChooseDigest(options, seed, signer, problems);
        var certification = PdfSigningChoices.ChooseCertification(options, seed, notes, problems);
        var reason = PdfSigningChoices.ChooseReason(options, seed, notes, problems);
        var lockDocument = ChooseLock(options, seed, problems);
        PdfSigningRules.Check(PdfDocumentSignatureValidation.GetSignatureDetails(document), field, certification, problems);
        PdfSeedChecks.Check(seed, options, identity, problems, notes);
        var appearance = ResolveAppearance(document, options, field, problems);
        CheckEmbeddedFonts(document, appearance, problems);
        AddVersionNotes(document, format, digest, notes);
        return new(
            format,
            digest,
            certification,
            lockDocument ? new PdfFieldLock(PdfFieldLockAction.All, [], PdfMdpPermission.None) : PdfSigningRules.FieldLockOf(field, store.Names),
            reason,
            Clean(options.Location),
            Clean(options.ContactInfo),
            SignerName(options, signer),
            options.SigningTime ?? (options.Clock ?? TimeProvider.System).GetUtcNow(),
            field,
            field?.Name ?? ChooseName(store, options, problems),
            appearance);
    }

    /// <summary>Gets the name to record for the signer.</summary>
    /// <param name="options">The options.</param>
    /// <param name="signer">The signing certificate, or <see langword="null"/>.</param>
    /// <returns>The name, or an empty string when it is not known.</returns>
    private static string SignerName(PdfSigningOptions options, PdfSignerCertificate? signer)
    {
        if (options.SignerName is { } name)
        {
            return name;
        }

        if (signer is not { } certificate)
        {
            return string.Empty;
        }

        // A subject with a plain-text common name gives the platform's simple name without loading the platform certificate.
        if (certificate.CommonName() is { } common)
        {
            return common;
        }

        using var loaded = X509CertificateLoader.LoadCertificate(certificate.Der);
        return loaded.GetNameInfo(X509NameType.SimpleName, false);
    }

    /// <summary>Turns blank text into <see langword="null"/>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text, or <see langword="null"/> when it is empty or only whitespace.</returns>
    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    /// <summary>Finds the existing field to sign.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The options.</param>
    /// <param name="problems">Receives the reasons the field cannot be signed.</param>
    /// <returns>The field, or <see langword="null"/> when a new field is added.</returns>
    private static PdfSigningField? ResolveField(PdfDocument document, PdfSigningOptions options, List<string> problems)
    {
        if (options.FieldName is not { } name)
        {
            return null;
        }

        var field = PdfSigningFields.Find(document.Objects, name);
        if (field is null)
        {
            problems.Add($"The document has no signature field named \"{name}\".");
        }
        else if (field.IsSigned)
        {
            problems.Add($"The signature field \"{name}\" is already signed.");
        }
        else if (!field.Id.IsValid)
        {
            problems.Add($"The signature field \"{name}\" is not an object of its own in the file, so it cannot be signed in an update.");
        }

        return field;
    }

    /// <summary>Chooses the name of a new field.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="options">The options.</param>
    /// <param name="problems">Receives the reason a name is refused.</param>
    /// <returns>The name.</returns>
    private static string ChooseName(PdfObjectStore store, PdfSigningOptions options, List<string> problems)
    {
        if (options.NewFieldName is not { Length: > 0 } name)
        {
            return PdfSigningFields.NextName(store);
        }

        if (PdfSigningFields.IsUsed(store, name))
        {
            problems.Add($"The document already has a field named \"{name}\".");
        }
        else if (name.Contains('.', StringComparison.Ordinal))
        {
            problems.Add("A new field's name cannot contain a full stop; the full stop separates the parts of a field's full name.");
        }

        return name;
    }

    /// <summary>Decides whether the signature locks the document.</summary>
    /// <param name="options">The options.</param>
    /// <param name="seed">The field's seed values, or <see langword="null"/>.</param>
    /// <param name="problems">Receives the reasons the choice is refused.</param>
    /// <returns><see langword="true"/> when every field is locked after signing.</returns>
    private static bool ChooseLock(PdfSigningOptions options, PdfSeedValues? seed, List<string> problems)
    {
        var intent = seed?.LockDocument ?? PdfSeedLockDocument.Auto;
        var wanted = options.LockDocument ?? (intent == PdfSeedLockDocument.Lock);
        if (seed?.IsRequired(PdfSeedValues.LockDocumentFlag) == true && intent != PdfSeedLockDocument.Auto && wanted != (intent == PdfSeedLockDocument.Lock))
        {
            problems.Add(intent == PdfSeedLockDocument.Lock
                ? "The signature field requires the document to be locked when it is signed."
                : "The signature field does not allow the document to be locked when it is signed.");
        }

        return wanted;
    }

    /// <summary>Works out the page and rectangle of the signature.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The options.</param>
    /// <param name="field">The existing field, or <see langword="null"/>.</param>
    /// <param name="problems">Receives the reasons the page is refused.</param>
    /// <returns>The appearance with its final page and rectangle.</returns>
    private static PdfSignatureAppearance ResolveAppearance(PdfDocument document, PdfSigningOptions options, PdfSigningField? field, List<string> problems)
    {
        var requested = options.Appearance ?? DefaultAppearance;
        if (field is null)
        {
            if ((uint)requested.PageIndex >= (uint)document.PageCount)
            {
                problems.Add($"The document has no page {requested.PageIndex + 1}.");
            }

            return requested;
        }

        var rectangle = field.Widget is not null && field.Widget.TryGetRectangle(KnownName.Rect, out var found) ? found : default;
        return requested with
        {
            PageIndex = Math.Max(0, PdfSigningFields.PageOf(document, field)),
            Rectangle = requested.Rectangle.IsEmpty ? rectangle : requested.Rectangle,
        };
    }

    /// <summary>Checks that a visible signature can be drawn in a PDF/A or PDF/UA file.</summary>
    /// <param name="document">The document.</param>
    /// <param name="appearance">The appearance.</param>
    /// <param name="problems">Receives the reasons it cannot.</param>
    private static void CheckEmbeddedFonts(PdfDocument document, PdfSignatureAppearance appearance, List<string> problems)
    {
        var part = PdfStampConformance.GetPdfAPart(document);
        var universalAccess = PdfDocumentMetadata.GetXmp(document)?.PdfUaPart is not null;
        if (!appearance.IsVisible || (part == 0 && !universalAccess))
        {
            return;
        }

        if (!appearance.DocumentFont.IsValid && appearance.HasText)
        {
            problems.Add(
                $"The document claims {(part > 0 ? "PDF/A" : "PDF/UA")}, which needs every font embedded, and the built-in fonts are not. "
                + "Choose a font already embedded in the document, turn the text off, or sign with no visible appearance.");
        }

        CheckTransparency(part, appearance, problems);
    }

    /// <summary>Checks that a signature image has no transparency in a PDF/A-1 file.</summary>
    /// <param name="part">The PDF/A part the document claims, or zero.</param>
    /// <param name="appearance">The appearance.</param>
    /// <param name="problems">Receives the reason it cannot.</param>
    private static void CheckTransparency(int part, PdfSignatureAppearance appearance, List<string> problems)
    {
        if (part == 1 && appearance.Image is { } image && HasTransparency(in image))
        {
            problems.Add("The document claims PDF/A-1, which does not allow transparency, so the signature image must be opaque.");
        }
    }

    /// <summary>Determines whether a BGRA image has any pixel that is not fully opaque.</summary>
    /// <param name="image">The image.</param>
    /// <returns><see langword="true"/> when one is not.</returns>
    private static bool HasTransparency(in PdfWatermarkBitmap image)
    {
        var pixels = image.Pixels.Span;
        for (var at = BytesPerPixel - 1; at < pixels.Length; at += BytesPerPixel)
        {
            if (pixels[at] != byte.MaxValue)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Adds the notes about what the signature does to the PDF version.</summary>
    /// <param name="document">The document.</param>
    /// <param name="format">The format.</param>
    /// <param name="digest">The digest.</param>
    /// <param name="notes">Receives the notes.</param>
    private static void AddVersionNotes(PdfDocument document, PdfSignatureFormat format, PdfDigestMethod digest, List<string> notes)
    {
        if (PdfDocumentVersioning.GetVersion(document) >= PdfVersion.Pdf20)
        {
            return;
        }

        if (PdfSigningDigests.IsSha3Family(digest))
        {
            notes.Add($"{PdfSigningDigests.DisplayName(digest)} is a PDF 2.0 extension (ISO/TS 32001), so the file is raised to PDF 2.0 and declares it.");
        }
        else if (format == PdfSignatureFormat.CadesDetached)
        {
            notes.Add("A PAdES (ETSI.CAdES.detached) signature in a file older than PDF 2.0 is declared with the ESIC developer extension.");
        }
    }

    /// <summary>The field, seed values and signer a plan is made for.</summary>
    /// <param name="Field">The existing field, or <see langword="null"/>.</param>
    /// <param name="Seed">The field's seed values, or <see langword="null"/>.</param>
    /// <param name="Identity">The signer, or <see langword="null"/>.</param>
    /// <param name="Signer">The signing certificate, or <see langword="null"/>.</param>
    private readonly record struct PlanContext(PdfSigningField? Field, PdfSeedValues? Seed, PdfSignerIdentity? Identity, PdfSignerCertificate? Signer);
}
