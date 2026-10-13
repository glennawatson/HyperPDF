// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Protection;

/// <summary>The kind of encryption a protected copy uses.</summary>
public enum ProtectionFormat
{
    /// <summary>AES-256, the strongest common choice and the default. Needs a PDF 2.0 reader.</summary>
    Strong = 0,

    /// <summary>AES-128, which older programs also open.</summary>
    Compatible = 1,

    /// <summary>AES-256 that also detects changed data. Few programs open it yet.</summary>
    Authenticated = 2,
}
