using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Pantheon.Game.Minds;
using Pantheon.Game.Settings;
using Pantheon.Oracle;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Minds;

/// <summary>The language models behind the gods' minds (ADR-065): what Claude and a local model are sent, and how their answers are read.</summary>
public sealed class OracleTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static ModelQuestion Question => MindCheck.Question;

    /// <summary>Claude's answer of some text, as the Messages API sends it.</summary>
    private static string ClaudeMessage(string text, string stop = "end_turn") => new JsonObject
    {
        ["id"] = "msg_test",
        ["type"] = "message",
        ["role"] = "assistant",
        ["model"] = "claude-opus-5-5",
        ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
        ["stop_reason"] = stop,
        ["stop_sequence"] = null,
        ["usage"] = new JsonObject { ["input_tokens"] = 120, ["output_tokens"] = 30 },
    }.ToJsonString();

    [Fact]
    public async Task Claude_IsAskedForJsonOfTheSchema_WithTheKeyTheEffortTheCachedInstructionsAndTheFallback()
    {
        var server = new StubServer(_ => (HttpStatusCode.OK, ClaudeMessage("{\"greeting\":\"Hail, little spirit.\"}")));
        using var model = new AnthropicLanguageModel("sk-ant-test", "claude-opus-5-5", "Claude Opus 5.5", Timeout, new HttpClient(server));

        var answer = await model.Ask(Question, CancellationToken.None);

        Assert.Equal(ModelOutcome.Answered, answer.Outcome);
        Assert.Equal("{\"greeting\":\"Hail, little spirit.\"}", answer.Json);
        Assert.Equal(120, answer.InputTokens);
        var request = Assert.Single(server.Requests);
        Assert.EndsWith("/v1/messages", request.Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("sk-ant-test", request.Header("x-api-key"));
        Assert.Contains(AnthropicLanguageModel.FallbackBeta, request.Header("anthropic-beta"), StringComparison.Ordinal);
        var body = JsonNode.Parse(request.Body)!;
        Assert.Equal("claude-opus-5-5", body["model"]!.GetValue<string>());
        Assert.Equal("default", body["fallbacks"]!.GetValue<string>());
        Assert.Equal("low", body["output_config"]!["effort"]!.GetValue<string>());
        Assert.Equal("json_schema", body["output_config"]!["format"]!["type"]!.GetValue<string>());
        Assert.Equal("greeting", body["output_config"]!["format"]!["schema"]!["required"]![0]!.GetValue<string>());
        Assert.Equal("ephemeral", body["system"]![0]!["cache_control"]!["type"]!.GetValue<string>());
        Assert.Equal(Question.Instructions, body["system"]![0]!["text"]!.GetValue<string>());
        Assert.Equal("user", body["messages"]![0]!["role"]!.GetValue<string>());
        Assert.Null(body["temperature"]);
        Assert.Null(body["thinking"]);
    }

    [Theory]
    [InlineData("refusal", ModelOutcome.Refused)]
    [InlineData("max_tokens", ModelOutcome.CutShort)]
    public async Task ClaudeThatStopsShort_GivesNoAnswer(string stop, ModelOutcome outcome)
    {
        var server = new StubServer(_ => (HttpStatusCode.OK, ClaudeMessage("{\"greet", stop)));
        using var model = new AnthropicLanguageModel("sk-ant-test", "claude-opus-5-5", "Claude Opus 5.5", Timeout, new HttpClient(server));

        var answer = await model.Ask(Question, CancellationToken.None);

        Assert.Equal(outcome, answer.Outcome);
        Assert.Empty(answer.Json);
    }

    [Fact]
    public async Task ClaudeRefusingTheKey_SaysToCheckIt()
    {
        var server = new StubServer(_ => (HttpStatusCode.Unauthorized, "{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\",\"message\":\"invalid x-api-key\"}}"));
        using var model = new AnthropicLanguageModel("sk-ant-wrong", "claude-opus-5-5", "Claude Opus 5.5", Timeout, new HttpClient(server));

        var answer = await model.Ask(Question, CancellationToken.None);

        Assert.Equal(ModelOutcome.Failed, answer.Outcome);
        Assert.Contains("did not accept the key", answer.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ALocalModel_IsAskedForJsonOfTheSchema_AtItsAddress()
    {
        var server = new StubServer(_ => (HttpStatusCode.OK, "{\"model\":\"llama3.2\",\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"greeting\\\":\\\"Well met.\\\"}\"},\"done\":true,\"done_reason\":\"stop\",\"prompt_eval_count\":80,\"eval_count\":12}"));
        using var model = new LocalLanguageModel(new Uri("http://localhost:11434"), "llama3.2", Timeout, new HttpClient(server));

        var answer = await model.Ask(Question, CancellationToken.None);

        Assert.Equal(ModelAnswer.Of("{\"greeting\":\"Well met.\"}", 80, 12), answer);
        var request = Assert.Single(server.Requests);
        Assert.Equal("http://localhost:11434/api/chat", request.Uri.ToString());
        var body = JsonNode.Parse(request.Body)!;
        Assert.Equal("llama3.2", body["model"]!.GetValue<string>());
        Assert.False(body["stream"]!.GetValue<bool>());
        Assert.Equal("object", body["format"]!["type"]!.GetValue<string>());
        Assert.Equal("system", body["messages"]![0]!["role"]!.GetValue<string>());
        Assert.Equal(Question.MaxTokens, body["options"]!["num_predict"]!.GetValue<int>());
    }

    [Fact]
    public async Task ALocalModelAtAPath_KeepsThePath()
    {
        var server = new StubServer(_ => (HttpStatusCode.OK, "{\"message\":{\"content\":\"{}\"},\"done_reason\":\"stop\"}"));
        using var model = new LocalLanguageModel(new Uri("http://example.test:8080/ollama"), "llama3.2", Timeout, new HttpClient(server));

        await model.Ask(Question, CancellationToken.None);

        Assert.Equal("http://example.test:8080/ollama/api/chat", Assert.Single(server.Requests).Uri.ToString());
    }

    [Fact]
    public async Task ALocalModelThatRunsLong_GivesNoAnswer_AndAMissingOneSaysSo()
    {
        var cut = new StubServer(_ => (HttpStatusCode.OK, "{\"message\":{\"content\":\"{\\\"gre\"},\"done_reason\":\"length\"}"));
        var missing = new StubServer(_ => (HttpStatusCode.NotFound, "{\"error\":\"model 'nope' not found\"}"));
        using var running = new LocalLanguageModel(new Uri("http://localhost:11434"), "llama3.2", Timeout, new HttpClient(cut));
        using var absent = new LocalLanguageModel(new Uri("http://localhost:11434"), "nope", Timeout, new HttpClient(missing));

        Assert.Equal(ModelOutcome.CutShort, (await running.Ask(Question, CancellationToken.None)).Outcome);
        var answer = await absent.Ask(Question, CancellationToken.None);
        Assert.Equal(ModelOutcome.Failed, answer.Outcome);
        Assert.Equal("model 'nope' not found", answer.Problem);
    }

    [Fact]
    public async Task NoLocalServer_SaysToStartOllama()
    {
        var server = new StubServer(_ => throw new HttpRequestException("Connection refused"));
        using var model = new LocalLanguageModel(new Uri("http://localhost:11434"), "llama3.2", Timeout, new HttpClient(server));

        var answer = await model.Ask(Question, CancellationToken.None);
        var (models, problem) = await model.Installed(CancellationToken.None);

        Assert.Contains("is Ollama running?", answer.Problem, StringComparison.Ordinal);
        Assert.Empty(models);
        Assert.Contains("is Ollama running?", problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLocalServersModels_AreListed()
    {
        var server = new StubServer(_ => (HttpStatusCode.OK, "{\"models\":[{\"name\":\"qwen3:8b\"},{\"name\":\"llama3.2:latest\"}]}"));
        using var model = new LocalLanguageModel(new Uri("http://localhost:11434"), "llama3.2", Timeout, new HttpClient(server));

        var (models, problem) = await model.Installed(CancellationToken.None);

        Assert.Equal(["llama3.2:latest", "qwen3:8b"], models);
        Assert.Null(problem);
        Assert.Equal("http://localhost:11434/api/tags", Assert.Single(server.Requests).Uri.ToString());
    }

    [Fact]
    public void TheSettings_OpenTheChosenModel()
    {
        var minds = TestContent.Minds;

        Assert.Null(MindFactory.Open(new GameSettings(), "sk-ant-test", minds, out var none));
        Assert.Null(none);
        using var claude = (AnthropicLanguageModel)MindFactory.Open(new GameSettings { Minds = MindProvider.Claude, ClaudeModel = "claude-haiku-5-5" }, "sk-ant-test", minds, out _)!;
        Assert.Equal("Claude Haiku 5.5", claude.Name);
        using var unknown = (AnthropicLanguageModel)MindFactory.Open(new GameSettings { Minds = MindProvider.Claude, ClaudeModel = "claude-nonsense" }, "sk-ant-test", minds, out _)!;
        Assert.Equal(minds.ClaudeModels[0].Name, unknown.Name);
        using var local = (LocalLanguageModel)MindFactory.Open(new GameSettings { Minds = MindProvider.Local, LocalModel = "qwen3:8b" }, null, minds, out _)!;
        Assert.Equal("qwen3:8b (on this computer)", local.Name);
    }

    [Fact]
    public void ClaudeWithoutAKey_IsNotOpened_AndSaysWhy()
    {
        if (Environment.GetEnvironmentVariable(MindFactory.KeyVariable) is { Length: > 0 })
        {
            return;
        }

        Assert.Null(MindFactory.Open(new GameSettings { Minds = MindProvider.Claude }, "  ", TestContent.Minds, out var problem));
        Assert.Contains("needs your Anthropic key", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTest_ReadsAGreeting_OrSaysWhyThereIsNone()
    {
        Assert.Equal((true, "Claude answers: “Hail.”"), MindCheck.Read("Claude", ModelAnswer.Of("{\"greeting\":\" Hail. \"}")));
        Assert.Equal((false, "Claude did not answer: it would not."), MindCheck.Read("Claude", ModelAnswer.Without(ModelOutcome.Refused, "it would not")));
        Assert.False(MindCheck.Read("Claude", ModelAnswer.Of("[1]")).Answered);
        Assert.False(MindCheck.Read("Claude", ModelAnswer.Of("{nope")).Answered);
    }

    /// <summary>A request the stub server received.</summary>
    private sealed record Received(Uri Uri, Dictionary<string, string> Headers, string Body)
    {
        public string Header(string name) => Headers.TryGetValue(name.ToLowerInvariant(), out var value) ? value : string.Empty;
    }

    /// <summary>An HTTP handler that answers from a script and keeps what it received.</summary>
    private sealed class StubServer : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> _answer;

        public StubServer(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> answer) => _answer = answer;

        public List<Received> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var header in request.Headers)
            {
                headers[header.Key.ToLowerInvariant()] = string.Join(",", header.Value);
            }

            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new Received(request.RequestUri!, headers, body));
            var (status, text) = _answer(request);
            return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
        }
    }
}
