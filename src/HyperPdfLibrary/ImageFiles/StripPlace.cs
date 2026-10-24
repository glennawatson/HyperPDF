// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Where the rows of a TIFF strip belong in the picture.</summary>
/// <param name="FirstRow">The picture row of the strip's first row.</param>
/// <param name="Rows">The rows in the strip.</param>
/// <param name="Plane">The plane the strip belongs to; zero unless the samples are stored in separate planes.</param>
[DebuggerDisplay("StripPlace: rows {FirstRow}+{Rows}, plane {Plane}")]
internal readonly record struct StripPlace(int FirstRow, int Rows, int Plane);
