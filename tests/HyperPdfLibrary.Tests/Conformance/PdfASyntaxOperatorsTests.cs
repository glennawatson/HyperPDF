// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Conformance;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Checks exact operator bytes and their operand classifications.</summary>
public sealed class PdfASyntaxOperatorsTests
{
    /// <summary>Gets all fixed lookup cases.</summary>
    /// <returns>The named cases.</returns>
    public static OperatorCase[] Operators() =>
    [
        new("b", (int)PdfASyntaxLink.None), new("B", (int)PdfASyntaxLink.None),
        new("b*", (int)PdfASyntaxLink.None), new("B*", (int)PdfASyntaxLink.None),
        new("BDC", (int)PdfASyntaxLink.TagAndProperties), new("BI", (int)PdfASyntaxLink.None),
        new("BMC", (int)PdfASyntaxLink.LastName), new("BT", (int)PdfASyntaxLink.None),
        new("BX", (int)PdfASyntaxLink.None), new("c", (int)PdfASyntaxLink.AllNumbers),
        new("cm", (int)PdfASyntaxLink.AllNumbers), new("CS", (int)PdfASyntaxLink.LastName),
        new("cs", (int)PdfASyntaxLink.LastName), new("d", (int)PdfASyntaxLink.Dash),
        new("d0", (int)PdfASyntaxLink.LastTwoNumbers), new("d1", (int)PdfASyntaxLink.AllNumbers),
        new("Do", (int)PdfASyntaxLink.None), new("DP", (int)PdfASyntaxLink.TagAndProperties),
        new("EI", (int)PdfASyntaxLink.None), new("EMC", (int)PdfASyntaxLink.None),
        new("ET", (int)PdfASyntaxLink.None), new("EX", (int)PdfASyntaxLink.None),
        new("f", (int)PdfASyntaxLink.None), new("F", (int)PdfASyntaxLink.None),
        new("f*", (int)PdfASyntaxLink.None), new("G", (int)PdfASyntaxLink.None),
        new("g", (int)PdfASyntaxLink.None), new("gs", (int)PdfASyntaxLink.None),
        new("h", (int)PdfASyntaxLink.None), new("i", (int)PdfASyntaxLink.LastNumber),
        new("ID", (int)PdfASyntaxLink.None), new("j", (int)PdfASyntaxLink.LastInteger),
        new("J", (int)PdfASyntaxLink.LastInteger), new("K", (int)PdfASyntaxLink.None),
        new("k", (int)PdfASyntaxLink.None), new("l", (int)PdfASyntaxLink.AllNumbers),
        new("m", (int)PdfASyntaxLink.AllNumbers), new("M", (int)PdfASyntaxLink.LastNumber),
        new("MP", (int)PdfASyntaxLink.LastName), new("n", (int)PdfASyntaxLink.None),
        new("q", (int)PdfASyntaxLink.None), new("Q", (int)PdfASyntaxLink.None),
        new("re", (int)PdfASyntaxLink.AllNumbers), new("RG", (int)PdfASyntaxLink.None),
        new("rg", (int)PdfASyntaxLink.None), new("ri", (int)PdfASyntaxLink.None),
        new("s", (int)PdfASyntaxLink.None), new("S", (int)PdfASyntaxLink.None),
        new("SC", (int)PdfASyntaxLink.LastName), new("sc", (int)PdfASyntaxLink.LastName),
        new("SCN", (int)PdfASyntaxLink.LastName), new("scn", (int)PdfASyntaxLink.LastName),
        new("sh", (int)PdfASyntaxLink.None), new("T*", (int)PdfASyntaxLink.None),
        new("Tc", (int)PdfASyntaxLink.LastNumber), new("Td", (int)PdfASyntaxLink.LastTwoNumbers),
        new("TD", (int)PdfASyntaxLink.LastTwoNumbers), new("Tf", (int)PdfASyntaxLink.Font),
        new("Tj", (int)PdfASyntaxLink.LastString), new("TJ", (int)PdfASyntaxLink.LastArray),
        new("TL", (int)PdfASyntaxLink.LastNumber), new("Tm", (int)PdfASyntaxLink.AllNumbers),
        new("Tr", (int)PdfASyntaxLink.None), new("Ts", (int)PdfASyntaxLink.LastNumber),
        new("Tw", (int)PdfASyntaxLink.LastNumber), new("Tz", (int)PdfASyntaxLink.LastNumber),
        new("v", (int)PdfASyntaxLink.AllNumbers), new("w", (int)PdfASyntaxLink.LastNumber),
        new("W", (int)PdfASyntaxLink.None), new("W*", (int)PdfASyntaxLink.None),
        new("y", (int)PdfASyntaxLink.AllNumbers), new("'", (int)PdfASyntaxLink.LastString),
        new("\"", (int)PdfASyntaxLink.None),
    ];

    /// <summary>Every standard operator retains its operand classification.</summary>
    /// <param name="sample">The named expected values.</param>
    /// <returns>The test task.</returns>
    [Test]
    [MethodDataSource(nameof(Operators))]
    public async Task StandardOperatorsPreserveOperands(OperatorCase sample)
    {
        var (word, expected) = sample;

        var bytes = Encoding.ASCII.GetBytes(word);
        await Assert.That(PdfASyntaxOperators.IsStandard(bytes)).IsTrue();
        await Assert.That((int)PdfASyntaxOperators.LinkOf(bytes)).IsEqualTo(expected);
    }

    /// <summary>Length tags prevent prefixes, null bytes and long words from aliasing an operator.</summary>
    /// <param name="word">The non-operator word.</param>
    /// <returns>The test task.</returns>
    [Test]
    [Arguments("")]
    [Arguments("T")]
    [Arguments("Tj\u0000")]
    [Arguments("BMC\u0000")]
    [Arguments("abcd")]
    [Arguments("zzz")]
    public async Task RejectsNonOperators(string word)
    {
        var bytes = Encoding.ASCII.GetBytes(word);
        await Assert.That(PdfASyntaxOperators.IsStandard(bytes)).IsFalse();
        await Assert.That((int)PdfASyntaxOperators.LinkOf(bytes)).IsEqualTo(0);
    }

    /// <summary>One exact lookup and its expected values.</summary>
    /// <param name="Word">The expected Word value.</param>
    /// <param name="Link">The expected Link value.</param>
    public readonly record struct OperatorCase(string Word, int Link);
}
