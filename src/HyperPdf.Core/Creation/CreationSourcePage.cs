// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Creation;

/// <summary>A page copied whole from another PDF as the next page. The text and images around it start on new pages.</summary>
/// <param name="Path">The PDF file.</param>
/// <param name="PageIndex">The zero-based index of the page in that file.</param>
/// <param name="Password">The file's password, or <see langword="null"/>.</param>
[DebuggerDisplay("CreationSourcePage: {Path} page {PageIndex}")]
public sealed record CreationSourcePage(string Path, int PageIndex, string? Password) : CreationBlock;
