// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using HyperPdf.Core.Speech;

namespace HyperPdf.Http.Speech;

/// <summary>
/// Reads aloud with Azure AI Speech's neural voices, using the person's own key and region, through Refit. Nothing is
/// sent until the person turns it on and enters their key; the text of each sentence is sent to their Azure resource.
/// </summary>
[DebuggerDisplay("AzureSpeechEngine: {Name}")]
public sealed class AzureSpeechEngine : ISpeechEngine
{
    /// <summary>The sample rate of the requested output format.</summary>
    private const int Hertz = 24_000;

    /// <summary>Scales 16 bit samples to the range -1 to 1.</summary>
    private const float SampleScale = 1F / 32_768F;

    /// <summary>The longest region name accepted.</summary>
    private const int MaxRegionLength = 40;

    /// <summary>The settings.</summary>
    private readonly AzureSpeechSettings _settings;

    /// <summary>The HTTP client, owned when created here.</summary>
    private readonly HttpClient _httpClient;

    /// <summary>Whether this engine created the HTTP client.</summary>
    private readonly bool _ownsClient;

    /// <summary>The Refit client, created when first used.</summary>
    private IAzureSpeechApi? _api;

    /// <summary>Initializes a new instance of the <see cref="AzureSpeechEngine"/> class.</summary>
    /// <param name="settings">The person's key and region.</param>
    public AzureSpeechEngine(AzureSpeechSettings settings)
        : this(settings, new HttpClient(), true)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AzureSpeechEngine"/> class with an HTTP client the caller owns.</summary>
    /// <param name="settings">The person's key and region.</param>
    /// <param name="httpClient">The HTTP client.</param>
    public AzureSpeechEngine(AzureSpeechSettings settings, HttpClient httpClient)
        : this(settings, httpClient, false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AzureSpeechEngine"/> class.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="ownsClient">Whether to dispose the client.</param>
    private AzureSpeechEngine(AzureSpeechSettings settings, HttpClient httpClient, bool ownsClient)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);
        _settings = settings;
        _httpClient = httpClient;
        _ownsClient = ownsClient;
    }

    /// <summary>Gets the voices offered: a few of Azure's most natural English neural voices.</summary>
    public static IReadOnlyList<SpeechVoice> AzureVoices { get; } =
    [
        new("en-US-AvaMultilingualNeural", "Ava", "American English, expressive"),
        new("en-US-AndrewMultilingualNeural", "Andrew", "American English, warm"),
        new("en-GB-SoniaNeural", "Sonia", "British English, clear"),
        new("en-GB-RyanNeural", "Ryan", "British English, relaxed"),
        new("en-AU-NatashaNeural", "Natasha", "Australian English, friendly"),
    ];

    /// <inheritdoc/>
    public string Name => "Azure AI Speech (your key)";

    /// <inheritdoc/>
    public bool IsReady => _settings.IsComplete;

    /// <inheritdoc/>
    public IReadOnlyList<SpeechVoice> Voices => AzureVoices;

    /// <summary>Determines whether a region name is safe to put in a host name.</summary>
    /// <param name="region">The region.</param>
    /// <returns><see langword="true"/> for lower case letters and digits only.</returns>
    public static bool IsValidRegion(string region)
    {
        if (string.IsNullOrEmpty(region) || region.Length > MaxRegionLength)
        {
            return false;
        }

        foreach (var c in region)
        {
            if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets a region's text to speech endpoint.</summary>
    /// <param name="region">The region.</param>
    /// <returns>The endpoint.</returns>
    /// <exception cref="ArgumentException">Thrown when the region is not a valid name.</exception>
    public static Uri EndpointFor(string region) =>
        IsValidRegion(region) ? new($"https://{region}.tts.speech.microsoft.com") : throw new ArgumentException("The region is not valid.", nameof(region));

    /// <summary>Converts 16 bit little endian PCM into samples.</summary>
    /// <param name="pcm">The PCM bytes.</param>
    /// <returns>The samples.</returns>
    public static float[] ToSamples(ReadOnlySpan<byte> pcm)
    {
        var samples = GC.AllocateUninitializedArray<float>(pcm.Length / sizeof(short));
        ConvertSamples(pcm, samples);
        return samples;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Thrown when no key or region is set.</exception>
    public async Task<SpeechAudio> SynthesizeAsync(string text, string voiceId, float speed, CancellationToken cancellationToken)
    {
        if (!IsReady)
        {
            throw new InvalidOperationException("Enter an Azure Speech key and region in Preferences first.");
        }

        _api ??= RefitClients.CreateAzureSpeechApi(_httpClient, _settings.Region);
        ArgumentNullException.ThrowIfNull(text);
        using var ssml = SsmlDocument.Build(text, voiceId, speed);
        using var content = new ReadOnlyMemoryContent(ssml.Bytes);
        content.Headers.ContentType = new("application/ssml+xml");
        using var response = await _api.SynthesizeAsync(_settings.Key, content, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        var pcm = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return new(ToSamples(pcm), Hertz);
    }

    /// <summary>Converts 16 bit little endian PCM into samples, a vector of them at a time where the machine allows.</summary>
    /// <param name="pcm">The PCM bytes; a trailing odd byte is ignored.</param>
    /// <param name="samples">Receives one sample for each pair of bytes.</param>
    internal static void ConvertSamples(ReadOnlySpan<byte> pcm, Span<float> samples)
    {
        var done = 0;
        if (BitConverter.IsLittleEndian && Vector.IsHardwareAccelerated)
        {
            var shorts = MemoryMarshal.Cast<byte, short>(pcm[..(samples.Length * sizeof(short))]);
            var width = Vector<short>.Count;
            for (; done <= samples.Length - width; done += width)
            {
                Vector.Widen(new Vector<short>(shorts[done..]), out var low, out var high);
                (Vector.ConvertToSingle(low) * SampleScale).CopyTo(samples[done..]);
                (Vector.ConvertToSingle(high) * SampleScale).CopyTo(samples[(done + Vector<float>.Count)..]);
            }
        }

        for (; done < samples.Length; done++)
        {
            samples[done] = BinaryPrimitives.ReadInt16LittleEndian(pcm[(done * sizeof(short))..]) * SampleScale;
        }
    }
}
