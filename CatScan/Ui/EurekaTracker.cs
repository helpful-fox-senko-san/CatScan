using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CatScan.Ui;

public partial class MainWindow : Window, IDisposable
{
    private void DrawEurekaTracker()
    {
        using var tabId = ImRaii.PushId("EurekaTracker");

        if (!HuntData.EurekaZones.TryGetValue(HuntModel.Territory.ZoneId, out var eurekaZone))
            return;

        using var table = ImRaii.Table("EurekaTable", 5, ImGuiTableFlags.Reorderable | ImGuiTableFlags.Resizable | ImGuiTableFlags.Hideable);
        ImGui.TableSetupColumn("Lv.", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort);
        ImGui.TableSetupColumn("NM Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Mob Name", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultHide);
        ImGui.TableSetupColumn("KC ", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Time ", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableHeadersRow();

        foreach (var nm in eurekaZone.NMs)
        {
            var color = _textColorGone;
            TimeSpan respawnIn = TimeSpan.Zero;
            float hpPct = 0.0f;
            DateTime firstSeen = DateTime.MinValue;
            bool dead = false;

            ScanResult? scanResult = null;
            ScanResult? scanResult2 = null;

            // XXX: Technically an implementation detail that its possible to get the scan results by name
            HuntModel.ScanResults.TryGetValue(nm.NMName, out scanResult);

            // XXX: Brothers and Lumber Jack fates have multiple enemies...
            //      Monster-based logic works OK here because Pagos and Pyros have infinite NM draw distance
            if (nm.FateName == "Brothers")
            {
                HuntModel.ScanResults.TryGetValue("Eldertaur", out scanResult2);

                // Average the HP of each boss

                if (scanResult != null)
                {
                    hpPct += scanResult.HpPct / 2.0f;
                    firstSeen = scanResult.FirstSeenTimeUtc;
                    dead = false;
                }

                if (scanResult2 != null)
                {
                    hpPct += scanResult2.HpPct / 2.0f;
                    firstSeen = scanResult2.FirstSeenTimeUtc;
                    dead = false;
                }

                dead = (hpPct == 0.0f);
            }
            else if (nm.FateName == "You Do Know Jack")
            {
                HuntModel.ScanResults.TryGetValue("Lumber Jack", out scanResult2);

                // Combine boss somewhat HP proportionally (25% on willow, 75% on jack)

                if (scanResult != null)
                {
                    hpPct = 75.0f + scanResult.HpPct * (1.0f / 4.0f);
                    firstSeen = scanResult.FirstSeenTimeUtc;
                    dead = false;
                }

                if (scanResult2 != null)
                {
                    hpPct = scanResult2.HpPct * (3.0f / 4.0f);
                    firstSeen = scanResult2.FirstSeenTimeUtc;
                    dead = false;
                }

                dead = (hpPct == 0.0f);
            }
            else if (scanResult != null)
            {
                // Generic logic for all other NMs
                // Hydatos lacks infinite draw distance on NMs, but their scan results are synthesized
                hpPct = scanResult.HpPct;
                firstSeen = scanResult.FirstSeenTimeUtc;
                dead = (hpPct == 0.0f);
            }
            else
            {
                // If there is no scanResult, that means the NM has not yet been seen, and assumed to be spawnable
            }

            if (dead)
            {
                var respawnTime = TimeSpan.FromHours(2);

                if (nm.NMName == "Ovni")
                    respawnTime = TimeSpan.FromMinutes(30);
                else if (nm.NMName == "Tristitia")
                    respawnTime = TimeSpan.Zero;

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
            ImGui.Text(nm.Level.ToString());
            ImGui.TableNextColumn();

            var name = nm.NMName;

            // XXX: More common names for NM encounters
            if (name == "Mindertaur")
                name = "Brothers";
            else if (name == "The Weeping Willow")
                name = "Lumber Jack";

            var nameStr = GameData.TranslateBNpcName(name);
            var kcNameStr = GameData.TranslateBNpcName(nm.KCName);

            if (scanResult == null)
            {
                var color2 = RGB(255, 255, 255);
                using var pushColor2 = ImRaii.PushColor(ImGuiCol.Text, color2, color2 != Vector4.Zero);
                ImGui.Text(nameStr);
            }
            else if (scanResult.Dead)
            {
                var color2 = _textColorDead;
                using var pushColor2 = ImRaii.PushColor(ImGuiCol.Text, color2, color2 != Vector4.Zero);
                ImGui.Text(nameStr);
            }
            else
            {
                if (hpPct < 100.0f)
                    nameStr += $" (HP: {hpPct:F1}%)";
                nameStr += "##" + scanResult.EnglishName;

                if (ImGui.Selectable(nameStr, false, ImGuiSelectableFlags.AllowItemOverlap))
                    GameFunctions.OpenMapLink(scanResult.MapX, scanResult.MapY);
            }
            ImGui.TableNextColumn();
            ImGui.Text(kcNameStr);
            ImGui.TableNextColumn();
            {
                // TODO: Clickable KC mob location

                if (respawnIn == TimeSpan.Zero)
                {
                    KillCount? kc = null;

                    foreach (var r in HuntModel.KillCountLog)
                    {
                        if (r.EnglishName == nm.KCName)
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
                        ImGui.Text("-  ");
                    }
                }
                else
                {
                    ImGui.Text("-  ");
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
                if (scanResult.Dead)
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
                    ActiveFate? activeFate = null;

                    foreach (var fate in HuntModel.ActiveFates.Values)
                    {
                        if (fate.EnglishName == nm.FateName)
                        {
                            activeFate = fate;
                            break;
                        }
                    }

                    if (activeFate != null)
                    {
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
}
