// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>What qpdf made of an encoded stream.</summary>
/// <param name="Data">The decoded bytes qpdf wrote.</param>
/// <param name="Errors">What qpdf wrote to its error output, which names a problem it found.</param>
internal sealed record QpdfDecodeResult(byte[] Data, string Errors);
