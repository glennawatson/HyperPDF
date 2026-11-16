// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdf.Core.Documents;

/// <summary>A change of place or shape for one object of a page, made together with changes to other objects as one undoable step.</summary>
/// <param name="ObjectIndex">The object's number from reading the page.</param>
/// <param name="Transformation">The change in page space points, with the page's top left corner as the origin and y going down.</param>
[DebuggerDisplay("PageObjectChange: object {ObjectIndex}")]
public readonly record struct PageObjectChange(int ObjectIndex, Matrix3x2 Transformation);
