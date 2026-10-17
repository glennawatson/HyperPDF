// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Interchange;

namespace HyperPdfLibrary.Tests.Interchange;

/// <summary>Checks the XFDF element mappings and their distinct case rules.</summary>
public sealed class XfdfNamesTests
{
    /// <summary>All supported subtypes round-trip, with case-insensitive XML input.</summary>
    /// <param name="subtype">The PDF subtype.</param>
    /// <param name="element">The XML element.</param>
    /// <returns>The test task.</returns>
    [Test]
    [Arguments("Text", "text")]
    [Arguments("Highlight", "highlight")]
    [Arguments("Underline", "underline")]
    [Arguments("StrikeOut", "strikeout")]
    [Arguments("Squiggly", "squiggly")]
    [Arguments("Ink", "ink")]
    [Arguments("Square", "square")]
    [Arguments("Circle", "circle")]
    [Arguments("Line", "line")]
    [Arguments("Polygon", "polygon")]
    [Arguments("PolyLine", "polyline")]
    [Arguments("FreeText", "freetext")]
    [Arguments("Stamp", "stamp")]
    [Arguments("Caret", "caret")]
    [Arguments("FileAttachment", "fileattachment")]
    public async Task PreservesBothDirections(string subtype, string element)
    {
        await Assert.That(XfdfNames.TryGetElement(subtype, out var written)).IsTrue();
        await Assert.That(written).IsEqualTo(element);
        await Assert.That(XfdfNames.TryGetSubtype(element.ToUpperInvariant(), out var read)).IsTrue();
        await Assert.That(read).IsEqualTo(subtype);
        await Assert.That(XfdfNames.TryGetElement(subtype.ToLowerInvariant(), out var invalid)).IsFalse();
        await Assert.That(invalid).IsEqualTo(string.Empty);
    }

    /// <summary>Unknown names and prefixes return no mapping.</summary>
    /// <param name="name">The unknown name.</param>
    /// <returns>The test task.</returns>
    [Test]
    [Arguments("")]
    [Arguments("textx")]
    [Arguments("fileattachmen")]
    [Arguments("Unknown")]
    public async Task UnknownNamesHaveNoMapping(string name)
    {
        await Assert.That(XfdfNames.TryGetElement(name, out var element)).IsFalse();
        await Assert.That(XfdfNames.TryGetSubtype(name, out var subtype)).IsFalse();
        await Assert.That(element).IsEqualTo(string.Empty);
        await Assert.That(subtype).IsEqualTo(string.Empty);
    }
}
