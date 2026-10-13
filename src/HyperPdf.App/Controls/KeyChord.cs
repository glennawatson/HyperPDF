// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Input;

namespace HyperPdf.App.Controls;

/// <summary>A key and the exact modifiers it is pressed with.</summary>
/// <param name="Key">The key.</param>
/// <param name="Modifiers">The modifiers.</param>
internal readonly record struct KeyChord(Key Key, KeyModifiers Modifiers);
