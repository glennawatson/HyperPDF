// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Settings;

/// <summary>
/// A signing certificate the user chose to remember. Only where the file is and who it names are kept: never its
/// password or private key, which stay in the protected certificate file.
/// </summary>
/// <param name="Path">The certificate file (.p12 or .pfx).</param>
/// <param name="Subject">Who the certificate names, so the user can tell certificates apart.</param>
/// <param name="Thumbprint">The certificate's SHA-1 fingerprint, a public value that tells two certificates apart.</param>
[DebuggerDisplay("RememberedCertificate: {Subject}")]
public sealed record RememberedCertificate(string Path, string Subject, string Thumbprint);
