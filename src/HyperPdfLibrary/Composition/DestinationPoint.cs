// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A named place in the document.</summary>
/// <param name="Page">The number of the page, counted from 1.</param>
/// <param name="X">The x of the place on the page.</param>
/// <param name="Y">The y of the place on the page.</param>
[DebuggerDisplay("DestinationPoint: page {Page}, ({X}, {Y})")]
internal readonly record struct DestinationPoint(int Page, float X, float Y);
