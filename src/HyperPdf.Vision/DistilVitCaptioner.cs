// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using HyperPdf.Core.Optimizing;
using Microsoft.ML.OnnxRuntime;

namespace HyperPdf.Vision;

/// <summary>
/// Describes pictures with Mozilla's distilvit model: a ViT encoder reads the picture, and a distilled GPT-2 decoder writes the
/// caption one token at a time, each time choosing the most likely token that is not on the model's list of words to avoid.
/// The sessions load on first use and are freed when the captioner is disposed. One picture is described at a time.
/// </summary>
[DebuggerDisplay("DistilVitCaptioner: {_directory}")]
public sealed class DistilVitCaptioner : IFigureDescriber, IDisposable
{
    /// <summary>The id of the token that starts a caption.</summary>
    private const long StartToken = 50_256;

    /// <summary>The most threads the model uses, so the rest of the app stays responsive.</summary>
    private const int MaximumThreads = 4;

    /// <summary>The processors left to the rest of the app.</summary>
    private const int ReservedProcessors = 2;

    /// <summary>The model folder.</summary>
    private readonly string _directory;

    /// <summary>Held while a picture is described or the sessions are freed.</summary>
    private readonly Lock _gate = new();

    /// <summary>The loaded model, or <see langword="null"/> before the first use.</summary>
    private LoadedModel? _model;

    /// <summary>Whether the captioner has been disposed.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="DistilVitCaptioner"/> class.</summary>
    /// <param name="directory">The folder holding the model files.</param>
    public DistilVitCaptioner(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        _directory = directory;
    }

    /// <summary>Gets a value indicating whether the model is loaded, which tests use to check that disposal frees it.</summary>
    internal bool IsLoaded
    {
        get
        {
            lock (_gate)
            {
                return _model is not null;
            }
        }
    }

    /// <inheritdoc/>
    public string? Describe(ReadOnlySpan<byte> bgra, int width, int height, CancellationToken cancellationToken)
    {
        var input = ArrayPool<float>.Shared.Rent(ImagePreprocessor.InputLength);
        try
        {
            ImagePreprocessor.Prepare(bgra, width, height, input);
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return Caption(_model ??= Load(_directory), input, cancellationToken);
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(input);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _model?.Dispose();
            _model = null;
        }
    }

    /// <summary>Loads the sessions, the vocabulary and the generation settings.</summary>
    /// <param name="directory">The model folder.</param>
    /// <returns>The model.</returns>
    private static LoadedModel Load(string directory)
    {
        var vocabulary = CaptionVocabulary.Parse(File.ReadAllBytes(Path.Combine(directory, DistilVitModel.VocabularyFile)));
        var settings = GenerationSettings.Parse(File.ReadAllBytes(Path.Combine(directory, DistilVitModel.GenerationFile)), vocabulary.Count);
        using var options = new SessionOptions { IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount - ReservedProcessors, 1, MaximumThreads) };
        var encoder = new InferenceSession(Path.Combine(directory, DistilVitModel.EncoderFile), options);
        try
        {
            return new(encoder, new(Path.Combine(directory, DistilVitModel.DecoderFile), options), vocabulary, settings);
        }
        catch
        {
            encoder.Dispose();
            throw;
        }
    }

    /// <summary>Describes a prepared picture.</summary>
    /// <param name="model">The loaded model.</param>
    /// <param name="input">The prepared picture.</param>
    /// <param name="cancellationToken">Cancels between tokens.</param>
    /// <returns>The caption, or <see langword="null"/> when the model wrote nothing.</returns>
    private static string? Caption(LoadedModel model, float[] input, CancellationToken cancellationToken)
    {
        using var run = new RunOptions();
        using var pixels = OrtValue.CreateTensorValueFromMemory(input, [1, ImagePreprocessor.Channels, ImagePreprocessor.Side, ImagePreprocessor.Side]);
        using var encoded = model.Encoder.Run(run, [model.Encoder.InputNames[0]], [pixels], [model.Encoder.OutputNames[0]]);
        List<long> ids = [StartToken];
        while (ids.Count < model.Settings.MaximumLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var next = ChooseNext(model, run, encoded[0], ids);
            if (next == model.Settings.EndToken)
            {
                break;
            }

            ids.Add(next);
        }

        var text = model.Vocabulary.Decode(CollectionsMarshal.AsSpan(ids)[1..]).Trim();
        return text.Length == 0 ? null : string.Concat(text[..1].ToUpper(CultureInfo.InvariantCulture), text.AsSpan(1));
    }

    /// <summary>Runs the decoder over the tokens so far and picks the most likely next token.</summary>
    /// <param name="model">The loaded model.</param>
    /// <param name="run">The run options.</param>
    /// <param name="encoded">The encoder's output.</param>
    /// <param name="ids">The tokens so far, starting with the start token.</param>
    /// <returns>The token.</returns>
    private static long ChooseNext(LoadedModel model, RunOptions run, OrtValue encoded, List<long> ids)
    {
        var tokens = ids.ToArray();
        using var idsValue = OrtValue.CreateTensorValueFromMemory(tokens, [1, tokens.Length]);
        using var outputs = model.Decoder.Run(run, ["input_ids", "encoder_hidden_states"], [idsValue, encoded], ["logits"]);
        var logits = outputs[0].GetTensorDataAsSpan<float>();
        return model.Settings.Pick(logits[((tokens.Length - 1) * (logits.Length / tokens.Length))..], tokens);
    }

    /// <summary>The loaded sessions and settings.</summary>
    /// <param name="Encoder">The picture encoder.</param>
    /// <param name="Decoder">The caption decoder.</param>
    /// <param name="Vocabulary">The token texts.</param>
    /// <param name="Settings">How captions are generated.</param>
    private sealed record LoadedModel(InferenceSession Encoder, InferenceSession Decoder, CaptionVocabulary Vocabulary, GenerationSettings Settings) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose()
        {
            Encoder.Dispose();
            Decoder.Dispose();
        }
    }
}
