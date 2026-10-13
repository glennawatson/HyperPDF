// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Interchange;

/// <summary>How the objects of an FDF file after its catalog are numbered.</summary>
/// <param name="Differences">The number of the differences stream, or 0 when there is none.</param>
/// <param name="FirstAnnotation">The number of the first annotation; the others follow in order.</param>
/// <param name="FirstExtra">The number of the first embedded file or pop-up; the others follow in the order the annotations bring them.</param>
[DebuggerDisplay("FdfNumbers: differences {Differences} annotations from {FirstAnnotation} extras from {FirstExtra}")]
internal readonly record struct FdfNumbers(int Differences, int FirstAnnotation, int FirstExtra);
