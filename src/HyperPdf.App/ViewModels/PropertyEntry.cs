// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.App.ViewModels;

/// <summary>A name and value shown in the properties dialog.</summary>
/// <param name="Name">The property name.</param>
/// <param name="Value">The property value.</param>
[DebuggerDisplay("PropertyEntry: {Name}: {Value}")]
public sealed record PropertyEntry(string Name, string Value);
