// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Text;

namespace HyperPdf.ViewModels.Markup;

/// <summary>The text and format being applied to one text edit session.</summary>
/// <param name="Edit">The edit session.</param>
/// <param name="Text">The text to write.</param>
/// <param name="Format">The text format.</param>
internal readonly record struct TextEditChange(TextEditSession Edit, string Text, TextFormat Format);
