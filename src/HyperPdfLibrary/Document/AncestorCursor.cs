// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>An ancestor whose remaining kids are visited in document order.</summary>
/// <param name="Kids">The ancestor's kids.</param>
/// <param name="NextChildIndex">The next kid's index.</param>
/// <param name="ChildCount">The number of kids when the ancestor was entered.</param>
/// <param name="Inherited">The attributes its kids inherit.</param>
/// <param name="InheritIndex">The position of those attributes in the page set's table.</param>
/// <param name="Depth">The kids' depth.</param>
internal readonly record struct AncestorCursor(PdfArray Kids, int NextChildIndex, int ChildCount, InheritedAttributes Inherited, int InheritIndex, int Depth);
