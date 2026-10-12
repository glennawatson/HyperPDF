// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Signatures;

/// <summary>A checked digital signature.</summary>
/// <param name="Index">The signature's index in the document.</param>
/// <param name="SignerName">The signer's name from the certificate, or an empty string when unknown.</param>
/// <param name="Issuer">Who issued the signer's certificate.</param>
/// <param name="SigningTime">When it was signed, if known.</param>
/// <param name="Reason">The reason the signer gave, or an empty string.</param>
/// <param name="Integrity">Whether the signed content is unchanged.</param>
/// <param name="IsTrusted">Whether the signer's certificate chains to a certificate this computer trusts.</param>
/// <param name="Detail">A short explanation when the signature could not be checked or is not trusted.</param>
[DebuggerDisplay("DocumentSignature: {SignerName}: {Integrity}, trusted {IsTrusted}")]
public sealed record DocumentSignature(int Index, string SignerName, string Issuer, DateTimeOffset? SigningTime, string Reason, SignatureIntegrity Integrity, bool IsTrusted, string Detail)
{
    /// <summary>Gets the trusted timestamp on the signature, or on the document for a document timestamp, if any.</summary>
    public SignatureTimestamp? Timestamp { get; init; }

    /// <summary>Gets a value indicating whether this is a document timestamp rather than a person's signature.</summary>
    public bool IsDocumentTimestamp { get; init; }

    /// <summary>Gets a value indicating whether the file stores the revocation data long-term validation needs (PAdES LTV).</summary>
    public bool HasLongTermValidation { get; init; }

    /// <summary>Gets the time the certificate was checked at: the trusted timestamp's time, or <see langword="null"/> when it was checked as of now.</summary>
    public DateTimeOffset? CheckedAt { get; init; }

    /// <summary>Gets a line about the timestamp and long-term validation, or an empty string when there is neither.</summary>
    public string TimestampSummary
    {
        get
        {
            var ltv = HasLongTermValidation ? " Long-term validation data is stored in the file." : string.Empty;
            return Timestamp switch
            {
                null => ltv.TrimStart(),
                { IsValid: false } => $"The timestamp from {Timestamp.Authority} does not match.{ltv}",
                { IsTrusted: false } => $"Timestamped {Timestamp.Time:g} by {Timestamp.Authority}, whose certificate is not trusted on this computer.{ltv}",
                _ => $"Timestamped {Timestamp.Time:g} by {Timestamp.Authority}; the certificate was checked as of that time.{ltv}",
            };
        }
    }

    /// <summary>Gets a one-line summary in plain words.</summary>
    public string Summary => Integrity switch
    {
        SignatureIntegrity.Intact when IsDocumentTimestamp && IsTrusted => "Valid document timestamp; the document has not changed since it was stamped.",
        SignatureIntegrity.ChangedAfterSigning when IsDocumentTimestamp => "The stamped part is unchanged; the document was changed after it was stamped.",
        SignatureIntegrity.Intact when IsTrusted => "Valid signature; the document has not changed since it was signed.",
        SignatureIntegrity.Intact => "The document has not changed since it was signed, but the signer's certificate is not trusted on this computer.",
        SignatureIntegrity.ChangedAfterSigning => "The signed part is unchanged; the document was changed after signing.",
        SignatureIntegrity.Invalid => "The signature does not match: the signed content was altered or the signature is damaged.",
        _ => "This signature could not be checked.",
    };
}
