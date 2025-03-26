using Dalamud.Interface.Windowing;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace CatScan.Ui;

public partial class MainWindow : Window, IDisposable
{
    private ISharedImmediateTexture? _iconGrey;
    private ISharedImmediateTexture? _iconGreen;
    private ISharedImmediateTexture? _iconRed;

    private class TrainLogMark
    {
        public int ZoneId;
        public string Name => GetLocalName();
        public string EnglishName = string.Empty;
        public int Instance;

        private string _cachedLocalName = string.Empty;

        private string GetLocalName()
        {
            if (_cachedLocalName.Length > 0)
                return _cachedLocalName;

            if (!GameData.NameDataReady)
                return EnglishName;

            _cachedLocalName = GameData.TranslateBNpcName(EnglishName);
            return _cachedLocalName;
        }
    }

    private struct TrainLogExpansion
    {
        public Expansion Expansion;
        public string Name;
        public List<TrainLogMark> Marks;
    }

    private List<TrainLogExpansion> _trainLogExpansions = new();
    private TrainLogExpansion _selectedTrain;
    private int _selectedTrainIndex;

    public void SetTrainLogExpansion(Expansion ex)
    {
        for (int i = 0; i < _trainLogExpansions.Count; ++i)
        {
            if (_trainLogExpansions[i].Expansion == ex)
            {
                _selectedTrainIndex = i;
                _selectedTrain = _trainLogExpansions[i];
            }
        }
    }

    private void InitTrainLog()
    {
        _iconGrey = DalamudService.TextureProvider.GetFromFile(Path.Combine(_resourcePath, "Grey.png"));
        _iconGreen = DalamudService.TextureProvider.GetFromFile(Path.Combine(_resourcePath, "Green.png"));
        _iconRed = DalamudService.TextureProvider.GetFromFile(Path.Combine(_resourcePath, "Red.png"));

        var getExpansionZoneIds = (Expansion ex) => {
            List<int> zoneIds = new();

            foreach (var zone in HuntData.Zones)
            {
                if (zone.Value.Expansion == ex)
                    zoneIds.Add(zone.Key);
            }

            return zoneIds.ToArray();
        };

        // Index all of the A ranks for each expansion
        // A row is added for each instanced version of a mark
        // TODO: Manually list the zones in a more familiar order
        var addTrain = (Expansion ex, string name, int[] exZoneIds) => {
            var train = new TrainLogExpansion(){
                Expansion = ex,
                Name = name,
                Marks = new()
            };

            _trainLogExpansions.Add(train);

            foreach (var zoneId in exZoneIds)
            {
                var zone = HuntData.Zones[zoneId];

                for (int i = 1; i <= zone.Instances; ++i)
                {
                    foreach (var mark in zone.Marks)
                    {
                        if (mark.Rank == Rank.A)
                        {
                            train.Marks.Add(new(){
                                ZoneId = zoneId,
                                EnglishName = mark.Name,
                                Instance = i
                            });
                        }
                    }
                }
            }
        };

        addTrain(Expansion.ARR, "A Realm Reborn", HuntData.ZoneOrderARR);
        addTrain(Expansion.HW, "Heavensward", HuntData.ZoneOrderHW);
        addTrain(Expansion.SB, "Stormblood", HuntData.ZoneOrderSB);
        addTrain(Expansion.ShB, "Shadowbringers", HuntData.ZoneOrderShB);
        addTrain(Expansion.EW, "Endwalker", HuntData.ZoneOrderEW);
        addTrain(Expansion.DT, "Dawntrail", HuntData.ZoneOrderDT);

        _selectedTrainIndex = _trainLogExpansions.Count - 1;
        _selectedTrain = _trainLogExpansions[_selectedTrainIndex];
    }

    private void CopyTrainLog()
    {
        var text = string.Empty;

        foreach (var mark in _selectedTrain.Marks)
        {
            var zone = HuntData.Zones[mark.ZoneId];
            var scanCache = HuntModel.ForZone(mark.ZoneId, mark.Instance);
            bool dead = false;
            ScanResult? scanResult = null;

            foreach (var result in scanCache.ScanResults.Values)
            {
                if (result.EnglishName == mark.Name)
                {
                    scanResult = result;
                    dead = result.Dead;
                    break;
                }
            }

            if (scanResult != null && !dead)
            {
                text += $"{mark.EnglishName} \uE0BB{zone.Name} ( {scanResult.MapX:F1}  , {scanResult.MapY:F1} )";

                if (zone.Instances > 1)
                    text += $" i{mark.Instance}";

                text += "\r\n";
            }
        }

        ImGui.SetClipboardText(text.TrimEnd('\r', '\n'));
    }

    private void PasteTrainLog()
    {
        if (!GameData.NameDataReady)
            return;

        var text = ImGui.GetClipboardText().Trim('\n');
        var lines = text.Split('\n');

        foreach (var line in lines)
        {
            // FIXME: Splitting the mob name / zone name without relying on this character will be fun
            var parts = line.Trim().Split(" \uE0BB", 2);

            if (parts.Length == 1)
                continue;

            var mobNameEn = parts[0];
            var mobName = GameData.TranslateBNpcName(parts[0]);
            var location = parts[1].TrimStart('\uE0BB');

            int p1 = location.IndexOf('(');
            int c = location.IndexOf(',');
            int p2 = location.IndexOf(')');

            if (p1 == -1 || c == -1 || p2 == -1)
                continue;

            string zoneName = location.Substring(0, p1).Trim();
            float mapX = float.Parse(location.Substring(p1+1, c - (p1+1)).Trim());
            float mapY = float.Parse(location.Substring(c+1, p2 - (c+1)).Trim());

            string instanceStr = location.Substring(p2+1).Trim();
            int instance = 1;

            for (int i = 1; i <= 9; ++i)
            {
                if (zoneName.EndsWith((char)(0xE0B0 + i)))
                {
                    instance = i;
                    zoneName = zoneName.Remove(zoneName.Length - 1);
                    break;
                }
            }

            if (instanceStr.Length > 1)
            {
                int ipos = instanceStr.IndexOf('i');
                if (ipos >= 0 && ipos < instanceStr.Length - 1 && int.TryParse(instanceStr.Substring(ipos + 1), out var iresult))
                    instance = int.Clamp(iresult, 1, 9);
            }

            foreach (var zone in HuntData.Zones)
            {
                if (zone.Value.Name == zoneName)
                {
                    var model = HuntModel.ForZone(zone.Key, instance);

                    bool found = false;

                    foreach (var scanResult in model.ScanResults)
                    {
                        if (scanResult.Key == mobNameEn)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        model.ScanResults.TryAdd(mobNameEn, new ScanResult(){
                            Rank = Rank.A,
                            EnglishName = mobName,
                            // Could calculate raw coords but we don't use them anyway
                            RawX = 0.0f,
                            RawZ = 0.0f,
                            MapX = mapX,
                            MapY = mapY,
                            HpPct = 100.0f,
                            // Object ID of 0 is treated special and should not trigger a ping
                            ObjectId = 0,
                            FirstSeenTimeUtc = HuntModel.UtcNow,
                            LastSeenTimeUtc = HuntModel.UtcNow,
                            KillTimeUtc = System.DateTime.MinValue,
                            Missing = true
                        });
                    }

                    // TODO: Should overwrite the position if it was found?

                    break;
                }
            }
        }
    }

    private void DrawTrainSelectBox()
    {
        ImGui.PushItemWidth(130.0f);
        using var combo = ImRaii.Combo("##Expansion", _selectedTrain.Name);

        if (combo.Success)
        {
            int i = 0;

            foreach (var train in _trainLogExpansions)
            {
                if (ImGui.Selectable(train.Name, (i == _selectedTrainIndex)))
                {
                    _selectedTrainIndex = i;
                    _selectedTrain = _trainLogExpansions[_selectedTrainIndex];
                }

                ++i;
            }
        }
    }

    private void DrawTrainLog()
    {
        using var tabId = ImRaii.PushId("TrainLog");

        DrawTrainSelectBox();

        ImGui.SameLine();
        if (ImGui.Button("Copy List"))
            CopyTrainLog();
        ImGui.SameLine();
        if (ImGui.Button("Paste List"))
            PasteTrainLog();

        ImGui.Columns(2);

        int rowNum = 0;
        int midPoint = (_selectedTrain.Marks.Count + 1) / 2;

        foreach (var mark in _selectedTrain.Marks)
        {
            if (rowNum++ == midPoint)
                ImGui.NextColumn();

            var zone = HuntData.Zones[mark.ZoneId];
            var rowName = (zone.Instances == 1) ? mark.Name : $"{mark.Name} {mark.Instance}";
            var scanCache = HuntModel.ForZone(mark.ZoneId, mark.Instance);
            bool dead = false;
            ScanResult? scanResult = null;

            foreach (var result in scanCache.ScanResults.Values)
            {
                if (result.EnglishName == mark.Name)
                {
                    scanResult = result;
                    dead = result.Dead;
                    break;
                }
            }

            var icon = _iconGrey;

            if (scanResult != null)
                icon = dead ? _iconRed : _iconGreen;

            if (icon != null)
            {
                ImGui.Image(icon.GetWrapOrEmpty().ImGuiHandle, new(16, 16));
                ImGui.SameLine();
            }

            Vector4 color = Vector4.Zero;

            if (icon == _iconGrey)
                color = _textColorGone;
            else if (icon == _iconRed)
                color = _textColorDead;

            using var pushColor = ImRaii.PushColor(ImGuiCol.Text, color, color != Vector4.Zero);

            if (scanResult != null)
            {
                if (ImGui.Selectable(rowName))
                    GameFunctions.OpenMapLink(mark.ZoneId, scanResult.MapX, scanResult.MapY);
            }
            else
            {
                ImGui.Text($"{rowName}");
            }

            // Subtle visual separation between zones
            if (_selectedTrain.Expansion != Expansion.ARR)
            {
                if (rowNum % 2 == 0)
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2.0f);
            }
        }

        ImGui.Columns();
    }
}
