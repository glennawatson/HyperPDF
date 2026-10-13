// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Text;
using HyperPdf.Core.Text.Fonts;

namespace HyperPdf.Core.Tests.Text;

/// <summary>Tests the fonts installed on the machine running the tests, so each operating system's own folders, collections and font formats are read.</summary>
public sealed class SystemFontTests
{
    /// <summary>The installed font folders are found and give embeddable faces with their families.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsInstalledFonts()
    {
        var catalog = SystemCatalog();

        await Assert.That(FontCatalog.SystemDirectories().Count).IsGreaterThan(0);
        await Assert.That(catalog.Faces.Count).IsGreaterThan(0);
        await Assert.That(catalog.Families.Count).IsGreaterThan(StandardFontFamilies.All.Count);
        await Assert.That(catalog.Faces.Select(static face => face.Family).All(catalog.Contains)).IsTrue();
    }

    /// <summary>Gets the installed fonts, skipping on Linux machines that have none.</summary>
    /// <returns>The catalog.</returns>
    /// <exception cref="TUnit.Core.Exceptions.SkipTestException">A Linux machine has no fonts installed.</exception>
    private static FontCatalog SystemCatalog()
    {
        var catalog = FontCatalog.System;
        if (catalog.Faces.Count == 0 && !OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
        {
            throw new TUnit.Core.Exceptions.SkipTestException("No embeddable fonts are installed.");
        }

        return catalog;
    }
}
