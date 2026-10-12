// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Ocr;
using HyperPdf.Http;
using HyperPdf.Http.Ocr;
using HyperPdf.Http.Remote;
using HyperPdf.Ocr;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the language pack work text recognition adds: the check made each time Recognise Text is pressed (which
/// packs the chosen languages still need, and where their data is), and a verified pack download through Refit from an
/// in-memory server, so the streaming, hashing and atomic move are timed without the network. Allocations are checked
/// from the EventPipe trace.
/// </summary>
public class OcrLanguagePackBenchmarks
{
    /// <summary>The size of the downloaded pack: one megabyte, about a quarter of English.</summary>
    private const int PackSize = 1024 * 1024;

    /// <summary>The language setting checked: English, downloaded, and German, not downloaded.</summary>
    private const string Languages = "eng+deu";

    /// <summary>The in-memory server.</summary>
    private readonly PackHandler _handler = new();

    /// <summary>The folder of downloaded packs.</summary>
    private string _folder = string.Empty;

    /// <summary>The pack downloaded each time.</summary>
    private OcrLanguagePack _pack = null!;

    /// <summary>The Refit client sending to the in-memory server.</summary>
    private IRemoteDocumentApi _api = null!;

    /// <summary>The chosen languages, as packs.</summary>
    private OcrLanguagePack[] _chosen = [];

    /// <summary>Creates the pack folder with English downloaded and the client for the in-memory server.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-packs-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_folder);
        var english = OcrLanguageCatalog.Find("eng")!;
        using (var file = File.Create(Path.Combine(_folder, english.FileName)))
        {
            file.SetLength(english.Bytes);
        }

        _chosen = [english, OcrLanguageCatalog.Find("deu")!];
        var content = new byte[PackSize];
        RandomNumberGenerator.Fill(content);
        _handler.Content = content;
        _pack = new("bench", "Benchmark", content.Length, Convert.ToHexStringLower(SHA256.HashData(content)));
        _api = RefitClients.CreateRemoteDocumentApi(new(_handler, false) { BaseAddress = new("https://raw.githubusercontent.com") });
    }

    /// <summary>Deletes the pack folder.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        Directory.Delete(_folder, true);
        _handler.Dispose();
    }

    /// <summary>Removes the pack downloaded by the last iteration, so each download starts from nothing.</summary>
    [IterationSetup(Target = nameof(DownloadPack))]
    public void RemovePack() => OcrLanguagePackDownloader.Remove(_pack, _folder);

    /// <summary>Checks a two-language setting the way Recognise Text does before starting.</summary>
    /// <returns>The packs still to download.</returns>
    [Benchmark]
    public int CheckLanguages()
    {
        _ = OcrLanguageCatalog.Parse(Languages);
        return TesseractEngine.FindDataDirectory(Languages, _folder) is null ? OcrLanguagePackDownloader.Missing(_chosen, _folder).Count : 0;
    }

    /// <summary>Downloads, hashes and moves a one megabyte pack into place.</summary>
    /// <returns>A task.</returns>
    [Benchmark]
    public Task DownloadPack() => OcrLanguagePackDownloader.DownloadAsync(_api, [_pack], _folder, NoProgress.Instance, CancellationToken.None);

    /// <summary>Discards progress.</summary>
    private sealed class NoProgress : IProgress<double>
    {
        /// <summary>Gets the shared instance.</summary>
        public static NoProgress Instance { get; } = new();

        /// <inheritdoc/>
        public void Report(double value)
        {
            // Progress is not part of the measurement.
        }
    }

    /// <summary>Answers every request with the pack's bytes.</summary>
    private sealed class PackHandler : HttpMessageHandler
    {
        /// <summary>Gets or sets the pack's bytes.</summary>
        public byte[] Content { get; set; } = [];

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Content) });
    }
}
