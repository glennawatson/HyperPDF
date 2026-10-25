// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Checks the PDF/A content checker's filter-name classification.</summary>
public sealed class PdfAContentScanTests
{
    /// <summary>General filters retain their classification without a shared set.</summary>
    /// <param name="name">The parsed filter name.</param>
    /// <param name="expected">Whether the checker reads the filter.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(KnownName.FlateDecode, true)]
    [Arguments(KnownName.ASCIIHexDecode, true)]
    [Arguments(KnownName.ASCII85Decode, true)]
    [Arguments(KnownName.LZWDecode, true)]
    [Arguments(KnownName.RunLengthDecode, true)]
    [Arguments(KnownName.Crypt, true)]
    [Arguments(KnownName.DCTDecode, false)]
    [Arguments(KnownName.JPXDecode, false)]
    [Arguments(KnownName.CCITTFaxDecode, false)]
    [Arguments(KnownName.JBIG2Decode, false)]
    [Arguments(KnownName.Fl, false)]
    [Arguments(KnownName.A85, false)]
    [Arguments(KnownName.Normal, false)]
    public async Task IsReadableFilter_ParsedName_PreservesClassification(KnownName name, bool expected) =>
        await Assert.That(PdfAContentScan.IsReadableFilter(name)).IsEqualTo(expected);
}
