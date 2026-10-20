// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>The kinds of node in a parsed form script. The values index the evaluator's table.</summary>
internal enum SandboxNodeKind
{
    /// <summary>A list of statements.</summary>
    Block = 0,

    /// <summary>A declaration of a variable, with an optional first value.</summary>
    Declare = 1,

    /// <summary>An <c>if</c> with an optional <c>else</c>.</summary>
    If = 2,

    /// <summary>A <c>for</c> loop: start, test, step, body.</summary>
    For = 3,

    /// <summary>A <c>while</c> loop: test, body.</summary>
    While = 4,

    /// <summary>A <c>break</c>.</summary>
    Break = 5,

    /// <summary>A <c>continue</c>.</summary>
    Continue = 6,

    /// <summary>A number literal.</summary>
    Number = 7,

    /// <summary>A string literal.</summary>
    String = 8,

    /// <summary>A constant: <c>true</c>, <c>false</c>, <c>null</c> or <c>undefined</c>.</summary>
    Constant = 9,

    /// <summary>A variable or global object.</summary>
    Name = 10,

    /// <summary>A property read, <c>a.b</c>.</summary>
    Member = 11,

    /// <summary>An element read, <c>a[i]</c>.</summary>
    Index = 12,

    /// <summary>A call, <c>f(x)</c> or <c>a.f(x)</c>.</summary>
    Call = 13,

    /// <summary>A prefix operator: <c>!</c>, <c>-</c>, <c>+</c> or <c>typeof</c>.</summary>
    Unary = 14,

    /// <summary>An increase or decrease, <c>++</c> or <c>--</c>, before or after its target.</summary>
    Update = 15,

    /// <summary>A binary operator that always reads both sides.</summary>
    Binary = 16,

    /// <summary>The <c>&amp;&amp;</c> and <c>||</c> operators, which may skip their right side.</summary>
    Logical = 17,

    /// <summary>A <c>? :</c> choice.</summary>
    Conditional = 18,

    /// <summary>An assignment, <c>=</c> or a compound form such as <c>+=</c>.</summary>
    Assign = 19,

    /// <summary>An array written as <c>[a, b]</c>.</summary>
    Array = 20,
}
