// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Metadata;

/// <summary>The leased values of one array property.</summary>
/// <param name="Buffer">The leased value storage.</param>
/// <param name="Count">The collected values.</param>
internal readonly record struct XmpPropertyValues(string[]? Buffer, int Count);
