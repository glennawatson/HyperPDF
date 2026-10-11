// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Filters;

/// <summary>Where a filter chain's data came from: the open context for cancellation and diagnostics, and the stream's object number.</summary>
/// <param name="Context">The open context, or <see langword="null"/>.</param>
/// <param name="ObjectNumber">The stream's object number, or 0.</param>
/// <param name="Decoders">The stream owner's idle Flate decoders, or null for an independent decode.</param>
/// <param name="CancellationToken">Cancels this decode operation.</param>
internal readonly record struct DecodeSource(PdfOpenContext? Context, int ObjectNumber, FlateDecoderState? Decoders, CancellationToken CancellationToken);
