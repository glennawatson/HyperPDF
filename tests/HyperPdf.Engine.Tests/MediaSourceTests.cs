// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Tests listing and writing out the sound and video a document carries.</summary>
public sealed class MediaSourceTests
{
    /// <summary>The bytes before the samples of a WAVE file.</summary>
    private const int WaveHeader = 44;

    /// <summary>The media in the test document: a sound, a screen clip and a rich media asset.</summary>
    private const int MediaCount = 3;

    /// <summary>The bytes of the sound's samples.</summary>
    private const int SampleCount = 4;

    /// <summary>The sampling rate of the test sound.</summary>
    private const int SoundRate = 8000;

    /// <summary>Where the sampling rate is in a WAVE file.</summary>
    private const int RateOffset = 24;

    /// <summary>Where the format tag is in a WAVE file.</summary>
    private const int FormatTagOffset = 8;

    /// <summary>The length of the RIFF tag, and of the sample text.</summary>
    private const int TagLength = 4;

    /// <summary>The length of the WAVE and format tags together.</summary>
    private const int FormatTagLength = 8;

    /// <summary>The sound, the screen clip and the rich media asset are listed in annotation order with plain labels and file names.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsSoundScreenAndRichMedia()
    {
        using var open = await OpenAsync();
        var media = DocumentFeature<IDocumentMediaSource>.Cast(open.Document)!.GetMedia();

        await Assert.That(media.Count).IsEqualTo(MediaCount);
        await Assert.That(media[0].Kind).IsEqualTo(DocumentMediaKind.Sound);
        await Assert.That(media[0].Name).IsEqualTo("Sound on page 1");
        await Assert.That(media[0].FileName).IsEqualTo("sound-1.wav");
        await Assert.That(media[0].ContentType).IsEqualTo("audio/wav");
        await Assert.That(media[0].Size).IsEqualTo(SampleCount);
        await Assert.That(media[1].Kind).IsEqualTo(DocumentMediaKind.Screen);
        await Assert.That(media[1].FileName).IsEqualTo("intro.mp4");
        await Assert.That(media[1].ContentType).IsEqualTo("video/mp4");
        await Assert.That(media[1].Name).IsEqualTo("intro.mp4 (video on page 1)");
        await Assert.That(media[2].Kind).IsEqualTo(DocumentMediaKind.RichMedia);
        await Assert.That(media[2].FileName).IsEqualTo("talk.webm");
        await Assert.That(media[2].ContentType).IsEqualTo("video/webm");
    }

    /// <summary>Saved clips are the embedded bytes, and a sound is written as a WAVE file with its rate and samples.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesTheMediaAsFiles()
    {
        using var open = await OpenAsync();
        var source = DocumentFeature<IDocumentMediaSource>.Cast(open.Document)!;

        var wave = Save(source, 0)!;

        await Assert.That(Text(wave, 0, TagLength)).IsEqualTo("RIFF");
        await Assert.That(Text(wave, FormatTagOffset, FormatTagLength)).IsEqualTo("WAVEfmt ");
        await Assert.That(BitConverter.ToInt32(wave, RateOffset)).IsEqualTo(SoundRate);
        await Assert.That(wave.Length).IsEqualTo(WaveHeader + SampleCount);
        await Assert.That(Text(wave, WaveHeader, SampleCount)).IsEqualTo("ABCD");
        await Assert.That(Encoding.ASCII.GetString(Save(source, 1)!)).IsEqualTo("MP4DATA!");
        await Assert.That(Encoding.ASCII.GetString(Save(source, MediaCount - 1)!)).IsEqualTo("WEBMDAT!");
        await Assert.That(Save(source, MediaCount)).IsNull();
    }

    /// <summary>A document with no media lists none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsNothingForPlainDocuments()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-plain-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.Create(1));
        try
        {
            using var document = new HyperPdfEngine().Open(path, null);

            await Assert.That(DocumentFeature<IDocumentMediaSource>.Cast(document)!.GetMedia()).IsEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Opens the media document from a temporary file.</summary>
    /// <returns>The open document, which removes its file when disposed.</returns>
    private static async Task<OpenMedia> OpenAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-media-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateMediaDocument());
        return new(new HyperPdfEngine().Open(path, null), path);
    }

    /// <summary>Saves a piece of media into memory.</summary>
    /// <param name="source">The media source.</param>
    /// <param name="index">The media's index.</param>
    /// <returns>The file's bytes, or <see langword="null"/> when it could not be written.</returns>
    private static byte[]? Save(IDocumentMediaSource source, int index)
    {
        using var stream = new MemoryStream();
        return source.SaveMedia(index, stream) ? stream.ToArray() : null;
    }

    /// <summary>Reads ASCII text from bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="start">The first byte.</param>
    /// <param name="length">The count of bytes.</param>
    /// <returns>The text.</returns>
    private static string Text(byte[] bytes, int start, int length) => Encoding.ASCII.GetString(bytes, start, length);

    /// <summary>A document opened for a test, with the file to remove afterwards.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Path">The file.</param>
    private sealed record OpenMedia(IDocument Document, string Path) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose()
        {
            Document.Dispose();
            File.Delete(Path);
        }
    }
}
