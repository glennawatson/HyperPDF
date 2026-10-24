// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Tests.Speech.Melo;

/// <summary>What MeloTTS's Python front end made of the reference sentences and words.</summary>
/// <param name="Cases">The sentences.</param>
/// <param name="Predictions">The spelling-to-sound predictions.</param>
internal sealed record MeloReference(MeloReferenceCase[] Cases, MeloReferencePrediction[] Predictions);
