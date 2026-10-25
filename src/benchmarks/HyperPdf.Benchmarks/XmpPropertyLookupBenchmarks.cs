// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>Measures retained XMP property snapshots and repeated span-based value lookup.</summary>
public class XmpPropertyLookupBenchmarks
{
    /// <summary>The packet bytes owned by this benchmark instance.</summary>
    private byte[] _packet = [];

    /// <summary>The fragmented scalar packet owned by this instance.</summary>
    private byte[] _fragmentedPacket = [];

    /// <summary>The empty-property packet owned by this instance.</summary>
    private byte[] _emptyPacket = [];

    /// <summary>The owner of an uncompressed metadata stream.</summary>
    private PdfDictionary _metadataOwner = null!;

    /// <summary>The immutable snapshot retained by this benchmark instance.</summary>
    private XmpMetadata _snapshot = XmpMetadata.Empty;

    /// <summary>Creates and warms the retained property snapshot.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _packet = """
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
              <rdf:Description xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:title>Retained title</dc:title>
                <dc:creator><rdf:Seq><rdf:li>Ann</rdf:li><rdf:li>Bob</rdf:li></rdf:Seq></dc:creator>
              </rdf:Description>
            </rdf:RDF>
            """u8.ToArray();
        _snapshot = XmpParser.Parse(_packet, CancellationToken.None);
        _metadataOwner = new(null);
        _metadataOwner.Set(KnownName.Metadata, PdfValue.FromStream(new(new(null), _packet)));
        _fragmentedPacket = """
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
              <rdf:Description xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:title>Retained <![CDATA[fragmented]]> title</dc:title>
              </rdf:Description>
            </rdf:RDF>
            """u8.ToArray();
        _emptyPacket = """
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
              <rdf:Description xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:title/><dc:creator/>
              </rdf:Description>
            </rdf:RDF>
            """u8.ToArray();
        _ = ReadTitle();
        _ = ReadCreators();
        _ = ReadMissingProperty();
    }

    /// <summary>Parses a packet and retains its detached snapshot.</summary>
    /// <returns>The number of parsed property keys.</returns>
    [Benchmark]
    public int ParsePropertySnapshot() => XmpParser.Parse(_packet, CancellationToken.None).Properties.Count;

    /// <summary>Parses fragmented scalar text using leased character storage.</summary>
    /// <returns>The number of retained properties.</returns>
    [Benchmark]
    public int ParseFragmentedPropertySnapshot() => XmpParser.Parse(_fragmentedPacket, CancellationToken.None).Properties.Count;

    /// <summary>Parses empty properties without constructing keys or value collections.</summary>
    /// <returns>The number of retained properties.</returns>
    [Benchmark]
    public int ParseEmptyPropertySnapshot() => XmpParser.Parse(_emptyPacket, CancellationToken.None).Properties.Count;

    /// <summary>Decodes into leased bytes and keeps only the detached metadata packet.</summary>
    /// <returns>The number of retained properties.</returns>
    [Benchmark]
    public int ParseDecodedMetadataSnapshot() => PdfDocumentMetadata.ParseXmp(_metadataOwner, CancellationToken.None)?.Properties.Count ?? 0;

    /// <summary>Reads an existing title by its namespace and local name.</summary>
    /// <returns>The title.</returns>
    [Benchmark]
    public string? ReadTitle() => _snapshot.Title;

    /// <summary>Reads the retained creator array.</summary>
    /// <returns>The number of creators.</returns>
    [Benchmark]
    public int ReadCreators() => _snapshot.Creators.Length;

    /// <summary>Looks up a missing property in the parsed dictionary.</summary>
    /// <returns>The absent value.</returns>
    [Benchmark]
    public string? ReadMissingProperty() => _snapshot.GetValue(XmpMetadata.DublinCoreNamespace, "missing");
}
