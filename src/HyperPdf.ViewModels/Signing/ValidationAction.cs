// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Signing;

/// <summary>What the signature upkeep window adds to a signed document.</summary>
public enum ValidationAction
{
    /// <summary>The certificates and revocation answers each signature needs, so it can be checked later (PAdES-LTV).</summary>
    AddValidationData = 0,

    /// <summary>A trusted timestamp over the whole file as it stands (PAdES-LTA).</summary>
    AddTimestamp = 1,
}
