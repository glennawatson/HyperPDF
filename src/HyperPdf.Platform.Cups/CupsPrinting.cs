// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Printing;

namespace HyperPdf.Platform.Cups;

/// <summary>Lists printers and sends jobs straight to their queues through libcups, as browsers do.</summary>
public static unsafe class CupsPrinting
{
    /// <summary>The most copies one job asks for.</summary>
    private const int MaxCopies = 999;

    /// <summary>The number of options sent with a job.</summary>
    private const int OptionCount = 5;

    /// <summary>The option holding a printer's description.</summary>
    private const string InfoOption = "printer-info";

    /// <summary>The libcups file names, most specific first.</summary>
    private static readonly string[] Candidates = OperatingSystem.IsMacOS() ? ["/usr/lib/libcups.2.dylib", "libcups.2.dylib"] : ["libcups.so.2", "libcups.so"];

    /// <summary>Gets a value indicating whether libcups can be loaded.</summary>
    public static bool IsAvailable
    {
        get
        {
            NativeLibraries.Register(typeof(CupsPrinting).Assembly, NativeMethods.Library, Candidates);
            return NativeLibraries.TryLoad(typeof(CupsPrinting).Assembly, NativeMethods.Library);
        }
    }

    /// <summary>Gets the printers, the default first.</summary>
    /// <returns>The printers; empty when CUPS is not installed or not running.</returns>
    public static IReadOnlyList<PrinterInfo> GetPrinters()
    {
        if (!IsAvailable)
        {
            return [];
        }

        var count = NativeMethods.CupsGetDests2(0, out var destinations);
        if (count <= 0 || destinations == 0)
        {
            return [];
        }

        try
        {
            var printers = new List<PrinterInfo>(count);
            var all = new ReadOnlySpan<CupsDest>((void*)destinations, count);
            foreach (ref readonly var destination in all)
            {
                if (destination.IsInstance || destination.Name is not { Length: > 0 } name)
                {
                    continue;
                }

                var info = destination.GetOption(InfoOption);
                printers.Add(new(name, string.IsNullOrWhiteSpace(info) ? name : info, destination.IsDefault));
            }

            printers.Sort(ComparePrinters);
            return printers;
        }
        finally
        {
            NativeMethods.CupsFreeDests(count, destinations);
        }
    }

    /// <summary>Sends a PDF to a printer's queue.</summary>
    /// <param name="filePath">The PDF.</param>
    /// <param name="title">The job title.</param>
    /// <param name="options">The printer and settings.</param>
    /// <returns>Whether the queue accepted the job.</returns>
    public static PrintOutcome Submit(string filePath, string title, in PrintJobOptions options)
    {
        if (!IsAvailable)
        {
            return new(false, "The print system (CUPS) is not installed.");
        }

        var settings = BuildOptions(options);
        var native = new CupsOption[OptionCount];
        try
        {
            native[0] = NativeOption(CupsJobOptions.CopiesName, settings.Copies);
            native[1] = NativeOption(CupsJobOptions.MediaName, settings.Media);
            native[2] = NativeOption(CupsJobOptions.SidesName, settings.Sides);
            native[3] = NativeOption(CupsJobOptions.ColorModeName, settings.ColorMode);
            native[4] = NativeOption(CupsJobOptions.ScalingName, settings.Scaling);

            var name = Encoding.UTF8.GetBytes(options.Printer + '\0');
            var file = Encoding.UTF8.GetBytes(filePath + '\0');
            var job = Encoding.UTF8.GetBytes(title + '\0');
            int id;
            fixed (byte* namePointer = name)
            {
                fixed (byte* filePointer = file)
                {
                    fixed (byte* titlePointer = job)
                    {
                        fixed (CupsOption* optionPointer = native)
                        {
                            id = NativeMethods.CupsPrintFile2(0, namePointer, filePointer, titlePointer, native.Length, optionPointer);
                        }
                    }
                }
            }

            return id > 0 ? new(true, string.Empty) : new(false, Marshal.PtrToStringUTF8(NativeMethods.CupsLastErrorString()) ?? "The printer did not accept the job.");
        }
        finally
        {
            foreach (var option in native)
            {
                Marshal.FreeCoTaskMem(option.NamePointer);
                Marshal.FreeCoTaskMem(option.ValuePointer);
            }
        }
    }

    /// <summary>Builds the CUPS options for a job.</summary>
    /// <param name="options">The job settings.</param>
    /// <returns>Option names and values.</returns>
    internal static CupsJobOptions BuildOptions(in PrintJobOptions options) =>
        new(Math.Clamp(options.Copies, 1, MaxCopies).ToString(CultureInfo.InvariantCulture), GetMedia(options.Paper), GetSides(options), options.Colour ? "color" : "monochrome", "none");

    /// <summary>Gets the PWG media name for a paper size.</summary>
    /// <param name="paper">The paper.</param>
    /// <returns>The media option.</returns>
    internal static string GetMedia(PaperSize paper) => paper switch
    {
        PaperSize.Letter => "na_letter_8.5x11in",
        PaperSize.A3 => "iso_a3_297x420mm",
        PaperSize.A5 => "iso_a5_148x210mm",
        PaperSize.Legal => "na_legal_8.5x14in",
        PaperSize.Tabloid => "na_ledger_11x17in",
        _ => "iso_a4_210x297mm",
    };

    /// <summary>Copies an option's name and value into native memory, which the caller frees.</summary>
    /// <param name="name">The option name.</param>
    /// <param name="value">The option value.</param>
    /// <returns>The native option.</returns>
    private static CupsOption NativeOption(string name, string value) => new(Marshal.StringToCoTaskMemUTF8(name), Marshal.StringToCoTaskMemUTF8(value));

    /// <summary>Gets the CUPS sides value for the chosen duplex edge.</summary>
    /// <param name="options">The job settings.</param>
    /// <returns>The sides option.</returns>
    private static string GetSides(in PrintJobOptions options)
    {
        if (!options.TwoSided)
        {
            return "one-sided";
        }

        return options.Binding == DuplexBinding.ShortEdge ? "two-sided-short-edge" : "two-sided-long-edge";
    }

    /// <summary>Orders printers: the default first, then by name.</summary>
    /// <param name="left">The first printer.</param>
    /// <param name="right">The second printer.</param>
    /// <returns>The order.</returns>
    private static int ComparePrinters(PrinterInfo left, PrinterInfo right)
    {
        if (left.IsDefault != right.IsDefault)
        {
            return left.IsDefault ? -1 : 1;
        }

        return string.Compare(left.DisplayName, right.DisplayName, StringComparison.CurrentCulture);
    }
}
