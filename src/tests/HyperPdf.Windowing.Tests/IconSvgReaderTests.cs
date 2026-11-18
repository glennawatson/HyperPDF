// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Viewer.Generator;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks that an icon saved by an image editor is read at build time: relative and shorthand path commands, simple shapes, and what a bad file reports.</summary>
public sealed class IconSvgReaderTests
{
    /// <summary>The name the test icons are read under.</summary>
    private const string Name = "Sample";

    /// <summary>Relative moves, level and upright lines and a close become absolute lines.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RelativeAndShorthandLinesBecomeAbsolute()
    {
        var path = Normalize("m2,2 h4 v-1 l1 1z");

        await Assert.That(path).IsEqualTo("M 2,2 L 6,2 L 6,1 L 7,2 Z");
    }

    /// <summary>Numbers after a move are lines, and an arc's flags may touch the number after them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatedNumbersAndPackedArcFlagsAreRead()
    {
        var path = Normalize("M0 0 4 0 4 4a1 1 0 011 1");

        await Assert.That(path).IsEqualTo("M 0,0 L 4,0 L 4,4 A 1,1 0 0 1 5,5");
    }

    /// <summary>Quadratic and smooth curves become cubic curves that follow the same line.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task QuadraticAndSmoothCurvesBecomeCubic()
    {
        var path = Normalize("M0,0 Q3,6 6,0 T12,0 M0,0 C0,3 3,3 3,0 S6,-3 6,0");

        await Assert.That(path).IsEqualTo("M 0,0 C 2,4 4,4 6,0 C 8,-4 10,-4 12,0 M 0,0 C 0,3 3,3 3,0 C 3,-3 6,-3 6,0");
    }

    /// <summary>A file with shapes in place of a path gives the same kind of outline, with its title and tone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShapesTitleAndToneAreRead()
    {
        var icon = IconReader.Read(
            Name,
            """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" data-tone="edit">
              <title>A ring over a line.</title>
              <g><circle cx="12" cy="12" r="4"/></g>
              <line x1="4" y1="20" x2="20" y2="20"/>
              <polygon points="1,1 3,1 3,3"/>
              <rect x="5" y="6" width="2" height="3"/>
            </svg>
            """);

        await Assert.That(icon.Error).IsEqualTo(string.Empty);
        await Assert.That(icon.Summary).IsEqualTo("A ring over a line.");
        await Assert.That(icon.Tone).IsEqualTo("edit");
        await Assert.That(icon.Path).IsEqualTo(
            "M 16,12 A 4,4 0 1 1 8,12 A 4,4 0 1 1 16,12 Z M 4,20 L 20,20 M 1,1 L 3,1 L 3,3 Z M 5,6 L 7,6 L 7,9 L 5,9 Z");
    }

    /// <summary>A file with no title, no outline, broken markup or broken path data says what is wrong, which stops the build.</summary>
    /// <param name="svg">The file's text.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("<svg><path d=\"M0,0 L1,1\"/></svg>")]
    [Arguments("<svg><title>Nothing to draw.</title></svg>")]
    [Arguments("<svg><title>Cut short.</title><path d=\"M0,0 L1\"/></svg>")]
    [Arguments("<svg><title>Not closed.</title>")]
    public async Task BadFilesSayWhatIsWrong(string svg)
    {
        var icon = IconReader.Read(Name, svg);

        await Assert.That(icon.Error.Length).IsGreaterThan(0);
    }

    /// <summary>Rewrites path data with the absolute commands the viewer reads.</summary>
    /// <param name="data">The path data.</param>
    /// <returns>The rewritten data.</returns>
    private static string Normalize(string data)
    {
        var output = new StringBuilder();
        new PathNormalizer(output).Append(data);
        return output.ToString().TrimEnd();
    }
}
