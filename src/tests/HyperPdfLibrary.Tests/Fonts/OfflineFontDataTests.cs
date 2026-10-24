// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Generation;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Checks that offline font data mode never generates or downloads, reports what is missing, and still uses cached data.</summary>
public sealed class OfflineFontDataTests
{
    /// <summary>The resource group under test.</summary>
    private const string Folder = "CMaps";

    /// <summary>The first requested asset.</summary>
    private const string FirstName = "second.bin";

    /// <summary>The second requested asset, which sorts before the first.</summary>
    private const string SecondName = "first.bin";

    /// <summary>The number of different assets the determinism test requests.</summary>
    private const int RequestedAssets = 2;

    /// <summary>The fake generated endpoint.</summary>
    private const string ResourcePath = "pinned/font.bin";

    /// <summary>The generated fixture bytes.</summary>
    private static readonly byte[] Bytes = [1];

    /// <summary>Missing assets are neither generated nor requested, and are listed in ordinal order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OfflineModeSkipsMissingAssetsAndListsThem()
    {
        using var cache = new FontDataTestCache();
        var handler = new NoNetworkHandler();
        var resources = new PdfFontResources(PdfFontDataMode.Offline, handler);
        var generated = 0;
        ValueTask<byte[]> Generate(CancellationToken _)
        {
            generated++;
            return ValueTask.FromResult(Bytes);
        }

        await FontDataResources.EnsureAsync(resources.Data, cache.DirectoryPath, Folder, FirstName, Generate, CancellationToken.None);
        await FontDataResources.EnsureAsync(resources.Data, cache.DirectoryPath, Folder, SecondName, Generate, CancellationToken.None);
        await FontDataResources.EnsureAsync(resources.Data, cache.DirectoryPath, Folder, FirstName, Generate, CancellationToken.None);

        await Assert.That(generated).IsEqualTo(0);
        await Assert.That(handler.Requests).IsEqualTo(0);
        await Assert.That(Directory.Exists(cache.DirectoryPath)).IsFalse();
        await Assert.That(resources.GetMissing()).IsEquivalentTo([$"{Folder}/{SecondName}", $"{Folder}/{FirstName}"]);
    }

    /// <summary>Data already in the font data folder is used in offline mode and is not reported missing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OfflineModeUsesCachedAssets()
    {
        using var cache = new FontDataTestCache();
        var path = Path.Combine(cache.DirectoryPath, Folder, FirstName);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, Bytes);
        var resources = new PdfFontResources(PdfFontDataMode.Offline, new NoNetworkHandler());
        var generated = 0;
        ValueTask<byte[]> Generate(CancellationToken _)
        {
            generated++;
            return ValueTask.FromResult(Bytes);
        }

        await FontDataResources.EnsureAsync(resources.Data, cache.DirectoryPath, Folder, FirstName, Generate, CancellationToken.None);

        await Assert.That(generated).IsEqualTo(0);
        await Assert.That(resources.GetMissing()).IsEmpty();
        await Assert.That(resources.Generation).IsEqualTo(1);
    }

    /// <summary>The same missing data gives the same list on every run and in every service.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingListIsDeterministic()
    {
        using var cache = new FontDataTestCache();
        var first = new PdfFontResources(PdfFontDataMode.Offline, null);
        var second = new PdfFontResources(PdfFontDataMode.Offline, null);
        ValueTask<byte[]> Generate(CancellationToken _) => ValueTask.FromResult(Bytes);

        foreach (var resources in new[] { first, second })
        {
            await FontDataResources.EnsureAsync(resources.Data, cache.DirectoryPath, Folder, FirstName, Generate, CancellationToken.None);
            await FontDataResources.EnsureAsync(resources.Data, cache.DirectoryPath, Folder, SecondName, Generate, CancellationToken.None);
        }

        await Assert.That(first.GetMissing()).IsEquivalentTo(second.GetMissing());
        await Assert.That(first.GetMissing().Length).IsEqualTo(RequestedAssets);
    }

    /// <summary>The drawing services carry the chosen mode to their font resources, and start with nothing missing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawingServicesSelectTheMode()
    {
        var defaults = new PdfDrawingServices(new());
        var offline = new PdfDrawingServices(new()) { FontDataMode = PdfFontDataMode.Offline };

        await Assert.That(defaults.FontDataMode).IsEqualTo(PdfFontDataMode.OnDemand);
        await Assert.That(defaults.FontResources.Data.Mode).IsEqualTo(PdfFontDataMode.OnDemand);
        await Assert.That(offline.FontResources.Data.Mode).IsEqualTo(PdfFontDataMode.Offline);
        await Assert.That(offline.GetMissingFontData()).IsEmpty();
    }

    /// <summary>The test transport really is on the download path, so a zero request count in offline mode means something.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnDemandModeUsesTheSuppliedTransport()
    {
        var handler = new NoNetworkHandler();
        var resources = new PdfFontResources(PdfFontDataMode.OnDemand, handler);
        var failed = false;
        try
        {
            _ = await FontDataDownload.ReadAsync(resources.Data, ResourcePath, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            failed = true;
        }
        finally
        {
            resources.CloseDownloadClient();
        }

        await Assert.That(failed).IsTrue();
        await Assert.That(handler.Requests).IsGreaterThanOrEqualTo(1);
    }
}
