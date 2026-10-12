// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Theming;

namespace HyperPdf.Core.Tests.Theming;

/// <summary>Checks every built-in scheme meets the comfort contrast minimums (docs/COMFORT.md, rule 7.2).</summary>
public sealed class ColorSchemeContrastTests
{
    /// <summary>The minimum contrast of normal text.</summary>
    private const double TextMinimum = 7.0;

    /// <summary>The maximum contrast of Calm text, so it never glares.</summary>
    private const double CalmTextMaximum = 9.5;

    /// <summary>The minimum contrast of inactive text and text on a selection.</summary>
    private const double InactiveMinimum = 4.5;

    /// <summary>The minimum contrast of icons, borders and the accent.</summary>
    private const double GraphicMinimum = 3.0;

    /// <summary>Gets the built-in schemes.</summary>
    /// <returns>The schemes.</returns>
    public static IEnumerable<Func<ColorScheme>> Schemes() => ColorSchemes.All.Select(static scheme => (Func<ColorScheme>)(() => scheme));

    /// <summary>Verifies text, inactive text and selected text against every surface.</summary>
    /// <param name="scheme">The scheme.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Schemes))]
    public async Task TextIsReadable(ColorScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        foreach (var surface in Surfaces(scheme))
        {
            await Assert.That(ColorMath.Contrast(scheme.Text, surface)).IsGreaterThanOrEqualTo(TextMinimum);
            await Assert.That(ColorMath.Contrast(scheme.InactiveText, surface)).IsGreaterThanOrEqualTo(InactiveMinimum);
        }

        await Assert.That(ColorMath.Contrast(scheme.Text, scheme.Selection)).IsGreaterThanOrEqualTo(InactiveMinimum);
    }

    /// <summary>Verifies icons, borders and the accent stand out from every surface.</summary>
    /// <param name="scheme">The scheme.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Schemes))]
    public async Task GraphicsAreVisible(ColorScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        uint[] graphics = [scheme.Border, scheme.Accent, scheme.Tints.Navigation, scheme.Tints.Add, scheme.Tints.Edit, scheme.Tints.Remove];
        foreach (var surface in Surfaces(scheme))
        {
            foreach (var graphic in graphics)
            {
                await Assert.That(ColorMath.Contrast(graphic, surface)).IsGreaterThanOrEqualTo(GraphicMinimum);
            }
        }
    }

    /// <summary>Verifies Calm text stays below the glare limit and the page ink is readable on its paper.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CalmIsSoft()
    {
        var calm = ColorSchemes.Calm;
        foreach (var surface in Surfaces(calm))
        {
            await Assert.That(ColorMath.Contrast(calm.Text, surface)).IsLessThan(CalmTextMaximum);
        }

        await Assert.That(ColorMath.Contrast(calm.PageTone.Ink, calm.PageTone.Paper)).IsGreaterThanOrEqualTo(TextMinimum);
    }

    /// <summary>Verifies the four schemes are distinct choices with distinct names.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SchemesAreDistinct()
    {
        const int schemeCount = 4;
        await Assert.That(ColorSchemes.All.Select(static scheme => scheme.Name).Distinct(StringComparer.Ordinal).Count()).IsEqualTo(schemeCount);
    }

    /// <summary>Gets the surfaces text and icons are drawn on.</summary>
    /// <param name="scheme">The scheme.</param>
    /// <returns>The window, header and view backgrounds.</returns>
    private static uint[] Surfaces(ColorScheme scheme) => [scheme.Window, scheme.Header, scheme.View];
}
