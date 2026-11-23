// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Text;
using HyperPdf.ViewModels.Markup;

namespace HyperPdf.ViewModels.Dialogs;

/// <summary>
/// The values menu entries pass to their commands, each boxed once. A menu entry holds its parameter as an object, so an enum
/// value passed straight in would be boxed again every time a menu is built.
/// </summary>
public static class MenuParameters
{
    /// <summary>Each page tool, boxed, indexed by its value.</summary>
    private static readonly object[] PageTools = Box<PageTool>();

    /// <summary>Each marking tool, boxed, indexed by its value.</summary>
    private static readonly object[] AnnotationTools = Box<AnnotationTool>();

    /// <summary>Each text alignment, boxed, indexed by its value.</summary>
    private static readonly object[] Alignments = Box<TextBoxAlignment>();

    /// <summary>Each kind of text file, boxed, indexed by its value.</summary>
    private static readonly object[] TextFormats = Box<DocumentTextFormat>();

    /// <summary>Each kind of form data file, boxed, indexed by its value.</summary>
    private static readonly object[] FormDataFormats = Box<FormDataFormat>();

    /// <summary>Gets a page tool as a menu parameter.</summary>
    /// <param name="value">The tool.</param>
    /// <returns>The boxed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object Of(PageTool value) => PageTools[(int)value];

    /// <summary>Gets a marking tool as a menu parameter.</summary>
    /// <param name="value">The tool.</param>
    /// <returns>The boxed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object Of(AnnotationTool value) => AnnotationTools[(int)value];

    /// <summary>Gets a text alignment as a menu parameter.</summary>
    /// <param name="value">The alignment.</param>
    /// <returns>The boxed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object Of(TextBoxAlignment value) => Alignments[(int)value];

    /// <summary>Gets a kind of text file as a menu parameter.</summary>
    /// <param name="value">The kind.</param>
    /// <returns>The boxed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object Of(DocumentTextFormat value) => TextFormats[(int)value];

    /// <summary>Gets a kind of form data file as a menu parameter.</summary>
    /// <param name="value">The kind.</param>
    /// <returns>The boxed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object Of(FormDataFormat value) => FormDataFormats[(int)value];

    /// <summary>Boxes every value of an enum whose values are small and not negative. A value the enum skips has no box.</summary>
    /// <typeparam name="T">The enum.</typeparam>
    /// <returns>The boxed values, indexed by value.</returns>
    private static object[] Box<T>()
        where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        var largest = 0;
        foreach (var value in values)
        {
            var copy = value;
            largest = Math.Max(largest, Unsafe.As<T, int>(ref copy));
        }

        var boxes = new object[largest + 1];
        foreach (var value in values)
        {
            var copy = value;
            boxes[Unsafe.As<T, int>(ref copy)] = value;
        }

        return boxes;
    }
}
