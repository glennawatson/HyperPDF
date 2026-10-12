// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Theming;

/// <summary>Colours for one-colour icons, chosen by what the action does, as 0xRRGGBB.</summary>
/// <param name="Navigation">Slate: folders, places, navigation, open, view.</param>
/// <param name="Add">Sage: add, new, save, confirm.</param>
/// <param name="Edit">Sand: edit, copy, settings, annotate.</param>
/// <param name="Remove">Clay: delete, remove, close, stop.</param>
[DebuggerDisplay("IconTints: {Navigation:X6} {Add:X6} {Edit:X6} {Remove:X6}")]
public readonly record struct IconTints(uint Navigation, uint Add, uint Edit, uint Remove);
