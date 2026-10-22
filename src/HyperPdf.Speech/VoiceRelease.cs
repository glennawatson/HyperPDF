// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Speech;

namespace HyperPdf.Speech;

/// <summary>
/// The GitHub release of this repository that holds every on-device voice file, built by the Voice models workflow
/// (<c>.github/workflows/voice-models.yml</c>). Voices are downloaded from here and nowhere else, and each file is
/// checked against the SHA-256 pinned below, copied from the release's <c>voices.json</c>.
/// </summary>
public static class VoiceRelease
{
    /// <summary>The release's tag; the workflow's <c>RELEASE_TAG</c>.</summary>
    private const string ReleaseTag = "voices-2";

    /// <summary>Where the release's files are downloaded from.</summary>
    private const string Address = $"https://github.com/glennawatson/HyperPDF/releases/download/{ReleaseTag}/";

    /// <summary>Each file's size and SHA-256, by its name in the release.</summary>
    private static readonly Dictionary<string, AssetInfo> Assets = new(StringComparer.Ordinal)
    {
        ["bert-en-vocab.txt"] = new(231_508, "07eced375cec144d27c900241f3e339478dec958f92fddbc551f295c992038a3"),
        ["bert-en.onnx"] = new(95_747_794, "eb44339cd9f71471955e906aff4145a976fa22e51b0e23668113d48cb2087184"),
        ["g2p-en.bin"] = new(3_339_684, "9c0eff98ae7f6c69fde21dc4bf4849c0f28240527524ef2e87400e2a229c965d"),
        ["kokoro-af_bella.bin"] = new(522_240, "f69d836209b78eb8c66e75e3cda491e26ea838a3674257e9d4e5703cbaf55c8b"),
        ["kokoro-af_heart.bin"] = new(522_240, "d583ccff3cdca2f7fae535cb998ac07e9fcb90f09737b9a41fa2734ec44a8f0b"),
        ["kokoro-am_michael.bin"] = new(522_240, "1d1f21dd8da39c30705cd4c75d039d265e9bc4a2a93ed09bc9e1b1225eb95ba1"),
        ["kokoro-bf_emma.bin"] = new(522_240, "669fe0647f9dd04fcab92f1439a40eeb4c8b4ab1f82e4996fe3d918ce4a63b73"),
        ["kokoro-bm_george.bin"] = new(522_240, "c4b235a4c1f2cd3b939fed08b899ce9385638b763f7b73a59616c4fc9bd6c9bc"),
        ["kokoro-model_uint8.onnx"] = new(177_464_632, "6607a397d77b8514065420b7c1e7320117f7aabfdb45ce15f0050c5b0fe75aea"),
        ["melo-en-lexicon.txt"] = new(3_577_488, "e0f56092fca1721f9556ac7e9cab66cff2d4ec7b5afe5618036747c99513a823"),
        ["melo-en.json"] = new(1_449, "d93a30aceedcd4854c456baf1c57457c05c9e530161d54e591ef62a5fe9bd126"),
        ["melo-en.onnx"] = new(170_495_455, "8e0246aa38588fd143af6562ebf2d2d9d92bbfc989b25770f61081ee182f66f5"),
        ["misaki-gb_gold.json"] = new(2_838_552, "29e62f4b60261c88f7f3c2c7811ca3825978948090b72d2b27d565b729282f71"),
        ["misaki-gb_silver.json"] = new(3_663_898, "48131e2d92ccc41655f4543e87e0f938e71463eb5a54be7f0693bb712ebb6bce"),
        ["misaki-us_gold.json"] = new(3_000_469, "dc414872a49a28ae6c141463d502fd945f3b2fde040484fdc47d00cc4612686f"),
        ["misaki-us_silver.json"] = new(3_099_517, "de8f67be911bb6c659187b4a65fd966b6a30e56350e0f790d763210b053ac475"),
    };

    /// <summary>Gets the release's tag.</summary>
    public static string Tag => ReleaseTag;

    /// <summary>Describes a file of the release.</summary>
    /// <param name="asset">Its name in the release.</param>
    /// <param name="localName">Its path inside the voice folder.</param>
    /// <returns>The file.</returns>
    /// <exception cref="ArgumentException">Thrown when the release has no such file.</exception>
    public static SpeechModelFile File(string asset, string localName)
    {
        ArgumentException.ThrowIfNullOrEmpty(asset);
        if (!Assets.TryGetValue(asset, out var known))
        {
            throw new ArgumentException($"The voice release has no file named {asset}.", nameof(asset));
        }

        return new(new(Address + asset), localName, known.Bytes, known.Sha256);
    }

    /// <summary>A release file's size and SHA-256.</summary>
    /// <param name="Bytes">The size in bytes.</param>
    /// <param name="Sha256">The pinned SHA-256, in lower case hex.</param>
    private readonly record struct AssetInfo(long Bytes, string Sha256);
}
