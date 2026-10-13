// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>Fills a span with random bytes: salts, file keys and seeds. Production code passes the system generator; tests pass a fixed sequence.</summary>
/// <param name="destination">The span to fill.</param>
internal delegate void FillBytes(Span<byte> destination);
