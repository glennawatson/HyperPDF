// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Metadata;

namespace HyperPdf.Benchmarks;

/// <summary>Measures complete XMP parsing with and without PDF Declaration structures.</summary>
public class PdfDeclarationReadingBenchmarks
{
    /// <summary>The source bytes owned by this instance.</summary>
    private byte[] _packet = [];

    /// <summary>Gets or sets whether the packet includes a declaration and claim data.</summary>
    [Params(false, true)]
    public bool HasDeclaration { get; set; }

    /// <summary>Prepares packet bytes outside the measured parser call.</summary>
    [GlobalSetup]
    public void Setup() =>
        _packet = HasDeclaration
            ? """
              <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
                <rdf:Description xmlns:pdfd="http://pdfa.org/declarations/">
                  <pdfd:declarations><rdf:Bag><rdf:li rdf:parseType="Resource">
                    <pdfd:conformsTo>urn:example:profile</pdfd:conformsTo>
                    <pdfd:claimData><rdf:Bag><rdf:li rdf:parseType="Resource">
                      <pdfd:claimBy>Claimant</pdfd:claimBy><pdfd:claimDate>2019-01-17</pdfd:claimDate>
                      <pdfd:claimCredentials>Credentials</pdfd:claimCredentials><pdfd:claimReport>#ef=report.pdf</pdfd:claimReport>
                    </rdf:li></rdf:Bag></pdfd:claimData>
                  </rdf:li></rdf:Bag></pdfd:declarations>
                </rdf:Description>
              </rdf:RDF>
              """u8.ToArray()
            : """
              <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
                <rdf:Description xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:title>Title</dc:title></rdf:Description>
              </rdf:RDF>
              """u8.ToArray();

    /// <summary>Parses the packet into a detached metadata snapshot.</summary>
    /// <returns>The snapshot, including associated declaration fields.</returns>
    [Benchmark]
    public XmpMetadata Parse() => XmpParser.Parse(_packet, CancellationToken.None);
}
