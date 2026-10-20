// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Optimizing;

/// <summary>Which names of a font object get a subset tag.</summary>
/// <param name="Kind">The kind of object whose names change.</param>
/// <param name="Tag">The six capital letters that mark the subset.</param>
[DebuggerDisplay("FontRename: {Kind} {Tag}")]
internal readonly record struct FontRename(FontRenameKind Kind, string Tag);
