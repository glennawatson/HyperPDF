// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Checks each predefined collection against its three PDF editions.</summary>
public sealed class PdfAPredefinedCMapsTests
{
    /// <summary>Character collection supplement 2.</summary>
    private const int Supplement2 = 2;

    /// <summary>Character collection supplement 4.</summary>
    private const int Supplement4 = 4;

    /// <summary>The Korean character collection.</summary>
    private const string Korean = "Korea1";

    /// <summary>The SimplifiedChinese character collection.</summary>
    private const string SimplifiedChinese = "GB1";

    /// <summary>Character collection supplement 5.</summary>
    private const int Supplement5 = 5;

    /// <summary>The TraditionalChinese character collection.</summary>
    private const string TraditionalChinese = "CNS1";

    /// <summary>Character collection supplement 7.</summary>
    private const int Supplement7 = 7;

    /// <summary>Character collection supplement 3.</summary>
    private const int Supplement3 = 3;

    /// <summary>The Japanese character collection.</summary>
    private const string Japanese = "Japan1";

    /// <summary>Character collection supplement 9.</summary>
    private const int Supplement9 = 9;

    /// <summary>The PDF/A parts based on PDF 1.7 and PDF 2.0.</summary>
    private const int Pdf17Part = Supplement2;

    /// <summary>The PDF/A part based on PDF 2.0.</summary>
    private const int Pdf20Part = Supplement4;

    /// <summary>Gets all fixed lookup cases.</summary>
    /// <returns>The named cases.</returns>
    public static CollectionCase[] Collections() => [.. ChineseCollections(), .. JapaneseCollections(), .. KoreanAndIdentityCollections()];

    /// <summary>Every CMap retains its version-specific collection and supplement.</summary>
    /// <param name="sample">The named expected values.</param>
    /// <returns>The test task.</returns>
    [Test]
    [MethodDataSource(nameof(Collections))]
    public async Task EveryEditionPreservesCollection(CollectionCase sample)
    {
        var (name, ordering, pdf14, pdf17, pdf20) = sample;

        await Assert.That(PdfAPredefinedCMaps.IsPredefined(name)).IsTrue();
        await Assert.That(PdfAPredefinedCMaps.Collection(name, 1)).IsEqualTo(Expected(ordering, pdf14));
        await Assert.That(PdfAPredefinedCMaps.Collection(name, Pdf17Part)).IsEqualTo(Expected(ordering, pdf17));
        await Assert.That(PdfAPredefinedCMaps.Collection(name, Pdf20Part)).IsEqualTo(Expected(ordering == Korean ? "KR" : ordering, pdf20));
    }

    /// <summary>Unknown and differently cased names do not name predefined collections.</summary>
    /// <param name="name">The unrecognised name.</param>
    /// <returns>The test task.</returns>
    [Test]
    [Arguments("")]
    [Arguments("UniJIS-UCS2-h")]
    [Arguments("Unknown")]
    [Arguments("Identity-Hx")]
    public async Task RejectsUnknownNames(string name)
    {
        await Assert.That(PdfAPredefinedCMaps.IsPredefined(name)).IsFalse();
        await Assert.That(PdfAPredefinedCMaps.Collection(name, Pdf20Part)).IsNull();
    }

    /// <summary>Builds the expected collection only when that edition defines it.</summary>
    /// <param name="ordering">The ordering.</param>
    /// <param name="supplement">The supplement, or -1 when absent.</param>
    /// <returns>The expected collection.</returns>
    private static PdfACMapCollection? Expected(string ordering, int supplement) => supplement < 0 ? null : new(ordering, supplement);

    /// <summary>Gets the Chinese collection edition cases.</summary>
    /// <returns>The named collection cases.</returns>
    private static CollectionCase[] ChineseCollections() =>
    [
        new("GB-EUC-H", SimplifiedChinese, 0, 0, Supplement5),
        new("GB-EUC-V", SimplifiedChinese, 0, 0, Supplement5),
        new("GBpc-EUC-H", SimplifiedChinese, 0, 0, Supplement5),
        new("GBpc-EUC-V", SimplifiedChinese, 0, 0, Supplement5),
        new("GBK-EUC-H", SimplifiedChinese, Supplement2, Supplement2, Supplement5),
        new("GBK-EUC-V", SimplifiedChinese, Supplement2, Supplement2, Supplement5),
        new("GBKp-EUC-H", SimplifiedChinese, Supplement2, Supplement2, Supplement5),
        new("GBKp-EUC-V", SimplifiedChinese, Supplement2, Supplement2, Supplement5),
        new("GBK2K-H", SimplifiedChinese, Supplement4, Supplement4, Supplement5),
        new("GBK2K-V", SimplifiedChinese, Supplement4, Supplement4, Supplement5),
        new("UniGB-UCS2-H", SimplifiedChinese, Supplement4, Supplement4, Supplement5),
        new("UniGB-UCS2-V", SimplifiedChinese, Supplement4, Supplement4, Supplement5),
        new("UniGB-UTF16-H", SimplifiedChinese, -1, Supplement4, Supplement5),
        new("UniGB-UTF16-V", SimplifiedChinese, -1, Supplement4, Supplement5),
        new("B5pc-H", TraditionalChinese, 0, 0, Supplement7),
        new("B5pc-V", TraditionalChinese, 0, 0, Supplement7),
        new("HKscs-B5-H", TraditionalChinese, Supplement3, Supplement3, Supplement7),
        new("HKscs-B5-V", TraditionalChinese, Supplement3, Supplement3, Supplement7),
        new("ETen-B5-H", TraditionalChinese, 0, 0, Supplement7),
        new("ETen-B5-V", TraditionalChinese, 0, 0, Supplement7),
        new("ETenms-B5-H", TraditionalChinese, 0, 0, Supplement7),
        new("ETenms-B5-V", TraditionalChinese, 0, 0, Supplement7),
        new("CNS-EUC-H", TraditionalChinese, 0, 0, Supplement7),
        new("CNS-EUC-V", TraditionalChinese, 0, 0, Supplement7),
        new("UniCNS-UCS2-H", TraditionalChinese, Supplement3, Supplement3, Supplement7),
        new("UniCNS-UCS2-V", TraditionalChinese, Supplement3, Supplement3, Supplement7),
        new("UniCNS-UTF16-H", TraditionalChinese, -1, Supplement4, Supplement7),
        new("UniCNS-UTF16-V", TraditionalChinese, -1, Supplement4, Supplement7),
    ];

    /// <summary>Gets the Japanese collection edition cases.</summary>
    /// <returns>The named collection cases.</returns>
    private static CollectionCase[] JapaneseCollections() =>
    [
        new("83pv-RKSJ-H", Japanese, 1, 1, Supplement7),
        new("90ms-RKSJ-H", Japanese, Supplement2, Supplement2, Supplement7),
        new("90ms-RKSJ-V", Japanese, Supplement2, Supplement2, Supplement7),
        new("90msp-RKSJ-H", Japanese, Supplement2, Supplement2, Supplement7),
        new("90msp-RKSJ-V", Japanese, Supplement2, Supplement2, Supplement7),
        new("90pv-RKSJ-H", Japanese, 1, 1, Supplement7),
        new("Add-RKSJ-H", Japanese, 1, 1, Supplement7),
        new("Add-RKSJ-V", Japanese, 1, 1, Supplement7),
        new("EUC-H", Japanese, 1, 1, Supplement7),
        new("EUC-V", Japanese, 1, 1, Supplement7),
        new("Ext-RKSJ-H", Japanese, Supplement2, Supplement2, Supplement7),
        new("Ext-RKSJ-V", Japanese, Supplement2, Supplement2, Supplement7),
        new("H", Japanese, 1, 1, Supplement7),
        new("V", Japanese, 1, 1, Supplement7),
        new("UniJIS-UCS2-H", Japanese, Supplement4, Supplement4, Supplement7),
        new("UniJIS-UCS2-V", Japanese, Supplement4, Supplement4, Supplement7),
        new("UniJIS-UCS2-HW-H", Japanese, Supplement4, Supplement4, Supplement7),
        new("UniJIS-UCS2-HW-V", Japanese, Supplement4, Supplement4, Supplement7),
        new("UniJIS-UTF16-H", Japanese, -1, Supplement5, Supplement7),
        new("UniJIS-UTF16-V", Japanese, -1, Supplement5, Supplement7),
    ];

    /// <summary>Gets the KoreanAndIdentity collection edition cases.</summary>
    /// <returns>The named collection cases.</returns>
    private static CollectionCase[] KoreanAndIdentityCollections() =>
    [
        new("KSC-EUC-H", Korean, 0, 0, Supplement9),
        new("KSC-EUC-V", Korean, 0, 0, Supplement9),
        new("KSCms-UHC-H", Korean, 1, 1, Supplement9),
        new("KSCms-UHC-V", Korean, 1, 1, Supplement9),
        new("KSCms-UHC-HW-H", Korean, 1, 1, Supplement9),
        new("KSCms-UHC-HW-V", Korean, 1, 1, Supplement9),
        new("KSCpc-EUC-H", Korean, 0, 0, Supplement9),
        new("UniKS-UCS2-H", Korean, 1, 1, Supplement9),
        new("UniKS-UCS2-V", Korean, 1, 1, Supplement9),
        new("UniKS-UTF16-H", Korean, -1, Supplement2, Supplement9),
        new("UniKS-UTF16-V", Korean, -1, Supplement2, Supplement9),
        new("Identity-H", "Identity", 0, 0, 0),
        new("Identity-V", "Identity", 0, 0, 0),
    ];

    /// <summary>One exact lookup and its expected values.</summary>
    /// <param name="Name">The expected Name value.</param>
    /// <param name="Ordering">The expected Ordering value.</param>
    /// <param name="Pdf14">The expected Pdf14 value.</param>
    /// <param name="Pdf17">The expected Pdf17 value.</param>
    /// <param name="Pdf20">The expected Pdf20 value.</param>
    public readonly record struct CollectionCase(string Name, string Ordering, int Pdf14, int Pdf17, int Pdf20);
}
