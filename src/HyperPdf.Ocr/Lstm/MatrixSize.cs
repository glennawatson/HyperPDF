// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Ocr.Lstm;

/// <summary>The number of rows and columns of a stored matrix.</summary>
/// <param name="Rows">The number of rows.</param>
/// <param name="Columns">The number of columns.</param>
internal readonly record struct MatrixSize(int Rows, int Columns);
