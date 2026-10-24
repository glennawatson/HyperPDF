// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Text;
using HyperPdf.Core.Text.Fonts;
using HyperPdf.TestAssets;

namespace HyperPdf.Core.Tests.Text;

/// <summary>Tests reading and finding installed fonts, using a generated font so results match everywhere.</summary>
public sealed class FontTests
{
    /// <summary>The restricted licence flag: the font must not be embedded.</summary>
    private const int Restricted = 0x0002;

    /// <summary>The licence flag forbidding subsetting.</summary>
    private const int NoSubsetting = 0x0100;

    /// <summary>The bold weight.</summary>
    private const int Bold = 700;

    /// <summary>The regular weight.</summary>
    private const int Regular = 400;

    /// <summary>The advance of an ordinary glyph in the test font.</summary>
    private const int OrdinaryAdvance = 500;

    /// <summary>The CJK character in the test font.</summary>
    private const int Zhong = 0x4E2D;

    /// <summary>The Hebrew letter alef.</summary>
    private const int Alef = 0x05D0;

    /// <summary>A character the test font lacks.</summary>
    private const int Missing = 0x263A;

    /// <summary>The face reader finds the family, style, weight, classification and licence.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsFaces()
    {
        using var folder = new FontFolder();
        var regular = folder.Write("regular.ttf", TestFont.Create());
        var faces = new List<FontFace>();
        FontFaceReader.Read(regular, faces);

        await Assert.That(faces.Count).IsEqualTo(1);
        var face = faces[0];
        await Assert.That(face.Family).IsEqualTo(TestFont.Family);
        await Assert.That(face.Style).IsEqualTo("Regular");
        await Assert.That(face.Weight).IsEqualTo(Regular);
        await Assert.That(face.IsItalic).IsFalse();
        await Assert.That(face.IsSerif).IsFalse();
        await Assert.That(face.HasEmbeddableOutlines).IsTrue();
        await Assert.That(face.CanEmbed && face.CanSubset).IsTrue();
    }

    /// <summary>The catalog offers embeddable families after the built in three, and finds the nearest face.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsTheNearestFace()
    {
        using var folder = new FontFolder();
        _ = folder.Write("regular.ttf", TestFont.Create());
        _ = folder.Write(Path.Combine("sub", "bold.ttf"), TestFont.Create(0, null, true));
        _ = folder.Write("locked.ttf", TestFont.Create(Restricted, "Locked Sans", false));
        _ = folder.Write("notes.txt", [0x1, 0x2, 0x3]);
        _ = folder.Write("broken.ttf", [0x0, 0x1, 0x0, 0x0, 0x9]);
        var catalog = FontCatalog.Scan([folder.Path, Path.Combine(folder.Path, "missing")]);

        var bold = catalog.Find(TestFont.Family, true, false);
        var italic = catalog.Find(TestFont.Family, false, true);

        await Assert.That(catalog.Families).IsEquivalentTo(["Helvetica", "Times", "Courier", TestFont.Family]);
        await Assert.That(catalog.Contains("times")).IsTrue();
        await Assert.That(catalog.Contains("Locked Sans")).IsFalse();
        await Assert.That(bold!.Face.Weight).IsEqualTo(Bold);
        await Assert.That(bold.SynthesizeBold).IsFalse();
        await Assert.That(italic!.Face.Weight).IsEqualTo(Regular);
        await Assert.That(italic.SynthesizeItalic).IsTrue();
        await Assert.That(catalog.Find("Helvetica", false, false)).IsNull();
        await Assert.That(FontCatalog.Empty.Families.Count).IsEqualTo(StandardFontFamilies.All.Count);
    }

    /// <summary>The loaded font gives metrics, advances, kerning and the character map, CJK and Hebrew included.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsMetricsAndCharacters()
    {
        var font = Load(TestFont.Create());

        await Assert.That(font.UnitsPerEm).IsEqualTo(TestFont.UnitsPerEm);
        await Assert.That(font.Ascent).IsEqualTo(TestFont.Ascender / (float)TestFont.UnitsPerEm);
        await Assert.That(font.GlyphFor('A')).IsEqualTo(TestFont.GlyphOf('A'));
        await Assert.That(font.GlyphFor(Zhong)).IsEqualTo(TestFont.GlyphOf('中'));
        await Assert.That(font.GlyphFor(Alef)).IsEqualTo(TestFont.GlyphOf('א'));
        await Assert.That(font.GlyphFor(Missing)).IsEqualTo((ushort)0);
        await Assert.That(font.Advance(TestFont.GlyphOf('A'))).IsEqualTo(OrdinaryAdvance);
        await Assert.That(font.Kerning(TestFont.GlyphOf('A'), TestFont.GlyphOf('V'))).IsEqualTo(TestFont.AvKerning);
        await Assert.That(font.Kerning(TestFont.GlyphOf('V'), TestFont.GlyphOf('A'))).IsEqualTo(0);
    }

    /// <summary>A licence forbidding subsetting still leaves the face embeddable, and the catalog says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoSubsettingLicenceStillEmbeds()
    {
        var font = Load(TestFont.Create(NoSubsetting, null, false));

        await Assert.That(font.Face.CanEmbed).IsTrue();
        await Assert.That(font.Face.CanSubset).IsFalse();
    }

    /// <summary>Loads a font's bytes as its first face.</summary>
    /// <param name="data">The font file.</param>
    /// <returns>The program.</returns>
    private static FontProgram Load(byte[] data)
    {
        var faces = new List<FontFace>();
        using var folder = new FontFolder();
        FontFaceReader.Read(folder.Write("font.ttf", data), faces);
        return FontProgram.FromBytes(faces[0] with { HasEmbeddableOutlines = true }, data)!;
    }

    /// <summary>A temporary folder of font files.</summary>
    private sealed class FontFolder : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="FontFolder"/> class.</summary>
        public FontFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pvl-fonts-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(Path);
        }

        /// <summary>Gets the folder.</summary>
        public string Path { get; }

        /// <summary>Writes a file.</summary>
        /// <param name="name">The name, relative to the folder.</param>
        /// <param name="bytes">The contents.</param>
        /// <returns>The full path.</returns>
        public string Write(string name, byte[] bytes)
        {
            var path = System.IO.Path.Combine(Path, name);
            _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        /// <inheritdoc/>
        public void Dispose() => Directory.Delete(Path, true);
    }
}
