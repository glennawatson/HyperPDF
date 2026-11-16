// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Services;

/// <summary>A value of a signal in a <see cref="LazySequence"/>, or the mark that the sequence has ended.</summary>
/// <typeparam name="T">The type of value.</typeparam>
/// <param name="Value">The value.</param>
/// <param name="More">Whether this is a value; <see langword="false"/> marks the end of the sequence.</param>
internal readonly record struct SequenceStep<T>(T Value, bool More);
