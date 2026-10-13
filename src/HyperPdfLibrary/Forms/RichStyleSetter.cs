// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Forms;

/// <summary>Applies one CSS property value to a rich text style.</summary>
/// <param name="style">The style so far.</param>
/// <param name="value">The property's value, trimmed.</param>
/// <returns>The style with the property applied.</returns>
internal delegate RichStyle RichStyleSetter(RichStyle style, ReadOnlySpan<char> value);
