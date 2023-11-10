using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;

namespace CatScan.Ui;

public partial class ConfigWindow : Window, IDisposable
{
    private string? _assemblyVersion = null;

    private void DrawConfig()
    {
        using var tabId = ImRaii.PushId("Config");

        bool b;
        float f;

        b = Plugin.Configuration.SoundEnabled;
        if (ImGui.Checkbox("Enable Sound Alerts", ref b))
        {
            Plugin.Configuration.SoundEnabled = b;
            Plugin.Configuration.Save();
        }
        using (var soundIndent = ImRaii.PushIndent(24.0f))
        {
            using var soundId = ImRaii.PushId("Sound");
            using var disabled = ImRaii.Disabled(!Plugin.Configuration.SoundEnabled);

            f = Plugin.Configuration.SoundVolume * 100.0f;
            if (ImGui.SliderFloat("Volume", ref f, 0.0f, 100.0f, "%.0f%%"))
            {
                Plugin.Configuration.SoundVolume = f / 100.0f;
            }

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                if (Plugin.Configuration.SoundEnabled)
                    Plugin.Notifications.PlaySfx("ping1.wav");
                Plugin.Configuration.Save();
            }

            b = Plugin.Configuration.SoundAlertFATE;
            if (ImGui.Checkbox("FATE", ref b))
            {
                Plugin.Configuration.SoundAlertFATE = b;
                Plugin.Configuration.Save();
            }

            ImGui.SameLine();
            b = Plugin.Configuration.SoundAlertS;
            if (ImGui.Checkbox("S Rank", ref b))
            {
                Plugin.Configuration.SoundAlertS = b;
                Plugin.Configuration.Save();
            }

            ImGui.SameLine();
            b = Plugin.Configuration.SoundAlertA;
            if (ImGui.Checkbox("A Rank", ref b))
            {
                Plugin.Configuration.SoundAlertA = b;
                Plugin.Configuration.Save();
            }

            b = Plugin.Configuration.SoundAlertB;
            if (ImGui.Checkbox("B Rank", ref b))
            {
                Plugin.Configuration.SoundAlertB = b;
                Plugin.Configuration.Save();
            }

            ImGui.SameLine();
            b = Plugin.Configuration.SoundAlertMinions;
            if (ImGui.Checkbox("Minion", ref b))
            {
                Plugin.Configuration.SoundAlertMinions = b;
                Plugin.Configuration.Save();
            }
        }

        ImGui.Separator();

        b = Plugin.Configuration.AutoOpenEnabled;
        if (ImGui.Checkbox("Open Window Automatically", ref b))
        {
            Plugin.Configuration.AutoOpenEnabled = b;
            Plugin.Configuration.Save();
        }
        using (var autoOpenIndent = ImRaii.PushIndent(24.0f))
        {
            using var autoOpenId = ImRaii.PushId("AutoOpen");
            using var disabled = ImRaii.Disabled(!Plugin.Configuration.AutoOpenEnabled);

            b = Plugin.Configuration.AutoOpenFATE;
            if (ImGui.Checkbox("FATE", ref b))
            {
                Plugin.Configuration.AutoOpenFATE = b;
                Plugin.Configuration.Save();
            }

            ImGui.SameLine();
            b = Plugin.Configuration.AutoOpenS;
            if (ImGui.Checkbox("S Rank", ref b))
            {
                Plugin.Configuration.AutoOpenS = b;
                Plugin.Configuration.Save();
            }
        }

        ImGui.Separator();

        b = Plugin.Configuration.AutoFlagEnabled;
        if (ImGui.Checkbox("Open Map Flag Automatically", ref b))
        {
            Plugin.Configuration.AutoFlagEnabled = b;
            Plugin.Configuration.Save();
        }
        using (var autoFlagIndent = ImRaii.PushIndent(24.0f))
        {
            using var autoFlagId = ImRaii.PushId("AutoFlag");
            using var disabled = ImRaii.Disabled(!Plugin.Configuration.AutoFlagEnabled);

            b = Plugin.Configuration.AutoFlagFATE;
            if (ImGui.Checkbox("FATE", ref b))
            {
                Plugin.Configuration.AutoFlagFATE = b;
                Plugin.Configuration.Save();
            }

            ImGui.SameLine();
            b = Plugin.Configuration.AutoFlagS;
            if (ImGui.Checkbox("S", ref b))
            {
                Plugin.Configuration.AutoFlagS = b;
                Plugin.Configuration.Save();
            }

            ImGui.SameLine();
            b = Plugin.Configuration.AutoFlagA;
            if (ImGui.Checkbox("A", ref b))
            {
                Plugin.Configuration.AutoFlagA = b;
                Plugin.Configuration.Save();
            }

            ImGui.SameLine();
            b = Plugin.Configuration.AutoFlagB;
            if (ImGui.Checkbox("B", ref b))
            {
                Plugin.Configuration.AutoFlagB = b;
                Plugin.Configuration.Save();
            }
        }

        ImGui.Separator();

        b = Plugin.Configuration.AutoCloseEnabled;
        if (ImGui.Checkbox("Hide Window Automatically", ref b))
        {
            Plugin.Configuration.AutoCloseEnabled = b;
            Plugin.Configuration.Save();
        }

        // XXX: This should also say "and in duties", but probably doesn't work as intended in quest duties and guildhests?
        ImGui.TextWrapped("Hide temporarily while in non-hunt zones.");

        ImGui.Separator();

        b = Plugin.Configuration.ShowMissingKC;
        if (ImGui.Checkbox("Count missing KC mobs", ref b))
        {
            Plugin.Configuration.ShowMissingKC = b;
            Plugin.Configuration.Save();
        }

        ImGui.TextWrapped("Keeps track of mobs that are no longer visible to you. Use to estimate a possible kill count range when you are not killing alone.");

        ImGui.Separator();

        b = Plugin.Configuration.SpecialFieldOps;
        if (ImGui.Checkbox("Eureka tracker mode", ref b))
        {
            Plugin.Configuration.SpecialFieldOps = b;
            Plugin.Configuration.Save();
            if (Plugin.MainWindow.IsOpen)
                Plugin.MainWindow.OpenTab(MainWindow.Tabs.ScanResults);
        }

        ImGui.TextWrapped("Enables special NM tracker interface while in Eureka.");

        ImGui.Separator();

        if (_assemblyVersion == null)
            _assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!.ToString();

        using var pushColor = ImRaii.PushColor(ImGuiCol.Text, _textColorGone);
        ImGuiHelpers.CenteredText($"Version {_assemblyVersion}");
    }
}
