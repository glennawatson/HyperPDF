// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Annotations;

namespace HyperPdf.ViewModels.Markup;

/// <summary>A review status chosen for a comment.</summary>
/// <param name="Annotation">The comment.</param>
/// <param name="State">The status.</param>
[DebuggerDisplay("AnnotationStatusChoice: {Annotation} {State}")]
public readonly record struct AnnotationStatusChoice(PageAnnotation Annotation, ReviewState State);
