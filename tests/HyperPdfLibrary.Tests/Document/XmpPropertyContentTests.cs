// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Metadata;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Checks forward-only XMP property reads and detached pooled text.</summary>
public sealed class XmpPropertyContentTests
{
    /// <summary>The number of array items that forces value storage growth.</summary>
    private const int ItemCount = 100;

    /// <summary>The character count that forces fragmented text storage growth.</summary>
    private const int FragmentLength = 1024;

    /// <summary>The namespace of the sample property keys.</summary>
    private const string PropertyNamespace = "urn:properties";

    /// <summary>The sample title key.</summary>
    private const string TitleKey = "title";

    /// <summary>The following property key.</summary>
    private const string FollowingKey = "after";

    /// <summary>Single text nodes, fragmented text and empty or resource properties keep their value semantics.</summary>
    /// <param name="property">The property content.</param>
    /// <param name="expected">The retained title.</param>
    /// <returns>The test task.</returns>
    [Test]
    [Arguments("<p:title> first <![CDATA[second]]> third </p:title>", "first second third")]
    [Arguments("<p:title>first<!--ignored-->second</p:title>", "firstsecond")]
    [Arguments("<p:title>first<ignored>hidden</ignored>second</p:title>", "firstsecond")]
    [Arguments("<p:title rdf:resource=\"urn:fallback\"/>", "urn:fallback")]
    [Arguments("<p:title rdf:resource=\"urn:fallback\">text</p:title>", "text")]
    [Arguments("<p:title/>", null)]
    [Arguments("<p:title>   </p:title>", null)]
    public async Task PropertyTextPreservesSemantics(string property, string? expected)
    {
        var snapshot = Parse($"{property}<p:after>Kept</p:after>");
        await Assert.That(snapshot.GetValue(PropertyNamespace, TitleKey)).IsEqualTo(expected);
        await Assert.That(snapshot.GetValue(PropertyNamespace, FollowingKey)).IsEqualTo("Kept");
        await Assert.That(snapshot.IsWellFormed).IsTrue();
    }

    /// <summary>Array values keep whitespace, fragmented text, empty entries and default-language ordering.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ArrayItemsKeepTextAndDefaultOrdering()
    {
        var snapshot = Parse("""
            <p:title><rdf:Alt>
              <rdf:li> first <![CDATA[second]]> third </rdf:li>
              <rdf:li/>
              <rdf:li xml:lang="x-default">Default</rdf:li>
              <rdf:li xml:lang="x-default"/>
            </rdf:Alt></p:title><p:after>Kept</p:after>
            """);
        await Assert.That(snapshot.GetValues(PropertyNamespace, TitleKey).ToArray()).IsEquivalentTo(
            [string.Empty, "Default", " first second third ", string.Empty]);
        await Assert.That(snapshot.GetValue(PropertyNamespace, FollowingKey)).IsEqualTo("Kept");
    }

    /// <summary>Resource and nested-description fields do not become array text items.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task StructuresKeepFieldsWithoutAddingArrayItems()
    {
        var snapshot = Parse("""
            <p:history><rdf:Seq>
              <rdf:li rdf:parseType="Resource"><p:value>One</p:value></rdf:li>
              <rdf:li><rdf:Description p:value="Two"><p:other>Other</p:other></rdf:Description></rdf:li>
              <rdf:li>Text</rdf:li>
            </rdf:Seq></p:history>
            <p:after>Kept</p:after>
            """);
        await Assert.That(snapshot.GetValues(PropertyNamespace, "value").ToArray()).IsEquivalentTo(["One", "Two"]);
        await Assert.That(snapshot.GetValue(PropertyNamespace, "other")).IsEqualTo("Other");
        await Assert.That(snapshot.GetValues(PropertyNamespace, "history").ToArray()).IsEquivalentTo(["Text"]);
        await Assert.That(snapshot.GetValue(PropertyNamespace, FollowingKey)).IsEqualTo("Kept");
    }

    /// <summary>Growing and reusing leased item storage never changes a published array.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task LargeArraysStayDetachedAfterLaterParses()
    {
        var xml = new StringBuilder("<p:items><rdf:Seq>");
        for (var index = 0; index < ItemCount; index++)
        {
            _ = xml.Append("<rdf:li>").Append(index).Append("</rdf:li>");
        }

        _ = xml.Append("</rdf:Seq></p:items>");
        var first = Parse(xml.ToString());
        _ = Parse("<p:items><rdf:Seq><rdf:li>Replacement</rdf:li></rdf:Seq></p:items>");
        var values = first.GetValues(PropertyNamespace, "items");
        await Assert.That(values.Length).IsEqualTo(ItemCount);
        await Assert.That(values[0]).IsEqualTo("0");
        await Assert.That(values[^1]).IsEqualTo("99");
    }

    /// <summary>A malformed fragmented property does not leak its working text into a later packet.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task MalformedFragmentedTextDoesNotAffectLaterParse()
    {
        var malformed = """
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns:p="urn:properties">
              <rdf:Description><p:title>First<![CDATA[Second]]><broken
            """u8.ToArray();
        await Assert.That(XmpParser.Parse(malformed, CancellationToken.None).IsWellFormed).IsFalse();
        await Assert.That(Parse("<p:title>Kept</p:title>").GetValue(PropertyNamespace, TitleKey)).IsEqualTo("Kept");
    }

    /// <summary>A single node retains its string; fragmented text detaches before the characters are returned.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task TextValueSharesSingleNodeAndDetachesFragments()
    {
        var first = new string('a', FragmentLength);
        var second = new string('b', FragmentLength);
        var value = default(XmpTextValue);
        string detached;
        try
        {
            XmpTextValues.Append(ref value, first, CancellationToken.None);
            await Assert.That(ReferenceEquals(first, XmpTextValues.GetText(value))).IsTrue();
            XmpTextValues.Append(ref value, second, CancellationToken.None);
            XmpTextValues.Append(ref value, first, CancellationToken.None);
            detached = XmpTextValues.GetText(value);
        }
        finally
        {
            XmpTextValues.Release(ref value);
        }

        await Assert.That(detached).IsEqualTo(first + second + first);
        await Assert.That(value).IsEqualTo(default(XmpTextValue));
    }

    /// <summary>Combined text lengths are checked before accessing or renting character storage.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task OversizedTextThrowsDomainError()
    {
        var value = new XmpTextValue(null, [], int.MaxValue);
        await Assert.That(() => XmpTextValues.Append(ref value, "x", CancellationToken.None)).Throws<PdfException>();
        await Assert.That(value.Length).IsEqualTo(int.MaxValue);
    }

    /// <summary>Parsing a memory segment retains only that segment and detaches its raw packet.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task MemorySegmentSnapshotIsDetached()
    {
        var xml = """
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns:p="urn:properties">
              <rdf:Description><p:title>Kept</p:title></rdf:Description>
            </rdf:RDF>
            """u8.ToArray();
        byte[] storage = [0, .. xml, 0];
        var snapshot = XmpParser.Parse(storage.AsMemory(1, xml.Length), CancellationToken.None);
        storage.AsSpan().Clear();
        _ = Parse("<p:title>Replacement</p:title>");
        await Assert.That(snapshot.GetValue(PropertyNamespace, TitleKey)).IsEqualTo("Kept");
        await Assert.That(snapshot.RawPacket.AsSpan().SequenceEqual(xml)).IsTrue();
    }

    /// <summary>Wraps a property sequence in one RDF description.</summary>
    /// <param name="properties">The property markup.</param>
    /// <returns>The detached metadata snapshot.</returns>
    private static XmpMetadata Parse(string properties)
    {
        var bytes = Encoding.UTF8.GetBytes(
            $"<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\" xmlns:p=\"urn:properties\"><rdf:Description>{properties}</rdf:Description></rdf:RDF>");
        return XmpParser.Parse(bytes, CancellationToken.None);
    }
}
