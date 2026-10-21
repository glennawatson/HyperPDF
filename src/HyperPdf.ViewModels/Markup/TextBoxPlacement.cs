// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Markup;

/// <summary>The top-left point and wrap width of a text box.</summary>
/// <param name="Location">The top-left point.</param>
/// <param name="WrapWidth">The width used to wrap its text.</param>
internal readonly record struct TextBoxPlacement(PagePoint Location, float WrapWidth);
