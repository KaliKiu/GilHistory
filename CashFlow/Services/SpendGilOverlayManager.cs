using System.IO;
using System.Media;

namespace CashFlow.Services;

public sealed class SpendGilOverlayManager : IDisposable
{
    private bool flashActive;
    private bool waitingForImageStart;
    private bool useImageThisFlash;
    private long flashStartMs;
    private long imageWaitDeadlineMs;
    private string overlayText = "";
    private string lastTexturePath = "";
    private ImTextureID textureHandle = default;
    private long nextTextureLoadAttemptMs = 0;
    private bool textureLoadPermanentlyFailed;
    private long lastSoundPlayMs;
    private static readonly HashSet<string> AllowedExtensions = ["png", "jpg", "jpeg", "bmp", "gif", "webp", "tex"];
    private static readonly HashSet<string> AllowedSoundExtensions = ["wav"];
    private const long MaxImageBytes = 12 * 1024 * 1024; // 12MB safety guard
    private const long MaxSoundBytes = 10 * 1024 * 1024; // 10MB safety guard

    private SpendGilOverlayManager()
    {
        Svc.PluginInterface.UiBuilder.Draw += Draw;
    }

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= Draw;
    }

    public void Trigger(long spentGil)
    {
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var imagePath = C.SpendGilAlertImagePath?.Trim() ?? "";
        var imagePathReady = imagePath.Length > 0 && IsImagePathValid(imagePath);

        overlayText = C.ShowSpendAmountInAlert ? $"{C.SpendGilAlertText}\n-{spentGil:N0} gil" : C.SpendGilAlertText;
        flashActive = true;
        useImageThisFlash = imagePathReady && textureHandle != default;
        waitingForImageStart = imagePathReady;
        flashStartMs = now;
        imageWaitDeadlineMs = now + 1500;
        TryPlaySound(now);
    }

    private void Draw()
    {
        if(!C.EnableSpendGilFullscreenFlash) return;
        // Never load textures during active flash to avoid frame spikes/crashes.
        if(!flashActive)
        {
            TryWarmupTextureCache();
            return;
        }
        if(!flashActive) return;

        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var displaySize = ImGui.GetIO().DisplaySize;
        if(displaySize.X <= 0 || displaySize.Y <= 0) return;

        var textureLoaded = textureHandle != default;
        var handle = textureHandle;
        var imagePathConfigured = useImageThisFlash;
        if(waitingForImageStart)
        {
            if(!textureLoaded)
            {
                if(now < imageWaitDeadlineMs)
                {
                    return;
                }

                // If image could not be loaded quickly, fail over to red flash for this event.
                waitingForImageStart = false;
                useImageThisFlash = false;
                flashStartMs = now;
            }
            else
            {
                // Start fixed-duration timer from the first frame the image is actually renderable.
                waitingForImageStart = false;
                flashStartMs = now;
            }
        }

        var durationMs = Math.Clamp(C.SpendGilFlashDurationMs, 200, 5000);
        if(now - flashStartMs >= durationMs)
        {
            flashActive = false;
            waitingForImageStart = false;
            useImageThisFlash = false;
            return;
        }

        ImGui.SetNextWindowPos(Vector2.Zero, ImGuiCond.Always);
        ImGui.SetNextWindowSize(displaySize, ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

        ImGui.PushStyleColor(ImGuiCol.WindowBg,
            textureLoaded || imagePathConfigured
                ? new Vector4(0f, 0f, 0f, 0f)
                : new Vector4(0.85f, 0.0f, 0.0f, 0.93f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));

        var flags = ImGuiWindowFlags.NoDecoration
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoInputs
            | ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.NoSavedSettings;

        if(ImGui.Begin("###CashFlowSpendGilOverlay", flags))
        {
            if(textureLoaded)
            {
                ImGui.SetCursorPos(Vector2.Zero);
                ImGui.Image(handle, displaySize);
            }

            var text = string.IsNullOrWhiteSpace(overlayText) ? "!!! you spend gil !!!" : overlayText;
            var textSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorPos(new Vector2(
                Math.Max(0f, (displaySize.X - textSize.X) * 0.5f),
                Math.Max(0f, (displaySize.Y - textSize.Y) * 0.5f)));
            ImGui.TextUnformatted(text);
        }
        ImGui.End();

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);
    }

    private void TryWarmupTextureCache()
    {
        var path = C.SpendGilAlertImagePath?.Trim() ?? "";
        if(path.Length == 0)
        {
            lastTexturePath = "";
            textureHandle = default;
            textureLoadPermanentlyFailed = false;
            return;
        }

        if(path != lastTexturePath)
        {
            lastTexturePath = path;
            textureHandle = default;
            textureLoadPermanentlyFailed = false;
            nextTextureLoadAttemptMs = 0;
        }

        if(textureHandle != default || textureLoadPermanentlyFailed) return;
        if(!IsImagePathValid(path))
        {
            textureLoadPermanentlyFailed = true;
            return;
        }

        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        if(now < nextTextureLoadAttemptMs) return;
        nextTextureLoadAttemptMs = now + 5_000;

        try
        {
            if(ThreadLoadImageHandler.TryGetTextureWrap(path, out var tex))
            {
                textureHandle = tex.Handle;
            }
        }
        catch
        {
            // If texture load throws, disable image loading for this path.
            textureLoadPermanentlyFailed = true;
        }
    }

    private static bool IsImagePathValid(string path)
    {
        if(path.Length == 0) return false;
        if(!File.Exists(path)) return false;
        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        if(!AllowedExtensions.Contains(ext)) return false;
        try
        {
            var fi = new FileInfo(path);
            if(fi.Length <= 0 || fi.Length > MaxImageBytes) return false;
        }
        catch
        {
            return false;
        }
        return true;
    }

    private void TryPlaySound(long now)
    {
        if(!C.EnableSpendGilSound) return;
        if(now - lastSoundPlayMs < 250) return;
        var path = C.SpendGilSoundPath?.Trim() ?? "";
        if(!IsSoundPathValid(path)) return;
        lastSoundPlayMs = now;

        _ = Task.Run(() =>
        {
            try
            {
                using var player = new SoundPlayer(path);
                player.Play();
            }
            catch
            {
                // Ignore sound errors to keep gameplay stable.
            }
        });
    }

    private static bool IsSoundPathValid(string path)
    {
        if(path.Length == 0) return false;
        if(!File.Exists(path)) return false;
        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        if(!AllowedSoundExtensions.Contains(ext)) return false;
        try
        {
            var fi = new FileInfo(path);
            if(fi.Length <= 0 || fi.Length > MaxSoundBytes) return false;
        }
        catch
        {
            return false;
        }
        return true;
    }
}
