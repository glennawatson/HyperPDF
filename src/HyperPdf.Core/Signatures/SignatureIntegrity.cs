// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Signatures;

/// <summary>Whether the signed content is what the signer signed.</summary>
public enum SignatureIntegrity
{
    /// <summary>The signature could not be checked, for example an unsupported format.</summary>
    Unknown = 0,

    /// <summary>The signature matches and covers the whole document.</summary>
    Intact = 1,

    /// <summary>The signature matches what was signed, but the document was changed afterwards (for example, filled in or annotated).</summary>
    ChangedAfterSigning = 2,

    /// <summary>The signature does not match: the signed content was altered or the signature is damaged.</summary>
    Invalid = 3,
}
