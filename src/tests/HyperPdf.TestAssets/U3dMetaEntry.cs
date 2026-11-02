// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>A text entry for the meta data section of a test block.</summary>
/// <param name="Attributes">The entry's flags: 2 hidden, 0x100 action, 0x200 file.</param>
/// <param name="Key">The key.</param>
/// <param name="Value">The value.</param>
public readonly record struct U3dMetaEntry(uint Attributes, string Key, string Value);
