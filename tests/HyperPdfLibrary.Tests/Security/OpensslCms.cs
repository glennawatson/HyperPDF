// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Decrypts a CMS EnvelopedData with the <c>openssl cms</c> command, a CMS reader independent of this library.</summary>
internal static class OpensslCms
{
    /// <summary>Decrypts an envelope for a recipient.</summary>
    /// <param name="envelope">The DER of the ContentInfo.</param>
    /// <param name="recipient">The recipient's certificate with its private key.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What openssl printed and the content it decrypted, or <see langword="null"/> when openssl is not installed.</returns>
    internal static async Task<OpensslCmsResult?> DecryptAsync(byte[] envelope, X509Certificate2 recipient, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"openssl-cms-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        try
        {
            var input = Path.Combine(folder, "envelope.der");
            var certificate = Path.Combine(folder, "recipient.pem");
            var key = Path.Combine(folder, "recipient.key");
            var output = Path.Combine(folder, "content.bin");
            await File.WriteAllBytesAsync(input, envelope, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(certificate, recipient.ExportCertificatePem(), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(key, ExportKey(recipient), cancellationToken).ConfigureAwait(false);
            var start = new ProcessStartInfo("openssl") { RedirectStandardOutput = true, RedirectStandardError = true };

            // -outform DER opens the output file in binary mode. Without it, openssl on Windows writes the content in text mode and turns
            // each 0x0A byte of the random seed into 0x0D 0x0A, so the content differs from the library's now and then.
            foreach (var argument in new[] { "cms", "-decrypt", "-inform", "DER", "-outform", "DER", "-in", input, "-recip", certificate, "-inkey", key, "-out", output })
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            var text = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var content = File.Exists(output) ? await File.ReadAllBytesAsync(output, cancellationToken).ConfigureAwait(false) : null;
            return new(await text.ConfigureAwait(false) + await errors.ConfigureAwait(false), content);
        }
        catch (Win32Exception)
        {
            return null;
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Exports a certificate's private key as PKCS #8 PEM.</summary>
    /// <param name="certificate">The certificate with its key.</param>
    /// <returns>The PEM text.</returns>
    private static string ExportKey(X509Certificate2 certificate)
    {
        using var rsa = certificate.GetRSAPrivateKey();
        if (rsa is not null)
        {
            return rsa.ExportPkcs8PrivateKeyPem();
        }

        using var agreement = certificate.GetECDiffieHellmanPrivateKey();
        return agreement!.ExportPkcs8PrivateKeyPem();
    }
}
