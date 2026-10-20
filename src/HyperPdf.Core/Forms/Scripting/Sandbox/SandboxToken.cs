// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>One token of a form script.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Text">The name, symbol or string contents.</param>
/// <param name="Number">The value of a number token.</param>
[DebuggerDisplay("SandboxToken: {Kind} {Text}")]
internal readonly record struct SandboxToken(SandboxTokenKind Kind, string Text, double Number);
