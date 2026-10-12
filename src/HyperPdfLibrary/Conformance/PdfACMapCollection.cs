// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The character collection a predefined CMap uses: always registered by Adobe.</summary>
/// <param name="Ordering">The ordering, such as Japan1.</param>
/// <param name="Supplement">The supplement.</param>
internal readonly record struct PdfACMapCollection(string Ordering, int Supplement);
