// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>The authenticated attributes of a PDF MAC token that checking needs (ISO/TS 32004:2024 6.3.6).</summary>
internal ref struct PdfMacAttributes
{
    /// <summary>Gets or sets a value indicating whether the content type attribute was seen.</summary>
    internal bool HasContentType { get; set; }

    /// <summary>Gets or sets a value indicating whether the message digest attribute was seen.</summary>
    internal bool HasMessageDigest { get; set; }

    /// <summary>Gets or sets a value indicating whether the algorithm protection attribute was seen.</summary>
    internal bool HasProtection { get; set; }

    /// <summary>Gets or sets the message digest value.</summary>
    internal ReadOnlySpan<byte> MessageDigest { get; set; }

    /// <summary>Gets or sets the contents of the algorithm protection value.</summary>
    internal ReadOnlySpan<byte> Protection { get; set; }
}
