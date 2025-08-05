using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Utility;

namespace CatScan.Ui;

public partial class MainWindow : Window, IDisposable
{
    // Kind of hacky copy-paste of Eureka interface
    private void DrawOccultTracker()
    {
        DrawOccultCETable();
        // 3 minute global cooldown after each CE ends
        DrawCECooldown();
    }

    private void DrawOccultCETable()
    {
        using var tabId = ImRaii.PushId("OccultTracker");

        if (!HuntData.OccultZones.TryGetValue(HuntModel.Territory.ZoneId, out var occultZone))
            return;

        using var table = ImRaii.Table("OccultTracker", 5, ImGuiTableFlags.Reorderable | ImGuiTableFlags.Resizable | ImGuiTableFlags.Hideable);
        ImGui.TableSetupColumn("CE Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Mob Name", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultHide);
        ImGui.TableSetupColumn("KC ", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Time ", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableHeadersRow();

        foreach (var ce in occultZone.CEs)
        {
            var color = _textColorGone;
            TimeSpan respawnIn = TimeSpan.Zero;
            float hpPct = 0.0f;
            DateTime firstSeen = DateTime.MinValue;
            bool dead = false;

            ScannedFate? scanResult = null;

            // XXX: Technically an implementation detail that its possible to get the scan results by name
            HuntModel.Fates.TryGetValue(ce.CEName, out scanResult);

            if (scanResult != null)
            {
                hpPct = 100.0f - scanResult.ProgressPct;
                if (scanResult.Missing)
                    hpPct = 0.0f;
                firstSeen = scanResult.FirstSeenTimeUtc;
                dead = (hpPct == 0.0f);
            }
            else
            {
                // If there is no scanResult, that means the NM has not yet been seen, and assumed to be spawnable
            }

            if (dead && scanResult != null)
            {
                var respawnTime = TimeSpan.FromHours(2);

                if (ce.CEName == "Calamity Bound")
                    respawnTime = TimeSpan.FromHours(1);

                respawnIn = respawnTime - (HuntModel.UtcNow - firstSeen);
                if (respawnIn < TimeSpan.Zero)
                {
                    respawnIn = TimeSpan.Zero;
                    scanResult = null;
                }
            }

            if (scanResult != null)
            {
                if (dead)
                    color = _textColorDead;
                else if (hpPct == 100.0)
                    color = RGB(192, 255, 192);
                else
                    color = _textColorPulled;
            }

            using var pushColor = ImRaii.PushColor(ImGuiCol.Text, color, color != Vector4.Zero);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var name = ce.CEName;

            var nameStr = GameData.TranslateBNpcName(name);
            var kcNameStr = GameData.TranslateBNpcName(ce.KCName);
            var kcShortName = kcNameStr;

            // Hacky and English-specific
            if (kcShortName.StartsWith("Crescent "))
                kcShortName = kcShortName[9..];
            else if (kcShortName.StartsWith("Occult "))
                kcShortName = kcShortName[7..];

            if (scanResult == null)
            {
                var color2 = RGB(255, 255, 255);
                using var pushColor2 = ImRaii.PushColor(ImGuiCol.Text, color2, color2 != Vector4.Zero);
                ImGui.Text(nameStr);
            }
            else if (scanResult.Missing)
            {
                var color2 = _textColorDead;
                using var pushColor2 = ImRaii.PushColor(ImGuiCol.Text, color2, color2 != Vector4.Zero);
                ImGui.Text(nameStr);
            }
            else
            {
                if (hpPct < 100.0f)
                    nameStr += $" (HP: {hpPct:F0}%)";
                nameStr += "##" + scanResult.EnglishName;

                if (ImGui.Selectable(nameStr, false, ImGuiSelectableFlags.AllowItemOverlap))
                    GameFunctions.OpenMapLink(scanResult.MapX, scanResult.MapY);
            }
            ImGui.TableNextColumn();
            ImGui.Text(kcShortName);
            ImGui.TableNextColumn();
            if (!ce.KCName.IsNullOrEmpty())
            {
                // TODO: Clickable KC mob location

                if (respawnIn == TimeSpan.Zero)
                {
                    KillCount? kc = null;

                    foreach (var r in HuntModel.KillCountLog)
                    {
                        if (r.EnglishName == ce.KCName)
                        {
                            kc = r;
                            break;
                        }
                    }

                    if (kc != null && kc.Killed > 0)
                    {
                        using var pushColor2 = ImRaii.PushColor(ImGuiCol.Text, _textColorKc, kc.Killed > 0);
                        if (Plugin.Configuration.ShowMissingKC)
                            ImGui.Text($"{kc.Killed} ～ {kc.Killed+kc.Missing}");
                        else
                            ImGui.Text($"{kc.Killed}  ");
                    }
                    else
                    {
                        ImGui.Text("-   ");
                    }
                }
                else
                {
                    ImGui.Text("-   ");
                }

                // XXX: The hover area for table text seems broken
                //      As a work-around I'm adding spaces to the end of KC text
                if (ImGui.IsItemHovered())
                {
                    var color2 = RGB(255, 255, 255);
                    using var pushColor2 = ImRaii.PushColor(ImGuiCol.Text, color2, color2 != Vector4.Zero);
                    ImGui.SetTooltip(kcNameStr);
                }
            }
            ImGui.TableNextColumn();
            if (scanResult != null)
            {
                if (scanResult.Missing)
                {
                    if (respawnIn == TimeSpan.Zero)
                    {
                        // Spawn is avaialble again
                        ImGui.Text("-");
                    }
                    else
                    {
                        var mins = System.Math.Floor(respawnIn.TotalMinutes + 0.99).ToString("0");
                        ImGui.Text($"{mins}m");
                    }
                }
                else
                {
                    ScannedFate? activeFate = null;

                    foreach (var fate in HuntModel.ActiveFateValues)
                    {
                        if (fate.EnglishName == ce.CEName)
                        {
                            activeFate = fate;
                            break;
                        }
                    }

                    if (activeFate != null)
                    {
                        // TODO: Create a fake timer
                        var timeRemaining = activeFate.TimeRemaining;
                        // The fate has ended so it should not be visible anymore
                        if (timeRemaining <= TimeSpan.Zero)
                            timeRemaining = TimeSpan.Zero;

                        var cappedTime = (float)System.Math.Clamp((900.0 - timeRemaining.TotalSeconds) / 4.0, 32.0, 224.0);
                        using var pushTimeColor = ImRaii.PushColor(ImGuiCol.Text, RGB(cappedTime, 255.0f - cappedTime, 32.0f));

                        var mins = System.Math.Floor(timeRemaining.TotalMinutes).ToString("0");
                        var secs = timeRemaining.Seconds.ToString("00");
                        ImGui.Text($"{mins}:{secs}");
                    }
                    else
                    {
                        ImGui.Text("!!!");
                    }
                }
            }
            else
            {
                // No data -- spawn may or may not be available
                ImGui.Text("-");
            }
        }
    }

    private void DrawCECooldown()
    {
        if (HuntModel.LastEndedCEUtc == DateTime.MinValue)
            return;
        var span = HuntModel.LastEndedCEUtc - HuntModel.UtcNow;
        var secs = System.Math.Floor(span.TotalSeconds) + 180;
        if (secs < 0)
            return;
        span = TimeSpan.FromSeconds(secs);
        using var pushColor = ImRaii.PushColor(ImGuiCol.Text, _textColorDead);
        ImGuiHelpers.CenteredText(span.ToString());
    }
}
