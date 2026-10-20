// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting;

/// <summary>The PDF form functions HyperPDF runs itself, without a JavaScript engine.</summary>
public enum FormScriptFunction
{
    /// <summary>A script HyperPDF does not run; it is left alone.</summary>
    Unknown = 0,

    /// <summary><c>AFNumber_Format</c> or <c>AFNumber_Keystroke</c>: numbers with decimals, separators and currency.</summary>
    Number = 1,

    /// <summary><c>AFPercent_Format</c> or <c>AFPercent_Keystroke</c>: a number shown as a percentage.</summary>
    Percent = 2,

    /// <summary><c>AFDate_Format(Ex)</c> or <c>AFDate_Keystroke(Ex)</c>: a date in a given format.</summary>
    Date = 3,

    /// <summary><c>AFSpecial_Format</c> or <c>AFSpecial_Keystroke</c>: zip code, zip+4, phone number or social security number.</summary>
    Special = 4,

    /// <summary><c>AFRange_Validate</c>: a number within limits.</summary>
    Range = 5,

    /// <summary><c>AFSimple_Calculate</c>: the sum, average, product, minimum or maximum of fields.</summary>
    Simple = 6,

    /// <summary>A simplified field notation calculation, such as <c>Price * Quantity</c>.</summary>
    Expression = 7,

    /// <summary><c>AFTime_Format(Ex)</c> or <c>AFTime_Keystroke(Ex)</c>: a time of day in a given format.</summary>
    Time = 8,

    /// <summary>Any other script within what the sandboxed interpreter runs; see <see cref="FormScriptProgram"/>.</summary>
    Program = 9,
}
