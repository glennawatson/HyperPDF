// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Speech;

namespace HyperPdf.Vision;

/// <summary>
/// The GitHub release of this repository that holds the picture description model files, beside the voice release. The files
/// are downloaded from here and nowhere else, and each is checked against the SHA-256 pinned below. They are Mozilla's distilvit
/// files (Apache-2.0) at one pinned revision of the upstream model page, copied without change.
/// </summary>
public static class VisionRelease
{
    /// <summary>The release's tag.</summary>
    private const string ReleaseTag = "vision-1";

    /// <summary>Where the release's files are downloaded from.</summary>
    private const string Address = $"https://github.com/glennawatson/HyperPDF/releases/download/{ReleaseTag}/";

    /// <summary>The name of the encoder file in the release.</summary>
    private const string EncoderAsset = "distilvit-encoder.onnx";

    /// <summary>The name of the decoder file in the release.</summary>
    private const string DecoderAsset = "distilvit-decoder.onnx";

    /// <summary>The name of the vocabulary file in the release.</summary>
    private const string VocabularyAsset = "distilvit-vocab.json";

    /// <summary>The name of the generation settings file in the release.</summary>
    private const string GenerationAsset = "distilvit-generation.json";

    /// <summary>The size of the encoder file in bytes.</summary>
    private const long EncoderBytes = 87_038_173;

    /// <summary>The size of the decoder file in bytes.</summary>
    private const long DecoderBytes = 98_065_763;

    /// <summary>The size of the vocabulary file in bytes.</summary>
    private const long VocabularyBytes = 798_156;

    /// <summary>The size of the generation settings file in bytes.</summary>
    private const long GenerationBytes = 7_022;

    /// <summary>Gets the release's tag.</summary>
    public static string Tag => ReleaseTag;

    /// <summary>Gets the files to download, with the names they have inside the model folder.</summary>
    public static IReadOnlyList<SpeechModelFile> Files { get; } =
    [
        new(new(Address + EncoderAsset), DistilVitModel.EncoderFile, EncoderBytes, "04622bdece4419687c8a07f4df0e8c1bea1db354fb6decde97bc0bf39f26bab2"),
        new(new(Address + DecoderAsset), DistilVitModel.DecoderFile, DecoderBytes, "8cda4a51db36003185beb5557cc379d0ad773d4efa9329e39793828b74d420f4"),
        new(new(Address + VocabularyAsset), DistilVitModel.VocabularyFile, VocabularyBytes, "3ba3c3109ff33976c4bd966589c11ee14fcaa1f4c9e5e154c2ed7f99d80709e7"),
        new(new(Address + GenerationAsset), DistilVitModel.GenerationFile, GenerationBytes, "4d34fb2d7ef7bb123f916c1f252971c2e077959235fd0c3676e21b5e3db4dfe7"),
    ];
}
