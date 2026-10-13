// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;

namespace HyperPdf.Cms;

/// <summary>
/// Says whether this computer's certificate chain engine can build a chain for a certificate. <see cref="X509Chain"/> uses the
/// operating system's engine, and that engine's key algorithm support differs by platform.
/// </summary>
internal static class CmsChainSupport
{
    /// <summary>Determines whether <see cref="X509Chain.Build(X509Certificate2)"/> can be asked about a certificate.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>
    /// <see langword="false"/> on Windows for an Ed25519 or Ed448 certificate: Windows' chain engine does not know those key
    /// algorithms, so <see cref="X509Chain.Build(X509Certificate2)"/> throws a <see cref="System.Security.Cryptography.CryptographicException"/> instead of
    /// giving an untrusted chain. <see langword="true"/> otherwise.
    /// </returns>
    internal static bool CanBuild(X509Certificate2 certificate) => !OperatingSystem.IsWindows() || !CmsCertificateKey.Read(certificate).IsEdwards;
}
