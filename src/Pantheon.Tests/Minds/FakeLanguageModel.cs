using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pantheon.Game.Minds;

namespace Pantheon.Tests.Minds;

/// <summary>A language model that answers from a script, for tests (ADR-065).</summary>
internal sealed class FakeLanguageModel : ILanguageModel
{
    private readonly Func<ModelQuestion, ModelAnswer> _answer;

    /// <summary>Creates the model.</summary>
    /// <param name="answer">What it answers each question.</param>
    /// <param name="name">Its name.</param>
    public FakeLanguageModel(Func<ModelQuestion, ModelAnswer> answer, string name = "Fake Model")
    {
        _answer = answer;
        Name = name;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>The questions it was asked, in order.</summary>
    public List<ModelQuestion> Asked { get; } = [];

    /// <inheritdoc />
    public Task<ModelAnswer> Ask(ModelQuestion question, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        Asked.Add(question);
        return Task.FromResult(_answer(question));
    }
}
