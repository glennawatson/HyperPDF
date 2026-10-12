// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Text;

/// <summary>Applies one named setting, read as text, to the settings read so far.</summary>
/// <param name="fields">The settings read so far.</param>
/// <param name="value">The setting's value.</param>
internal delegate void FormatSetter(ref FormatFields fields, ReadOnlySpan<char> value);
