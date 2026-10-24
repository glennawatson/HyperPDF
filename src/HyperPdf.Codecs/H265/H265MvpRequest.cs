// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>What a motion vector predictor is wanted for.</summary>
/// <param name="List">The reference list, 0 or 1.</param>
/// <param name="RefIdx">The reference index in that list.</param>
/// <param name="MvpFlag">Which of the two predictors to pick.</param>
internal readonly record struct H265MvpRequest(int List, int RefIdx, int MvpFlag);
