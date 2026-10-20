// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>The kinds of token of the form script language.</summary>
internal enum SandboxTokenKind
{
    /// <summary>The end of the script.</summary>
    End = 0,

    /// <summary>A number.</summary>
    Number = 1,

    /// <summary>A string.</summary>
    String = 2,

    /// <summary>A name: a variable, a property, or a keyword such as <c>if</c>.</summary>
    Name = 3,

    /// <summary>An operator or punctuation, such as <c>+</c>, <c>==</c>, <c>(</c> or <c>;</c>.</summary>
    Symbol = 4,
}
