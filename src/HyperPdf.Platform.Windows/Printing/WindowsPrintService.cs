// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Printing;

namespace HyperPdf.Platform.Windows.Printing;

/// <summary>
/// Prints on Windows through the print spooler: pages are drawn by the document engine in bands at the printer's own
/// resolution and sent to the printer's device context. Jobs go straight to a chosen printer with the copies, colour,
/// two-sided and paper settings applied to its DEVMODE, or through the Windows print dialog.
/// </summary>
[DebuggerDisplay("WindowsPrintService: Windows printing")]
public sealed class WindowsPrintService : IPrintService
{
    /// <summary>Local and network printers.</summary>
    private const uint LocalAndConnections = 0x2 | 0x4;

    /// <summary>The PRINTER_INFO_4 level.</summary>
    private const uint InfoLevel = 4;

    /// <summary>The size of PRINTER_INFO_4 on 64 bit Windows.</summary>
    private const int InfoSize = 24;

    /// <summary>The longest printer name.</summary>
    private const int MaxName = 256;

    /// <summary>DocumentProperties: write the merged settings.</summary>
    private const uint OutBuffer = 2;

    /// <summary>DocumentProperties: read settings to merge.</summary>
    private const uint InBuffer = 8;

    /// <summary>The offset of DEVMODEW.dmFields.</summary>
    private const int FieldsOffset = 72;

    /// <summary>The offset of DEVMODEW.dmOrientation.</summary>
    private const int OrientationOffset = 76;

    /// <summary>The offset of DEVMODEW.dmPaperSize.</summary>
    private const int PaperOffset = 78;

    /// <summary>The offset of DEVMODEW.dmCopies.</summary>
    private const int CopiesOffset = 86;

    /// <summary>The offset of DEVMODEW.dmColor.</summary>
    private const int ColorOffset = 92;

    /// <summary>The offset of DEVMODEW.dmDuplex.</summary>
    private const int DuplexOffset = 94;

    /// <summary>The DEVMODE fields set: DM_ORIENTATION, DM_PAPERSIZE, DM_COPIES, DM_COLOR and DM_DUPLEX.</summary>
    private const uint ChangedFields = 0x1 | 0x2 | 0x100 | 0x800 | 0x1000;

    /// <summary>Upright paper (DMORIENT_PORTRAIT). Duplex edges are named for upright paper, and wide sheets are turned to fit it.</summary>
    private const short Portrait = 1;

    /// <summary>US Letter paper (DMPAPER_LETTER).</summary>
    private const short LetterPaper = 1;

    /// <summary>A4 paper (DMPAPER_A4).</summary>
    private const short A4Paper = 9;

    /// <summary>Tabloid paper (DMPAPER_TABLOID).</summary>
    private const short TabloidPaper = 3;

    /// <summary>Legal paper (DMPAPER_LEGAL).</summary>
    private const short LegalPaper = 5;

    /// <summary>A3 paper (DMPAPER_A3).</summary>
    private const short A3Paper = 8;

    /// <summary>A5 paper (DMPAPER_A5).</summary>
    private const short A5Paper = 11;

    /// <summary>Black and white printing (DMCOLOR_MONOCHROME).</summary>
    private const short Monochrome = 1;

    /// <summary>Colour printing (DMCOLOR_COLOR).</summary>
    private const short Colour = 2;

    /// <summary>One-sided printing (DMDUP_SIMPLEX).</summary>
    private const short OneSided = 1;

    /// <summary>DMDUP_VERTICAL, two-sided along the long edge.</summary>
    private const short LongEdge = 2;

    /// <summary>DMDUP_HORIZONTAL, two-sided along the short edge.</summary>
    private const short ShortEdge = 3;

    /// <summary>The most copies one job asks for.</summary>
    private const short MaxCopies = 999;

    /// <summary>GetDeviceCaps HORZRES.</summary>
    private const int HorizontalResolution = 8;

    /// <summary>GetDeviceCaps VERTRES.</summary>
    private const int VerticalResolution = 10;

    /// <summary>GetDeviceCaps LOGPIXELSX.</summary>
    private const int PixelsPerInchX = 88;

    /// <summary>GetDeviceCaps LOGPIXELSY.</summary>
    private const int PixelsPerInchY = 90;

    /// <summary>The raster operation that copies the source.</summary>
    private const uint SourceCopy = 0x00CC0020;

    /// <summary>The most memory one band of a page uses.</summary>
    private const int BandBudget = 16 * 1024 * 1024;

    /// <summary>The print dialog flags: return a device context, no page ranges, selection or current page.</summary>
    private const uint DialogFlags = 0x100 | 0x8 | 0x4 | 0x80_0000;

    /// <summary>Open the dialog on its General page.</summary>
    private const uint GeneralPage = uint.MaxValue;

    /// <summary>The dialog result meaning Print was chosen.</summary>
    private const uint PrintChosen = 1;

    /// <summary>The engine that draws the pages.</summary>
    private readonly IDocumentEngine _engine;

    /// <summary>Initializes a new instance of the <see cref="WindowsPrintService"/> class.</summary>
    /// <param name="engine">The engine that draws the pages.</param>
    public WindowsPrintService(IDocumentEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public unsafe IReadOnlyList<PrinterInfo> GetPrinters()
    {
        _ = NativeMethods.EnumPrinters(LocalAndConnections, null, InfoLevel, null, 0, out var needed, out _);
        if (needed == 0)
        {
            return [];
        }

        var buffer = new byte[needed];
        fixed (byte* start = buffer)
        {
            if (NativeMethods.EnumPrinters(LocalAndConnections, null, InfoLevel, start, needed, out _, out var count) == 0)
            {
                return [];
            }

            var defaultName = GetDefaultPrinterName();
            var printers = new List<PrinterInfo>((int)count);
            for (var i = 0; i < count; i++)
            {
                if (Marshal.PtrToStringUni(*(nint*)(start + (i * InfoSize))) is { Length: > 0 } name)
                {
                    printers.Add(new(name, name, string.Equals(name, defaultName, StringComparison.OrdinalIgnoreCase)));
                }
            }

            printers.Sort(ComparePrinters);
            return printers;
        }
    }

    /// <inheritdoc/>
    public async Task<PrintOutcome> SubmitAsync(string filePath, string title, PrintJobOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(title);
        return await Task.Run(() => SubmitJobAsync(filePath, title, options, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>Shows the Windows print dialog on the calling (UI) thread, then prints on a background thread.</remarks>
    public async Task<bool> PrintAsync(string filePath, string title, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(title);
        var deviceContext = ShowDialog();
        if (deviceContext == 0)
        {
            return false;
        }

        try
        {
            var outcome = await Task.Run(() => PrintDocumentAsync(deviceContext, filePath, title, false, cancellationToken), cancellationToken).ConfigureAwait(false);
            return outcome.Sent;
        }
        finally
        {
            DeleteDeviceContext(deviceContext);
        }
    }

    /// <summary>Gets the DEVMODE paper value for a paper size.</summary>
    /// <param name="paper">The paper.</param>
    /// <returns>The DMPAPER value.</returns>
    internal static short GetPaper(PaperSize paper) => paper switch
    {
        PaperSize.Letter => LetterPaper,
        PaperSize.A3 => A3Paper,
        PaperSize.A5 => A5Paper,
        PaperSize.Legal => LegalPaper,
        PaperSize.Tabloid => TabloidPaper,
        _ => A4Paper,
    };

    /// <summary>Gets the DEVMODE duplex value for a print job.</summary>
    /// <param name="options">The job settings.</param>
    /// <returns>The printer's duplex mode.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short GetDuplex(in PrintJobOptions options)
    {
        if (!options.TwoSided)
        {
            return OneSided;
        }

        return options.Binding == DuplexBinding.ShortEdge ? ShortEdge : LongEdge;
    }

    /// <summary>Writes the job's choices into a printer's DEVMODE: upright paper, so the duplex edge names the sheet's own edge.</summary>
    /// <param name="devMode">The DEVMODEW bytes.</param>
    /// <param name="options">The job settings.</param>
    internal static void ApplyChoices(Span<byte> devMode, in PrintJobOptions options)
    {
        var fields = MemoryMarshal.Read<uint>(devMode[FieldsOffset..]) | ChangedFields;
        MemoryMarshal.Write(devMode[FieldsOffset..], in fields);
        WriteShort(devMode, OrientationOffset, Portrait);
        WriteShort(devMode, PaperOffset, GetPaper(options.Paper));
        WriteShort(devMode, CopiesOffset, (short)Math.Clamp(options.Copies, 1, MaxCopies));
        WriteShort(devMode, ColorOffset, options.Colour ? Colour : Monochrome);
        WriteShort(devMode, DuplexOffset, GetDuplex(options));
    }

    /// <summary>Puts the default printer first, then sorts by name.</summary>
    /// <param name="left">The left printer.</param>
    /// <param name="right">The right printer.</param>
    /// <returns>The order.</returns>
    private static int ComparePrinters(PrinterInfo left, PrinterInfo right)
    {
        if (left.IsDefault != right.IsDefault)
        {
            return left.IsDefault ? -1 : 1;
        }

        return string.Compare(left.DisplayName, right.DisplayName, StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>Writes one DEVMODE value.</summary>
    /// <param name="devMode">The DEVMODEW bytes.</param>
    /// <param name="offset">The field's offset.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteShort(Span<byte> devMode, int offset, short value) => MemoryMarshal.Write(devMode[offset..], in value);

    /// <summary>Shows the Windows print dialog.</summary>
    /// <returns>The chosen printer's device context, or zero when cancelled.</returns>
    private static unsafe nint ShowDialog()
    {
        var dialog = new PrintDialogEx(NativeMethods.GetForegroundWindow(), DialogFlags, GeneralPage);
        var result = NativeMethods.PrintDlgEx(&dialog);
        var deviceContext = dialog.DeviceContext;
        FreeDialog(dialog);
        if (result >= 0 && dialog.ResultAction == PrintChosen)
        {
            return deviceContext;
        }

        DeleteDeviceContext(deviceContext);
        return 0;
    }

    /// <summary>Gets the default printer's name.</summary>
    /// <returns>The name, or empty.</returns>
    private static unsafe string GetDefaultPrinterName()
    {
        var size = (uint)MaxName;
        char* name = stackalloc char[MaxName];
        return NativeMethods.GetDefaultPrinter(name, ref size) != 0 ? new(name) : string.Empty;
    }

    /// <summary>Frees the settings the print dialog allocated.</summary>
    /// <param name="dialog">The dialog.</param>
    private static void FreeDialog(in PrintDialogEx dialog)
    {
        if (dialog.DevMode != 0)
        {
            _ = NativeMethods.GlobalFree(dialog.DevMode);
        }

        if (dialog.DevNames != 0)
        {
            _ = NativeMethods.GlobalFree(dialog.DevNames);
        }
    }

    /// <summary>Deletes a device context when there is one.</summary>
    /// <param name="deviceContext">The device context.</param>
    private static void DeleteDeviceContext(nint deviceContext)
    {
        if (deviceContext != 0)
        {
            _ = NativeMethods.DeleteDC(deviceContext);
        }
    }

    /// <summary>Builds a printer's settings with the job's choices applied.</summary>
    /// <param name="options">The printer and choices.</param>
    /// <returns>The DEVMODE, or null when the printer cannot be opened.</returns>
    private static unsafe byte[]? BuildSettings(in PrintJobOptions options)
    {
        if (NativeMethods.OpenPrinter(options.Printer, out var printer, null) == 0)
        {
            return null;
        }

        try
        {
            var size = NativeMethods.DocumentProperties(0, printer, options.Printer, null, null, 0);
            if (size <= 0)
            {
                return null;
            }

            var settings = new byte[size];
            fixed (byte* devMode = settings)
            {
                if (NativeMethods.DocumentProperties(0, printer, options.Printer, devMode, null, OutBuffer) < 0)
                {
                    return null;
                }

                ApplyChoices(settings, options);
                _ = NativeMethods.DocumentProperties(0, printer, options.Printer, devMode, devMode, OutBuffer | InBuffer);
            }

            return settings;
        }
        finally
        {
            _ = NativeMethods.ClosePrinter(printer);
        }
    }

    /// <summary>Draws one page in bands.</summary>
    /// <param name="deviceContext">The printer's device context.</param>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="size">The page size in points.</param>
    /// <param name="device">The printer's resolution and printable area.</param>
    /// <param name="flags">The render flags.</param>
    private static unsafe void DrawPage(nint deviceContext, IDocument document, int page, PageSize size, in Device device, RenderFlags flags)
    {
        var placement = DevicePlacement.Fit(size, device.DpiX, device.DpiY, device.Width, device.Height);
        var rows = placement.RowsPerBand(BandBudget);
        var stride = placement.Width * sizeof(int);
        var buffer = ArrayPool<byte>.Shared.Rent(stride * rows);
        try
        {
            for (var top = 0; top < placement.Height; top += rows)
            {
                var bandRows = Math.Min(rows, placement.Height - top);
                var target = new RenderTarget(buffer.AsSpan(0, stride * bandRows), placement.Width, bandRows, stride);
                if (!document.Render(new(page, placement.Scale, placement.Rotation, 0, top, flags), target))
                {
                    return;
                }

                var deviceTop = placement.DeviceRow(top);
                var deviceRows = placement.DeviceRow(top + bandRows) - deviceTop;
                var header = BitmapInfoHeader.TopDown(placement.Width, bandRows);
                fixed (byte* pixels = buffer)
                {
                    _ = NativeMethods.StretchDIBits(
                        deviceContext,
                        placement.Left,
                        placement.Top + deviceTop,
                        placement.Width,
                        Math.Max(1, deviceRows),
                        0,
                        0,
                        placement.Width,
                        bandRows,
                        pixels,
                        &header,
                        0,
                        SourceCopy);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Creates a device context with the selected printer settings.</summary>
    /// <param name="printer">The printer name.</param>
    /// <param name="settings">The merged DEVMODE.</param>
    /// <returns>The device context, or zero on failure.</returns>
    private static unsafe nint CreateDeviceContext(string printer, byte[] settings)
    {
        fixed (byte* devMode = settings)
        {
            return NativeMethods.CreateDC("WINSPOOL", printer, null, devMode);
        }
    }

    /// <summary>Starts a named spooler job.</summary>
    /// <param name="deviceContext">The printer's device context.</param>
    /// <param name="title">The job title.</param>
    /// <returns>The job identifier, or a non-positive error.</returns>
    private static unsafe int StartDocument(nint deviceContext, string title)
    {
        fixed (char* name = title)
        {
            var info = new DocumentInfo((nint)name);
            return NativeMethods.StartDoc(deviceContext, &info);
        }
    }

    /// <summary>Sends a document straight to a printer.</summary>
    /// <param name="filePath">The PDF.</param>
    /// <param name="title">The job title.</param>
    /// <param name="options">The printer and choices.</param>
    /// <param name="cancellationToken">Stops between pages.</param>
    /// <returns>What happened.</returns>
    private async Task<PrintOutcome> SubmitJobAsync(string filePath, string title, PrintJobOptions options, CancellationToken cancellationToken)
    {
        if (BuildSettings(options) is not { } settings)
        {
            return new(false, $"The printer {options.Printer} could not be opened.");
        }

        var deviceContext = CreateDeviceContext(options.Printer, settings);

        if (deviceContext == 0)
        {
            return new(false, $"The printer {options.Printer} is not available.");
        }

        try
        {
            return await PrintDocumentAsync(deviceContext, filePath, title, !options.Colour, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DeleteDeviceContext(deviceContext);
        }
    }

    /// <summary>Prints every page of a document to a printer's device context.</summary>
    /// <param name="deviceContext">The device context.</param>
    /// <param name="filePath">The PDF.</param>
    /// <param name="title">The job title.</param>
    /// <param name="grayscale">Whether to draw in grey.</param>
    /// <param name="cancellationToken">Stops between pages, cancelling the job.</param>
    /// <returns>What happened.</returns>
    private async Task<PrintOutcome> PrintDocumentAsync(nint deviceContext, string filePath, string title, bool grayscale, CancellationToken cancellationToken)
    {
        using var document = _engine.Open(filePath, null);
        var sizes = document.GetPageSizes();
        var job = StartDocument(deviceContext, title);

        if (job <= 0)
        {
            return new(false, "The printer did not accept the job.");
        }

        var device = new Device(
            NativeMethods.GetDeviceCaps(deviceContext, PixelsPerInchX),
            NativeMethods.GetDeviceCaps(deviceContext, PixelsPerInchY),
            NativeMethods.GetDeviceCaps(deviceContext, HorizontalResolution),
            NativeMethods.GetDeviceCaps(deviceContext, VerticalResolution));
        var flags = RenderFlags.Printing | RenderFlags.Annotations | (grayscale ? RenderFlags.Grayscale : RenderFlags.None);
        try
        {
            for (var page = 0; page < sizes.Length; page++)
            {
                await document.PreparePageAsync(page, cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested || NativeMethods.StartPage(deviceContext) <= 0)
                {
                    _ = NativeMethods.AbortDoc(deviceContext);
                    return new(false, "Printing stopped.");
                }

                DrawPage(deviceContext, document, page, sizes[page], device, flags);
                _ = NativeMethods.EndPage(deviceContext);
            }

            return NativeMethods.EndDoc(deviceContext) > 0 ? new(true, string.Empty) : new(false, "The printer did not finish the job.");
        }
        catch
        {
            _ = NativeMethods.AbortDoc(deviceContext);
            throw;
        }
    }

    /// <summary>A printer's resolution and printable area.</summary>
    /// <param name="DpiX">The horizontal resolution.</param>
    /// <param name="DpiY">The vertical resolution.</param>
    /// <param name="Width">The printable width in pixels.</param>
    /// <param name="Height">The printable height in pixels.</param>
    private readonly record struct Device(int DpiX, int DpiY, int Width, int Height);
}
