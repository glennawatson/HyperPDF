// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>The kinds of value a form script works with.</summary>
internal enum SandboxValueKind
{
    /// <summary>No value.</summary>
    Undefined = 0,

    /// <summary>The null value.</summary>
    Null = 1,

    /// <summary>True or false, held as 1 or 0.</summary>
    Boolean = 2,

    /// <summary>A number.</summary>
    Number = 3,

    /// <summary>A piece of text.</summary>
    String = 4,

    /// <summary>A host object, an array or a field.</summary>
    Object = 5,
}
