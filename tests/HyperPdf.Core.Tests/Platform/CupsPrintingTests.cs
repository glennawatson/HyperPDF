// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Platform;
using HyperPdf.Core.Printing;
using HyperPdf.Platform.Cups;

namespace HyperPdf.Core.Tests.Platform;

/// <summary>Tests for printing through CUPS.</summary>
public sealed class CupsPrintingTests
{
    /// <summary>The printer queue used by the option tests.</summary>
    private const string PrinterQueue = "office";

    /// <summary>The CUPS option for single-sided or duplex printing.</summary>
    private const string SidesOption = "sides";

    /// <summary>The copies asked for.</summary>
    private const int Copies = 2;

    /// <summary>More copies than allowed.</summary>
    private const int TooMany = 5000;

    /// <summary>Verifies the settings become the standard CUPS job options.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BuildsJobOptions()
    {
        var options = CupsPrinting.BuildOptions(new(PrinterQueue, Copies, false, true, PaperSize.Letter)).ToDictionary(static o => o.Name, static o => o.Value);

        await Assert.That(options["copies"]).IsEqualTo("2");
        await Assert.That(options["media"]).IsEqualTo("na_letter_8.5x11in");
        await Assert.That(options[SidesOption]).IsEqualTo("two-sided-long-edge");
        await Assert.That(options["print-color-mode"]).IsEqualTo("monochrome");
        await Assert.That(options["print-scaling"]).IsEqualTo("none");
    }

    /// <summary>Verifies copies are kept to a sensible number and one-sided colour A4 is the plain case.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsCopiesSensible()
    {
        var options = CupsPrinting.BuildOptions(new(PrinterQueue, TooMany, true, false, PaperSize.A4)).ToDictionary(static o => o.Name, static o => o.Value);

        await Assert.That(options["copies"]).IsEqualTo("999");
        await Assert.That(options["media"]).IsEqualTo("iso_a4_210x297mm");
        await Assert.That(options[SidesOption]).IsEqualTo("one-sided");
        await Assert.That(options["print-color-mode"]).IsEqualTo("color");
    }

    /// <summary>Verifies that short-edge binding reaches CUPS only when two-sided printing is enabled.</summary>
    /// <param name="twoSided">Whether to print on both sides.</param>
    /// <param name="expected">The CUPS sides option.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true, "two-sided-short-edge")]
    [Arguments(false, "one-sided")]
    public async Task AppliesShortEdgeBinding(bool twoSided, string expected)
    {
        var job = new PrintJobOptions(PrinterQueue, Copies, true, twoSided, PaperSize.A4) { Binding = DuplexBinding.ShortEdge };
        var options = CupsPrinting.BuildOptions(job).ToDictionary(static option => option.Name, static option => option.Value);

        await Assert.That(options[SidesOption]).IsEqualTo(expected);
    }

    /// <summary>Verifies every paper size reaches CUPS as its PWG media name.</summary>
    /// <param name="paper">The paper.</param>
    /// <param name="expected">The media name.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PaperSize.A4, "iso_a4_210x297mm")]
    [Arguments(PaperSize.Letter, "na_letter_8.5x11in")]
    [Arguments(PaperSize.A3, "iso_a3_297x420mm")]
    [Arguments(PaperSize.A5, "iso_a5_148x210mm")]
    [Arguments(PaperSize.Legal, "na_legal_8.5x14in")]
    [Arguments(PaperSize.Tabloid, "na_ledger_11x17in")]
    public async Task NamesEveryPaper(PaperSize paper, string expected) => await Assert.That(CupsPrinting.GetMedia(paper)).IsEqualTo(expected);

    /// <summary>Verifies listing printers works, returning none rather than failing when no print server is running.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsPrintersWithoutFailing()
    {
        var printers = CupsPrinting.GetPrinters();

        await Assert.That(printers).IsNotNull();
        await Assert.That(printers.All(static p => !string.IsNullOrEmpty(p.Name))).IsTrue();
    }
}
