// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Encodes bytes with one filter.</summary>
/// <param name="input">The bytes.</param>
/// <param name="output">Receives the encoded bytes.</param>
internal delegate void FilterEncode(ReadOnlySpan<byte> input, ref PooledBuffer output);
