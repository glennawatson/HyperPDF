// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Interchange;

/// <summary>How many annotations were added, replaced and skipped.</summary>
/// <param name="Added">The annotations added.</param>
/// <param name="Replaced">The annotations that replaced one.</param>
/// <param name="Skipped">The annotations left out.</param>
internal readonly record struct AnnotationCounts(int Added, int Replaced, int Skipped);
