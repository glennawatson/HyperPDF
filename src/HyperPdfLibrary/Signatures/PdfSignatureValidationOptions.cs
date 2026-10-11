// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Signatures;

/// <summary>How signatures are checked.</summary>
[DebuggerDisplay("PdfSignatureValidationOptions: network {AllowNetwork}")]
public sealed record PdfSignatureValidationOptions
{
    /// <summary>Gets independent default options: system trust, no network, checked at the timestamp or now.</summary>
    public static PdfSignatureValidationOptions Default => new();

    /// <summary>Gets the roots to trust instead of the system's; when empty the system's trust store is used.</summary>
    public X509Certificate2Collection TrustedRoots { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether chain building may download certificates and check revocation online. Off by
    /// default, so validation never touches the network.
    /// </summary>
    public bool AllowNetwork { get; init; }

    /// <summary>Gets the time to check certificates at; when <see langword="null"/>, a valid timestamp's time or else the current time.</summary>
    public DateTimeOffset? VerificationTime { get; init; }

    /// <summary>Gets the clock that supplies the current time.</summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;
}
