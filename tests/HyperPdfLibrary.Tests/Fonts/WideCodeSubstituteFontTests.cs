// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>A composite font drawn with a substitute face draws codes of four bytes, such as those of UTF-32 CMaps.</summary>
public sealed class WideCodeSubstituteFontTests
{
    /// <summary>A code beyond the 16 bits the outline table covers, standing for the letter A.</summary>
    private const int WideCodeOfA = 0x10041;

    /// <summary>A code below 65536, standing for the letter B.</summary>
    private const int NarrowCodeOfB = 0x0042;

    /// <summary>A CMap of four-byte codes for A and B, mapped to one CID each.</summary>
    private const string WideCMap = "%!PS-Adobe-3.0 Resource-CMap\n/CIDInit /ProcSet findresource begin 12 dict begin begincmap /CMapName /Wide def "
        + "1 begincodespacerange <00000000> <0001FFFF> endcodespacerange 2 begincidrange <00010041> <00010041> 1 <00000042> <00000042> 2 endcidrange endcmap";

    /// <summary>A /ToUnicode CMap that gives the two codes their letters.</summary>
    private const string ToUnicode = "/CIDInit /ProcSet findresource begin 12 dict begin begincmap 1 begincodespacerange <00000000> <0001FFFF> endcodespacerange "
        + "2 beginbfchar <00010041> <0041> <00000042> <0042> endbfchar endcmap";

    /// <summary>A code of more than 16 bits gets an outline from the substitute face, like a smaller code.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CodeBeyondSixteenBitsGetsAnOutline()
    {
        using var document = new FontTestDocument(new() { Subtype = "Type0", CidSubtype = "CIDFontType2", BaseFont = "Helvetica", CidEncoding = WideCMap, ToUnicode = ToUnicode });

        var wide = document.Font.GetOutline(WideCodeOfA);
        var narrow = document.Font.GetOutline(NarrowCodeOfB);
        var wideAgain = document.Font.GetOutline(WideCodeOfA);

        await Assert.That(narrow).IsNotNull();
        await Assert.That(wide).IsNotNull();
        await Assert.That(wideAgain).IsSameReferenceAs(wide);
    }
}
