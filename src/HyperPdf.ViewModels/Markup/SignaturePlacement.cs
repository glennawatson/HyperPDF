// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Markup;

/// <summary>A signature or initials being placed: shown on the page as a preview until the user places it.</summary>
/// <param name="Page">The zero-based page.</param>
/// <param name="Bounds">Where the mark would go, in page space.</param>
/// <param name="Mark">The mark.</param>
[DebuggerDisplay("SignaturePlacement: Page {Page}: {Bounds}")]
public sealed record SignaturePlacement(int Page, PageRect Bounds, SignatureMark Mark);
