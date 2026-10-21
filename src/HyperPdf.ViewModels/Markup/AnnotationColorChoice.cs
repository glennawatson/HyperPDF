// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Annotations;

namespace HyperPdf.ViewModels.Markup;

/// <summary>A colour chosen for an annotation.</summary>
/// <param name="Annotation">The annotation.</param>
/// <param name="Color">The colour.</param>
[DebuggerDisplay("AnnotationColorChoice: {Annotation} {Color}")]
public readonly record struct AnnotationColorChoice(PageAnnotation Annotation, uint Color);
