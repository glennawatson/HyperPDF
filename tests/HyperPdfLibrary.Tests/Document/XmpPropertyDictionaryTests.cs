// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Text;
using HyperPdfLibrary.Metadata;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Checks read-only XMP snapshots and span-based parsed-key lookup.</summary>
public sealed class XmpPropertyDictionaryTests
{
    /// <summary>The title in the parsed packet.</summary>
    private const string Title = "Retained title";

    /// <summary>The adversarial XML structure depth.</summary>
    private const int StructureDepth = 10_000;

    /// <summary>Gets a packet with one title and a creator array.</summary>
    private static ReadOnlySpan<byte> Packet => """
        <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
          <rdf:Description xmlns:dc="http://purl.org/dc/elements/1.1/">
            <dc:title>Retained title</dc:title>
            <dc:creator><rdf:Seq><rdf:li>Ann</rdf:li><rdf:li>Bob</rdf:li></rdf:Seq></dc:creator>
          </rdf:Description>
        </rdf:RDF>
        """u8;

    /// <summary>Parsed properties cannot be changed through mutable dictionary interfaces.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ParsedPropertiesRejectMutation()
    {
        var snapshot = XmpParser.Parse(Packet.ToArray(), CancellationToken.None);
        var writable = (IDictionary<string, string[]>)snapshot.Properties;
        var key = XmpMetadata.Key(XmpMetadata.DublinCoreNamespace, "title");
        await Assert.That(() => writable[key] = ["Changed"]).Throws<NotSupportedException>();
        await Assert.That(() => writable.Clear()).Throws<NotSupportedException>();
        await Assert.That(snapshot.Title).IsEqualTo(Title);
        await Assert.That(snapshot.Creators.ToArray()).IsEquivalentTo(["Ann", "Bob"]);
    }

    /// <summary>The shared empty snapshot cannot acquire properties.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task EmptySnapshotRejectsMutation()
    {
        var writable = (IDictionary<string, string[]>)XmpMetadata.Empty.Properties;
        await Assert.That(() => writable.Add("key", ["Value"])).Throws<NotSupportedException>();
        await Assert.That(XmpMetadata.Empty.Properties.Count).IsEqualTo(0);
        await Assert.That(XmpMetadata.Empty.Title).IsNull();
    }

    /// <summary>Parsed values and raw bytes stay detached from the input and later reads.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task SnapshotKeepsItsOriginalBytesAndValues()
    {
        var input = Packet.ToArray();
        var first = XmpParser.Parse(input, CancellationToken.None);
        input.AsSpan().Clear();
        var second = XmpParser.Parse(Packet.ToArray(), CancellationToken.None);
        await Assert.That(first.Title).IsEqualTo(Title);
        await Assert.That(first.Creators.AsSpan().SequenceEqual(second.Creators.AsSpan())).IsTrue();
        await Assert.That(first.RawPacket.AsSpan().SequenceEqual(Packet)).IsTrue();
    }

    /// <summary>Parsed XML keys preserve ordinal case-sensitive lookup.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ParsedKeysRemainCaseSensitive()
    {
        var snapshot = XmpParser.Parse(Packet.ToArray(), CancellationToken.None);
        await Assert.That(snapshot.GetValue(XmpMetadata.DublinCoreNamespace, "TITLE")).IsNull();
        await Assert.That(snapshot.GetValue(XmpMetadata.DublinCoreNamespace, "title")).IsEqualTo(Title);
    }

    /// <summary>Caller-created read-only dictionaries retain their own comparison rules.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task CallerDictionaryUsesItsOwnComparer()
    {
        var values = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { [XmpMetadata.Key(XmpMetadata.DublinCoreNamespace, "TITLE")] = [Title] };
        var snapshot = new XmpMetadata([], new ReadOnlyDictionary<string, string[]>(values), true);
        await Assert.That(snapshot.Title).IsEqualTo(Title);
    }

    /// <summary>Cancelled parsing stops before constructing XML reader state.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task CancellationStopsParsing()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.That(() => XmpParser.Parse([], cancellation.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Deeply nested structures stop following fields at the parser's nesting limit.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task DeepStructuresDoNotExhaustTheStack()
    {
        var xml = new StringBuilder("""
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns:p="urn:properties">
              <rdf:Description>
            """);
        for (var depth = 0; depth < StructureDepth; depth++)
        {
            _ = xml.Append("<p:field rdf:parseType=\"Resource\">");
        }

        _ = xml.Append("<p:value>Bounded</p:value>");
        for (var depth = 0; depth < StructureDepth; depth++)
        {
            _ = xml.Append("</p:field>");
        }

        _ = xml.Append("</rdf:Description></rdf:RDF>");
        var snapshot = XmpParser.Parse(Encoding.UTF8.GetBytes(xml.ToString()), CancellationToken.None);
        await Assert.That(snapshot.IsWellFormed).IsTrue();
        await Assert.That(snapshot.GetValue("urn:properties", "value")).IsNull();
    }
}
