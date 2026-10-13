// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>What signing would do to a document, worked out without changing it, so the user can check it before signing.</summary>
/// <param name="FieldName">The name of the signature field.</param>
/// <param name="IsNewField">Whether signing adds the field rather than filling an empty one.</param>
/// <param name="PageNumber">The one-based number of the page the field is on.</param>
/// <param name="IsVisible">Whether the signature is drawn on the page.</param>
/// <param name="Format">The signature format that will be written.</param>
/// <param name="Digest">The digest that will be used.</param>
/// <param name="Reason">The reason that will be recorded; empty for none.</param>
/// <param name="Location">The location that will be recorded; empty for none.</param>
/// <param name="Certification">The later changes the signature allows.</param>
/// <param name="LocksDocument">Whether no later change to a field keeps the signature valid.</param>
/// <param name="LaterChanges">A plain sentence saying what later changes the signature allows.</param>
/// <param name="TimestampRequired">Whether the field's seed values require a signature timestamp.</param>
/// <param name="Problems">Every reason signing would be refused, each a plain sentence.</param>
/// <param name="Notes">What the user should know, each a plain sentence.</param>
[DebuggerDisplay("SigningPreview: {FieldName} page {PageNumber} {Format} {Digest}")]
public sealed record SigningPreview(
    string FieldName,
    bool IsNewField,
    int PageNumber,
    bool IsVisible,
    SigningFormat Format,
    SigningDigest Digest,
    string Reason,
    string Location,
    CertificationLevel Certification,
    bool LocksDocument,
    string LaterChanges,
    bool TimestampRequired,
    IReadOnlyList<string> Problems,
    IReadOnlyList<string> Notes)
{
    /// <summary>Gets a value indicating whether signing can go ahead.</summary>
    public bool CanSign => Problems.Count == 0;
}
