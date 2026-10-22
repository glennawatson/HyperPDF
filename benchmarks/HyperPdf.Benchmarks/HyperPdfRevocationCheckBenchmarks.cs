// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Signatures;

namespace HyperPdf.Benchmarks;

/// <summary>Checks the revocation status of six certificates against a security store that holds a signed OCSP response for each of them.</summary>
public class HyperPdfRevocationCheckBenchmarks
{
    /// <summary>The first byte of every leaf certificate's serial number, clear of the sign bit.</summary>
    private const byte SerialPrefix = 0x10;

    /// <summary>The days a certificate is valid for.</summary>
    private const int ValidDays = 30;

    /// <summary>The key that signs the certificates and the responses.</summary>
    private ECDsa _issuerKey = null!;

    /// <summary>The issuing certificate.</summary>
    private X509Certificate2 _issuer = null!;

    /// <summary>The certificates checked.</summary>
    private X509Certificate2[] _leaves = [];

    /// <summary>The store holding the responses.</summary>
    private PdfSecurityStore _store = null!;

    /// <summary>Gets or sets the certificates, and so the OCSP responses, in the store: a short chain, a longer one and a store holding several documents' chains.</summary>
    [Params(3, 6, 12)]
    public int Certificates { get; set; }

    /// <summary>Makes the certificates, signs a response for each and puts them in a store.</summary>
    /// <exception cref="InvalidOperationException">A response is not accepted.</exception>
    [GlobalSetup]
    public void Setup()
    {
        var from = TimeProvider.System.GetUtcNow().AddDays(-1);
        var to = from.AddDays(ValidDays);
        _issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var authority = new CertificateRequest("CN=Benchmark Authority", _issuerKey, HashAlgorithmName.SHA256);
        authority.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        _issuer = authority.CreateSelfSigned(from, to);
        _leaves = new X509Certificate2[Certificates];
        var responses = new byte[Certificates][];
        for (var i = 0; i < Certificates; i++)
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request = new CertificateRequest($"CN=Leaf {i}", key, HashAlgorithmName.SHA256);
            _leaves[i] = request.Create(_issuer, from, to, [SerialPrefix, (byte)i]);
            responses[i] = OcspResponses.Build(_issuerKey, _issuer, _leaves[i], from, false);
        }

        _store = new([_issuer.RawData], responses, [], new ReadOnlyDictionary<string, PdfValidationRelatedInfo>(new Dictionary<string, PdfValidationRelatedInfo>(StringComparer.Ordinal)));
        if (CheckEveryCertificate() != Certificates)
        {
            throw new InvalidOperationException("A generated OCSP response was not accepted, so the benchmark would not measure the check.");
        }
    }

    /// <summary>Releases the certificates and the key.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var leaf in _leaves)
        {
            leaf.Dispose();
        }

        _issuer.Dispose();
        _issuerKey.Dispose();
    }

    /// <summary>Checks every certificate against the store.</summary>
    /// <returns>The certificates found not revoked.</returns>
    [Benchmark]
    public int CheckEveryCertificate()
    {
        var good = 0;
        foreach (var leaf in _leaves)
        {
            good += PdfRevocationChecker.Check(leaf, _issuer, _store) == PdfRevocationStatus.NotRevoked ? 1 : 0;
        }

        return good;
    }
}
