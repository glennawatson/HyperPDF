// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdf.Vision;

namespace HyperPdf.Core.Tests.Vision;

/// <summary>Checks the picture description model's vocabulary, settings, preparation of pictures, file list and, when its files are present, the model itself.</summary>
public sealed class DistilVitTests
{
    /// <summary>The environment variable that names a folder holding the downloaded model files.</summary>
    private const string ModelFolderVariable = "HYPERPDF_ALTTEXT_MODEL";

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The width of the wide test picture.</summary>
    private const int WideWidth = 500;

    /// <summary>The height of the wide test picture.</summary>
    private const int WideHeight = 300;

    /// <summary>The side of the square picture drawn for the model.</summary>
    private const int Side = 256;

    /// <summary>The length of a SHA-256 in hexadecimal.</summary>
    private const int HashLength = 64;

    /// <summary>The token id the sample settings end captions with.</summary>
    private const int SampleEndToken = 9;

    /// <summary>The most tokens the sample settings allow.</summary>
    private const int SampleMaximum = 12;

    /// <summary>The number of token ids in the sample vocabulary and logits.</summary>
    private const int SampleTokens = 6;

    /// <summary>The sample token that is a word to avoid on its own.</summary>
    private const long BlockedToken = 2;

    /// <summary>The first token of the sample blocked sequence.</summary>
    private const long SequenceStart = 3;

    /// <summary>The token the sample scores rank first among those allowed.</summary>
    private const long BestAllowed = 4;

    /// <summary>The token the sample scores rank next.</summary>
    private const long NextAllowed = 5;

    /// <summary>A token id that is not in the sample vocabulary.</summary>
    private const long UnknownToken = 99;

    /// <summary>The error allowed in a prepared value.</summary>
    private const float Tolerance = 0.001F;

    /// <summary>The planes of the prepared input, for working out where the blue plane starts.</summary>
    private const int BluePlane = 2;

    /// <summary>The divisor that finds the middle of a plane.</summary>
    private const int Middle = 2;

    /// <summary>The divisor that finds a third of the scene's height.</summary>
    private const int Third = 3;

    /// <summary>The divisor that finds a sixth of the scene's side.</summary>
    private const int Sixth = 6;

    /// <summary>The sky colour: blue, green, red, alpha.</summary>
    private static readonly byte[] Sky = [255, 160, 80, 255];

    /// <summary>The ground colour: blue, green, red, alpha.</summary>
    private static readonly byte[] Ground = [40, 160, 40, 255];

    /// <summary>The disc colour: blue, green, red, alpha.</summary>
    private static readonly byte[] Disc = [0, 0, 255, 255];

    /// <summary>The red picture's colour: blue, green, red, alpha.</summary>
    private static readonly byte[] Red = [0, 0, 255, 255];

    /// <summary>The transparent colour.</summary>
    private static readonly byte[] Clear = [0, 0, 0, 0];

    /// <summary>The scores of the six sample tokens, which prefer a blocked token, then token 4, then token 5.</summary>
    private static readonly float[] SampleLogits = [0F, 0F, 10F, 0F, 9F, 1F];

    /// <summary>The sizes of the pictures prepared, the second and third not square.</summary>
    private static readonly int[][] Sizes = [[1, 1], [WideWidth, WideHeight], [WideHeight, WideWidth]];

    /// <summary>Token strings are turned back into text through the byte map, so a space and an accented letter come out right.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VocabularyDecodesByteLevelTokens()
    {
        var vocabulary = CaptionVocabulary.Parse("""{"a":0,"Ġman":1,"Ċ":2,"Ã©":3}"""u8);

        using (Assert.Multiple())
        {
            await Assert.That(vocabulary.Decode([0L, 1L])).IsEqualTo("a man");
            await Assert.That(vocabulary.Decode([BlockedToken])).IsEqualTo("\n");
            await Assert.That(vocabulary.Decode([SequenceStart])).IsEqualTo("é");
            await Assert.That(vocabulary.Decode([UnknownToken])).IsEqualTo(string.Empty);
        }
    }

    /// <summary>A vocabulary that is not an object of ids is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VocabularyRefusesOtherJson()
    {
        await Assert.That(static () => CaptionVocabulary.Parse("[1,2]"u8.ToArray())).Throws<InvalidDataException>();
        await Assert.That(static () => CaptionVocabulary.Parse("{"u8.ToArray())).Throws<InvalidDataException>();
    }

    /// <summary>The best allowed token is chosen: blocked tokens and tokens that would finish a blocked sequence are skipped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GenerationSkipsBlockedTokens()
    {
        var settings = GenerationSettings.Parse("""{"max_length":12,"eos_token_id":9,"bad_words_ids":[[2],[3,4]]}"""u8.ToArray(), SampleTokens);

        using (Assert.Multiple())
        {
            await Assert.That(settings.MaximumLength).IsEqualTo(SampleMaximum);
            await Assert.That(settings.EndToken).IsEqualTo(SampleEndToken);
            await Assert.That(settings.Pick(SampleLogits, [1L])).IsEqualTo(BestAllowed);
            await Assert.That(settings.Pick(SampleLogits, [SequenceStart])).IsEqualTo(NextAllowed);
        }
    }

    /// <summary>A solid red picture prepares as a full red plane and empty green and blue planes, at any size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SolidRedPreparesAsRedPlane()
    {
        const int plane = ImagePreprocessor.Side * ImagePreprocessor.Side;
        foreach (var size in Sizes)
        {
            var pixels = Solid(size[0], size[1], Red);
            var input = new float[ImagePreprocessor.InputLength];

            ImagePreprocessor.Prepare(pixels, size[0], size[1], input);

            using (Assert.Multiple())
            {
                await Assert.That(input[0]).IsEqualTo(1F).Within(Tolerance);
                await Assert.That(input[plane - 1]).IsEqualTo(1F).Within(Tolerance);
                await Assert.That(input[plane]).IsEqualTo(-1F).Within(Tolerance);
                await Assert.That(input[(BluePlane * plane) + (plane / Middle)]).IsEqualTo(-1F).Within(Tolerance);
            }
        }
    }

    /// <summary>A fully transparent picture is placed over white.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TransparentPreparesAsWhite()
    {
        var pixels = Solid(WideHeight, WideHeight, Clear);
        var input = new float[ImagePreprocessor.InputLength];

        ImagePreprocessor.Prepare(pixels, WideHeight, WideHeight, input);

        await Assert.That(input.Min()).IsEqualTo(1F).Within(Tolerance);
    }

    /// <summary>A picture that is too short for its size is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShortPicturesAreRefused()
    {
        var input = new float[ImagePreprocessor.InputLength];

        await Assert.That(() => ImagePreprocessor.Prepare(new byte[PixelBytes], Middle, Middle, input)).Throws<ArgumentException>();
    }

    /// <summary>The download list names checked files from this repository's own release and nowhere else.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FileListComesFromTheProjectReleaseAndIsChecked()
    {
        foreach (var file in DistilVitModel.Files)
        {
            using (Assert.Multiple())
            {
                await Assert.That(file.Source.Scheme).IsEqualTo(Uri.UriSchemeHttps);
                await Assert.That(file.Source.Host).IsEqualTo("github.com");
                await Assert.That(file.Source.AbsolutePath).Contains($"/releases/download/{VisionRelease.Tag}/");
                await Assert.That(file.Sha256.Length).IsEqualTo(HashLength);
                await Assert.That(file.Bytes).IsGreaterThan(0L);
            }
        }
    }

    /// <summary>When the model files are present they match the list, and they describe a picture the same way each time.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RealModelDescribesAPicture()
    {
        var folder = Environment.GetEnvironmentVariable(ModelFolderVariable);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            Skip.Test($"Set {ModelFolderVariable} to a folder holding the downloaded model files.");
        }

        foreach (var file in DistilVitModel.Files)
        {
            var path = Path.Combine(folder!, file.LocalName);
            await Assert.That(new FileInfo(path).Length).IsEqualTo(file.Bytes);
            await Assert.That(Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(path)))).IsEqualTo(file.Sha256);
        }

        var captioner = new DistilVitCaptioner(folder!);
        var picture = Scene();

        var first = captioner.Describe(picture, Side, Side, CancellationToken.None);
        var second = captioner.Describe(picture, Side, Side, CancellationToken.None);
        var loaded = captioner.IsLoaded;
        captioner.Dispose();

        using (Assert.Multiple())
        {
            await Assert.That(first).IsNotNull();
            await Assert.That(first!.Length).IsGreaterThan(0);
            await Assert.That(second).IsEqualTo(first);
            await Assert.That(loaded).IsTrue();
            await Assert.That(captioner.IsLoaded).IsFalse();
            await Assert.That(() => captioner.Describe(picture, Side, Side, CancellationToken.None)).Throws<ObjectDisposedException>();
        }
    }

    /// <summary>Makes a picture of one colour.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="pixel">The blue, green, red and alpha of every pixel.</param>
    /// <returns>The pixels.</returns>
    private static byte[] Solid(int width, int height, byte[] pixel)
    {
        var pixels = new byte[width * height * PixelBytes];
        for (var offset = 0; offset < pixels.Length; offset += PixelBytes)
        {
            pixel.CopyTo(pixels, offset);
        }

        return pixels;
    }

    /// <summary>Picks the colour of a point of the scene: a disc in the upper middle, sky above the middle and ground below it.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The colour.</returns>
    private static byte[] ColourAt(int x, int y)
    {
        var dx = x - (Side / Middle);
        var dy = y - (Side / Third);
        if ((dx * dx) + (dy * dy) < (Side / Sixth * (Side / Sixth)))
        {
            return Disc;
        }

        return y < Side / Middle ? Sky : Ground;
    }

    /// <summary>Makes a simple scene: blue sky over green ground with a red disc.</summary>
    /// <returns>The pixels.</returns>
    private static byte[] Scene()
    {
        var pixels = new byte[Side * Side * PixelBytes];
        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                ColourAt(x, y).CopyTo(pixels, ((y * Side) + x) * PixelBytes);
            }
        }

        return pixels;
    }
}
