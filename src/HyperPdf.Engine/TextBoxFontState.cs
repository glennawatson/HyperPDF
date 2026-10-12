// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Text.Fonts;
using HyperPdf.Core.Text.Layout;

namespace HyperPdf.Engine;

/// <summary>Remembers font shapers and fallback searches for documents opened by one viewer engine.</summary>
internal sealed class TextBoxFontState
{
    /// <summary>Gets the standard-font shapers used by this engine.</summary>
    internal StandardTextShaper?[] StandardShapers { get; } = new StandardTextShaper?[StandardTextShaper.FontCount];

    /// <summary>Gets the shaper of each installed font used by the document.</summary>
    internal ConcurrentDictionary<FontProgram, FontProgramShaper> Shapers { get; } = new();

    /// <summary>Gets fallback faces without keeping discarded font catalogs alive.</summary>
    internal ConditionalWeakTable<FontCatalog, ConcurrentDictionary<FallbackKey, FontFace>> Found { get; } = new();

    /// <summary>A fallback lookup within the font catalog that owns its table.</summary>
    /// <param name="Block">The block of the first character outside Latin-1.</param>
    /// <param name="Bold">Whether bold is wanted.</param>
    /// <param name="Italic">Whether italic is wanted.</param>
    internal readonly record struct FallbackKey(int Block, bool Bold, bool Italic);
}
