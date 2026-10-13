// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signing;

/// <summary>A certificate seed value dictionary (ISO 32000-2, Table 238): what the signing certificate must be.</summary>
/// <param name="Flags">The /Ff bits: a set bit makes the matching entry a required constraint.</param>
/// <param name="Subjects">The acceptable signing certificates, DER encoded.</param>
/// <param name="Issuers">The acceptable issuer certificates, DER encoded.</param>
/// <param name="Policies">The certificate policy object identifiers that must be present.</param>
/// <param name="SubjectNames">The subject distinguished name attributes required, one set per dictionary; each pairs an attribute name or OID with a value.</param>
/// <param name="KeyUsages">The acceptable key usage patterns, such as <c>1XXXXXXXX</c>.</param>
[DebuggerDisplay("PdfSeedCertificate: flags {Flags}")]
internal sealed record PdfSeedCertificate(
    int Flags,
    byte[][] Subjects,
    byte[][] Issuers,
    string[] Policies,
    PdfSeedNameAttribute[][] SubjectNames,
    string[] KeyUsages)
{
    /// <summary>The /Ff bit for the acceptable certificates.</summary>
    internal const int SubjectFlag = 0x1;

    /// <summary>The /Ff bit for the acceptable issuers.</summary>
    internal const int IssuerFlag = 0x2;

    /// <summary>The /Ff bit for the certificate policies.</summary>
    internal const int PolicyFlag = 0x4;

    /// <summary>The /Ff bit for the subject name attributes.</summary>
    internal const int SubjectNameFlag = 0x8;

    /// <summary>The /Ff bit for the key usage.</summary>
    internal const int KeyUsageFlag = 0x20;

    /// <summary>Determines whether an entry is a required constraint.</summary>
    /// <param name="flag">The /Ff bit of the entry.</param>
    /// <returns><see langword="true"/> when the entry must be honoured.</returns>
    internal bool IsRequired(int flag) => (Flags & flag) != 0;
}
