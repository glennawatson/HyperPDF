// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Signing;

/// <summary>Checks the seed value constraints of a signature field that format, digest, reasons and certification do not cover (ISO 32000-2, Table 237).</summary>
internal static class PdfSeedChecks
{
    /// <summary>Checks the constraints and adds a plain reason to the problems for each required one the signature would break.</summary>
    /// <param name="seed">The field's seed values, or <see langword="null"/>.</param>
    /// <param name="options">The options.</param>
    /// <param name="identity">The signer, or <see langword="null"/> when it is not known yet.</param>
    /// <param name="problems">Receives the reasons signing is refused.</param>
    /// <param name="notes">Receives what the user should know.</param>
    internal static void Check(PdfSeedValues? seed, PdfSigningOptions options, PdfSignerIdentity? identity, List<string> problems, List<string> notes)
    {
        if (seed is null)
        {
            return;
        }

        if (seed.IsRequired(PdfSeedValues.FilterFlag) && seed.Filter is { } filter && !PdfSeedValueReader.IsSupportedHandler(filter))
        {
            problems.Add($"The signature field requires the \"{filter}\" signature handler, which this library does not provide (it uses Adobe.PPKLite).");
        }

        if (seed.IsRequired(PdfSeedValues.VersionFlag) && seed.MinimumVersion > PdfSeedValues.SupportedVersion)
        {
            problems.Add("The signature field needs a newer seed value parser than this library has, so its constraints cannot be honoured.");
        }

        CheckRevocation(seed, problems, notes);
        CheckAppearanceFilter(seed, problems, notes);
        CheckTimestamp(seed, options, problems, notes);
        CheckCertificate(seed, identity, problems);
    }

    /// <summary>Checks the signing certificate against the field's certificate constraints.</summary>
    /// <param name="seed">The seed values.</param>
    /// <param name="identity">The signer, or <see langword="null"/> when it is not known yet.</param>
    /// <param name="problems">Receives the reasons signing is refused.</param>
    private static void CheckCertificate(PdfSeedValues seed, PdfSignerIdentity? identity, List<string> problems)
    {
        if (seed.Certificate is not { } constraints || identity is not { Certificates.Count: > 0 })
        {
            return;
        }

        // The platform certificate object is only needed to read extensions and names, which only the field's certificate constraints do.
        using var certificate = X509CertificateLoader.LoadCertificate(identity.Certificates[0]);
        PdfSeedCertificateCheck.Check(constraints, certificate, identity.Certificates, problems);
    }

    /// <summary>Checks the request to embed revocation information in the signature.</summary>
    /// <param name="seed">The seed values.</param>
    /// <param name="problems">Receives the reasons signing is refused.</param>
    /// <param name="notes">Receives what the user should know.</param>
    private static void CheckRevocation(PdfSeedValues seed, List<string> problems, List<string> notes)
    {
        if (!seed.AddRevInfo)
        {
            return;
        }

        if (seed.IsRequired(PdfSeedValues.RevocationFlag))
        {
            problems.Add(
                "The signature field requires revocation information inside the signature, which this library does not put there. "
                + "Add long-term validation data to the document after signing instead.");
        }
        else
        {
            notes.Add("The signature field asks for revocation information inside the signature, which this library does not put there. Add long-term validation data to the document after signing.");
        }
    }

    /// <summary>Checks the request for a named appearance.</summary>
    /// <param name="seed">The seed values.</param>
    /// <param name="problems">Receives the reasons signing is refused.</param>
    /// <param name="notes">Receives what the user should know.</param>
    private static void CheckAppearanceFilter(PdfSeedValues seed, List<string> problems, List<string> notes)
    {
        if (seed.AppearanceFilter is not { } name)
        {
            return;
        }

        if (seed.IsRequired(PdfSeedValues.AppearanceFilterFlag))
        {
            problems.Add($"The signature field requires the named appearance \"{name}\", which is not available here.");
        }
        else
        {
            notes.Add($"The signature field prefers the named appearance \"{name}\", which is not available here, so a standard one is used.");
        }
    }

    /// <summary>Checks the request for a signature timestamp.</summary>
    /// <param name="seed">The seed values.</param>
    /// <param name="options">The options.</param>
    /// <param name="problems">Receives the reasons signing is refused.</param>
    /// <param name="notes">Receives what the user should know.</param>
    private static void CheckTimestamp(PdfSeedValues seed, PdfSigningOptions options, List<string> problems, List<string> notes)
    {
        if (options.Timestamp is not null || (seed.TimestampUrl is null && !seed.TimestampRequired))
        {
            return;
        }

        var where = seed.TimestampUrl is { } url ? $" from {url}" : string.Empty;
        if (seed.TimestampRequired)
        {
            problems.Add($"The signature field requires a timestamp{where}, and no timestamp client was given.");
        }
        else
        {
            notes.Add($"The signature field suggests a timestamp{where}.");
        }
    }
}
