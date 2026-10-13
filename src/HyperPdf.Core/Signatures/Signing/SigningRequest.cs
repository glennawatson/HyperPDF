// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>What a certificate signature records.</summary>
/// <param name="PageIndex">The page the signature field belongs to.</param>
/// <param name="Reason">Why the document is signed, for example "Approved"; may be empty.</param>
/// <param name="Location">Where it was signed; may be empty.</param>
/// <param name="Time">When it was signed.</param>
[DebuggerDisplay("SigningRequest: Page {PageIndex}: {Reason}")]
public readonly record struct SigningRequest(int PageIndex, string Reason, string Location, DateTimeOffset Time)
{
    /// <summary>Gets the digest the signature is made with; SHA-256 unless set.</summary>
    public SigningDigest Digest { get; init; }

    /// <summary>Gets the signature format; the field's seed values choose it unless set.</summary>
    public SigningFormat Format { get; init; }

    /// <summary>Gets the later changes the signature allows; <see cref="CertificationLevel.None"/> signs as an approval.</summary>
    public CertificationLevel Certification { get; init; }

    /// <summary>Gets the name of an existing empty signature field to sign, or <see langword="null"/> to add a new field.</summary>
    public string? FieldName { get; init; }

    /// <summary>Gets where a visible signature is drawn, or an empty area for an invisible one.</summary>
    public SigningArea Area { get; init; }

    /// <summary>Gets how a person can reach the signer; may be empty.</summary>
    public string? ContactInfo { get; init; }

    /// <summary>Gets the digest of the timestamp request; SHA-256 unless set.</summary>
    public SigningDigest TimestampDigest { get; init; }
}
