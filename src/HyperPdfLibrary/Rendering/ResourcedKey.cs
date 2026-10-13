// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>Identifies a parsed object together with the resource dictionary its names resolved through.</summary>
/// <typeparam name="TSource">The type of the parsed object's source.</typeparam>
/// <param name="Source">The array or dictionary that was parsed.</param>
/// <param name="Resources">The resource dictionary the parse read names from, or null.</param>
[DebuggerDisplay("ResourcedKey")]
internal readonly record struct ResourcedKey<TSource>(TSource Source, PdfDictionary? Resources)
    where TSource : notnull;
