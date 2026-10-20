using System;
using System.Collections.Generic;
using System.Linq;
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
    public void TheSettings_OpenTheChosenModel_WithItsOwnKey()
    {
        var minds = TestContent.Minds;
        var keys = new MindKeys("sk-ant-test", "sk-openai-test", "AIza-test");

        Assert.Null(MindFactory.Open(new GameSettings(), keys, minds, out var none));
        Assert.Null(none);
        using var claude = (AnthropicLanguageModel)MindFactory.Open(new GameSettings { Minds = MindProvider.Claude, ClaudeModel = "claude-haiku-5-5" }, keys, minds, out _)!;
        Assert.Equal("Claude Haiku 5.5", claude.Name);
        using var unknown = (AnthropicLanguageModel)MindFactory.Open(new GameSettings { Minds = MindProvider.Claude, ClaudeModel = "claude-nonsense" }, keys, minds, out _)!;
        Assert.Equal(minds.ClaudeModels[0].Name, unknown.Name);
        using var gpt = (OpenAILanguageModel)MindFactory.Open(new GameSettings { Minds = MindProvider.OpenAI, OpenAIModel = "gpt-6-luna" }, keys, minds, out _)!;
        Assert.Equal("GPT-6 Luna", gpt.Name);
        using var gemini = (GeminiLanguageModel)MindFactory.Open(new GameSettings { Minds = MindProvider.Gemini }, keys, minds, out _)!;
        Assert.Equal("Gemini 3.8 Flash", gemini.Name);
        using var local = (LocalLanguageModel)MindFactory.Open(new GameSettings { Minds = MindProvider.Local, LocalModel = "qwen3:8b" }, MindKeys.None, minds, out _)!;
        Assert.Equal("qwen3:8b (on this computer)", local.Name);
    }

    [Theory]
    [InlineData(MindProvider.Claude, "needs your Anthropic key")]
    [InlineData(MindProvider.OpenAI, "needs your OpenAI key")]
    [InlineData(MindProvider.Gemini, "needs your Gemini API key")]
    public void AnOnlineModelWithoutItsKey_IsNotOpened_AndSaysWhy(MindProvider provider, string why)
    {
        if (MindFactory.KeyVariables(provider).Any(v => Environment.GetEnvironmentVariable(v) is { Length: > 0 }))
        {
            return;
        }

        // Another provider's key is not this one's.
        var keys = new MindKeys(provider == MindProvider.Claude ? null : "sk-ant-test", provider == MindProvider.OpenAI ? "  " : "sk-openai-test", provider == MindProvider.Gemini ? null : "AIza-test");

        Assert.Null(MindFactory.Open(new GameSettings { Minds = provider }, keys, TestContent.Minds, out var problem));
        Assert.Contains(why, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Keys_AreEachProvidersOwn()
    {
        var keys = MindKeys.None.With(MindProvider.OpenAI, "sk-openai").With(MindProvider.Gemini, "AIza").With(MindProvider.Local, "ignored");

        Assert.Equal(new MindKeys(null, "sk-openai", "AIza"), keys);
        Assert.Null(keys.For(MindProvider.Claude));
        Assert.Null(keys.For(MindProvider.Local));
        Assert.Equal(["GEMINI_API_KEY", "GOOGLE_API_KEY"], MindFactory.KeyVariables(MindProvider.Gemini));
    }

    /// <summary>GPT's answer of some text, as the Responses API sends it, after a reasoning item.</summary>
    private static string GptResponse(string text) => new JsonObject
    {
        ["id"] = "resp_test",
        ["object"] = "response",
        ["status"] = "completed",
        ["output"] = new JsonArray(
            new JsonObject { ["type"] = "reasoning", ["summary"] = new JsonArray() },
            new JsonObject { ["type"] = "message", ["role"] = "assistant", ["content"] = new JsonArray(new JsonObject { ["type"] = "output_text", ["text"] = text }) }),
        ["usage"] = new JsonObject { ["input_tokens"] = 200, ["output_tokens"] = 40 },
    }.ToJsonString();

    [Fact]
    public async Task Gpt_IsAskedThroughTheResponsesApi_ForStrictJsonOfTheSchema()
    {
        var server = new StubServer(_ => (HttpStatusCode.OK, GptResponse("{\"greeting\":\"Greetings.\"}")));
        using var model = new OpenAILanguageModel("sk-openai-test", "gpt-6.1-sol", "GPT-6.1 Sol", Timeout, new HttpClient(server));

        var answer = await model.Ask(Question with { Turns = [new ModelTurn(false, "Hi."), new ModelTurn(true, "{}"), new ModelTurn(false, "Again.")] }, CancellationToken.None);

        Assert.Equal(ModelAnswer.Of("{\"greeting\":\"Greetings.\"}", 200, 40), answer);
        var request = Assert.Single(server.Requests);
        Assert.Equal("https://api.openai.com/v1/responses", request.Uri.ToString());
        Assert.Equal("Bearer sk-openai-test", request.Header("authorization"));
        var body = JsonNode.Parse(request.Body)!;
        Assert.Equal("gpt-6.1-sol", body["model"]!.GetValue<string>());
        Assert.Equal(Question.Instructions, body["instructions"]!.GetValue<string>());
        Assert.Equal(["user", "assistant", "user"], body["input"]!.AsArray().Select(m => m!["role"]!.GetValue<string>()));
        Assert.Equal("low", body["reasoning"]!["effort"]!.GetValue<string>());
        Assert.Equal(Question.MaxTokens, body["max_output_tokens"]!.GetValue<int>());
        Assert.Equal("json_schema", body["text"]!["format"]!["type"]!.GetValue<string>());
        Assert.True(body["text"]!["format"]!["strict"]!.GetValue<bool>());
        Assert.Equal("greeting", body["text"]!["format"]!["schema"]!["required"]![0]!.GetValue<string>());
        Assert.False(body["store"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("{\"status\":\"incomplete\",\"incomplete_details\":{\"reason\":\"max_output_tokens\"},\"output\":[]}", ModelOutcome.CutShort)]
    [InlineData("{\"status\":\"incomplete\",\"incomplete_details\":{\"reason\":\"content_filter\"},\"output\":[]}", ModelOutcome.Refused)]
    [InlineData("{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"refusal\",\"refusal\":\"No.\"}]}]}", ModelOutcome.Refused)]
    [InlineData("{\"status\":\"completed\",\"output\":[]}", ModelOutcome.Failed)]
    [InlineData("not json", ModelOutcome.Failed)]
    public void GptThatDoesNotAnswer_GivesNoAnswer(string body, ModelOutcome outcome)
    {
        Assert.Equal(outcome, OpenAILanguageModel.Read(body).Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{\"error\":{\"message\":\"Incorrect API key\",\"code\":\"invalid_api_key\"}}", "OpenAI did not accept the key")]
    [InlineData(HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"quota\",\"code\":\"insufficient_quota\"}}", "no credit left")]
    [InlineData(HttpStatusCode.NotFound, "{\"error\":{\"message\":\"no such model\"}}", "does not know the model gpt-6.1-sol")]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"Invalid schema\"}}", "OpenAI refused the question: Invalid schema")]
    public async Task GptsErrors_AreToldInWords(HttpStatusCode status, string body, string problem)
    {
        var server = new StubServer(_ => (status, body));
        using var model = new OpenAILanguageModel("sk-openai-test", "gpt-6.1-sol", "GPT-6.1 Sol", Timeout, new HttpClient(server));

        var answer = await model.Ask(Question, CancellationToken.None);

        Assert.Equal(ModelOutcome.Failed, answer.Outcome);
        Assert.Contains(problem, answer.Problem, StringComparison.Ordinal);
    }

    /// <summary>Gemini's answer of some text, as generateContent sends it, after a thought.</summary>
    private static string GeminiResponse(string text, string finish = "STOP") => new JsonObject
    {
        ["candidates"] = new JsonArray(new JsonObject
        {
            ["content"] = new JsonObject
            {
                ["role"] = "model",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = "Let me think.", ["thought"] = true }, new JsonObject { ["text"] = text }),
            },
            ["finishReason"] = finish,
        }),
        ["usageMetadata"] = new JsonObject { ["promptTokenCount"] = 150, ["candidatesTokenCount"] = 25, ["thoughtsTokenCount"] = 90 },
    }.ToJsonString();

    [Fact]
    public async Task Gemini_IsAskedThroughGenerateContent_ForJsonOfTheSchema()
    {
        var server = new StubServer(_ => (HttpStatusCode.OK, GeminiResponse("{\"greeting\":\"Be welcome.\"}")));
        using var model = new GeminiLanguageModel("AIza-test", "gemini-3.8-flash", "Gemini 3.8 Flash", Timeout, new HttpClient(server));

        var answer = await model.Ask(Question with { Effort = ModelEffort.Medium, Turns = [new ModelTurn(false, "Hi."), new ModelTurn(true, "{}"), new ModelTurn(false, "Again.")] }, CancellationToken.None);

        Assert.Equal(ModelAnswer.Of("{\"greeting\":\"Be welcome.\"}", 150, 25), answer);
        var request = Assert.Single(server.Requests);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash:generateContent", request.Uri.ToString());
        Assert.Equal("AIza-test", request.Header("x-goog-api-key"));
        var body = JsonNode.Parse(request.Body)!;
        Assert.Equal(Question.Instructions, body["systemInstruction"]!["parts"]![0]!["text"]!.GetValue<string>());
        Assert.Equal(["user", "model", "user"], body["contents"]!.AsArray().Select(c => c!["role"]!.GetValue<string>()));
        var config = body["generationConfig"]!;
        Assert.Equal(Question.MaxTokens, config["maxOutputTokens"]!.GetValue<int>());
        Assert.Equal("APPLICATION_JSON", config["responseFormat"]!["text"]!["mimeType"]!.GetValue<string>());
        Assert.Equal("greeting", config["responseFormat"]!["text"]!["schema"]!["required"]![0]!.GetValue<string>());
        Assert.Equal("medium", config["thinkingConfig"]!["thinkingLevel"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("MAX_TOKENS", ModelOutcome.CutShort)]
    [InlineData("SAFETY", ModelOutcome.Refused)]
    [InlineData("PROHIBITED_CONTENT", ModelOutcome.Refused)]
    public void GeminiThatStopsShort_GivesNoAnswer(string finish, ModelOutcome outcome)
    {
        Assert.Equal(outcome, GeminiLanguageModel.Read(GeminiResponse("{\"gre", finish)).Outcome);
    }

    [Fact]
    public void GeminiThatBlocksTheQuestion_OrSaysOnlyThoughts_GivesNoAnswer()
    {
        Assert.Equal(ModelOutcome.Refused, GeminiLanguageModel.Read("{\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}").Outcome);
        Assert.Equal(ModelOutcome.Failed, GeminiLanguageModel.Read("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Hmm.\",\"thought\":true}]},\"finishReason\":\"STOP\"}]}").Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":{\"code\":400,\"message\":\"API key not valid. Please pass a valid API key.\",\"status\":\"INVALID_ARGUMENT\"}}", "Google did not accept the key")]
    [InlineData(HttpStatusCode.TooManyRequests, "{\"error\":{\"code\":429,\"status\":\"RESOURCE_EXHAUSTED\"}}", "Google asks to wait")]
    [InlineData(HttpStatusCode.NotFound, "{\"error\":{\"code\":404,\"status\":\"NOT_FOUND\"}}", "does not know the model gemini-3.8-flash")]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":{\"code\":400,\"message\":\"Invalid JSON payload\"}}", "Gemini refused the question: Invalid JSON payload")]
    public async Task GeminisErrors_AreToldInWords(HttpStatusCode status, string body, string problem)
    {
        var server = new StubServer(_ => (status, body));
        using var model = new GeminiLanguageModel("AIza-test", "gemini-3.8-flash", "Gemini 3.8 Flash", Timeout, new HttpClient(server));

        var answer = await model.Ask(Question, CancellationToken.None);

        Assert.Equal(ModelOutcome.Failed, answer.Outcome);
        Assert.Contains(problem, answer.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoOnlineServer_SaysToCheckTheConnection()
    {
        var server = new StubServer(_ => throw new HttpRequestException("Connection refused"));
        using var gpt = new OpenAILanguageModel("sk-openai-test", "gpt-6.1-sol", "GPT-6.1 Sol", Timeout, new HttpClient(server));
        using var gemini = new GeminiLanguageModel("AIza-test", "gemini-3.8-flash", "Gemini 3.8 Flash", Timeout, new HttpClient(server));

        Assert.Contains("is this computer online?", (await gpt.Ask(Question, CancellationToken.None)).Problem, StringComparison.Ordinal);
        Assert.Contains("is this computer online?", (await gemini.Ask(Question, CancellationToken.None)).Problem, StringComparison.Ordinal);
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
