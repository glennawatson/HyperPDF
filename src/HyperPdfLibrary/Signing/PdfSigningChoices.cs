// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using HyperPdf.Cms;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>Chooses the format, digest, certification and reason of a signature from the options, the field's seed values and the signing key.</summary>
internal static class PdfSigningChoices
{
    /// <summary>The first DocMDP permission value (no changes).</summary>
    private const int FirstPermission = 1;

    /// <summary>The last DocMDP permission value (annotating, form filling and signing).</summary>
    private const int LastPermission = 3;

    /// <summary>The digests tried, in order, when neither the options nor the seed values name one.</summary>
    private static readonly PdfDigestMethod[] DefaultDigests = [PdfDigestMethod.Sha256, PdfDigestMethod.Sha384, PdfDigestMethod.Sha512];

    /// <summary>Chooses the format.</summary>
    /// <param name="options">The options.</param>
    /// <param name="seed">The field's seed values, or <see langword="null"/>.</param>
    /// <param name="problems">Receives the reasons the choice is refused.</param>
    /// <returns>The format.</returns>
    internal static PdfSignatureFormat ChooseFormat(PdfSigningOptions options, PdfSeedValues? seed, List<string> problems)
    {
        var preferred = FirstFormat(seed);
        if (options.Format is not { } requested)
        {
            return ChooseDefaultFormat(preferred, seed, problems);
        }

        if (requested is not (PdfSignatureFormat.Pkcs7Detached or PdfSignatureFormat.CadesDetached))
        {
            problems.Add("Only adbe.pkcs7.detached and ETSI.CAdES.detached signatures can be made.");
            return PdfSignatureFormat.Pkcs7Detached;
        }

        if (RequiresFormat(seed) && preferred != requested)
        {
            problems.Add($"The signature field requires the {FormatName(preferred)} encoding, not {FormatName(requested)}.");
        }

        return requested;
    }

    /// <summary>Gets the name PDF uses for a signature format in /SubFilter.</summary>
    /// <param name="format">The format.</param>
    /// <returns>The name without the slash.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string FormatName(PdfSignatureFormat format) => Encoding.ASCII.GetString(FormatSpelling(format));

    /// <summary>Gets the name PDF uses for a signature format in /SubFilter as UTF-8 bytes.</summary>
    /// <param name="format">The format.</param>
    /// <returns>The name without the slash.</returns>
    internal static ReadOnlySpan<byte> FormatSpelling(PdfSignatureFormat format) => format switch
    {
        PdfSignatureFormat.CadesDetached => "ETSI.CAdES.detached"u8,
        PdfSignatureFormat.Rfc3161 => "ETSI.RFC3161"u8,
        PdfSignatureFormat.Pkcs7Detached => "adbe.pkcs7.detached"u8,
        _ => "an unsupported"u8,
    };

    /// <summary>Chooses the digest.</summary>
    /// <param name="options">The options.</param>
    /// <param name="seed">The field's seed values, or <see langword="null"/>.</param>
    /// <param name="signer">The signing certificate, or <see langword="null"/> when it is not known yet.</param>
    /// <param name="problems">Receives the reasons the choice is refused.</param>
    /// <returns>The digest.</returns>
    internal static PdfDigestMethod ChooseDigest(PdfSigningOptions options, PdfSeedValues? seed, PdfSignerCertificate? signer, List<string> problems)
    {
        var listed = seed is null ? [] : seed.DigestMethods;
        var required = seed is not null && seed.IsRequired(PdfSeedValues.DigestMethodFlag) && listed.Length > 0;
        if (options.Digest is not { } requested)
        {
            return FirstUsable(listed, required, signer, problems);
        }

        if (required && Array.IndexOf(listed, requested) < 0)
        {
            problems.Add($"The signature field requires one of these digests: {Names(listed)}.");
        }

        if (DigestProblem(requested, signer) is { } problem)
        {
            problems.Add(problem);
        }

        return requested;
    }

    /// <summary>Chooses the certification: the option, or the one the field's seed values require.</summary>
    /// <param name="options">The options.</param>
    /// <param name="seed">The field's seed values, or <see langword="null"/>.</param>
    /// <param name="notes">Receives what the user should know.</param>
    /// <param name="problems">Receives the reasons the choice is refused.</param>
    /// <returns>The permission; <see cref="PdfMdpPermission.None"/> for an approval signature.</returns>
    internal static PdfMdpPermission ChooseCertification(PdfSigningOptions options, PdfSeedValues? seed, List<string> notes, List<string> problems)
    {
        if (options.Certification is < PdfMdpPermission.None or > PdfMdpPermission.AnnotateFormFillAndSign)
        {
            problems.Add("The certification level must be none, no changes, form filling and signing, or annotating as well.");
            return PdfMdpPermission.None;
        }

        return seed is null || seed.Mdp < 0 ? options.Certification : ApplySeedCertification(options.Certification, seed.Mdp, notes, problems);
    }

    /// <summary>Chooses the reason to record.</summary>
    /// <param name="options">The options.</param>
    /// <param name="seed">The field's seed values, or <see langword="null"/>.</param>
    /// <param name="notes">Receives what the user should know.</param>
    /// <param name="problems">Receives the reasons the choice is refused.</param>
    /// <returns>The reason, or <see langword="null"/> to leave it out.</returns>
    internal static string? ChooseReason(PdfSigningOptions options, PdfSeedValues? seed, List<string> notes, List<string> problems)
    {
        var reason = string.IsNullOrWhiteSpace(options.Reason) ? null : options.Reason;
        if (seed is null || !seed.IsRequired(PdfSeedValues.ReasonsFlag))
        {
            return reason;
        }

        if (!seed.HasReasons || (seed.Reasons.Length == 1 && seed.Reasons[0] == "."))
        {
            if (reason is not null)
            {
                notes.Add("The signature field does not allow a reason, so none is recorded.");
            }

            return null;
        }

        if (reason is null)
        {
            notes.Add($"The signature field requires one of its reasons; \"{seed.Reasons[0]}\" is used.");
            return seed.Reasons[0];
        }

        if (Array.IndexOf(seed.Reasons, reason) < 0)
        {
            problems.Add($"The signature field requires one of these reasons: {string.Join("; ", seed.Reasons)}.");
        }

        return reason;
    }

    /// <summary>Gets the first sub-filter in a seed value list that this library makes.</summary>
    /// <param name="seed">The seed values, or <see langword="null"/>.</param>
    /// <returns>The format, or <see cref="PdfSignatureFormat.Unknown"/> when there is no list or no listed format is one this library makes.</returns>
    private static PdfSignatureFormat FirstFormat(PdfSeedValues? seed)
    {
        foreach (var format in seed is null ? [] : seed.SubFilters)
        {
            if (format != PdfSignatureFormat.Unknown)
            {
                return format;
            }
        }

        return PdfSignatureFormat.Unknown;
    }

    /// <summary>Determines whether the seed values require one of their sub-filters.</summary>
    /// <param name="seed">The seed values, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    private static bool RequiresFormat(PdfSeedValues? seed) => seed is { SubFilters.Length: > 0 } && seed.IsRequired(PdfSeedValues.SubFilterFlag);

    /// <summary>Chooses the format when the options do not name one.</summary>
    /// <param name="preferred">The first format the seed values list that this library makes.</param>
    /// <param name="seed">The seed values, or <see langword="null"/>.</param>
    /// <param name="problems">Receives the reasons the choice is refused.</param>
    /// <returns>The format.</returns>
    private static PdfSignatureFormat ChooseDefaultFormat(PdfSignatureFormat preferred, PdfSeedValues? seed, List<string> problems)
    {
        if (preferred != PdfSignatureFormat.Unknown)
        {
            return preferred;
        }

        if (RequiresFormat(seed))
        {
            problems.Add("The signature field requires a signature encoding this library does not make (it makes adbe.pkcs7.detached and ETSI.CAdES.detached).");
        }

        return PdfSignatureFormat.Pkcs7Detached;
    }

    /// <summary>Finds the first digest that can be used: from the seed values' list, then (unless the list is required) the usual ones.</summary>
    /// <param name="listed">The digests the seed values name.</param>
    /// <param name="required">Whether the list is a required constraint.</param>
    /// <param name="signer">The signing certificate, or <see langword="null"/>.</param>
    /// <param name="problems">Receives the reason none can be used.</param>
    /// <returns>The digest.</returns>
    private static PdfDigestMethod FirstUsable(PdfDigestMethod[] listed, bool required, PdfSignerCertificate? signer, List<string> problems)
    {
        if (TryFirstUsable(listed, signer, out var digest) || (!required && TryFirstUsable(DefaultDigests, signer, out digest)))
        {
            return digest;
        }

        problems.Add(required
            ? $"The signature field requires one of these digests, and this certificate cannot sign with any of them: {Names(listed)}."
            : "This certificate cannot sign with any digest this library offers.");
        return PdfDigestMethod.Sha256;
    }

    /// <summary>Finds the first digest in a list that the signing certificate can use.</summary>
    /// <param name="candidates">The digests, in order of preference.</param>
    /// <param name="signer">The signing certificate, or <see langword="null"/>.</param>
    /// <param name="digest">Receives the digest.</param>
    /// <returns><see langword="true"/> when one can be used.</returns>
    private static bool TryFirstUsable(PdfDigestMethod[] candidates, PdfSignerCertificate? signer, out PdfDigestMethod digest)
    {
        foreach (var candidate in candidates)
        {
            if (DigestProblem(candidate, signer) is not null)
            {
                continue;
            }

            digest = candidate;
            return true;
        }

        digest = PdfDigestMethod.Sha256;
        return false;
    }

    /// <summary>Applies the certification a field's seed values ask for.</summary>
    /// <param name="chosen">The certification the options ask for.</param>
    /// <param name="mdp">The seed values' /MDP /P value.</param>
    /// <param name="notes">Receives what the user should know.</param>
    /// <param name="problems">Receives the reasons the choice is refused.</param>
    /// <returns>The permission.</returns>
    private static PdfMdpPermission ApplySeedCertification(PdfMdpPermission chosen, int mdp, List<string> notes, List<string> problems)
    {
        var wanted = mdp is >= FirstPermission and <= LastPermission ? (PdfMdpPermission)mdp : PdfMdpPermission.None;
        if (chosen == PdfMdpPermission.None && wanted != PdfMdpPermission.None)
        {
            notes.Add("The signature field asks for a certification signature, so the document is certified.");
            return wanted;
        }

        if (chosen != wanted)
        {
            problems.Add(wanted == PdfMdpPermission.None
                ? "The signature field allows only an approval signature, not a certification."
                : $"The signature field requires a certification that allows {DescribeLevel(wanted)}.");
        }

        return chosen;
    }

    /// <summary>Explains why a digest cannot be used with a certificate.</summary>
    /// <param name="digest">The digest.</param>
    /// <param name="signer">The signing certificate, or <see langword="null"/> when it is not known.</param>
    /// <returns>The reason in plain words, or <see langword="null"/> when the digest can be used.</returns>
    private static string? DigestProblem(PdfDigestMethod digest, PdfSignerCertificate? signer)
    {
        var cms = PdfSigningDigests.ToCms(digest);
        if (cms == CmsDigestAlgorithm.Unknown)
        {
            return "MD5, SHA-1 and RIPEMD-160 are not used for new signatures. Choose SHA-256 or stronger.";
        }

        if (CmsDigests.Status(cms) != CmsDigestStatus.Available)
        {
            return $"This computer cannot compute the {PdfSigningDigests.DisplayName(digest)} digest. Choose SHA-256 instead.";
        }

        if (signer is not { } certificate)
        {
            return null;
        }

        if (!certificate.IsEllipticCurve)
        {
            return digest == PdfDigestMethod.Shake256
                ? "SHAKE256 signatures need an elliptic-curve (ECDSA) certificate; this certificate's key is not one. Choose SHA3-256 or SHA-256 instead."
                : null;
        }

        return CurveProblem(CmsEcRules.CheckCurveSigner(certificate.Parameters, cms), digest);
    }

    /// <summary>Explains in plain words why an elliptic-curve certificate cannot sign with a digest.</summary>
    /// <param name="rule">The rule the signer would break.</param>
    /// <param name="digest">The digest.</param>
    /// <returns>The explanation, or <see langword="null"/> when no rule is broken.</returns>
    private static string? CurveProblem(CmsSignatureStatus rule, PdfDigestMethod digest) => rule switch
    {
        CmsSignatureStatus.Valid => null,
        CmsSignatureStatus.CurveDigestNotAllowed =>
            $"This certificate's elliptic curve cannot sign with {PdfSigningDigests.DisplayName(digest)} in a PDF (ISO/TS 32002, Table 3). "
            + "Choose SHA-256 for P-256, SHA-384 for P-384, or SHA-512 for P-521 and brainpoolP512r1.",
        CmsSignatureStatus.UnnamedCurve => "This certificate gives its elliptic curve as explicit parameters, not a named curve, so a PDF signature cannot use it (ISO/TS 32002, 5.1.3).",
        _ => "This certificate's elliptic curve is not one PDF signatures use (P-256, P-384, P-521 or Brainpool), or this computer's cryptography library lacks it.",
    };

    /// <summary>Lists digest names for a message.</summary>
    /// <param name="digests">The digests.</param>
    /// <returns>The names, comma separated.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Names(PdfDigestMethod[] digests) => string.Join(", ", Array.ConvertAll(digests, PdfSigningDigests.DisplayName));

    /// <summary>Describes what a certification level allows, for a message.</summary>
    /// <param name="level">The level.</param>
    /// <returns>The description.</returns>
    private static string DescribeLevel(PdfMdpPermission level) => level switch
    {
        PdfMdpPermission.NoChanges => "no changes",
        PdfMdpPermission.FormFillAndSign => "form filling and signing",
        _ => "form filling, signing and annotating",
    };
}
