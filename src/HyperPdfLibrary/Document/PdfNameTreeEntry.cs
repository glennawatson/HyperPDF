// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>A name and its value in a name tree.</summary>
/// <param name="Name">The name, as text.</param>
/// <param name="Value">The value as the tree stores it: a direct value, or a reference to an object.</param>
[DebuggerDisplay("PdfNameTreeEntry: {Name}")]
public sealed record PdfNameTreeEntry(string Name, PdfValue Value);
