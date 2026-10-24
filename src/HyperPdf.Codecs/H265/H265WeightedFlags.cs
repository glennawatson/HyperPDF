// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Whether P slices and B slices use weighted prediction.</summary>
/// <param name="Pred">Whether P slices do.</param>
/// <param name="Bipred">Whether B slices do.</param>
internal readonly record struct H265WeightedFlags(bool Pred, bool Bipred);
