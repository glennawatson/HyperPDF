// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using HyperPdf.Http.Speech;

namespace HyperPdf.Core.Tests.Speech;

/// <summary>Tests for <see cref="AzureSpeechEngine"/> against a recording HTTP handler.</summary>
public sealed class AzureSpeechEngineTests
{
    /// <summary>A loud positive sample, half of full scale.</summary>
    private const short HalfScale = 16_384;

    /// <summary>The expected value of <see cref="HalfScale"/>.</summary>
    private const float Half = 0.5F;

    /// <summary>A brisk speed.</summary>
    private const float Brisk = 1.25F;

    /// <summary>Kokoro and Azure output sample rate.</summary>
    private const int Hertz = 24_000;

    /// <summary>The samples in the recorded reply.</summary>
    private const int ReplySamples = 2;

    /// <summary>Replies with two samples: silence, then half scale.</summary>
    private static readonly RecordingHandler PcmHandler = new([0, 0, 0, 0x40]);

    /// <summary>A client over <see cref="PcmHandler"/>.</summary>
    private static readonly HttpClient PcmClient = new(PcmHandler);

    /// <summary>Records that nothing was sent.</summary>
    private static readonly RecordingHandler UnusedHandler = new([]);

    /// <summary>A client over <see cref="UnusedHandler"/>.</summary>
    private static readonly HttpClient UnusedClient = new(UnusedHandler);

    /// <summary>The engine sends SSML with the key and output format, and turns the PCM reply into samples.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SendsSsmlAndReadsPcm()
    {
        var handler = PcmHandler;
        using var engine = new AzureSpeechEngine(new("secret", "uksouth"), PcmClient);

        var audio = await engine.SynthesizeAsync("Fish & chips", "en-GB-SoniaNeural", Brisk, CancellationToken.None);

        await Assert.That(handler.Uri!.ToString()).IsEqualTo("https://uksouth.tts.speech.microsoft.com/cognitiveservices/v1");
        await Assert.That(handler.Key).IsEqualTo("secret");
        await Assert.That(handler.Format).IsEqualTo("raw-24khz-16bit-mono-pcm");
        await Assert.That(handler.ContentType).IsEqualTo("application/ssml+xml");
        await Assert.That(handler.Body!).Contains("Fish &amp; chips");
        await Assert.That(handler.Body!).Contains("rate=\"+25%\"");
        await Assert.That(handler.Body!).Contains("xml:lang=\"en-GB\"");
        await Assert.That(audio.SampleRate).IsEqualTo(Hertz);
        await Assert.That(audio.Samples.Length).IsEqualTo(ReplySamples);
        await Assert.That(audio.Samples[1]).IsEqualTo(Half);
    }

    /// <summary>Without a key the engine is not ready and sends nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IsNotReadyWithoutKey()
    {
        var handler = UnusedHandler;
        using var engine = new AzureSpeechEngine(new(string.Empty, "uksouth"), UnusedClient);

        await Assert.That(engine.IsReady).IsFalse();
        await Assert.That(async () => _ = await engine.SynthesizeAsync("Hello", "en-GB-SoniaNeural", 1, CancellationToken.None)).Throws<InvalidOperationException>();
        await Assert.That(handler.Uri).IsNull();
    }

    /// <summary>Regions are only accepted when they are plain names, so they cannot change the host.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsUnsafeRegions()
    {
        await Assert.That(AzureSpeechEngine.IsValidRegion("westeurope")).IsTrue();
        await Assert.That(AzureSpeechEngine.IsValidRegion("evil.com/x")).IsFalse();
        await Assert.That(AzureSpeechEngine.IsValidRegion(string.Empty)).IsFalse();
        await Assert.That(new AzureSpeechSettings("key", "bad region").ToString()).DoesNotContain("key");
    }

    /// <summary>16 bit PCM is scaled into the range -1 to 1.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConvertsPcm()
    {
        var bytes = new byte[sizeof(short)];
        System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(bytes, HalfScale);

        await Assert.That(AzureSpeechEngine.ToSamples(bytes)[0]).IsEqualTo(Half);
    }

    /// <summary>Records the request and replies with fixed PCM.</summary>
    /// <param name="reply">The reply body.</param>
    private sealed class RecordingHandler(byte[] reply) : HttpMessageHandler
    {
        /// <summary>Gets the requested URI.</summary>
        public Uri? Uri { get; private set; }

        /// <summary>Gets the key header.</summary>
        public string? Key { get; private set; }

        /// <summary>Gets the output format header.</summary>
        public string? Format { get; private set; }

        /// <summary>Gets the content type.</summary>
        public string? ContentType { get; private set; }

        /// <summary>Gets the body.</summary>
        public string? Body { get; private set; }

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            Key = request.Headers.GetValues("Ocp-Apim-Subscription-Key").Single();
            Format = request.Headers.GetValues("X-Microsoft-OutputFormat").Single();
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(reply) };
        }
    }
}
