// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Speech;

namespace HyperPdf.Vision;

/// <summary>
/// The files of Mozilla's distilvit image captioning model (Apache-2.0), the model Firefox uses to describe pictures: an
/// 8 bit ViT encoder, an 8 bit distilled GPT-2 decoder and the vocabulary and generation settings that go with them. They
/// are fetched from this repository's own release (<see cref="VisionRelease"/>) and checked against their SHA-256. About 186 MB.
/// </summary>
public static class DistilVitModel
{
    /// <summary>Gets the encoder's file name inside the model folder.</summary>
    public static string EncoderFile => "encoder.onnx";

    /// <summary>Gets the decoder's file name inside the model folder.</summary>
    public static string DecoderFile => "decoder.onnx";

    /// <summary>Gets the vocabulary's file name inside the model folder.</summary>
    public static string VocabularyFile => "vocab.json";

    /// <summary>Gets the generation settings' file name inside the model folder.</summary>
    public static string GenerationFile => "generation_config.json";

    /// <summary>Gets the files to download.</summary>
    public static IReadOnlyList<SpeechModelFile> Files => VisionRelease.Files;
}
