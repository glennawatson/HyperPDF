// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Barcodes;

/// <summary>A module's place in a barcode symbol.</summary>
/// <param name="X">The column from the left.</param>
/// <param name="Y">The row from the top.</param>
internal readonly record struct ModulePosition(int X, int Y);
