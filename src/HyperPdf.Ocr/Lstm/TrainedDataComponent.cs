// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Ocr.Lstm;

/// <summary>The parts a language data file is made of, numbered as the file's table of offsets numbers them.</summary>
public enum TrainedDataComponent
{
    /// <summary>The language configuration.</summary>
    LanguageConfig = 0,

    /// <summary>The character set of the older engine.</summary>
    Unicharset = 1,

    /// <summary>The older engine's ambiguity rules.</summary>
    Ambiguities = 2,

    /// <summary>The older engine's templates.</summary>
    IntegerTemplates = 3,

    /// <summary>The older engine's prototype table.</summary>
    PrototypeTable = 4,

    /// <summary>The older engine's normalisation prototypes.</summary>
    NormalisationPrototypes = 5,

    /// <summary>The older engine's punctuation word list.</summary>
    PunctuationDawg = 6,

    /// <summary>The older engine's word list.</summary>
    SystemDawg = 7,

    /// <summary>The older engine's number patterns.</summary>
    NumberDawg = 8,

    /// <summary>The older engine's frequent words.</summary>
    FrequentDawg = 9,

    /// <summary>The older engine's fixed length words.</summary>
    FixedLengthDawgs = 10,

    /// <summary>The character set of the removed Cube engine.</summary>
    CubeUnicharset = 11,

    /// <summary>The word list of the removed Cube engine.</summary>
    CubeSystemDawg = 12,

    /// <summary>The older engine's shape table.</summary>
    ShapeTable = 13,

    /// <summary>The older engine's word pair list.</summary>
    BigramDawg = 14,

    /// <summary>The older engine's unambiguous words.</summary>
    UnambiguousDawg = 15,

    /// <summary>The older engine's parameter model.</summary>
    ParametersModel = 16,

    /// <summary>The recurrent network and its settings.</summary>
    Lstm = 17,

    /// <summary>The network's punctuation word list.</summary>
    LstmPunctuationDawg = 18,

    /// <summary>The network's word list.</summary>
    LstmSystemDawg = 19,

    /// <summary>The network's number patterns.</summary>
    LstmNumberDawg = 20,

    /// <summary>The network's character set, as text.</summary>
    LstmUnicharset = 21,

    /// <summary>The table turning the network's outputs into characters.</summary>
    LstmRecoder = 22,

    /// <summary>The version text of the file.</summary>
    Version = 23,
}
