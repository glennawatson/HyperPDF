// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Signing;

/// <summary>Checks a signing certificate against a field's certificate seed value constraints that are marked required (ISO 32000-2, Table 238).</summary>
internal static class PdfSeedCertificateCheck
{
    /// <summary>The certificate policies extension (RFC 5280, 4.2.1.4).</summary>
    private const string CertificatePoliciesOid = "2.5.29.32";

    /// <summary>The number of key usage characters the specification defines.</summary>
    private const int KeyUsageCharacters = 9;

    /// <summary>The key usages in the order Table 238 numbers them.</summary>
    private static readonly X509KeyUsageFlags[] KeyUsageOrder =
    [
        X509KeyUsageFlags.DigitalSignature,
        X509KeyUsageFlags.NonRepudiation,
        X509KeyUsageFlags.KeyEncipherment,
        X509KeyUsageFlags.DataEncipherment,
        X509KeyUsageFlags.KeyAgreement,
        X509KeyUsageFlags.KeyCertSign,
        X509KeyUsageFlags.CrlSign,
        X509KeyUsageFlags.EncipherOnly,
        X509KeyUsageFlags.DecipherOnly,
    ];

    /// <summary>Checks a certificate and adds a plain reason to the problems for each required constraint it breaks.</summary>
    /// <param name="seed">The constraints.</param>
    /// <param name="signer">The signing certificate.</param>
    /// <param name="chain">The DER certificates that go with it, the signing certificate first.</param>
    /// <param name="problems">Receives the reasons.</param>
    internal static void Check(PdfSeedCertificate seed, X509Certificate2 signer, IReadOnlyList<byte[]> chain, List<string> problems)
    {
        if (seed.IsRequired(PdfSeedCertificate.SubjectFlag) && !IsAcceptable(seed.Subjects, signer.RawDataMemory.Span))
        {
            problems.Add("The signature field accepts only certain certificates, and this one is not among them.");
        }

        if (seed.IsRequired(PdfSeedCertificate.IssuerFlag) && seed.Issuers.Length > 0 && !HasIssuer(seed.Issuers, chain))
        {
            problems.Add("The signature field accepts only certificates from certain issuers, and this certificate does not come from one of them.");
        }

        CheckDetails(seed, signer, problems);
    }

    /// <summary>Checks the policy, subject name and key usage constraints.</summary>
    /// <param name="seed">The constraints.</param>
    /// <param name="signer">The signing certificate.</param>
    /// <param name="problems">Receives the reasons.</param>
    private static void CheckDetails(PdfSeedCertificate seed, X509Certificate2 signer, List<string> problems)
    {
        if (seed.IsRequired(PdfSeedCertificate.PolicyFlag) && seed.Policies.Length > 0 && !HasPolicy(seed.Policies, signer))
        {
            problems.Add("The signature field needs a certificate with a particular certificate policy, and this one does not have it.");
        }

        if (seed.IsRequired(PdfSeedCertificate.SubjectNameFlag) && !HasSubjectNames(seed.SubjectNames, signer))
        {
            problems.Add("The signature field needs a certificate whose subject name has certain details, and this one lacks them.");
        }

        if (seed.IsRequired(PdfSeedCertificate.KeyUsageFlag) && seed.KeyUsages.Length > 0 && !HasKeyUsage(seed.KeyUsages, signer))
        {
            problems.Add("The signature field needs a certificate allowed to sign with the key usage it names, and this one is not.");
        }
    }

    /// <summary>Determines whether a certificate is one of the acceptable ones; an empty list accepts any.</summary>
    /// <param name="acceptable">The acceptable certificates.</param>
    /// <param name="certificate">The certificate.</param>
    /// <returns><see langword="true"/> when it is acceptable.</returns>
    private static bool IsAcceptable(byte[][] acceptable, ReadOnlySpan<byte> certificate)
    {
        if (acceptable.Length == 0)
        {
            return true;
        }

        foreach (var item in acceptable)
        {
            if (item.AsSpan().SequenceEqual(certificate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether any certificate after the signer's is one of the acceptable issuers.</summary>
    /// <param name="issuers">The acceptable issuers.</param>
    /// <param name="chain">The certificates, the signer's first.</param>
    /// <returns><see langword="true"/> when one matches.</returns>
    private static bool HasIssuer(byte[][] issuers, IReadOnlyList<byte[]> chain)
    {
        for (var i = 1; i < chain.Count; i++)
        {
            if (IsAcceptable(issuers, chain[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether the certificate carries one of the policies.</summary>
    /// <param name="policies">The acceptable policy object identifiers.</param>
    /// <param name="signer">The certificate.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasPolicy(string[] policies, X509Certificate2 signer)
    {
        foreach (var extension in signer.Extensions)
        {
            if (extension.Oid?.Value != CertificatePoliciesOid)
            {
                continue;
            }

            foreach (var found in PoliciesOf(extension.RawData))
            {
                if (Array.IndexOf(policies, found) >= 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Reads the policy identifiers from a certificate policies extension.</summary>
    /// <param name="raw">The extension's value.</param>
    /// <returns>The identifiers.</returns>
    private static List<string> PoliciesOf(byte[] raw)
    {
        var found = new List<string>();
        try
        {
            var policies = new AsnReader(raw, AsnEncodingRules.DER).ReadSequence();
            while (policies.HasData)
            {
                var information = policies.ReadSequence();
                found.Add(information.ReadObjectIdentifier());
            }
        }
        catch (AsnContentException)
        {
            // A damaged extension lists no usable policy.
        }

        return found;
    }

    /// <summary>Determines whether the subject name has every attribute of every required set.</summary>
    /// <param name="sets">The required sets.</param>
    /// <param name="signer">The certificate.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasSubjectNames(PdfSeedNameAttribute[][] sets, X509Certificate2 signer)
    {
        var present = new List<PdfSeedNameAttribute>();
        foreach (var part in signer.SubjectName.EnumerateRelativeDistinguishedNames())
        {
            present.Add(new(part.GetSingleElementType().Value ?? string.Empty, part.GetSingleElementValue() ?? string.Empty));
        }

        foreach (var set in sets)
        {
            foreach (var wanted in set)
            {
                if (!present.Contains(new(OidOf(wanted.Key), wanted.Value)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Gets the object identifier of a distinguished name attribute named by its usual abbreviation, or returns the text when it already is one.</summary>
    /// <param name="key">The attribute name or identifier.</param>
    /// <returns>The object identifier.</returns>
    private static string OidOf(string key) => key switch
    {
        "CN" => "2.5.4.3",
        "SN" => "2.5.4.4",
        "SERIALNUMBER" or "serialNumber" => "2.5.4.5",
        "C" => "2.5.4.6",
        "L" => "2.5.4.7",
        "ST" => "2.5.4.8",
        "O" => "2.5.4.10",
        "OU" => "2.5.4.11",
        "T" or "TITLE" => "2.5.4.12",
        "GN" => "2.5.4.42",
        "E" or "emailAddress" => "1.2.840.113549.1.9.1",
        "DC" => "0.9.2342.19200300.100.1.25",
        _ => key,
    };

    /// <summary>Determines whether the certificate matches any acceptable key usage pattern.</summary>
    /// <param name="patterns">The patterns, such as <c>1XXXXXXXX</c>.</param>
    /// <param name="signer">The certificate.</param>
    /// <returns><see langword="true"/> when one matches.</returns>
    private static bool HasKeyUsage(string[] patterns, X509Certificate2 signer)
    {
        var usage = X509KeyUsageFlags.None;
        foreach (var extension in signer.Extensions)
        {
            if (extension is X509KeyUsageExtension keyUsage)
            {
                usage = keyUsage.KeyUsages;
            }
        }

        foreach (var pattern in patterns)
        {
            if (Matches(pattern, usage))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks one key usage pattern: 1 means the usage must be set, 0 that it must not be, and anything else that it does not matter.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="usage">The certificate's key usage.</param>
    /// <returns><see langword="true"/> when the usage fits.</returns>
    private static bool Matches(string pattern, X509KeyUsageFlags usage)
    {
        for (var i = 0; i < KeyUsageCharacters && i < pattern.Length; i++)
        {
            var isSet = (usage & KeyUsageOrder[i]) != 0;
            if ((pattern[i] == '1' && !isSet) || (pattern[i] == '0' && isSet))
            {
                return false;
            }
        }

        return true;
    }
}
