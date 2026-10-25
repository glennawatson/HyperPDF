// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Checks exact retained type spellings and reuse of standard names.</summary>
public sealed class PdfStructureTypesSpellingTests
{
    /// <summary>Standard type strings are shared without decoding another string.</summary>
    /// <param name="name">The standard spelling.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Document")]
    [Arguments("P")]
    [Arguments("H1")]
    [Arguments("Table")]
    [Arguments("TH")]
    public async Task GetSpelling_StandardName_DoesNotAllocate(string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        var first = PdfStructureTypes.GetSpelling(bytes);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var actual = PdfStructureTypes.GetSpelling(bytes);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(actual).IsEqualTo(name);
        await Assert.That(actual).IsSameReferenceAs(first);
        await Assert.That(allocated).IsEqualTo(0);
    }

    /// <summary>Names outside the standard table retain their exact UTF-8 spelling.</summary>
    /// <param name="name">The custom spelling.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("H99")]
    [Arguments("Custom")]
    [Arguments("章")]
    [Arguments("ThisCustomStructureTypeExceedsTheStackBuffer")]
    public async Task GetSpelling_CustomName_PreservesText(string name) =>
        await Assert.That(PdfStructureTypes.GetSpelling(Encoding.UTF8.GetBytes(name))).IsEqualTo(name);
}
