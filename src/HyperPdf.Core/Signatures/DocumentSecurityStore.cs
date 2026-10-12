// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Signatures.Signing;

namespace HyperPdf.Core.Signatures;

/// <summary>
/// The document security store (<c>/DSS</c>, PAdES long-term validation): certificates, OCSP responses and CRLs saved
/// in the file so its signatures can still be checked after certificates expire or their issuers go offline.
/// </summary>
/// <param name="Certificates">The stored certificates.</param>
/// <param name="OcspResponseCount">How many OCSP responses are stored.</param>
/// <param name="CrlCount">How many certificate revocation lists are stored.</param>
[DebuggerDisplay("DocumentSecurityStore: {Certificates.Count} certificates, {OcspResponseCount} OCSP, {CrlCount} CRLs")]
public sealed record DocumentSecurityStore(X509Certificate2Collection Certificates, int OcspResponseCount, int CrlCount)
{
    /// <summary>Gets an empty store, for files without one.</summary>
    public static DocumentSecurityStore Empty => new([], 0, 0);

    /// <summary>Gets a value indicating whether the store holds revocation data, which long-term validation needs.</summary>
    public bool HasRevocationData => OcspResponseCount + CrlCount > 0;

    /// <summary>Reads a file's store.</summary>
    /// <param name="file">The PDF's bytes.</param>
    /// <returns>The store, or <see cref="Empty"/> when there is none or it cannot be read.</returns>
    public static DocumentSecurityStore Read(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.AsSpan().IndexOf("/DSS"u8) < 0)
        {
            return Empty;
        }

        try
        {
            var structure = PdfReader.Read(file);
            var catalog = PdfReader.GetObject(structure, PdfPages.ReadReference(structure.Trailer, "Root"u8));
            var dssAt = PdfSyntax.FindKey(catalog.Span, 0, "DSS"u8);
            if (dssAt < 0)
            {
                return Empty;
            }

            var dss = PdfReader.Resolve(structure, catalog, dssAt);
            var certificates = new X509Certificate2Collection();
            foreach (var stream in Streams(structure, dss, "Certs"u8))
            {
                TryAdd(certificates, stream);
            }

            return new(certificates, Count(structure, dss, "OCSPs"u8), Count(structure, dss, "CRLs"u8));
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            return Empty;
        }
    }

    /// <summary>Adds a DER certificate, skipping one that cannot be read.</summary>
    /// <param name="certificates">The collection.</param>
    /// <param name="der">The certificate's bytes.</param>
    private static void TryAdd(X509Certificate2Collection certificates, byte[] der)
    {
        try
        {
            _ = certificates.Add(X509CertificateLoader.LoadCertificate(der));
        }
        catch (CryptographicException)
        {
            // A damaged entry is left out; the rest of the store still helps.
        }
    }

    /// <summary>Counts the entries of an array in the store.</summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="dss">The store's dictionary.</param>
    /// <param name="key">The array's key.</param>
    /// <returns>The number of references in it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Count(PdfStructure structure, ReadOnlyMemory<byte> dss, ReadOnlySpan<byte> key) => References(structure, dss, key).Count;

    /// <summary>Reads the decoded streams an array in the store refers to.</summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="dss">The store's dictionary.</param>
    /// <param name="key">The array's key.</param>
    /// <returns>The streams' bytes.</returns>
    private static List<byte[]> Streams(PdfStructure structure, ReadOnlyMemory<byte> dss, ReadOnlySpan<byte> key)
    {
        var streams = new List<byte[]>();
        foreach (var number in References(structure, dss, key))
        {
            try
            {
                streams.Add(PdfReader.GetObjectStream(structure, number));
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
            {
                // Skip a stream that cannot be decoded.
            }
        }

        return streams;
    }

    /// <summary>Reads the object numbers in an array of the store.</summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="dss">The store's dictionary.</param>
    /// <param name="key">The array's key.</param>
    /// <returns>The numbers.</returns>
    private static List<int> References(PdfStructure structure, ReadOnlyMemory<byte> dss, ReadOnlySpan<byte> key)
    {
        var numbers = new List<int>();
        var at = PdfSyntax.FindKey(dss.Span, 0, key);
        if (at < 0)
        {
            return numbers;
        }

        var array = PdfReader.Resolve(structure, dss, at).Span;
        var index = PdfSyntax.SkipSpace(array, 1);
        while (index < array.Length && array[index] != (byte)']')
        {
            if (PdfSyntax.TryReadReference(array, index, out var number))
            {
                numbers.Add(number);
                var generation = PdfSyntax.SkipSpace(array, PdfSyntax.TokenEnd(array, index));
                index = PdfSyntax.SkipSpace(array, PdfSyntax.TokenEnd(array, generation));
                index = PdfSyntax.SkipSpace(array, index + 1);
                continue;
            }

            index = PdfSyntax.SkipSpace(array, PdfSyntax.ValueEnd(array, index));
        }

        return numbers;
    }
}
