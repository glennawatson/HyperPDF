// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Functions;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Content;

/// <summary>
/// Reads the device-dependent ExtGState entries of ISO 32000-2 Table 53 that are not overprint: flatness, smoothness, stroke
/// adjustment, transfer functions, halftones, black generation and undercolour removal. Only the transfer function changes
/// what a screen shows; the others are kept in the graphics state. A malformed value leaves the state as it was.
/// </summary>
internal static class ContentDeviceFunctions
{
    /// <summary>The largest flatness tolerance (ISO 32000-2 Table 53, /FL).</summary>
    internal const float MaxFlatness = 100;

    /// <summary>The functions in a transfer function array that are read: red, green and blue.</summary>
    internal const int TransferFunctions = 3;

    /// <summary>The position of the blue table.</summary>
    private const int BlueTable = 2;

    /// <summary>The largest byte as a float.</summary>
    private const float ByteMax = 255;

    /// <summary>Applies one ExtGState entry when it is one of these.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="dictionary">The ExtGState dictionary, which tells whether a second version of a key is present.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, resolved.</param>
    /// <returns><see langword="true"/> when the key is one of these, whether or not its value was usable.</returns>
    internal static bool Apply(ContentInterpreter self, PdfDictionary dictionary, KnownName key, PdfValue value)
    {
        switch (key)
        {
            case KnownName.FL:
                {
                    ContentDeviceFunctions.ApplyFlatness(self, value);
                    return true;
                }

            case KnownName.SM:
                {
                    ContentDeviceFunctions.ApplySmoothness(self, value);
                    return true;
                }

            case KnownName.SA:
                {
                    ContentDeviceFunctions.ApplyStrokeAdjustment(self, value);
                    return true;
                }

            case KnownName.HT:
                {
                    ContentDeviceFunctions.ApplyHalftone(self, value);
                    return true;
                }

            default:
                {
                    return ContentDeviceFunctions.ApplyFunctionEntry(self, dictionary, key, value);
                }
        }
    }

    /// <summary>Reads a boolean entry; a boolean or a number counts, anything else is not usable.</summary>
    /// <param name="value">The value.</param>
    /// <param name="result">Receives the boolean.</param>
    /// <returns><see langword="true"/> when the value is usable.</returns>
    internal static bool TryReadBoolean(PdfValue value, out bool result)
    {
        result = value.AsBoolean() || value.AsInt32() != 0;
        return value.Kind == PdfKind.Boolean || value.IsNumber;
    }

    /// <summary>Builds the tables of a transfer function value.</summary>
    /// <param name="value">A function, or an array of at least three functions for red, green and blue; a name stands for the identity.</param>
    /// <returns>The tables, or <see langword="null"/> when the value is not a usable transfer function.</returns>
    internal static PdfTransfer? BuildTransfer(PdfValue value)
    {
        var tables = new byte[PdfTransfer.Entries * PdfTransfer.Channels];
        if (value.AsArray() is { } array)
        {
            return array.Count < ContentDeviceFunctions.TransferFunctions ? null : ContentDeviceFunctions.SampleEach(array, tables);
        }

        return PdfFunction.Parse(value) is { } function && ContentDeviceFunctions.Sample(function, tables.AsSpan(0, PdfTransfer.Entries))
            ? ContentDeviceFunctions.Share(tables)
            : null;
    }

    /// <summary>Applies the entries that carry a function: transfer, black generation and undercolour removal.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="dictionary">The ExtGState dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, resolved.</param>
    /// <returns><see langword="true"/> when the key is one of these.</returns>
    private static bool ApplyFunctionEntry(ContentInterpreter self, PdfDictionary dictionary, KnownName key, PdfValue value)
    {
        switch (key)
        {
            case KnownName.TR2:
                {
                    ContentDeviceFunctions.ApplyTransfer(self, value);
                    return true;
                }

            case KnownName.TR:
                {
                    // /TR2 takes precedence, wherever it appears in the dictionary.
                    if (!dictionary.ContainsKey(KnownName.TR2))
                    {
                        ContentDeviceFunctions.ApplyTransfer(self, value);
                    }

                    return true;
                }

            case KnownName.BG2:
                {
                    ContentDeviceFunctions.ApplyBlackGeneration(self, value);
                    return true;
                }

            case KnownName.BG:
                {
                    if (!dictionary.ContainsKey(KnownName.BG2))
                    {
                        ContentDeviceFunctions.ApplyBlackGeneration(self, value);
                    }

                    return true;
                }

            default:
                {
                    return ContentDeviceFunctions.ApplyRemovalEntry(self, dictionary, key, value);
                }
        }
    }

    /// <summary>Applies the undercolour removal entries.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="dictionary">The ExtGState dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, resolved.</param>
    /// <returns><see langword="true"/> when the key is /UCR or /UCR2.</returns>
    private static bool ApplyRemovalEntry(ContentInterpreter self, PdfDictionary dictionary, KnownName key, PdfValue value)
    {
        switch (key)
        {
            case KnownName.UCR2:
                {
                    ContentDeviceFunctions.ApplyUndercolorRemoval(self, value);
                    return true;
                }

            case KnownName.UCR:
                {
                    if (!dictionary.ContainsKey(KnownName.UCR2))
                    {
                        ContentDeviceFunctions.ApplyUndercolorRemoval(self, value);
                    }

                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>Applies /FL; a value that is not a number is ignored and a number is kept within 0 to 100.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="value">The value.</param>
    private static void ApplyFlatness(ContentInterpreter self, PdfValue value)
    {
        if (value.IsNumber)
        {
            self.State.Flatness = Math.Clamp(value.AsSingle(), 0, ContentDeviceFunctions.MaxFlatness);
        }
    }

    /// <summary>Applies /SM; a value that is not a number is ignored and a number is kept within 0 to 1.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="value">The value.</param>
    private static void ApplySmoothness(ContentInterpreter self, PdfValue value)
    {
        if (value.IsNumber)
        {
            self.State.Smoothness = Math.Clamp(value.AsSingle(), 0, 1);
        }
    }

    /// <summary>Applies /SA; a value that is not a boolean or a number is ignored.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="value">The value.</param>
    private static void ApplyStrokeAdjustment(ContentInterpreter self, PdfValue value)
    {
        if (ContentDeviceFunctions.TryReadBoolean(value, out var adjust))
        {
            self.State.StrokeAdjustment = adjust;
        }
    }

    /// <summary>Applies /HT; a name selects the default halftone, a dictionary or stream sets one, anything else is ignored.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="value">The value.</param>
    private static void ApplyHalftone(ContentInterpreter self, PdfValue value)
    {
        if (!value.AsName().IsNone)
        {
            self.State.Halftone = null;
        }
        else if (value.AsDictionary() is { } halftone)
        {
            self.State.Halftone = halftone;
        }
    }

    /// <summary>
    /// Applies /TR or /TR2. A name (/Default or /Identity) means no transfer function, a function or an array of functions
    /// sets one, and a value that is not a usable function is ignored. The table of each function is built once.
    /// </summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="value">The value.</param>
    private static void ApplyTransfer(ContentInterpreter self, PdfValue value)
    {
        if (!value.AsName().IsNone)
        {
            self.State.Transfer = null;
            return;
        }

        var transfer = self.Cache.Transfers.GetOrCreate(value, 0, static (key, _) => ContentDeviceFunctions.BuildTransfer(key));
        if (transfer is not null)
        {
            self.State.Transfer = transfer.IsIdentity ? null : transfer;
        }
    }

    /// <summary>Applies /BG or /BG2; a name selects the default, a function sets one, anything else is ignored.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="value">The value.</param>
    private static void ApplyBlackGeneration(ContentInterpreter self, PdfValue value)
    {
        if (ContentDeviceFunctions.TryReadFunction(self, value, out var function))
        {
            self.State.BlackGeneration = function;
        }
    }

    /// <summary>Applies /UCR or /UCR2; a name selects the default, a function sets one, anything else is ignored.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="value">The value.</param>
    private static void ApplyUndercolorRemoval(ContentInterpreter self, PdfValue value)
    {
        if (ContentDeviceFunctions.TryReadFunction(self, value, out var function))
        {
            self.State.UndercolorRemoval = function;
        }
    }

    /// <summary>Reads a function value through the cache.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="value">The value.</param>
    /// <param name="function">Receives the function, or <see langword="null"/> for a name.</param>
    /// <returns><see langword="true"/> for a name or a usable function.</returns>
    private static bool TryReadFunction(ContentInterpreter self, PdfValue value, out PdfFunction? function)
    {
        function = null;
        if (!value.AsName().IsNone)
        {
            return true;
        }

        function = self.Cache.Functions.GetOrCreate(value, 0, static (key, _) => PdfFunction.Parse(key));
        return function is not null;
    }

    /// <summary>Samples the first three functions of an array into the three tables.</summary>
    /// <param name="array">The array.</param>
    /// <param name="tables">The tables, filled in place.</param>
    /// <returns>The transfer, or <see langword="null"/> when a function is not usable.</returns>
    private static PdfTransfer? SampleEach(PdfArray array, byte[] tables)
    {
        for (var channel = 0; channel < ContentDeviceFunctions.TransferFunctions; channel++)
        {
            var table = tables.AsSpan(channel * PdfTransfer.Entries, PdfTransfer.Entries);
            var element = array.Get(channel);
            if (!element.AsName().IsNone)
            {
                ContentDeviceFunctions.Identity(table);
            }
            else if (PdfFunction.Parse(element) is not { } function || !ContentDeviceFunctions.Sample(function, table))
            {
                return null;
            }
        }

        return new(tables);
    }

    /// <summary>Copies the red table into the green and blue tables, as one function for every channel does.</summary>
    /// <param name="tables">The tables with the red one filled.</param>
    /// <returns>The transfer.</returns>
    private static PdfTransfer Share(byte[] tables)
    {
        var red = tables.AsSpan(0, PdfTransfer.Entries);
        red.CopyTo(tables.AsSpan(PdfTransfer.Entries, PdfTransfer.Entries));
        red.CopyTo(tables.AsSpan(PdfTransfer.Entries * ContentDeviceFunctions.BlueTable, PdfTransfer.Entries));
        return new(tables);
    }

    /// <summary>Fills a table with the identity.</summary>
    /// <param name="table">The table.</param>
    private static void Identity(Span<byte> table)
    {
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = (byte)i;
        }
    }

    /// <summary>Samples a one-input function at the 256 byte values into a table.</summary>
    /// <param name="function">The function.</param>
    /// <param name="table">The table.</param>
    /// <returns><see langword="false"/> when the function does not take one input and give an output.</returns>
    private static bool Sample(PdfFunction function, Span<byte> table)
    {
        if (function.InputCount != 1 || function.OutputCount < 1)
        {
            return false;
        }

        Span<float> input = stackalloc float[1];
        Span<float> output = stackalloc float[PdfFunction.MaxComponents];
        for (var i = 0; i < table.Length; i++)
        {
            input[0] = i / ContentDeviceFunctions.ByteMax;
            function.Evaluate(input, output);
            table[i] = (byte)MathF.Round(Math.Clamp(output[0], 0, 1) * ContentDeviceFunctions.ByteMax);
        }

        return true;
    }
}
