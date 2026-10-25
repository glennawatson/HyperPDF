// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Spelling;
using HyperPdf.Engine;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the services take everything toolkit specific from the render head.</summary>
public sealed class AppServicesHeadTests
{
    /// <summary>The picture file the signature test chooses.</summary>
    private const string PictureName = "scan.png";

    /// <summary>The services keep the head they were given, and share it with the render hub.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ServicesExposeTheirHead()
    {
        var head = new FakeRenderHead();
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "head-settings.json");

        using var services = new AppServices(new SettingsStore(path), new HyperPdfEngine(), new FallbackPlatform(), head);

        await Assert.That(services.Head).IsSameReferenceAs(head);
        await Assert.That(head.FactoriesCreated).IsEqualTo(1);
    }

    /// <summary>The services refuse a missing head.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ServicesRequireAHead()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "no-head-settings.json");

        await Assert.That(() => new AppServices(new SettingsStore(path), new HyperPdfEngine(), new FallbackPlatform(), null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Without a desktop spell checker the services check nothing, and the one supplied at start up wins.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SpellCheckerComesFromTheComposition()
    {
        var head = new FakeRenderHead();
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "spell-settings.json");
        using var plain = new AppServices(new SettingsStore(path), new HyperPdfEngine(), new FallbackPlatform(), head);

        var plainChecker = plain.CreateSpellChecker();

        await Assert.That(plainChecker).IsSameReferenceAs(NullSpellChecker.Instance);
    }

    /// <summary>The licence notices are read from the folder the head names, and are empty when it has no notices file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoticesComeFromTheHeadFolder()
    {
        var notices = new LicenceNotices(new FakeRenderHead().NoticesFolder);

        var document = notices.Load();

        await Assert.That(document.AllEntries().ToList().Count).IsEqualTo(0);
        await Assert.That(notices.Load()).IsSameReferenceAs(document);
    }

    /// <summary>Notices cannot be read without a folder.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoticesRequireAFolder() =>
        await Assert.That(static () => new LicenceNotices(string.Empty)).Throws<ArgumentException>();

    /// <summary>The picture a head decodes reaches the signature window's reader.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeadReadsSignaturePictures()
    {
        var head = new FakeRenderHead { Image = new(PictureName, new byte[4], 1, 1) };
        using var request = new SignatureMarkViewModel(SignatureMarkKind.Signature, null, head.LoadImage);
        using var handler = request.BrowseImageInteraction.RegisterHandler(static context => context.SetOutput(PictureName));

        _ = await request.ChooseImageCommand.Execute().ToTask();

        await Assert.That(head.ReadPaths.ToArray()).IsEquivalentTo([PictureName]);
        await Assert.That(request.Style).IsEqualTo(SignatureMarkStyle.Image);
    }
}
