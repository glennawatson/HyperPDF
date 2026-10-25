// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the annotation drawing rules.</summary>
public sealed class AnnotationRulesTests
{
    /// <summary>Drawings, lines and unfilled shapes are outlines; marks on text are not.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="expected">Whether it is an outline.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(AnnotationKind.Ink, true)]
    [Arguments(AnnotationKind.Rectangle, true)]
    [Arguments(AnnotationKind.Polygon, true)]
    [Arguments(AnnotationKind.Highlight, false)]
    [Arguments(AnnotationKind.Note, false)]
    public async Task OutlineKindsShowThePageThrough(AnnotationKind kind, bool expected) =>
        await Assert.That(AnnotationRules.IsOutline(kind)).IsEqualTo(expected);
}
