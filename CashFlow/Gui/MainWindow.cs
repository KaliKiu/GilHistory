using CashFlow.Gui.BaseTabs;
using CashFlow.Gui.Components;
using ECommons.ChatMethods;
using ECommons.Funding;
using ECommons.SimpleGui;

namespace CashFlow.Gui;
public unsafe class MainWindow : ConfigWindow
{
    public volatile Dictionary<ulong, Sender> CIDMap = [];
    public TabGilHistory TabGilHistory = new();
    public DateTime DateGraphStart = DateTimeOffset.FromUnixTimeSeconds(C.GraphStartDate).ToLocalTime().DateTime;
    public string DateGraphStartStr = DateTimeOffset.FromUnixTimeSeconds(C.GraphStartDate).ToLocalTime().DateTime.ToString(DateWidget.DateFormat);

    public void UpdateData(bool forced)
    {
        TabGilHistory.NeedsUpdate = true;
    }

    private MainWindow()
    {
        EzConfigGui.Init(this);
    }

    public override void Draw()
    {
        PatreonBanner.DrawRight();
        ImGuiEx.EzTabBar("tabs", PatreonBanner.Text, [
            ("Gil History", TabGilHistory.Draw, null, true),
            ("Settings", DrawSettings, null, true),
            ]);
    }

    void DrawSettings()
    {
        DrawSettingsGeneral();
    }

    private void DrawSettingsGeneral()
    {
        ImGui.SetNextItemWidth(150);
        ImGuiEx.SliderInt("Records per page", ref C.PerPage, 1000, 10000);
        ImGui.SetNextItemWidth(220);
        if(ImGui.SliderInt("Gil capture interval (seconds)", ref C.GilRecordIntervalSeconds, 5, 600))
        {
            C.GilRecordIntervalSeconds = Math.Clamp(C.GilRecordIntervalSeconds, 5, 3600);
        }
        ImGuiEx.HelpMarker("How often gil is sampled while the plugin is running.");
        ImGui.Separator();
        ImGui.Checkbox("Alert when gil is spent", ref C.EnableSpendGilAlert);
        if(C.EnableSpendGilAlert)
        {
            ImGui.Indent();
            ImGui.Checkbox("Show spent amount in alert", ref C.ShowSpendAmountInAlert);
            ImGui.Checkbox("Full-screen flash on spend", ref C.EnableSpendGilFullscreenFlash);
            ImGui.SetNextItemWidth(220);
            if(ImGui.SliderInt("Flash duration (ms)", ref C.SpendGilFlashDurationMs, 200, 5000))
            {
                C.SpendGilFlashDurationMs = Math.Clamp(C.SpendGilFlashDurationMs, 200, 5000);
            }
            ImGui.SetNextItemWidth(350);
            ImGui.InputText("Alert text", ref C.SpendGilAlertText, 256);
            ImGui.SetNextItemWidth(350);
            ImGui.InputText("Alert image path (optional)", ref C.SpendGilAlertImagePath, 512);
            ImGuiEx.HelpMarker("Use a full file path for image flash.");
            ImGui.Checkbox("Play sound on spend", ref C.EnableSpendGilSound);
            if(C.EnableSpendGilSound)
            {
                ImGui.SetNextItemWidth(350);
                ImGui.InputText("Sound path (.wav)", ref C.SpendGilSoundPath, 512);
                ImGuiEx.HelpMarker("Use full path to a .wav file.");
            }
            ImGui.Unindent();
        }
        ImGuiEx.TextV("Date format:");
        ImGui.SameLine();
        ImGuiEx.RadioButtonBool("Month/Day", "Day.Month", ref C.ReverseDayMonth, sameLine: true, inverted: true);

        ImGui.Separator();
        ImGui.Checkbox("Censor Names", ref C.CensorConfig.Enabled);
        ImGui.Indent();
        ImGui.Checkbox("Lesser Censor Mode", ref C.CensorConfig.LesserCensor);
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Undo, "Reset Censor Seed")) C.CensorConfig.Seed = Guid.NewGuid().ToString();
        ImGui.Unindent();

        ImGui.Separator();
        ImGui.Checkbox($"Display UTC time", ref C.UseUTCTime);
        ImGuiEx.HelpMarker($"Your local time will still be used for internal calculations");
        ImGui.Checkbox($"Use custom time format", ref C.UseCustomTimeFormat);
        if(C.UseCustomTimeFormat)
        {
            ImGui.Indent();
            ImGui.SetNextItemWidth(200f);
            ImGui.InputText($"Custom time format", ref C.CustomTimeFormat, 100);
            ImGuiEx.HelpMarker("Click to open help");
            if(ImGuiEx.HoveredAndClicked())
            {
                ShellStart("https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings");
            }
            ImGui.Unindent();
        }

        ImGui.Checkbox("Set fixed graph start date", ref C.UseGraphStartDate);
        if(C.UseGraphStartDate)
        {
            ImGui.Indent();
            if(DateWidget.DatePickerWithInput("##min", 1, ref DateGraphStartStr, ref DateGraphStart, out var isOpen))
            {
                C.GraphStartDate = DateGraphStart.ToUniversalTime().ToUnixTimeMilliseconds() / 1000;
            }
            ImGui.Unindent();
        }
    }
}
