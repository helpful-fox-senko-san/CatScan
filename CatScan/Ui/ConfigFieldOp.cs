using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;

namespace CatScan.Ui;

public partial class ConfigWindow : Window, IDisposable
{
    private void DrawFieldOp()
    {
        bool b;

        b = Plugin.Configuration.SpecialFieldOps;
        if (ImGui.Checkbox("Field Op tracker mode", ref b))
        {
            Plugin.Configuration.SpecialFieldOps = b;
            Plugin.Configuration.Save();
            if (Plugin.MainWindow.IsOpen)
                Plugin.MainWindow.OpenTab(MainWindow.Tabs.ScanResults);
        }

        ImGui.TextWrapped("Enables special NM/CE tracker interface while inside Eureka or Occult Crescent zones.");

        ImGui.Separator();

        ImGui.TextUnformatted("Occult Crescent");

        using (var occultIndent = ImRaii.PushIndent(24.0f))
        {
            using var occultId = ImRaii.PushId("Occult");

            ImGui.TextUnformatted("Sound Alerts");

            using (var soundIndent = ImRaii.PushIndent(24.0f))
            {
                using var soundId = ImRaii.PushId("Sound");

                b = Plugin.Configuration.OccultSoundAlertFATE;
                if (ImGui.Checkbox("FATE", ref b))
                {
                    Plugin.Configuration.OccultSoundAlertFATE = b;
                    Plugin.Configuration.Save();
                }

                ImGui.SameLine();
                b = Plugin.Configuration.OccultSoundAlertCE;
                if (ImGui.Checkbox("CE", ref b))
                {
                    Plugin.Configuration.OccultSoundAlertCE = b;
                    Plugin.Configuration.Save();
                }

                ImGui.SameLine();
                b = Plugin.Configuration.OccultSoundAlertPotFATE;
                if (ImGui.Checkbox("Pot FATE", ref b))
                {
                    Plugin.Configuration.OccultSoundAlertPotFATE = b;
                    Plugin.Configuration.Save();
                }
            }

            ImGui.TextUnformatted("Open Map Flag Automatically");

            using (var autoFlagIndent = ImRaii.PushIndent(24.0f))
            {
                using var autoFlagId = ImRaii.PushId("AutoFlag");

                b = Plugin.Configuration.OccultAutoOpenFATE;
                if (ImGui.Checkbox("FATE", ref b))
                {
                    Plugin.Configuration.OccultAutoOpenFATE = b;
                    Plugin.Configuration.Save();
                }

                ImGui.SameLine();
                b = Plugin.Configuration.OccultAutoOpenCE;
                if (ImGui.Checkbox("CE", ref b))
                {
                    Plugin.Configuration.OccultAutoOpenCE = b;
                    Plugin.Configuration.Save();
                }

                ImGui.SameLine();
                b = Plugin.Configuration.OccultAutoOpenPotFATE;
                if (ImGui.Checkbox("Pot FATE", ref b))
                {
                    Plugin.Configuration.OccultAutoOpenPotFATE = b;
                    Plugin.Configuration.Save();
                }
            }
        }
    }
}