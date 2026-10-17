// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdf.Cms;

namespace HyperPdfLibrary.Signing;

/// <summary>Owns native digest contexts while exclusively leased from one caller's scratch pools.</summary>
internal sealed class PdfSigningHashState : IDisposable
{
    /// <summary>The reusable SHA-1 context.</summary>
    private IncrementalHash? _sha1;

    /// <summary>The reusable SHA-256 context.</summary>
    private IncrementalHash? _sha256;

    /// <summary>The reusable SHA-384 context.</summary>
    private IncrementalHash? _sha384;

    /// <summary>The reusable SHA-512 context.</summary>
    private IncrementalHash? _sha512;

    /// <summary>The reusable SHA3-256 context.</summary>
    private IncrementalHash? _sha3_256;

    /// <summary>The reusable SHA3-384 context.</summary>
    private IncrementalHash? _sha3_384;

    /// <summary>The reusable SHA3-512 context.</summary>
    private IncrementalHash? _sha3_512;

    /// <summary>The reusable SHAKE256 context.</summary>
    private Shake256? _shake256;

    /// <summary>Whether disposal has ended this lease's native lifetime.</summary>
    private int _disposed;

    /// <summary>Gets the exclusively leased SHAKE256 slot.</summary>
    /// <exception cref="ObjectDisposedException">The state was disposed.</exception>
    internal ref Shake256? Shake
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return ref _shake256;
        }
    }

    /// <summary>Disposes each native context once.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Only the lease holder can dispose this state; idle states belong exclusively to their scratch pool.
        _sha1?.Dispose();
        _sha256?.Dispose();
        _sha384?.Dispose();
        _sha512?.Dispose();
        _sha3_256?.Dispose();
        _sha3_384?.Dispose();
        _sha3_512?.Dispose();
        _shake256?.Dispose();
        _sha1 = null;
        _sha256 = null;
        _sha384 = null;
        _sha512 = null;
        _sha3_256 = null;
        _sha3_384 = null;
        _sha3_512 = null;
        _shake256 = null;
    }

    /// <summary>Gets the slot for a supported fixed-length digest.</summary>
    /// <param name="digest">The digest algorithm.</param>
    /// <returns>The exclusively leased native-context slot.</returns>
    /// <exception cref="ObjectDisposedException">The state was disposed.</exception>
    /// <exception cref="PdfException">The digest has no fixed-length native context.</exception>
    internal ref IncrementalHash? Hash(CmsDigestAlgorithm digest)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        switch (digest)
        {
            case CmsDigestAlgorithm.Sha1:
            {
                return ref _sha1;
            }

            case CmsDigestAlgorithm.Sha256:
            {
                return ref _sha256;
            }

            case CmsDigestAlgorithm.Sha384:
            {
                return ref _sha384;
            }

            case CmsDigestAlgorithm.Sha512:
            {
                return ref _sha512;
            }

            case CmsDigestAlgorithm.Sha3_256:
            {
                return ref _sha3_256;
            }

            case CmsDigestAlgorithm.Sha3_384:
            {
                return ref _sha3_384;
            }

            case CmsDigestAlgorithm.Sha3_512:
            {
                return ref _sha3_512;
            }

            default:
            {
                throw new PdfException(PdfError.Format, "The signature digest has no fixed-length hash context.");
            }
        }
    }
}
