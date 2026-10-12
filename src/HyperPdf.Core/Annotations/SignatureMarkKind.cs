// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Annotations;

/// <summary>Whether a mark is a full signature or initials.</summary>
public enum SignatureMarkKind
{
    /// <summary>A full signature.</summary>
    Signature = 0,

    /// <summary>Initials, for example beside a change on each page.</summary>
    Initials = 1,
}
