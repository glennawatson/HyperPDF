// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Markup;

/// <summary>A piece of page content picked or pointed at in Edit mode.</summary>
/// <param name="Page">The zero based page index.</param>
/// <param name="Object">The object on that page.</param>
[DebuggerDisplay("PageObjectPick: page {Page}, {Object}")]
public sealed record PageObjectPick(int Page, PageObjectInfo Object);
