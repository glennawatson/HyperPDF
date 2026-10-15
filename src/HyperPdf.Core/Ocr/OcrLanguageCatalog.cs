// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Ocr;

/// <summary>
/// The language packs offered for text recognition: Tesseract's fast models from the <c>tessdata_fast</c> 4.1.0 release
/// on GitHub. The release is pinned so each file's size and SHA-256 stay fixed. English comes first, then the rest by name.
/// </summary>
public static class OcrLanguageCatalog
{
    /// <summary>The path of the packs' folder on the host without a leading slash, which a pack's request path continues.</summary>
    internal const string RequestBase = "tesseract-ocr/tessdata_fast/4.1.0/";

    /// <summary>The host the packs are downloaded from, with a trailing slash.</summary>
    private const string Host = "https://raw.githubusercontent.com/";

    /// <summary>The language used when none is chosen.</summary>
    private const string English = "eng";

    /// <summary>The character Tesseract puts between languages, as in <c>eng+deu</c>.</summary>
    private const char LanguageSeparator = '+';

    /// <summary>Bytes in a megabyte.</summary>
    private const long BytesPerMegabyte = 1024 * 1024;

    /// <summary>Each pack's code, name, size and SHA-256, measured from the pinned release.</summary>
    private static readonly (string Code, string Name, long Bytes, string Sha256)[] Table =
    [
        ("eng", "English", 4_113_088, "7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2"),
        ("ara", "Arabic", 1_432_056, "e3206d3dc87fd50c24a0fb9f01838615911d25168f4e64415244b67d2bb3e729"),
        ("ben", "Bengali", 855_841, "31163084c279aaebd376216f0c3d5c17ad4b5fee8db49dae79c20000b5de5964"),
        ("chi_sim", "Chinese (simplified)", 2_469_156, "a5fcb6f0db1e1d6d8522f39db4e848f05984669172e584e8d76b6b3141e1f730"),
        ("chi_tra", "Chinese (traditional)", 2_366_642, "529c5b5797d64b126065cd55f2bb4c7fd7b15790798091b1ff259941a829330b"),
        ("ces", "Czech", 3_795_684, "934bcaf97ef3348413263331131c9fa7f55f30db333c711929c124fb635f7e1b"),
        ("dan", "Danish", 2_580_059, "acb1fd074487a31d1294fcdfd7d7c673467ffd8aeacb2ccd61ebcbf04eb4e2fa"),
        ("nld", "Dutch", 6_050_296, "ced0e5e046a84c908a6aa7accbef9a232c4a5d9a8276691b81c6ee64d02963f6"),
        ("fin", "Finnish", 7_865_732, "61a04cd62b507c3d9ae0e1cda399e6715ebf49dea9df47897c8acdcd3bd3e13c"),
        ("fra", "French", 1_130_365, "ced037562e8c80c13122dece28dd477d399af80911a28791a66a63ac1e3445ca"),
        ("deu", "German", 1_525_436, "19d219bbb6672c869d20a9636c6816a81eb9a71796cb93ebe0cb1530e2cdb22d"),
        ("ell", "Greek", 1_419_514, "4fba8a0b461038d51f1c20d043d4f2ac38c4e778f1b90830847f7bd8fa3ba726"),
        ("heb", "Hebrew", 961_404, "11f9e43ab227f786352a50f75c94c2e9906f1baba86d93276da19da7ce0904db"),
        ("hin", "Hindi", 1_122_751, "4c73ffc59d497c186b19d1e90f5d721d678ea6b2e277b719bee4e2af12271825"),
        ("hun", "Hungarian", 5_296_273, "35067e7cfe102dcdc953f9a758fdfaa6296b17a1ee6d874ee780fa306430b9fb"),
        ("ind", "Indonesian", 1_122_661, "69786901da87ab8766c1ea7fbb10b28f2110c14da3f6c8f2735df131fba95d88"),
        ("ita", "Italian", 2_701_314, "b8f89e1e785118dac4d51ae042c029a64edb5c3ee42ef73027a6d412748d8827"),
        ("jpn", "Japanese", 2_471_260, "1f5de9236d2e85f5fdf4b3c500f2d4926f8d9449f28f5394472d9e8d83b91b4d"),
        ("kor", "Korean", 1_677_415, "6b85e11d9bbf07863b97b3523b1b112844c43e713df8b66418a081fd1060b3b2"),
        ("nor", "Norwegian", 3_610_079, "0451eb4f8049ae78196806bf878a389a2f40f1386fe038568cf4441226ba6ef2"),
        ("pol", "Polish", 4_765_518, "c4476cdbc0e33d898d32345122b7be1cbf85ace15f920f06c7714756e1ef79b2"),
        ("por", "Portuguese", 1_982_756, "c4932b937207a9514b7514d518b931a99938c02a28a5a5a553f8599ed58b7deb"),
        ("ron", "Romanian", 2_376_323, "9adfde6b51ba4b97efd10ea37c3070fd3fc2bad7815e81f5c3c198cd96216cc9"),
        ("rus", "Russian", 3_861_738, "e16e5e036cce1d9ec2b00063cf8b54472625b9e14d893a169e2b0dedeb4df225"),
        ("spa", "Spanish", 2_294_433, "6f2e04d02774a18f01bed44b1111f2cd7f3ba7ac9dc4373cd3f898a40ea6b464"),
        ("swe", "Swedish", 4_167_034, "f7304988d41f833efebcc2d529df54b1903ecebbc3da1faabd19a0fddd4fe586"),
        ("tha", "Thai", 1_072_600, "294227cc2d1292b0acb28d61d4115c88252b96d466ca90b417cf4cf0c67bf07c"),
        ("tur", "Turkish", 4_550_554, "7393381111e1152420fc4092cb44eef4237580d21b92bf30d7d221aad192c6b7"),
        ("ukr", "Ukrainian", 3_825_102, "d59e53e2bded32f4445f124b4b00240fcac7e8044c003ab822ccb94f0b3db59b"),
        ("vie", "Vietnamese", 531_275, "79df64caf7bcfb2a27df5042ecb6121e196eada34da774956995747636d5bfa1"),
    ];

    /// <summary>Gets the language used when none is chosen: English, <c>eng</c>.</summary>
    public static string DefaultLanguage => English;

    /// <summary>Gets the folder the packs are downloaded from, ending in a slash.</summary>
    public static Uri BaseAddress { get; } = new(Host + RequestBase);

    /// <summary>Gets every pack offered, English first.</summary>
    public static IReadOnlyList<OcrLanguagePack> Packs { get; } = Array.ConvertAll(Table, static row => new OcrLanguagePack(row.Code, row.Name, row.Bytes, row.Sha256));

    /// <summary>Finds a pack by its language code.</summary>
    /// <param name="code">The code, for example <c>deu</c>.</param>
    /// <returns>The pack, or <see langword="null"/> when it is not offered.</returns>
    public static OcrLanguagePack? Find(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        foreach (var pack in Packs)
        {
            if (string.Equals(pack.Code, code, StringComparison.Ordinal))
            {
                return pack;
            }
        }

        return null;
    }

    /// <summary>Splits a language setting such as <c>eng+deu</c> into its codes, dropping blanks and repeats.</summary>
    /// <param name="languages">The setting.</param>
    /// <returns>The codes in their order; English alone when the setting names none.</returns>
    public static List<string> Parse(string? languages)
    {
        var codes = new List<string>();
        var text = (languages ?? string.Empty).AsSpan();

        // Each code is cut out of the setting only when it is new, so blanks and repeats make no strings.
        foreach (var range in text.Split(LanguageSeparator))
        {
            var part = text[range].Trim();
            if (!part.IsEmpty && !Contains(codes, part))
            {
                codes.Add(CodeOf(part));
            }
        }

        if (codes.Count == 0)
        {
            codes.Add(English);
        }

        return codes;
    }

    /// <summary>Joins language codes into the form Tesseract takes, for example <c>eng+deu</c>.</summary>
    /// <param name="codes">The codes.</param>
    /// <returns>The setting; English when there are no codes.</returns>
    public static string Format(IReadOnlyList<string> codes)
    {
        ArgumentNullException.ThrowIfNull(codes);
        return codes.Count == 0 ? English : string.Join(LanguageSeparator, codes);
    }

    /// <summary>Describes a size for people, rounding up to whole megabytes, for example "about 4 MB".</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The description.</returns>
    public static string DescribeSize(long bytes) => $"about {Math.Max(1, (bytes + BytesPerMegabyte - 1) / BytesPerMegabyte)} MB";

    /// <summary>Gets a code as a string, using the offered pack's own string when it is one, so a known code makes no new string.</summary>
    /// <param name="code">The code as written.</param>
    /// <returns>The code.</returns>
    private static string CodeOf(ReadOnlySpan<char> code)
    {
        for (var i = 0; i < Packs.Count; i++)
        {
            var pack = Packs[i];
            if (pack.Code.AsSpan().SequenceEqual(code))
            {
                return pack.Code;
            }
        }

        return code.ToString();
    }

    /// <summary>Determines whether a list already holds a code.</summary>
    /// <param name="codes">The codes so far.</param>
    /// <param name="code">The code.</param>
    /// <returns><see langword="true"/> when it is there.</returns>
    private static bool Contains(List<string> codes, ReadOnlySpan<char> code)
    {
        foreach (var existing in codes)
        {
            if (existing.AsSpan().SequenceEqual(code))
            {
                return true;
            }
        }

        return false;
    }
}
