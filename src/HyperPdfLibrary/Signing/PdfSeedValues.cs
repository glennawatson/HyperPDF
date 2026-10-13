// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>A signature field's seed value dictionary (ISO 32000-2, Table 237) as signing reads it.</summary>
/// <param name="Flags">The /Ff bits: a set bit makes the matching entry a required constraint.</param>
/// <param name="Filter">The required signature handler, or <see langword="null"/>.</param>
/// <param name="SubFilters">The acceptable sub-filter names in order of preference; <see cref="PdfSignatureFormat.Unknown"/> where the name is not one signing makes.</param>
/// <param name="DigestMethods">The acceptable digests in order; empty when the entry is absent.</param>
/// <param name="MinimumVersion">The /V entry: the capability level the seed value parser needs.</param>
/// <param name="Reasons">The reasons offered; empty when the entry is absent.</param>
/// <param name="HasReasons">Whether the /Reasons entry is present.</param>
/// <param name="Mdp">The /MDP /P value, or -1 when the entry is absent.</param>
/// <param name="TimestampUrl">The timestamp authority's URL, or <see langword="null"/>.</param>
/// <param name="TimestampRequired">Whether the signature must have a timestamp.</param>
/// <param name="AddRevInfo">Whether revocation information must be embedded in the signature.</param>
/// <param name="LockDocument">The /LockDocument intent.</param>
/// <param name="AppearanceFilter">The named appearance to use, or <see langword="null"/>.</param>
/// <param name="Certificate">The certificate constraints, or <see langword="null"/>.</param>
[DebuggerDisplay("PdfSeedValues: flags {Flags}")]
internal sealed record PdfSeedValues(
    int Flags,
    string? Filter,
    PdfSignatureFormat[] SubFilters,
    PdfDigestMethod[] DigestMethods,
    int MinimumVersion,
    string[] Reasons,
    bool HasReasons,
    int Mdp,
    string? TimestampUrl,
    bool TimestampRequired,
    bool AddRevInfo,
    PdfSeedLockDocument LockDocument,
    string? AppearanceFilter,
    PdfSeedCertificate? Certificate)
{
    /// <summary>The /Ff bit for the signature handler.</summary>
    internal const int FilterFlag = 0x1;

    /// <summary>The /Ff bit for the sub-filter.</summary>
    internal const int SubFilterFlag = 0x2;

    /// <summary>The /Ff bit for the seed value parser version.</summary>
    internal const int VersionFlag = 0x4;

    /// <summary>The /Ff bit for the reasons.</summary>
    internal const int ReasonsFlag = 0x8;

    /// <summary>The /Ff bit for the revocation information.</summary>
    internal const int RevocationFlag = 0x20;

    /// <summary>The /Ff bit for the digest method.</summary>
    internal const int DigestMethodFlag = 0x40;

    /// <summary>The /Ff bit for locking the document.</summary>
    internal const int LockDocumentFlag = 0x80;

    /// <summary>The /Ff bit for the named appearance.</summary>
    internal const int AppearanceFilterFlag = 0x100;

    /// <summary>The highest seed value parser version this reader understands (ISO 32000-2, Table 237).</summary>
    internal const int SupportedVersion = 3;

    /// <summary>Determines whether an entry is a required constraint.</summary>
    /// <param name="flag">The /Ff bit of the entry.</param>
    /// <returns><see langword="true"/> when the entry must be honoured.</returns>
    internal bool IsRequired(int flag) => (Flags & flag) != 0;
}
