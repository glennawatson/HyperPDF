// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpx;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// Runs the codestreams of the JPEG 2000 conformance test suite (T.803, also ISO/IEC 15444-4) through the decoder and checks
/// each reference component against its allowed peak error and mean squared error (clause B.2, Cclass 1 tables C.6 to C.8).
/// The test streams and reference images are an electronic attachment to that standard and are not in this repository. Set
/// <c>HYPERPDF_JPEG2000_CONFORMANCE</c> to the folder that holds the attachment's directories <c>codestreams_profile0</c>,
/// <c>codestreams_profile1</c>, <c>htj2k_bsets_profile0</c>, <c>htj2k_bsets_profile1</c>, <c>reference_class1_profile0</c>,
/// <c>reference_class1_profile1</c> and <c>reference_class1HF_profile1</c>; without it the tests skip. A codestream or reference
/// image that is missing from the folder is skipped, and a test with nothing to check skips. A checkout of the OpenJPEG test data
/// (uclouvain/openjpeg-data, with <c>input/conformance</c> and <c>baseline/conformance</c>) works as the folder too; it holds
/// the Part 1 codestreams and references but not the HTJ2K sets or the Class 1HF files. Keep such data outside the repository.
/// </summary>
public sealed class JpxConformanceTests
{
    /// <summary>The environment variable that names the folder of the conformance attachment.</summary>
    private const string FolderVariable = "HYPERPDF_JPEG2000_CONFORMANCE";

    /// <summary>Where a copy of the OpenJPEG test data (uclouvain/openjpeg-data) keeps the conformance codestreams, below the folder.</summary>
    private const string OpenJpegStreams = "input/conformance";

    /// <summary>Where a copy of the OpenJPEG test data keeps the reference images, below the folder.</summary>
    private const string OpenJpegReferences = "baseline/conformance";

    /// <summary>The resource name of the tolerance table.</summary>
    private const string ToleranceResource = "HyperPdfLibrary.Tests.JpxConformanceTolerances.tsv";

    /// <summary>The suite of table C.6, Class 1 Profile-0.</summary>
    private const string Profile0Suite = "class1p0";

    /// <summary>The suite of table C.7, Class 1 Profile-1.</summary>
    private const string Profile1Suite = "class1p1";

    /// <summary>The suite of table C.8, Class 1HF.</summary>
    private const string HighFidelitySuite = "class1hf";

    /// <summary>The columns of a tolerance row.</summary>
    private const int Columns = 7;

    /// <summary>The column of the codestream name.</summary>
    private const int StreamColumn = 1;

    /// <summary>The column of the component number.</summary>
    private const int ComponentColumn = 2;

    /// <summary>The column of the reference file.</summary>
    private const int ReferenceColumn = 3;

    /// <summary>The column of the resolution reduction.</summary>
    private const int ReductionColumn = 4;

    /// <summary>The column of the allowed peak error.</summary>
    private const int PeakColumn = 5;

    /// <summary>The column of the allowed mean squared error.</summary>
    private const int MseColumn = 6;

    /// <summary>The most magnitude bits per code-block the HT decoder supports, which is the claimed BMAGB limit.</summary>
    private const int MaxMagnitudeBound = 30;

    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits of two bytes.</summary>
    private const int ShortBits = 16;

    /// <summary>The bytes of a 16-bit sample.</summary>
    private const int ShortBytes = 2;

    /// <summary>The bytes of a 32-bit sample.</summary>
    private const int IntBytes = 4;

    /// <summary>The fields of a PGX header: the magic, byte order, sign and depth, width and height.</summary>
    private const int HeaderFields = 5;

    /// <summary>The field of a PGX header that holds the sign and depth.</summary>
    private const int DepthField = 2;

    /// <summary>The fields a sign and depth take when the sign stands alone.</summary>
    private const int SplitSignFields = 2;

    /// <summary>The characters that begin a PGX header.</summary>
    private const string PgxMagic = "PG";

    /// <summary>The slack for rounding in the allowed mean squared error, which the standard gives to three decimals.</summary>
    private const double MseSlack = 1e-9;

    /// <summary>The reference components of table C.6.</summary>
    private const int Profile0Components = 35;

    /// <summary>The reference components of table C.7.</summary>
    private const int Profile1Components = 17;

    /// <summary>The side of the measurement test image.</summary>
    private const int SmallSide = 16;

    /// <summary>The bytes of a pixel the decoder writes for colour images: blue, green, red and alpha.</summary>
    private const int BgraBytes = 4;

    /// <summary>The folder of the JPH test files.</summary>
    private const string JphDirectory = "testfiles_jph";

    /// <summary>The fields of a PGM header before the data: the magic, width, height and maximum value.</summary>
    private const int PgmFields = 4;

    /// <summary>One past the last JPH file that wraps a JP2 test image.</summary>
    private const int JphColourFiles = 10;

    /// <summary>The colour channels compared: a reference may carry an opacity channel after them.</summary>
    private const int ColourChannels = 3;

    /// <summary>The offset of the red byte in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The peak error T.803 allows the JP2 file tests (table G.1).</summary>
    private const int JpxPeakTolerance = 4;

    /// <summary>The depth of the measurement test image.</summary>
    private const int Eight = 8;

    /// <summary>The depth of the signed test image.</summary>
    private const int TwelveBits = 12;

    /// <summary>The unsigned sample 0xFE of the second test image.</summary>
    private const int HighSample = 0xFE;

    /// <summary>The conformance files that need a feature outside what PDF uses, with the reason.</summary>
    private static readonly Dictionary<string, string> UnsupportedFiles = new(StringComparer.Ordinal)
    {
        ["mct1.j2k"] = "Part 2 multiple component transformation (MCT, MCC and MCO markers), which JPXDecode in PDF does not use",
        ["mct2.j2k"] = "Part 2 multiple component transformation (MCT, MCC and MCO markers), which JPXDecode in PDF does not use",
        ["mct3o.j2k"] = "Part 2 multiple component transformation (MCT, MCC and MCO markers), which JPXDecode in PDF does not use",
        ["nlt1.j2k"] = "Part 2 non-linearity transformation (NLT marker), which JPXDecode in PDF does not use",
        ["float.jpx"] = "a pixel format box that reads the samples as half-float, which PDF does not use",
        ["reboxing.jpx"] = "two codestreams in fragment tables with cross-reference boxes, outside the JPX baseline PDF allows",
    };

    /// <summary>The samples of the signed test image: -1, then 7.</summary>
    private static readonly int[] SignedSamples = [-1, 7];

    /// <summary>The samples of the unsigned test image.</summary>
    private static readonly int[] UnsignedSamples = [HighSample, 1];

    /// <summary>A signed 12-bit PGX file of two samples, with the sign as a field of its own.</summary>
    private static readonly byte[] SignedFile = [.. "PG ML - 12 2 1\n"u8, 0xFF, 0xFF, 0x00, 0x07];

    /// <summary>An unsigned 8-bit PGX file of two samples.</summary>
    private static readonly byte[] UnsignedFile = [.. "PG ML +8 2 1\n"u8, 0xFE, 0x01];

    /// <summary>Class 1, Profile-0 codestreams decode within the allowed errors (T.803 table C.6).</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task Class1Profile0CodestreamsMeetTheTolerances() => Verify(Profile0Suite, "codestreams_profile0", "reference_class1_profile0", null);

    /// <summary>Class 1, Profile-1 codestreams decode within the allowed errors (T.803 table C.7).</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task Class1Profile1CodestreamsMeetTheTolerances() => Verify(Profile1Suite, "codestreams_profile1", "reference_class1_profile1", null);

    /// <summary>The Class 1HF codestreams, with both the J2K and HTJ2K versions, decode within the allowed errors (T.803 table C.8).</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task HighFidelityCodestreamsMeetTheTolerances() => Verify(HighFidelitySuite, "codestreams_hifi", "reference_class1HF_profile1", null);

    /// <summary>The HTJ2K versions of the Profile-0 codestreams, at the highest magnitude bound the decoder supports, decode within the errors of table C.6.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task HighThroughputProfile0CodestreamsMeetTheTolerances() => Verify(Profile0Suite, "htj2k_bsets_profile0", "reference_class1_profile0", "ds0");

    /// <summary>The HTJ2K versions of the Profile-1 codestreams, at the highest magnitude bound the decoder supports, decode within the errors of table C.7.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task HighThroughputProfile1CodestreamsMeetTheTolerances() => Verify(Profile1Suite, "htj2k_bsets_profile1", "reference_class1_profile1", "ds1");

    /// <summary>
    /// Every codestream and file of the conformance data decodes, whether or not the folder has a reference image for it. The files
    /// in <see cref="UnsupportedFiles"/> need features outside what PDF uses and are skipped.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryConformanceFileDecodes()
    {
        var folder = Environment.GetEnvironmentVariable(FolderVariable);
        var files = string.IsNullOrEmpty(folder) ? [] : ConformanceFiles(folder);
        if (files.Count == 0)
        {
            Skip.Test($"{FolderVariable} does not name the conformance data (a T.803 attachment or a copy of the OpenJPEG test data).");
            return;
        }

        var failures = new List<string>();
        foreach (var path in files.Where(static file => !UnsupportedFiles.ContainsKey(Path.GetFileName(file))))
        {
            var planes = JpxDecoderTests.DecodePlanes(await File.ReadAllBytesAsync(path));
            if (planes is null || planes.Length == 0)
            {
                failures.Add(Path.GetFileName(path));
            }
        }

        await Assert.That(string.Join(", ", failures)).IsEmpty();
    }

    /// <summary>
    /// The JP2 test files decode to their sRGB reference images within a peak error of 4 (T.803 clause G.4 and table G.1): the
    /// colour boxes, channel definitions, palettes, subsampling and ICC profiles of the files are read as the standard says.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Jp2FilesMatchTheirReferenceImages()
    {
        var folder = Environment.GetEnvironmentVariable(FolderVariable);
        var files = string.IsNullOrEmpty(folder) ? [] : Directory.EnumerateFiles(Path.Combine(folder, "testfiles_jp2"), "file*.jp2").Order(StringComparer.Ordinal).ToList();
        if (files.Count == 0)
        {
            Skip.Test($"{FolderVariable} does not name a T.803 attachment with testfiles_jp2.");
            return;
        }

        var failures = new List<string>();
        foreach (var path in files)
        {
            var number = Path.GetFileNameWithoutExtension(path)["file".Length..];
            var reference = ConformanceTiff.Read(Path.Combine(folder!, "reference_jp2", $"jp2_{number}.tif"));
            var image = reference is null ? null : JpxImageDecoder.Decode(new(reference.Width, reference.Height, Eight, false, false, null, null, null), await File.ReadAllBytesAsync(path), 0);
            failures.AddRange(JudgeColours(Path.GetFileName(path), image, reference));
        }

        await Assert.That(string.Join("; ", failures)).IsEmpty();
    }

    /// <summary>
    /// The JPH files that wrap the JP2 test images as HTJ2K (file1 to file9, the highest magnitude bound the decoder supports) decode to the
    /// same reference images within the same peak error (T.803 clause G.5.2).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JphFilesMatchTheirReferenceImages()
    {
        var folder = Environment.GetEnvironmentVariable(FolderVariable);
        var directory = string.IsNullOrEmpty(folder) ? string.Empty : Path.Combine(folder, JphDirectory);
        if (!Directory.Exists(directory))
        {
            Skip.Test($"{FolderVariable} does not name a T.803 attachment with {JphDirectory}.");
            return;
        }

        var failures = new List<string>();
        for (var number = 1; number < JphColourFiles; number++)
        {
            var path = Directory.EnumerateFiles(directory, $"file{number}_b*.jph")
                .Where(static file => BoundOf(Path.GetFileNameWithoutExtension(file)) <= MaxMagnitudeBound)
                .MaxBy(static file => BoundOf(Path.GetFileNameWithoutExtension(file)));
            var reference = ConformanceTiff.Read(Path.Combine(folder!, "reference_jp2", $"jp2_{number}.tif"));
            var image = path is null || reference is null ? null : JpxImageDecoder.Decode(HeaderFor(reference), await File.ReadAllBytesAsync(path), 0);
            failures.AddRange(JudgeColours($"file{number}.jph", image, reference));
        }

        await Assert.That(string.Join("; ", failures)).IsEmpty();
    }

    /// <summary>
    /// The JPH file with five components (file10) has no colour box, application-defined colour channel definitions and an opacity
    /// channel; its lossless version decodes every component to its reference image exactly (T.803 clause G.5.2).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JphFileWithApplicationColoursDecodesEveryComponent()
    {
        var folder = Environment.GetEnvironmentVariable(FolderVariable);
        var path = string.IsNullOrEmpty(folder) ? string.Empty : Path.Combine(folder, JphDirectory, "file10_b12.jph");
        if (!File.Exists(path))
        {
            Skip.Test($"{FolderVariable} does not name a T.803 attachment with {JphDirectory}/file10_b12.jph.");
            return;
        }

        var planes = JpxDecoderTests.DecodePlanes(await File.ReadAllBytesAsync(path));
        await Assert.That(planes).IsNotNull();
        for (var c = 0; c < planes!.Length; c++)
        {
            var reference = ReadPnm(Path.Combine(folder!, "reference_jph", $"jph_10_{c}.pgm"));
            await Assert.That(planes[c].AsSpan().SequenceEqual(reference)).IsTrue().Because($"component {c} of file10 matches its reference");
        }
    }

    /// <summary>
    /// The JPX file with two codestreams and two compositing layers (multi_img.jpx) decodes its first codestream, the first layer, to its
    /// first reference image, as the JPX baseline asks of a reader that shows one image (ISO 15444-2 M.9.2.2).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JpxFileWithTwoLayersShowsTheFirst()
    {
        var folder = Environment.GetEnvironmentVariable(FolderVariable);
        var path = string.IsNullOrEmpty(folder) ? string.Empty : Path.Combine(folder, "testfiles_jpx", "multi_img.jpx");
        if (!File.Exists(path))
        {
            Skip.Test($"{FolderVariable} does not name a T.803 attachment with testfiles_jpx/multi_img.jpx.");
            return;
        }

        var reference = ConformanceTiff.Read(Path.Combine(folder!, "reference_jpx", "multi_img-0.tif"));
        var image = reference is null ? null : JpxImageDecoder.Decode(HeaderFor(reference), await File.ReadAllBytesAsync(path), 0);

        await Assert.That(string.Join("; ", JudgeColours("multi_img.jpx", image, reference))).IsEmpty();
    }

    /// <summary>The tolerance table lists every reference component of tables C.6 and C.7.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ToleranceTableHoldsTheStandardsRows()
    {
        await Assert.That(ReadTolerances(Profile0Suite).Count).IsEqualTo(Profile0Components);
        await Assert.That(ReadTolerances(Profile1Suite).Count).IsEqualTo(Profile1Components);
    }

    /// <summary>The measurement accepts a lossless decode against its own samples and reports a reference that differs by more than the allowed peak.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeasurementComparesPeakAndMeanSquaredError()
    {
        var options = new JpxTestOptions { Width = SmallSide, Height = SmallSide };
        var plane = JpxDecoderTests.Planes(options)[0];
        var stream = JpxTestEncoder.Encode(options, [plane]);
        var exact = new PgxImage(Eight, false, SmallSide, SmallSide, plane);
        var off = new PgxImage(Eight, false, SmallSide, SmallSide, [.. plane.Select(static sample => sample + 1)]);
        var strict = new Tolerance("t", 0, string.Empty, 0, 0, 0);
        var loose = strict with { Peak = 1, Mse = 1 };

        await Assert.That(MeasureComponent(stream, strict, exact)).IsEmpty();
        await Assert.That(MeasureComponent(stream, strict, off).Length).IsEqualTo(1);
        await Assert.That(MeasureComponent(stream, loose, off)).IsEmpty();
    }

    /// <summary>A PGX file reads with its depth, sign, size and big-endian samples, including a sign written as a field of its own.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PgxHeaderAndSamplesAreRead()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pgx-{Guid.NewGuid():N}.pgx");
        try
        {
            await File.WriteAllBytesAsync(path, SignedFile);
            var signed = ReadPgx(path);
            await File.WriteAllBytesAsync(path, UnsignedFile);
            var unsigned = ReadPgx(path);

            await Assert.That(signed).IsNotNull();
            await Assert.That(signed!.Depth).IsEqualTo(TwelveBits);
            await Assert.That(signed.Signed).IsTrue();
            await Assert.That(signed.Samples).IsEquivalentTo(SignedSamples);
            await Assert.That(unsigned!.Signed).IsFalse();
            await Assert.That(unsigned.Samples).IsEquivalentTo(UnsignedSamples);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Reads a reference image in the PGX format (T.803 B.2.6).</summary>
    /// <param name="path">The file.</param>
    /// <returns>The image, or <see langword="null"/> when the header is damaged.</returns>
    internal static PgxImage? ReadPgx(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var end = Array.IndexOf(bytes, (byte)'\n');
        if (end < 0)
        {
            return null;
        }

        var fields = System.Text.Encoding.ASCII.GetString(bytes, 0, end).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length < HeaderFields || fields[0] != PgxMagic)
        {
            return null;
        }

        // The sign may be a field of its own, followed by the depth: "PG ML + 8 128 128".
        var separate = fields[DepthField] is "+" or "-";
        var depthText = separate ? fields[DepthField] + fields[DepthField + 1] : fields[DepthField];
        var size = DepthField + (separate ? SplitSignFields : 1);
        if (fields.Length < size + ShortBytes || !TryReadHeader(depthText, fields[size], fields[size + 1], out var depth, out var width, out var height))
        {
            return null;
        }

        var signed = depthText.StartsWith('-');
        return new(depth, signed, width, height, ReadSamples(bytes.AsSpan(end + 1), depth, signed, fields[1] == "LM", width * height));
    }

    /// <summary>Decodes a component of a codestream and measures its error against a reference.</summary>
    /// <param name="stream">The codestream bytes.</param>
    /// <param name="row">The tolerance row.</param>
    /// <param name="reference">The reference image.</param>
    /// <returns>A description of the failure, or nothing when the errors are within the tolerance.</returns>
    internal static string[] MeasureComponent(byte[] stream, Tolerance row, PgxImage reference) => Measure(stream, row, reference);

    /// <summary>Parses the depth, width and height fields of a PGX header.</summary>
    /// <param name="depthText">The depth with its sign.</param>
    /// <param name="widthText">The width.</param>
    /// <param name="heightText">The height.</param>
    /// <param name="depth">Receives the depth.</param>
    /// <param name="width">Receives the width.</param>
    /// <param name="height">Receives the height.</param>
    /// <returns><see langword="true"/> when all three are numbers.</returns>
    private static bool TryReadHeader(string depthText, string widthText, string heightText, out int depth, out int width, out int height)
    {
        width = 0;
        height = 0;
        return int.TryParse(depthText.TrimStart('+', '-'), CultureInfo.InvariantCulture, out depth)
            && int.TryParse(widthText, CultureInfo.InvariantCulture, out width)
            && int.TryParse(heightText, CultureInfo.InvariantCulture, out height);
    }

    /// <summary>Gets the bytes of a PGX sample: one for 1 to 8 bits, two for 9 to 16 and four above.</summary>
    /// <param name="depth">The bits per sample.</param>
    /// <returns>The bytes.</returns>
    private static int SampleBytes(int depth)
    {
        if (depth <= ByteBits)
        {
            return 1;
        }

        return depth <= ShortBits ? ShortBytes : IntBytes;
    }

    /// <summary>Reads the samples of a PGX file: big-endian ("ML"), or little-endian ("LM") as the Class 1HF references are.</summary>
    /// <param name="data">The bytes after the header line.</param>
    /// <param name="depth">The bits per sample.</param>
    /// <param name="signed">Whether the samples are two's complement.</param>
    /// <param name="littleEndian">Whether the least significant byte comes first.</param>
    /// <param name="count">The samples.</param>
    /// <returns>The samples, with zeros after the end of short data.</returns>
    private static int[] ReadSamples(ReadOnlySpan<byte> data, int depth, bool signed, bool littleEndian, int count)
    {
        var size = SampleBytes(depth);
        var extend = ByteBits * (IntBytes - size);
        var samples = new int[count];
        for (var i = 0; i < count && ((i + 1) * size) <= data.Length; i++)
        {
            var value = 0;
            for (var b = 0; b < size; b++)
            {
                value = (value << ByteBits) | data[(i * size) + (littleEndian ? size - 1 - b : b)];
            }

            samples[i] = signed ? (value << extend) >> extend : value;
        }

        return samples;
    }

    /// <summary>Reads the tolerance rows of a suite.</summary>
    /// <param name="suite">The suite column value.</param>
    /// <returns>The rows.</returns>
    /// <exception cref="InvalidDataException">The table is missing from the test assembly.</exception>
    private static List<Tolerance> ReadTolerances(string suite)
    {
        using var stream = typeof(JpxConformanceTests).Assembly.GetManifestResourceStream(ToleranceResource)
            ?? throw new InvalidDataException("The conformance tolerances are missing from the test assembly.");
        using var reader = new StreamReader(stream);
        var rows = new List<Tolerance>();
        while (reader.ReadLine() is { } line)
        {
            var fields = line.Split('\t');
            if (line.Length == 0 || line[0] == '#' || fields.Length != Columns || fields[0] != suite)
            {
                continue;
            }

            rows.Add(new(
                fields[StreamColumn],
                int.Parse(fields[ComponentColumn], CultureInfo.InvariantCulture),
                fields[ReferenceColumn],
                int.Parse(fields[ReductionColumn], CultureInfo.InvariantCulture),
                int.Parse(fields[PeakColumn], CultureInfo.InvariantCulture),
                double.Parse(fields[MseColumn], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    /// <summary>
    /// Finds a codestream file. For the HTJ2K sets it is the one of the set with the largest magnitude bound the decoder supports,
    /// named like <c>ds0_ht_04_b12.j2k</c> in the folder <c>p0_04_bset</c>.
    /// </summary>
    /// <param name="folder">The attachment folder.</param>
    /// <param name="directory">The codestream directory.</param>
    /// <param name="stream">The codestream name from the table, such as <c>p0_04</c>.</param>
    /// <param name="derivedSet">The derived set prefix, such as <c>ds0</c>, or <see langword="null"/> for J2K codestreams.</param>
    /// <returns>The path, or <see langword="null"/> when the file is not there.</returns>
    private static string? Locate(string folder, string directory, string stream, string? derivedSet)
    {
        if (derivedSet is null)
        {
            return FirstExisting(Path.Combine(folder, directory, $"{stream}.j2k"), Path.Combine(folder, OpenJpegStreams, $"{stream}.j2k"));
        }

        var set = Path.Combine(folder, directory, $"{stream}_bset");
        if (!Directory.Exists(set))
        {
            return null;
        }

        var number = stream[(stream.IndexOf('_') + 1)..];
        return Directory.EnumerateFiles(set, $"{derivedSet}_h?_{number}_b*.j2k")
            .Where(static path => BoundOf(Path.GetFileNameWithoutExtension(path)) <= MaxMagnitudeBound)
            .MaxBy(static path => BoundOf(Path.GetFileNameWithoutExtension(path)));
    }

    /// <summary>Makes the image header the JP2 file tests decode with: the reference's size and no colour space.</summary>
    /// <param name="reference">The reference image.</param>
    /// <returns>The header.</returns>
    private static ImageHeader HeaderFor(ConformanceTiff.TiffImage reference) => new(reference.Width, reference.Height, Eight, false, false, null, null, null);

    /// <summary>Reads the samples of an 8-bit binary PGM file ("P5").</summary>
    /// <param name="path">The file.</param>
    /// <returns>The samples.</returns>
    private static int[] ReadPnm(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var position = 0;
        for (var fields = 0; fields < PgmFields; fields++)
        {
            while (char.IsWhiteSpace((char)bytes[position]))
            {
                position++;
            }

            while (!char.IsWhiteSpace((char)bytes[position]))
            {
                position++;
            }
        }

        return [.. bytes.AsSpan(position + 1).ToArray().Select(static value => (int)value)];
    }

    /// <summary>Compares a decoded image with its reference, as 8-bit colour.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="image">The decoded image, or <see langword="null"/>.</param>
    /// <param name="reference">The reference, or <see langword="null"/>.</param>
    /// <returns>The failure, or nothing.</returns>
    private static string[] JudgeColours(string name, PdfImageData? image, ConformanceTiff.TiffImage? reference)
    {
        if (reference is null || image is null || image.UnsupportedCodec != PdfImageCodec.None)
        {
            return [$"{name}: not decoded or no reference"];
        }

        var step = image.IsGray ? 1 : BgraBytes;
        if (image.Width != reference.Width || image.Height != reference.Height || image.Pixels.Length < reference.Width * reference.Height * step)
        {
            return [$"{name}: decoded {image.Width}x{image.Height} with {image.Pixels.Length} bytes, reference {reference.Width}x{reference.Height} with {reference.Components} components"];
        }

        var peak = PeakError(image, reference, step);
        return peak <= JpxPeakTolerance ? [] : [$"{name}: peak {peak} (allowed {JpxPeakTolerance})"];
    }

    /// <summary>Measures the largest difference between a decoded image and its reference, as 8-bit samples.</summary>
    /// <param name="image">The decoded image, which has the reference's size.</param>
    /// <param name="reference">The reference.</param>
    /// <param name="step">The bytes of a decoded pixel.</param>
    /// <returns>The peak error.</returns>
    private static int PeakError(PdfImageData image, ConformanceTiff.TiffImage reference, int step)
    {
        var gray = reference.Components == 1;
        var shift = reference.Bits - Eight;
        var peak = 0;
        for (var i = 0; i < reference.Width * reference.Height; i++)
        {
            for (var c = 0; c < Math.Min(reference.Components, ColourChannels); c++)
            {
                var expected = reference.Values[(i * reference.Components) + c] >> shift;
                var decoded = image.Pixels[(i * step) + (gray ? 0 : RedOffset - c)];
                peak = Math.Max(peak, Math.Abs(expected - decoded));
            }
        }

        return peak;
    }

    /// <summary>Lists the codestreams and files of the conformance data, in the T.803 attachment layout and in the OpenJPEG layout.</summary>
    /// <param name="folder">The data folder.</param>
    /// <returns>The paths, in order.</returns>
    private static List<string> ConformanceFiles(string folder)
    {
        string[] directories = ["codestreams_profile0", "codestreams_profile1", "codestreams_hifi", "codestreams_extended", "testfiles_jp2", "testfiles_jph", "testfiles_jpx", OpenJpegStreams];
        var files = new List<string>();
        foreach (var directory in directories.Select(name => Path.Combine(folder, name)).Where(Directory.Exists))
        {
            files.AddRange(Directory.EnumerateFiles(directory).Where(static file => Path.GetExtension(file) is ".j2k" or ".j2c" or ".jp2" or ".jph" or ".jpx"));
        }

        foreach (var directory in new[] { "htj2k_bsets_profile0", "htj2k_bsets_profile1" }.Select(name => Path.Combine(folder, name)).Where(Directory.Exists))
        {
            files.AddRange(Directory.EnumerateFiles(directory, "*.j2k", SearchOption.AllDirectories));
        }

        return [.. files.Order(StringComparer.Ordinal)];
    }

    /// <summary>Gets the first of some paths that is a file.</summary>
    /// <param name="paths">The candidate paths.</param>
    /// <returns>The path, or <see langword="null"/> when none exists.</returns>
    private static string? FirstExisting(params ReadOnlySpan<string> paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>Gets the name an OpenJPEG test data copy gives a reference: the component number follows an underscore, not a hyphen.</summary>
    /// <param name="reference">The T.803 reference file name, such as <c>c1p0_04-1.pgx</c>.</param>
    /// <returns>The name, such as <c>c1p0_04_1.pgx</c>.</returns>
    private static string UnderscoreName(string reference)
    {
        var hyphen = reference.LastIndexOf('-');
        return hyphen < 0 ? reference : string.Concat(reference.AsSpan(0, hyphen), "_", reference.AsSpan(hyphen + 1));
    }

    /// <summary>Reads the magnitude bound from the end of a codestream name such as <c>ds0_ht_04_b12</c>.</summary>
    /// <param name="name">The name without its extension.</param>
    /// <returns>The bound, or -1 when the name has none.</returns>
    private static int BoundOf(string name)
    {
        var marker = name.LastIndexOf("_b", StringComparison.Ordinal);
        return marker >= 0 && int.TryParse(name.AsSpan(marker + "_b".Length), CultureInfo.InvariantCulture, out var bound) ? bound : -1;
    }

    /// <summary>Decodes a component of a codestream and measures its error against a reference.</summary>
    /// <param name="stream">The codestream bytes.</param>
    /// <param name="row">The tolerance row.</param>
    /// <param name="reference">The reference image.</param>
    /// <returns>A description of the failure, or nothing when the errors are within the tolerance.</returns>
    private static string[] Measure(byte[] stream, Tolerance row, PgxImage reference)
    {
        if (JpxCodestream.Read(stream) is not { } codestream || row.Component >= codestream.Geometry.Components.Length)
        {
            return [$"{row.Stream} component {row.Component}: the codestream was not accepted"];
        }

        using var image = JpxDecoder.Decode(codestream, stream, row.Reduction, CancellationToken.None);
        var area = image.Areas[row.Component];
        if (area.Width < reference.Width || area.Height < reference.Height)
        {
            return [$"{row.Stream} component {row.Component}: decoded {area.Width}x{area.Height}, reference {reference.Width}x{reference.Height}"];
        }

        var info = codestream.Geometry.Components[row.Component];
        var shift = Math.Max(info.Precision - reference.Depth, 0);
        var low = info.Signed ? -(1 << (info.Precision - 1)) : 0;
        var high = low + (1 << info.Precision) - 1;
        long peak = 0;
        double squares = 0;
        for (var y = 0; y < reference.Height; y++)
        {
            for (var x = 0; x < reference.Width; x++)
            {
                var decoded = Math.Clamp(image.Planes[row.Component][(y * area.Width) + x], low, high) >> shift;
                var difference = Math.Abs((long)reference.Samples[(y * reference.Width) + x] - decoded);
                peak = Math.Max(peak, difference);
                squares += (double)difference * difference;
            }
        }

        var mse = squares / ((double)reference.Width * reference.Height);
        return peak <= row.Peak && mse <= row.Mse + MseSlack ? [] : [$"{row.Stream} component {row.Component}: peak {peak} (allowed {row.Peak}), MSE {mse:0.###} (allowed {row.Mse})"];
    }

    /// <summary>Checks every reference component of a suite that the folder holds.</summary>
    /// <param name="suite">The suite in the tolerance table.</param>
    /// <param name="streams">The codestream directory.</param>
    /// <param name="references">The reference image directory.</param>
    /// <param name="derivedSet">The HTJ2K derived set prefix, or <see langword="null"/> for J2K codestreams.</param>
    /// <returns>A task.</returns>
    private static async Task Verify(string suite, string streams, string references, string? derivedSet)
    {
        var folder = Environment.GetEnvironmentVariable(FolderVariable);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            Skip.Test($"The JPEG 2000 conformance test files (the electronic attachment of T.803) are not available. Set {FolderVariable} to the folder that holds them.");
            return;
        }

        var failures = new List<string>();
        var checkedComponents = 0;
        foreach (var row in ReadTolerances(suite))
        {
            var codestream = Locate(folder, streams, row.Stream, derivedSet);
            var referencePath = FirstExisting(
                Path.Combine(folder, references, row.Reference),
                Path.Combine(folder, OpenJpegReferences, row.Reference),
                Path.Combine(folder, OpenJpegReferences, UnderscoreName(row.Reference)));
            if (codestream is null || referencePath is null || ReadPgx(referencePath) is not { } reference)
            {
                continue;
            }

            checkedComponents++;
            failures.AddRange(Measure(await File.ReadAllBytesAsync(codestream), row, reference));
        }

        if (checkedComponents == 0)
        {
            Skip.Test($"{FolderVariable} has none of the {streams} codestreams with their reference images in {references}.");
            return;
        }

        await Assert.That(string.Join("; ", failures)).IsEmpty();
    }

    /// <summary>A reference image: the depth, signedness, size and samples.</summary>
    /// <param name="Depth">The bits per sample.</param>
    /// <param name="Signed">Whether the samples are signed.</param>
    /// <param name="Width">The width.</param>
    /// <param name="Height">The height.</param>
    /// <param name="Samples">The samples, row by row.</param>
    internal sealed record PgxImage(int Depth, bool Signed, int Width, int Height, int[] Samples);

    /// <summary>One reference component and the errors the standard allows for it.</summary>
    /// <param name="Stream">The codestream name.</param>
    /// <param name="Component">The component.</param>
    /// <param name="Reference">The reference file name.</param>
    /// <param name="Reduction">The resolution levels to skip.</param>
    /// <param name="Peak">The allowed peak error.</param>
    /// <param name="Mse">The allowed mean squared error.</param>
    internal sealed record Tolerance(string Stream, int Component, string Reference, int Reduction, int Peak, double Mse);
}
