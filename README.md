# CAudioVisualizer Mixins

A C# 6 / .NET Framework-compatible fork of the https://github.com/SilenZcience/CAudioVisualizer repository intended for Space Engineers mods.

This branch replaces the upstream desktop application (NAudio/OpenTK/ImGui) with
the shared-project implementation supplied in `External/CAudioVisualizer.SE`.
The original desktop sources remain available in Git history.

This repository uses **Visual Studio Shared Projects** (`.shproj` + `.projitems`) rather than normal class-library projects. The sources are mixed directly into the consuming project.

## Mixins

### `CAudioVisualizer.Core`

Game-independent implementation:

- Mono signed PCM16 little-endian input
- PCM can be pushed in arbitrarily small chunks
- Default 24 kHz sample rate
- Rolling sample buffer
- Hann window
- Dependency-free radix-2 FFT
- Logarithmic spectrum bands
- Attack/release smoothing
- Software RGBA bar renderer
- Injectable worker scheduler (use the game's parallel scheduler in Space Engineers)
- No render-job backlog: the newest PCM snapshot wins
- 512x512 default texture size
- 1024x1024 maximum supported configuration

### `CAudioVisualizer.SpaceEngineers`

Space Engineers integration layer:

- Generated-texture interface matrix
- `MyAPIGateway.GeneratedTextures` adapter
- 60 Hz tick interface with default `RenderEveryNthTick = 2` (30 visual FPS)
- Creates one generated texture on the first completed frame, then reuses it
- Uploads only complete frames, on the thread calling `Tick()`
- Disposes the texture and cancels publication of pending work without waiting


```xml
<Import Project="..\External\CAudioVisualizer.SE\CAudioVisualizer.Core\CAudioVisualizer.Core.projitems" Label="Shared" />
<Import Project="..\External\CAudioVisualizer.SE\CAudioVisualizer.SpaceEngineers\CAudioVisualizer.SpaceEngineers.projitems" Label="Shared" />
```

The consuming project controls the target framework and language version. The sources are written for C# 6 and do not require NuGet packages.

## Basic usage

```csharp
var options = new SeAudioVisualizer.Core.AudioVisualizerOptions
{
    SampleRate = 24000,
    TextureWidth = 512,
    TextureHeight = 512,
    FftSize = 2048,
    BarCount = 64
};

var core = new SeAudioVisualizer.Core.AudioVisualizerCore(
    options, work => Sandbox.ModAPI.MyAPIGateway.Parallel.Start(work));
var host = new SeAudioVisualizer.SpaceEngineers.SpaceEngineersVisualizerHost(
    core,
    new SeAudioVisualizer.SpaceEngineers.SpaceEngineersGeneratedTextureFactory());
```

Feed every raw audio chunk, without waiting for a visual tick:

```csharp
host.PushPcm16Mono(buffer, 0, buffer.Length);
```

From the normal 60 Hz game update:

```csharp
host.Tick();

var texture = host.CurrentTexture;
if (texture != null)
{
    string generatedTextureName = texture.Name;
    // Hand generatedTextureName to the media-player/display path.
}
```

`Tick()` publishes on every second tick by default. While the background worker is still producing a frame, no second job is queued.

Call `Tick()` once per simulation update, not once per draw. Push PCM corresponding
to audible playback, not a file/network buffer submitted ahead of the playback
clock. Feeding a full FFT-sized playback window is also supported: it replaces
all samples considered by the next FFT, including after seeks.

Keep options unchanged after constructing the core. The constructor without a
scheduler runs synchronously (useful for tests); game integrations must supply the
parallel scheduler as above. `TryGetLatestFrame` borrows the current pixel array:
consume it before scheduling another render. The host handles that ordering.

For ADK LCD consumers, call `LcdDynamicTexture.NotifyContentChanged(texture.Name)`
when `Tick()` returns true, and `NotifyDisposed` before disposing the host. This
invalidates sprite caches even though the generated texture name stays the same.
Dispose the host when its view is closed or hidden; audio playback can continue.

## License

The original CAudioVisualizer project is MIT licensed. Its original license and copyright notice are retained in `LICENSE`.
