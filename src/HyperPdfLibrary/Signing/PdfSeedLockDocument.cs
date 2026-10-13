// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signing;

/// <summary>The author's intent for locking the document at signing (ISO 32000-2, Table 237, /LockDocument).</summary>
internal enum PdfSeedLockDocument
{
    /// <summary>The application decides (the default).</summary>
    Auto = 0,

    /// <summary>The document should be locked.</summary>
    Lock = 1,

    /// <summary>The document should not be locked.</summary>
    Unlock = 2,
}
