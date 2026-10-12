// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Printing;

namespace HyperPdf.Core.Tests.Printing;

/// <summary>Tests for <see cref="DevicePlacement"/>, which places pages on Windows printers.</summary>
public sealed class DevicePlacementTests
{
    /// <summary>A common printer resolution.</summary>
    private const int Dpi = 600;

    /// <summary>A Letter page's width at 600 DPI.</summary>
    private const int LetterWidth = 5_100;

    /// <summary>A Letter page's height at 600 DPI.</summary>
    private const int LetterHeight = 6_600;

    /// <summary>A roomy printable width.</summary>
    private const int RoomyWidth = 10_000;

    /// <summary>A roomy printable height.</summary>
    private const int RoomyHeight = 20_000;

    /// <summary>A Letter page's height at 1200 DPI.</summary>
    private const int LetterHeightAt1200 = 13_200;

    /// <summary>A Letter page in points.</summary>
    private static readonly Geometry.PageSize Letter = new(612, 792);

    /// <summary>A wide page is turned a quarter counter-clockwise to fill upright paper, as print systems turn it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TurnsWidePagesOntoUprightPaper()
    {
        var placement = DevicePlacement.Fit(new(Letter.Height, Letter.Width), Dpi, Dpi, LetterWidth, LetterHeight);

        await Assert.That(placement.Rotation).IsEqualTo(Geometry.PageRotation.Rotate270);
        await Assert.That(placement.Width).IsEqualTo(LetterWidth);
        await Assert.That(placement.Height).IsEqualTo(LetterHeight);
        await Assert.That(placement.Left).IsEqualTo(0);
        await Assert.That(placement.Top).IsEqualTo(0);
    }

    /// <summary>Pages that match the paper's orientation are not turned, whichever way the paper faces.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsMatchingOrientation()
    {
        var upright = DevicePlacement.Fit(Letter, Dpi, Dpi, LetterWidth, LetterHeight);
        var sideways = DevicePlacement.Fit(new(Letter.Height, Letter.Width), Dpi, Dpi, LetterHeight, LetterWidth);
        var tallOnSideways = DevicePlacement.Fit(Letter, Dpi, Dpi, LetterHeight, LetterWidth);

        await Assert.That(upright.Rotation).IsEqualTo(Geometry.PageRotation.None);
        await Assert.That(sideways.Rotation).IsEqualTo(Geometry.PageRotation.None);
        await Assert.That(sideways.Width).IsEqualTo(LetterHeight);
        await Assert.That(tallOnSideways.Rotation).IsEqualTo(Geometry.PageRotation.Rotate270);
        await Assert.That(tallOnSideways.Width).IsEqualTo(LetterHeight);
    }

    /// <summary>A page that fits is printed at its true size, centred.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsTrueSize()
    {
        const int Printable = 4_900;
        const int PrintableHeight = 6_400;
        var placement = DevicePlacement.Fit(Letter, Dpi, Dpi, LetterWidth, LetterHeight);

        await Assert.That(placement.Width).IsEqualTo(LetterWidth);
        await Assert.That(placement.Height).IsEqualTo(LetterHeight);
        await Assert.That(placement.Left).IsEqualTo(0);

        var margins = DevicePlacement.Fit(Letter, Dpi, Dpi, Printable, PrintableHeight);
        await Assert.That(margins.Width).IsLessThanOrEqualTo(Printable);
        await Assert.That(margins.DeviceHeight).IsLessThanOrEqualTo(PrintableHeight);
        await Assert.That(margins.Left).IsGreaterThanOrEqualTo(0);
    }

    /// <summary>A different vertical resolution stretches rows rather than distorting the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StretchesForUnevenResolution()
    {
        const int Vertical = 1_200;
        var placement = DevicePlacement.Fit(Letter, Dpi, Vertical, RoomyWidth, RoomyHeight);

        await Assert.That(placement.Height).IsEqualTo(LetterHeight);
        await Assert.That(placement.DeviceHeight).IsEqualTo(LetterHeightAt1200);
        await Assert.That(placement.DeviceRow(placement.Height)).IsEqualTo(placement.DeviceHeight);
    }

    /// <summary>Bands stay within the memory budget and always hold at least one row.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BandsWithinBudget()
    {
        const int Budget = 16 * 1024 * 1024;
        var placement = DevicePlacement.Fit(Letter, Dpi, Dpi, LetterWidth, LetterHeight);
        var rows = placement.RowsPerBand(Budget);

        await Assert.That(rows * placement.Width * sizeof(int)).IsLessThanOrEqualTo(Budget);
        await Assert.That(placement.RowsPerBand(1)).IsEqualTo(1);
    }
}
