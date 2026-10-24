// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that the interpreter copies paths for devices that may keep them and lends one reused path to devices that do not.</summary>
public sealed class PathHandoverTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The commands of a rectangle: a move, three lines and a close.</summary>
    private const int RectangleCommands = 5;

    /// <summary>The commands of a line: a move and a line.</summary>
    private const int LineCommands = 2;

    /// <summary>The left edge of the lent rectangle.</summary>
    private const int Left = 1;

    /// <summary>The top edge of the lent rectangle.</summary>
    private const int Top = 2;

    /// <summary>The right edge of the lent rectangle.</summary>
    private const int Right = 11;

    /// <summary>The bottom edge of the lent rectangle.</summary>
    private const int Bottom = 22;

    /// <summary>The paths handed over by the content: a fill, a clip and a stroke.</summary>
    private const int Handovers = 3;

    /// <summary>The page content: a filled rectangle, a rectangular clip and a stroked line.</summary>
    private const string Content = "0 0 10 10 re f 5 5 20 20 re W n 1 2 m 3 4 l S";

    /// <summary>A device that keeps paths gets an independent copy of each, which still holds its commands afterwards.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeviceThatKeepsPathsGetsIndependentCopies()
    {
        var device = Run(true);

        await Assert.That(device.Paths.Count).IsEqualTo(Handovers);
        await Assert.That(device.Paths.Distinct().Count()).IsEqualTo(Handovers);
        await Assert.That(device.Paths.Select(static path => path.Commands.Length)).IsEquivalentTo(device.CommandsDuringCall);
        await Assert.That(device.CommandsDuringCall).IsEquivalentTo([RectangleCommands, RectangleCommands, LineCommands]);
    }

    /// <summary>A device that keeps no paths gets the same reused path each time, holding the right commands during each call.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeviceThatKeepsNoPathsGetsOneReusedPath()
    {
        var device = Run(false);

        await Assert.That(device.Paths.Count).IsEqualTo(Handovers);
        await Assert.That(device.Paths.Distinct().Count()).IsEqualTo(1);
        await Assert.That(device.CommandsDuringCall).IsEquivalentTo([RectangleCommands, RectangleCommands, LineCommands]);
    }

    /// <summary>Lending the commands of a builder empties it, and the lent path shows the bounds of what it held.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LendingEmptiesTheBuilder()
    {
        var builder = new PdfPathBuilder();
        var rectangle = new PdfRect(Left, Top, Right, Bottom);
        builder.AddRect(rectangle);
        var scratch = new PdfPath();

        var lent = builder.Lend(scratch);

        await Assert.That(lent).IsSameReferenceAs(scratch);
        await Assert.That(lent.Commands.Length).IsEqualTo(RectangleCommands);
        await Assert.That(lent.Bounds).IsEqualTo(rectangle);
        await Assert.That(builder.Detach().IsEmpty).IsTrue();
    }

    /// <summary>Runs the content on a device.</summary>
    /// <param name="retains">Whether the device says it keeps paths.</param>
    /// <returns>The device.</returns>
    private static PathDevice Run(bool retains)
    {
        using var document = PdfDocumentReader.Open(new RenderTestPdf(Size, Size) { Content = Content }.ToBytes(), null);
        var device = new PathDevice(retains);
        using var interpreter = ContentInterpreter.Rent(PdfDocumentRendering.GetRenderCache(document), device, 0);
        ContentExecution.RunPage(interpreter, PdfDocumentPages.GetPage(document, 0));
        return device;
    }

    /// <summary>A content device that keeps the paths it is given.</summary>
    /// <param name="retains">What the device reports about keeping paths.</param>
    [DebuggerDisplay("PathDevice: {Paths.Count} paths")]
    private sealed class PathDevice(bool retains) : IContentDevice
    {
        /// <inheritdoc/>
        public bool RetainsPaths => retains;

        /// <summary>Gets the paths received, in order.</summary>
        internal List<PdfPath> Paths { get; } = [];

        /// <summary>Gets how many commands each path held during the call that received it.</summary>
        internal List<int> CommandsDuringCall { get; } = [];

        /// <inheritdoc/>
        public void Fill(PdfPath path, bool evenOdd, ref GraphicsState state) => Keep(path);

        /// <inheritdoc/>
        public void Stroke(PdfPath path, ref GraphicsState state) => Keep(path);

        /// <inheritdoc/>
        public void Clip(PdfPath path, bool evenOdd, Matrix3x2 ctm) => Keep(path);

        /// <inheritdoc/>
        public void Save()
        {
            // Only the paths matter here.
        }

        /// <inheritdoc/>
        public void Restore()
        {
            // Only the paths matter here.
        }

        /// <inheritdoc/>
        public void DrawImage(IPdfRenderImage image, bool isMask, bool smooth, ref GraphicsState state)
        {
            // Only the paths matter here.
        }

        /// <inheritdoc/>
        public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
        {
            // Only the paths matter here.
        }

        /// <inheritdoc/>
        public void PaintShading(PdfShading shading, ref GraphicsState state)
        {
            // Only the paths matter here.
        }

        /// <inheritdoc/>
        public void BeginGroup(in GroupInfo group)
        {
            // Only the paths matter here.
        }

        /// <inheritdoc/>
        public void EndGroup(in GroupInfo group)
        {
            // Only the paths matter here.
        }

        /// <inheritdoc/>
        public void BeginMarkedContent(ReadOnlySpan<byte> tag, PdfDictionary? properties)
        {
            // Only the paths matter here.
        }

        /// <inheritdoc/>
        public void EndMarkedContent()
        {
            // Only the paths matter here.
        }

        /// <inheritdoc/>
        public IPictureDevice CreatePictureDevice(PdfRect cull) => throw new NotSupportedException("The test content records no pictures.");

        /// <summary>Keeps a path and notes what it held now.</summary>
        /// <param name="path">The path.</param>
        private void Keep(PdfPath path)
        {
            Paths.Add(path);
            CommandsDuringCall.Add(path.Commands.Length);
        }
    }
}
