using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pantheon.Game.Settings;

/// <summary>
/// The player's settings (spec §105's graphics scaling, §60's audio; ADR-021), kept between runs in
/// <c>user://settings.json</c>. Unlike content, the file is the player's: a missing or broken file
/// gives the defaults, unknown members are ignored and values out of range are brought into range.
/// </summary>
public sealed record GameSettings
{
    /// <summary>Lowest render scale offered.</summary>
    public const float MinRenderScale = 0.5f;

    /// <summary>Lowest share of the citizens offered.</summary>
    public const float MinCitizens = 0.2f;

    /// <summary>Lowest share of the vegetation offered.</summary>
    public const float MinVegetation = 0.25f;

    /// <summary>Lowest share of the particles offered.</summary>
    public const float MinParticles = 0.25f;

    /// <summary>Lowest terrain quality offered.</summary>
    public const float MinTerrainQuality = 0.5f;

    /// <summary>Smallest simulation budget offered, in milliseconds a frame.</summary>
    public const float MinSimulationBudget = 2f;

    /// <summary>Largest simulation budget offered, in milliseconds a frame.</summary>
    public const float MaxSimulationBudget = 16f;

    /// <summary>The simulation budget unless the player chooses another, in milliseconds a frame.</summary>
    public const float DefaultSimulationBudget = 6f;

    /// <summary>Slowest camera speed offered.</summary>
    public const float MinCameraSpeed = 0.5f;

    /// <summary>Fastest camera speed offered.</summary>
    public const float MaxCameraSpeed = 2f;

    /// <summary>Smallest interface offered.</summary>
    public const float MinInterfaceScale = 0.75f;

    /// <summary>Largest interface offered.</summary>
    public const float MaxInterfaceScale = 1.5f;

    /// <summary>The Claude model asked unless the player chooses another.</summary>
    public const string DefaultClaudeModel = "claude-opus-5-5";

    /// <summary>The GPT model asked unless the player chooses another (ADR-067).</summary>
    public const string DefaultOpenAIModel = "gpt-6.1-sol";

    /// <summary>The Gemini model asked unless the player chooses another (ADR-067).</summary>
    public const string DefaultGeminiModel = "gemini-3.8-flash";

    /// <summary>Where a local model is served unless the player says otherwise.</summary>
    public const string DefaultLocalUrl = "http://localhost:11434";

    /// <summary>The local model asked unless the player names another.</summary>
    public const string DefaultLocalModel = "llama3.2";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString };

    /// <summary>Whether the game fills the screen.</summary>
    public bool Fullscreen { get; init; }

    /// <summary>Whether frames wait for the display's refresh.</summary>
    public bool VSync { get; init; } = true;

    /// <summary>Whether the sun casts shadows.</summary>
    public bool Shadows { get; init; } = true;

    /// <summary>Whether the light's finer effects are on: ambient occlusion in the folds of the land and a soft glow (ADR-051).</summary>
    public bool LightingEffects { get; init; } = true;

    /// <summary>Share of the window's resolution the world is drawn at (0.5..1), upscaled.</summary>
    public float RenderScale { get; init; } = 1f;

    /// <summary>Share of the representative citizens shown (0.2..1); it applies to the next world shown.</summary>
    public float Citizens { get; init; } = 1f;

    /// <summary>
    /// Share of the vegetation drawn (0.25..1, spec §105, ADR-056): how far from the camera the ground
    /// cover reaches and the forests are planted; it applies when the game next starts.
    /// </summary>
    public float Vegetation { get; init; } = 1f;

    /// <summary>Share of the particles drawn (0.25..1): rain, fire, smoke and the miracles' looks.</summary>
    public float Particles { get; init; } = 1f;

    /// <summary>
    /// The terrain's detail (0.5..1): the size of Terrain3D's finest mesh, or how far the fallback
    /// draws the land in full detail; it applies when the game next starts.
    /// </summary>
    public float TerrainQuality { get; init; } = 1f;

    /// <summary>
    /// The CPU budget the simulation keeps within, in milliseconds a frame (2..16, spec §105): past it,
    /// the ticks still due wait for the next frame.
    /// </summary>
    public float SimulationBudget { get; init; } = DefaultSimulationBudget;

    /// <summary>Whether a mouse resting at the window's edge pans the view (ADR-058; off by default).</summary>
    public bool EdgeScroll { get; init; }

    /// <summary>Whether a trackpad's two-finger scroll zooms the view rather than moving it (ADR-058).</summary>
    public bool TrackpadScrollZooms { get; init; }

    /// <summary>How fast the keys, the window's edge and trackpad gestures move and turn the view (0.5..2).</summary>
    public float CameraSpeed { get; init; } = 1f;

    /// <summary>
    /// How large the interface is drawn (0.75..1.5, ADR-059), on top of the window's own stretch: 1
    /// draws it at its design size for a window of 1600 by 900.
    /// </summary>
    public float InterfaceScale { get; init; } = 1f;

    /// <summary>Loudness of everything (0..1).</summary>
    public float MasterVolume { get; init; } = 0.8f;

    /// <summary>Loudness of the music (0..1).</summary>
    public float MusicVolume { get; init; } = 0.55f;

    /// <summary>Loudness of miracles, Creatures, disasters and alerts (0..1).</summary>
    public float EffectsVolume { get; init; } = 0.8f;

    /// <summary>Loudness of the wind and the rain (0..1).</summary>
    public float AmbienceVolume { get; init; } = 0.6f;

    /// <summary>
    /// Whether the debug tools are on (ADR-068): their keys, the god's bar's Debug button and the debug
    /// overlays. <c>null</c> lets the build decide: on when the game runs from the project, off in an
    /// exported game.
    /// </summary>
    public bool? DebugTools { get; init; }

    /// <summary>
    /// Whether the land is drawn by the simple renderer rather than Terrain3D, for a computer Terrain3D
    /// does not suit (ADR-068); it applies when the game next starts, and <c>--terrain</c> overrides it.
    /// </summary>
    public bool SimpleTerrain { get; init; }

    /// <summary>
    /// Who gives the rival gods their minds (ADR-065, ADR-067): the scribes (offline, the default),
    /// Claude, GPT, Gemini or a local model. The keys are not kept here but on their own in the profile.
    /// </summary>
    public Minds.MindProvider Minds { get; init; } = Pantheon.Game.Minds.MindProvider.Scribes;

    /// <summary>The Claude model asked, one of <c>minds.json</c>'s.</summary>
    public string ClaudeModel { get; init; } = DefaultClaudeModel;

    /// <summary>The GPT model asked, one of <c>minds.json</c>'s (ADR-067).</summary>
    public string OpenAIModel { get; init; } = DefaultOpenAIModel;

    /// <summary>The Gemini model asked, one of <c>minds.json</c>'s (ADR-067).</summary>
    public string GeminiModel { get; init; } = DefaultGeminiModel;

    /// <summary>Where the local model is served.</summary>
    public string LocalUrl { get; init; } = DefaultLocalUrl;

    /// <summary>The local model asked.</summary>
    public string LocalModel { get; init; } = DefaultLocalModel;

    /// <summary>The same settings with every value in range.</summary>
    /// <returns>The settings.</returns>
    public GameSettings Clamped() => this with
    {
        RenderScale = Range(RenderScale, MinRenderScale, 1f, 1f),
        Citizens = Range(Citizens, MinCitizens, 1f, 1f),
        Vegetation = Range(Vegetation, MinVegetation, 1f, 1f),
        Particles = Range(Particles, MinParticles, 1f, 1f),
        TerrainQuality = Range(TerrainQuality, MinTerrainQuality, 1f, 1f),
        SimulationBudget = Range(SimulationBudget, MinSimulationBudget, MaxSimulationBudget, DefaultSimulationBudget),
        CameraSpeed = Range(CameraSpeed, MinCameraSpeed, MaxCameraSpeed, 1f),
        InterfaceScale = Range(InterfaceScale, MinInterfaceScale, MaxInterfaceScale, 1f),
        MasterVolume = Range(MasterVolume, 0f, 1f, 0.8f),
        MusicVolume = Range(MusicVolume, 0f, 1f, 0.55f),
        EffectsVolume = Range(EffectsVolume, 0f, 1f, 0.8f),
        AmbienceVolume = Range(AmbienceVolume, 0f, 1f, 0.6f),
        Minds = Enum.IsDefined(Minds) ? Minds : Pantheon.Game.Minds.MindProvider.Scribes,
        ClaudeModel = string.IsNullOrWhiteSpace(ClaudeModel) ? DefaultClaudeModel : ClaudeModel.Trim(),
        OpenAIModel = string.IsNullOrWhiteSpace(OpenAIModel) ? DefaultOpenAIModel : OpenAIModel.Trim(),
        GeminiModel = string.IsNullOrWhiteSpace(GeminiModel) ? DefaultGeminiModel : GeminiModel.Trim(),
        LocalUrl = Uri.TryCreate(LocalUrl?.Trim(), UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps) ? LocalUrl.Trim() : DefaultLocalUrl,
        LocalModel = string.IsNullOrWhiteSpace(LocalModel) ? DefaultLocalModel : LocalModel.Trim(),
    };

    /// <summary>Reads settings from a file's text: the defaults when it is missing or broken, every value in range.</summary>
    /// <param name="json">The file's text, or <c>null</c> when there is none.</param>
    /// <returns>The settings.</returns>
    public static GameSettings Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new GameSettings();
        }

        try
        {
            return (JsonSerializer.Deserialize<GameSettings>(json, Options) ?? new GameSettings()).Clamped();
        }
        catch (JsonException)
        {
            return new GameSettings();
        }
    }

    /// <summary>The settings as a file's text.</summary>
    /// <returns>Indented JSON.</returns>
    public string Serialize() => JsonSerializer.Serialize(Clamped(), Options);

    /// <summary>A value within a range; a value that is not a number gives the fallback.</summary>
    private static float Range(float value, float min, float max, float fallback) => float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
