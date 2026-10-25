// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Ocr.Lstm;

namespace HyperPdf.Ocr.Tests;

/// <summary>Checks the model loaded from the English data shipped beside the tests.</summary>
public sealed class LstmModelTests
{
    /// <summary>The height of picture the English model reads.</summary>
    private const int ModelHeight = 36;

    /// <summary>The number the space has in every character set.</summary>
    private const int SpaceId = 0;

    /// <summary>The pixels across an output step of the English model.</summary>
    private const int StepWidth = 3;

    /// <summary>The model reads the shape, characters and codes from the file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LoadsTheShippedEnglishModel()
    {
        var model = LoadEnglish();

        await Assert.That(model.Shape.Height).IsEqualTo(ModelHeight);
        await Assert.That(model.Shape.Depth).IsEqualTo(1);
        await Assert.That(model.StepWidth).IsEqualTo(StepWidth);
        await Assert.That(model.Characters[SpaceId]).IsEqualTo(" ");
        await Assert.That(model.Characters.Count).IsGreaterThan(1);
        await Assert.That(model.Coder.CodeRange).IsGreaterThan(model.BlankCode);
    }

    /// <summary>A blank line gives only blanks, so nothing is read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ABlankLineReadsAsNothing()
    {
        var model = LoadEnglish();
        var blank = new byte[ModelHeight * ModelHeight * StepWidth];
        Array.Fill(blank, byte.MaxValue);

        var scratch = new RecognitionScratch();
        var prepared = LinePreparer.Prepare(blank, ModelHeight * StepWidth, ModelHeight, ModelHeight, scratch);
        var characters = new List<RecognizedCharacter>();
        CtcDecoder.Decode(model.Run(prepared, scratch), model.Coder, model.BlankCode, characters, scratch);

        await Assert.That(characters.Count(static character => character.Id != SpaceId)).IsEqualTo(0);
    }

    /// <summary>Loads the English data from the folder beside the tests.</summary>
    /// <returns>The model.</returns>
    private static LstmModel LoadEnglish()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "tessdata", "eng.traineddata");
        return LstmModel.Load(TrainedData.Open(File.ReadAllBytes(path)));
    }
}
