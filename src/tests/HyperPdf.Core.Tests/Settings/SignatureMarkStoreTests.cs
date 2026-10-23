// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Settings;

namespace HyperPdf.Core.Tests.Settings;

/// <summary>Tests remembering and forgetting a signature and initials.</summary>
public sealed class SignatureMarkStoreTests
{
    /// <summary>The picture's width in pixels.</summary>
    private const int PictureWidth = 2;

    /// <summary>A small picture: opaque ink beside soft ink.</summary>
    private static readonly byte[] Ink = [0, 0, 0, 255, 0, 0, 0, 128];

    /// <summary>A drawn zigzag.</summary>
    private static readonly PagePoint[] Zigzag = [new(1, 2), new(8, 9), new(15, 2), new(22, 9)];

    /// <summary>A drawn curve and a picture come back exactly as remembered, after the store is opened again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RememberedMarksSurviveReopening()
    {
        var directory = NewDirectory();
        try
        {
            var settings = new SettingsStore(Path.Combine(directory, "settings.json"));
            var drawn = SignatureMark.Drawn(SignatureMarkKind.Signature, Zigzag, [Zigzag.Length])!;
            var picture = SignatureMark.FromImage(SignatureMarkKind.Initials, SignatureImage.Create(Ink, PictureWidth, 1, false)!);
            SignatureMarkStore.Beside(settings).Save(new() { Signature = drawn, Initials = picture });

            var loaded = SignatureMarkStore.Beside(settings).Load();

            await Assert.That(loaded.Signature).IsNotNull();
            await Assert.That(loaded.Signature!.Style).IsEqualTo(SignatureMarkStyle.Drawn);
            await Assert.That(loaded.Signature.Points.ToArray()).IsEquivalentTo(drawn.Points.ToArray());
            await Assert.That(loaded.Signature.StrokeLengths.ToArray()).IsEquivalentTo(drawn.StrokeLengths.ToArray());
            await Assert.That(loaded.Signature.Width).IsEqualTo(drawn.Width);
            await Assert.That(loaded.Initials).IsNotNull();
            await Assert.That(loaded.Initials!.Kind).IsEqualTo(SignatureMarkKind.Initials);
            await Assert.That(loaded.Initials.Pixels.ToArray()).IsEquivalentTo(Ink);
            await Assert.That(loaded.Get(SignatureMarkKind.Initials)).IsSameReferenceAs(loaded.Initials);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Forgetting everything deletes the file, so nothing of the signature is left behind.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ForgettingEverythingDeletesTheFile()
    {
        var directory = NewDirectory();
        try
        {
            var store = new SignatureMarkStore(Path.Combine(directory, "marks.json"));
            var marks = new SavedSignatureMarks();
            marks.Set(SignatureMarkKind.Signature, SignatureMark.Typed(SignatureMarkKind.Signature, "Glenn Watson"));
            store.Save(marks);
            var saved = File.Exists(store.FilePath);

            marks.Set(SignatureMarkKind.Signature, null);
            store.Save(marks);

            await Assert.That(saved).IsTrue();
            await Assert.That(File.Exists(store.FilePath)).IsFalse();
            await Assert.That(store.Load().Signature).IsNull();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>A damaged mark that cannot be placed is dropped when loading rather than offered.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DropsMarksThatCannotBePlaced()
    {
        var directory = NewDirectory();
        try
        {
            var store = new SignatureMarkStore(Path.Combine(directory, "marks.json"));
            await File.WriteAllTextAsync(store.FilePath, """{ "signature": { "style": "Drawn", "width": 4, "height": 4, "points": [1, 2, 3], "strokeLengths": [2] } }""");

            await Assert.That(store.Load().Signature).IsNull();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>The certificates the user chose to remember come back after the settings are opened again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RememberedCertificatesSurviveReopening()
    {
        var directory = NewDirectory();
        try
        {
            var store = new SettingsStore(Path.Combine(directory, "settings.json"));
            var settings = new AppSettings();
            settings.RememberedCertificates.Add(new("/keys/work.pfx", "Work", "AB12"));
            store.Save(settings);

            var loaded = store.Load();

            await Assert.That(loaded.RememberedCertificates).IsEquivalentTo([new RememberedCertificate("/keys/work.pfx", "Work", "AB12")]);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Makes an empty working directory.</summary>
    /// <returns>The directory.</returns>
    private static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hyperpdf-marks-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        return directory;
    }
}
