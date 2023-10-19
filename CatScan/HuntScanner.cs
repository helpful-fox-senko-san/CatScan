using System.Collections.Generic;

namespace CatScan;

// Consumes events from GameScanner and uses it to build up the state in HuntModel
public class HuntScanner
{
	private class KCEnemy
	{
		public string Name;
		public bool Missing = false;

		public KCEnemy(GameEnemy enemy)
		{
			Name = enemy.Name;
		}
	}

	private GameScanner _gameScanner;
	private Dictionary<uint, KCEnemy> _kcEnemies = new();

	public HuntScanner(GameScanner gameScanner)
	{
		_gameScanner = gameScanner;

		_gameScanner.NewEnemy += OnNewEnemy;
		_gameScanner.LostEnemy += OnLostEnemy;
		_gameScanner.UpdatedEnemy += OnUpdatedEnemy;
		_gameScanner.ZoneChange += OnZoneChange;
	}

	private void OnNewEnemy(GameEnemy enemy)
	{
		foreach (var mark in HuntModel.Territory.ZoneData.Marks)
		{
			if (mark.Name == enemy.Name)
			{
				// Don't actually log KC monsters as marks
				if (mark.Rank == Rank.KC)
				{
					if (_kcEnemies.ContainsKey(enemy.ObjectId))
					{
						DalamudService.Log.Error($"Received NewEnemy event for the same object id twice.");
						break;
					}

					_kcEnemies.Add(enemy.ObjectId, new KCEnemy(enemy));
					// Tell GameScanner to only update us if the enemy is killed
					enemy.InterestingKC = true;
					break;
				}

				if (HuntModel.ScanResults.ContainsKey(enemy.Name))
				{
					// New object ID with the same name as an already logged mark
					if (HuntModel.ScanResults[enemy.Name].PossiblyDead)
					{
						// Probably a respawn
						HuntModel.ScanResults[enemy.Name].Update(enemy);
						enemy.Interesting = true;
					}
					else
					{
						// XXX: This now appears when using "Import from Clipboard" debug feature
						DalamudService.Log.Warning($"Received NewEnemy event for a monster we already found. (boss fate aoe dummy issue)");
					}
				}
				else
				{
					HuntModel.ScanResults.Add(mark.Name, new ScanResult(mark, enemy));
					// Tell GameScanner to continue to poll for information about this enemy
					enemy.Interesting = true;
				}

				break;
			}
		}
	}

	private void OnLostEnemy(GameEnemy enemy)
	{
		// Its not possible to tell if a KC mob dies while out of range, so keep count of them
		if (_kcEnemies.ContainsKey(enemy.ObjectId))
		{
			if (!_kcEnemies[enemy.ObjectId].Missing && HuntModel.KillCountLog.ContainsKey(enemy.Name))
			{
				_kcEnemies[enemy.ObjectId].Missing = true;
				++HuntModel.KillCountLog[enemy.Name].Missing;
			}
		}

		if (HuntModel.ScanResults.ContainsKey(enemy.Name))
			HuntModel.ScanResults[enemy.Name].Lost();
	}

	private void OnUpdatedEnemy(GameEnemy enemy)
	{
		// This is a KC mob dying or coming back in range
		if (_kcEnemies.ContainsKey(enemy.ObjectId))
		{
			if (_kcEnemies[enemy.ObjectId].Missing && HuntModel.KillCountLog.ContainsKey(enemy.Name))
			{
				_kcEnemies[enemy.ObjectId].Missing = false;
				--HuntModel.KillCountLog[enemy.Name].Missing;
			}

			if (enemy.HpPct == 0.0)
			{
				if (HuntModel.KillCountLog.ContainsKey(enemy.Name))
					++HuntModel.KillCountLog[enemy.Name].Killed;
				_kcEnemies.Remove(enemy.ObjectId);
			}
		}

		if (HuntModel.ScanResults.ContainsKey(enemy.Name))
			HuntModel.ScanResults[enemy.Name].Update(enemy);
	}

	private void OnZoneChange(GameZoneInfo zoneInfo)
	{
		// XXX: This loses the "missing" kc data -- probably needs to be cached per zone as well
		_kcEnemies.Clear();

		// Tell GameScanner to scan for enemies if we're in a known hunt zone
		if (HuntData.Zones.ContainsKey(zoneInfo.ZoneId))
			_gameScanner.EnableScanning();

		HuntModel.SwitchZone(zoneInfo.WorldId, zoneInfo.ZoneId, zoneInfo.Instance);
		HuntModel.Territory.WorldName = zoneInfo.WorldName;
	}
}
