// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;

namespace HyperPdf.App.ViewModels;

/// <summary>Text being typed on the page: where its box is, and the text box it replaces when it was there before.</summary>
/// <param name="Page">The zero based page index.</param>
/// <param name="Location">The box's top-left corner, in page space.</param>
/// <param name="WrapWidth">The width lines wrap at, in points, or 0 for a box that grows as text is typed.</param>
/// <param name="Replacing">The text box being edited again, or <see langword="null"/> for new text.</param>
[DebuggerDisplay("TextEditSession: page {Page} at {Location}")]
public sealed record TextEditSession(int Page, PagePoint Location, float WrapWidth, PageAnnotation? Replacing)
{
    /// <summary>Gets a value indicating whether the session writes new text rather than editing a text box.</summary>
    public bool IsNew => Replacing is null;
}
