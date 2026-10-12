// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Platform;
using HyperPdf.Core.Printing;
using HyperPdf.Platform.Windows.Printing;

namespace HyperPdf.Core.Tests.Platform;

/// <summary>Tests the printer settings passed to Windows.</summary>
public sealed class WindowsPrintServiceTests
{
    /// <summary>Why the tests skip elsewhere.</summary>
    private const string NeedsWindows = "Windows printer settings require Windows.";

    /// <summary>The Windows value for single-sided printing.</summary>
    private const short OneSided = 1;

    /// <summary>The Windows value for long-edge duplex printing.</summary>
    private const short LongEdge = 2;

    /// <summary>The Windows value for short-edge duplex printing.</summary>
    private const short ShortEdge = 3;

    /// <summary>The size of a DEVMODEW without driver data.</summary>
    private const int DevModeSize = 220;

    /// <summary>The offset of DEVMODEW.dmFields.</summary>
    private const int FieldsOffset = 72;

    /// <summary>The offset of DEVMODEW.dmOrientation.</summary>
    private const int OrientationOffset = 76;

    /// <summary>The offset of DEVMODEW.dmDuplex.</summary>
    private const int DuplexOffset = 94;

    /// <summary>DM_ORIENTATION and DM_DUPLEX.</summary>
    private const uint OrientationAndDuplexFields = 0x1 | 0x1000;

    /// <summary>The Windows value for upright paper.</summary>
    private const short Portrait = 1;

    /// <summary>DMORIENT_LANDSCAPE, a printer's saved default that must not change the duplex edge.</summary>
    private const short Landscape = 2;

    /// <summary>Verifies that the chosen edge becomes the correct Windows duplex value.</summary>
    /// <param name="twoSided">Whether duplex printing is enabled.</param>
    /// <param name="binding">The edge used to turn the sheet.</param>
    /// <param name="expected">The Windows duplex value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false, DuplexBinding.LongEdge, OneSided)]
    [Arguments(false, DuplexBinding.ShortEdge, OneSided)]
    [Arguments(true, DuplexBinding.LongEdge, LongEdge)]
    [Arguments(true, DuplexBinding.ShortEdge, ShortEdge)]
    public async Task AppliesDuplexBinding(bool twoSided, DuplexBinding binding, short expected)
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test(NeedsWindows);
            return;
        }

        var options = new PrintJobOptions("printer", 1, true, twoSided, PaperSize.A4) { Binding = binding };

        await Assert.That(WindowsPrintService.GetDuplex(options)).IsEqualTo(expected);
    }

    /// <summary>Verifies that a printer saved as landscape is set upright, so the chosen duplex edge stays the sheet's own edge.</summary>
    /// <param name="binding">The edge used to turn the sheet.</param>
    /// <param name="expected">The Windows duplex value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(DuplexBinding.LongEdge, LongEdge)]
    [Arguments(DuplexBinding.ShortEdge, ShortEdge)]
    public async Task SetsUprightPaperForDuplexEdge(DuplexBinding binding, short expected)
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test(NeedsWindows);
            return;
        }

        var devMode = new byte[DevModeSize];
        _ = BitConverter.TryWriteBytes(devMode.AsSpan(OrientationOffset), Landscape);
        PrintJobOptions options = new("printer", 1, true, true, PaperSize.A4) { Binding = binding };
        WindowsPrintService.ApplyChoices(devMode, options);

        await Assert.That(BitConverter.ToUInt32(devMode, FieldsOffset) & OrientationAndDuplexFields).IsEqualTo(OrientationAndDuplexFields);
        await Assert.That(BitConverter.ToInt16(devMode, OrientationOffset)).IsEqualTo(Portrait);
        await Assert.That(BitConverter.ToInt16(devMode, DuplexOffset)).IsEqualTo(expected);
    }

    /// <summary>Verifies every paper size becomes its Windows paper value.</summary>
    /// <param name="paper">The paper.</param>
    /// <param name="expected">The DMPAPER value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PaperSize.Tabloid, (short)3)]
    [Arguments(PaperSize.Letter, (short)1)]
    [Arguments(PaperSize.Legal, (short)5)]
    [Arguments(PaperSize.A3, (short)8)]
    [Arguments(PaperSize.A4, (short)9)]
    [Arguments(PaperSize.A5, (short)11)]
    public async Task NamesEveryPaper(PaperSize paper, short expected)
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test(NeedsWindows);
            return;
        }

        await Assert.That(WindowsPrintService.GetPaper(paper)).IsEqualTo(expected);
    }
}
