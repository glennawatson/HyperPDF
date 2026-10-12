// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Checks loopback asset serving and configuration resource ownership.</summary>
public sealed class PdfJsPageServerTests
{
    /// <summary>The maximum duration of a loopback request.</summary>
    private const int RequestSeconds = 30;

    /// <summary>Unrelated invalid host settings cannot affect the server or install file reload providers.</summary>
    /// <param name="cancellationToken">Cancels creating the test settings.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConfigurationDoesNotLoadOrWatchFiles(CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory(nameof(PdfJsPageServerTests));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "appsettings.json"), "invalid JSON", cancellationToken);
            var builder = PdfJsPageServer.CreateBuilder(directory.FullName);
            await using var application = builder.Build();
            await Assert.That(builder.Configuration.Sources.OfType<FileConfigurationSource>().Count()).IsEqualTo(0);
            await Assert.That(((IConfigurationRoot)application.Configuration).Providers.OfType<FileConfigurationProvider>().Count()).IsEqualTo(0);
            await application.StartAsync(cancellationToken);
            await application.StopAsync(cancellationToken);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Exact in-memory assets survive requests and disposing the server closes its listener.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task AssetsAreServedUntilServerDisposal(CancellationToken cancellationToken)
    {
        const string assetPath = "/build/pdf.mjs";
        var bytes = "export const version = 'test';"u8.ToArray();
        var assets = new Dictionary<string, byte[]>(StringComparer.Ordinal) { [assetPath] = bytes };
        await using var server = await PdfJsPageServer.CreateAsync(assets, cancellationToken);
        await using var services = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient();
        client.Timeout = TimeSpan.FromSeconds(RequestSeconds);
        var address = new Uri(server.BaseUrl + assetPath);
        using var response = await client.GetAsync(address, cancellationToken);
        var actual = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("text/javascript");
        await Assert.That(actual.AsSpan().SequenceEqual(bytes)).IsTrue();
        using var missing = await client.GetAsync(new Uri($"{server.BaseUrl}/absent.mjs"), cancellationToken);
        await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await server.DisposeAsync();
        await Assert.That(async () =>
        {
            using var closed = await client.GetAsync(address, cancellationToken);
        }).Throws<HttpRequestException>();
        await server.DisposeAsync();
    }
}
