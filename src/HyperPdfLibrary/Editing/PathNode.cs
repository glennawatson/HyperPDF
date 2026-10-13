// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>A page tree node on a <see cref="PageTreePath"/>.</summary>
/// <param name="Id">The node's object id.</param>
/// <param name="Node">The node's dictionary.</param>
/// <param name="ChildIndex">The index in its <c>/Kids</c> of the kid the path goes through.</param>
[DebuggerDisplay("PathNode: {Id} kid {ChildIndex}")]
internal readonly record struct PathNode(PdfObjectId Id, PdfDictionary Node, int ChildIndex);
