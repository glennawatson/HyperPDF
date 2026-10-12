// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>A range of CIDs sharing one width in a /W array.</summary>
/// <param name="First">The first CID.</param>
/// <param name="Last">The last CID.</param>
/// <param name="Width">The width.</param>
internal readonly record struct PdfACidWidthRange(int First, int Last, double Width);
