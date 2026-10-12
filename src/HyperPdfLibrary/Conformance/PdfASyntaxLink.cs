// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>Which operands of a content stream operator veraPDF checks against the implementation limits.</summary>
internal enum PdfASyntaxLink
{
    /// <summary>No operand is checked.</summary>
    None = 0,

    /// <summary>The last operand when it is a number, as for <c>w</c> and <c>Tc</c>.</summary>
    LastNumber = 1,

    /// <summary>The last two operands when they are numbers, as for <c>Td</c> and <c>d0</c>.</summary>
    LastTwoNumbers = 2,

    /// <summary>The last operand when it is an integer, as for <c>J</c> and <c>j</c>.</summary>
    LastInteger = 3,

    /// <summary>Every number among the operands and inside array operands, as for <c>cm</c> and <c>re</c>.</summary>
    AllNumbers = 4,

    /// <summary>The dash array and the dash phase of <c>d</c>.</summary>
    Dash = 5,

    /// <summary>The last operand when it is a string, as for <c>Tj</c>.</summary>
    LastString = 6,

    /// <summary>The last operand when it is an array, as for <c>TJ</c>.</summary>
    LastArray = 7,

    /// <summary>The font name and size of <c>Tf</c>.</summary>
    Font = 8,

    /// <summary>The last operand when it is a name, as for <c>cs</c>, <c>scn</c> and <c>BMC</c>.</summary>
    LastName = 9,

    /// <summary>The tag and an inline property list of <c>BDC</c> and <c>DP</c>.</summary>
    TagAndProperties = 10,
}
