// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Text;
using HyperPdf.Core.Text.Fonts;

namespace HyperPdf.Core.Tests.Text;

/// <summary>
/// Tests the fonts installed on the machine running the tests, so each operating system's own folders, collections
/// and font formats are read, subset and read back.
/// </summary>
public sealed class SystemFontTests
{
    /// <summary>The text each face is subset for.</summary>
    private const string Sample = "Hello world";

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

    /// <summary>Every embeddable installed face loads, subsets the letters it has and reads back with the same letters.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SubsetsEveryInstalledFace()
    {
        var failures = new List<string>();
        foreach (var file in SystemCatalog().Faces.GroupBy(static face => face.Path))
        {
            var data = await File.ReadAllBytesAsync(file.Key);
            foreach (var face in file)
            {
                if (!SubsetsAndReadsBack(face, data))
                {
                    failures.Add($"{face.Family} {face.Style} ({face.Path}#{face.FaceIndex})");
                }
            }
        }

        await Assert.That(failures).IsEmpty();
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

    /// <summary>Loads a face, subsets the sample's letters it has, and checks the subset maps them the same way.</summary>
    /// <param name="face">The face.</param>
    /// <param name="data">The face's font file.</param>
    /// <returns><see langword="true"/> when the subset reads back with every letter.</returns>
    private static bool SubsetsAndReadsBack(FontFace face, byte[] data)
    {
        var font = FontProgram.FromBytes(face, data);
        if (font is null)
        {
            return false;
        }

        var glyphs = new List<ushort>();
        foreach (var letter in Sample)
        {
            var glyph = font.GlyphFor(letter);
            if (glyph != 0)
            {
                glyphs.Add(glyph);
            }
        }

        var subset = FontSubsetter.Create(font, glyphs);
        var reloaded = subset is null ? null : FontProgram.FromBytes(face with { FaceIndex = 0 }, subset.Data);
        if (subset is null || reloaded is null)
        {
            return false;
        }

        foreach (var letter in Sample)
        {
            var glyph = font.GlyphFor(letter);
            if (glyph != 0 && reloaded.GlyphFor(letter) != subset.Map(glyph))
            {
                return false;
            }
        }

        return true;
    }
}
