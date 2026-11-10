using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

public static class SecurityState
{
	private static string lastSentFp;

	private static float nextCheck;

	private static int sent;

	private static int applied;

	public static string Counters => sent.ToString(CultureInfo.InvariantCulture) + "/" + applied.ToString(CultureInfo.InvariantCulture);

	public static MsgSecState Capture()
	{
		MsgSecState msgSecState = new MsgSecState();
		PlayerStore store = Game.Store;
		if (!Game.ShopLoaded || store == null)
		{
			return msgSecState;
		}
		SecData secData = store.secData;
		if (secData != null)
		{
			if (secData.crimeTypes != null)
			{
				msgSecState.crimeTypes = new List<string>(secData.crimeTypes);
			}
			if (secData.crimeAmount != null)
			{
				msgSecState.crimeAmount = new List<int>(secData.crimeAmount);
			}
			if (secData.crimeAmountTotal != null)
			{
				msgSecState.crimeAmountTotal = new List<int>(secData.crimeAmountTotal);
			}
			msgSecState.evidence = secData.evidenceLevel;
			msgSecState.secState = secData.state;
			msgSecState.trackers = new int[8] { secData.dirtyWaterTracker, secData.mislabeledWaterTracker, secData.badChemTrackerFraud, secData.badChemTrackerEndangerment, secData.bribeTracker, secData.badAmmoTracker, secData.badAmmoTrackerLegal, secData.endangermentItemTracker };
		}
		msgSecState.day = Game.Day;
		msgSecState.wrongful = store.wrongfulReportCount;
		if (store.wantedStatus != null)
		{
			msgSecState.wanted = new Dictionary<string, int>(store.wantedStatus);
		}
		msgSecState.augPresence = store.augPresence;
		msgSecState.augIntroduced = store.isAugIntroduced;
		msgSecState.augEnded = store.isAugEnded;
		msgSecState.nextAugDay = store.nextAugSpawnDay;
		msgSecState.shootingUnlocked = store.isShootingUnlocked;
		msgSecState.vandalized = store.isVandalized;
		msgSecState.daySinceVandalism = store.daySinceLastVandalism;
		msgSecState.inspLossDay = store.lastInspectionLossDay;
		msgSecState.inspLossValue = store.lastInspectionLossValue;
		msgSecState.theftDay = store.lastTheftDay;
		msgSecState.theftValue = store.lastTheftValue;
		msgSecState.alarmActive = store.isSecurityAlarmActive;
		msgSecState.cartelCount = store.cartelRequiredCount;
		msgSecState.cartelPurity = store.cartelRequiredPurity;
		msgSecState.cartelStage = store.cartelExitStage;
		msgSecState.cartelExited = store.isCartelExited;
		msgSecState.poster = (Object)(object)StorePoster.Instance != (Object)null && StorePoster.Instance.isPulled;
		StoreClientManager storeClientManager = store.storeClientManager;
		if (storeClientManager != null)
		{
			if (storeClientManager.arrestedClients != null)
			{
				msgSecState.arrested = new List<string>(storeClientManager.arrestedClients);
			}
			msgSecState.daySinceInspection = storeClientManager.daySinceInspection;
			msgSecState.dayUntilInspection = storeClientManager.dayUntilInspection;
			msgSecState.inspectionSeeded = storeClientManager.inspectionSeeded;
		}
		StoreUIManager instance = StoreUIManager.Instance;
		msgSecState.arresting = store.isClientBeingArrested;
		msgSecState.shootout = store.isHandlingShootout;
		msgSecState.bellFlag = store.lockNextBellFlag;
		if ((Object)(object)instance != (Object)null)
		{
			msgSecState.shot = instance.isShot;
			msgSecState.reportedAug = instance.isReportedAug;
			msgSecState.glassClosed = (Object)(object)instance.StoreGlass != (Object)null && instance.StoreGlass.IsClosed;
		}
		msgSecState.monologue = (Object)(object)DialogUIManager.Instance != (Object)null && DialogUIManager.Instance.isSecMonologuePlaying;
		msgSecState.fp = Fingerprint(msgSecState);
		return msgSecState;
	}

	public static string Fingerprint(MsgSecState m)
	{
		StringBuilder stringBuilder = new StringBuilder(256);
		stringBuilder.Append(string.Join(",", m.crimeTypes)).Append('|').Append(string.Join(",", m.crimeAmount))
			.Append('|')
			.Append(string.Join(",", m.crimeAmountTotal))
			.Append('|')
			.Append(m.evidence)
			.Append('|')
			.Append(m.secState)
			.Append('|')
			.Append(string.Join(",", m.trackers ?? new int[0]))
			.Append('|')
			.Append(m.wrongful)
			.Append('|');
		List<string> list = new List<string>(m.wanted.Keys);
		list.Sort(StringComparer.Ordinal);
		foreach (string item in list)
		{
			stringBuilder.Append(item).Append('=').Append(m.wanted[item])
				.Append(';');
		}
		stringBuilder.Append('|').Append(m.augPresence).Append(m.augIntroduced ? 'I' : 'i')
			.Append(m.augEnded ? 'E' : 'e')
			.Append(m.nextAugDay)
			.Append('|')
			.Append(m.shootingUnlocked ? 'S' : 's')
			.Append(m.vandalized ? 'V' : 'v')
			.Append(m.daySinceVandalism)
			.Append('|')
			.Append(m.inspLossDay)
			.Append(',')
			.Append(m.inspLossValue)
			.Append(',')
			.Append(m.theftDay)
			.Append(',')
			.Append(m.theftValue)
			.Append('|')
			.Append(m.alarmActive ? 'A' : 'a')
			.Append(m.cartelCount)
			.Append(',')
			.Append(m.cartelPurity)
			.Append(',')
			.Append(m.cartelStage)
			.Append(m.cartelExited ? 'X' : 'x')
			.Append('|')
			.Append(m.poster ? 'P' : 'p')
			.Append('|')
			.Append(string.Join(",", m.arrested))
			.Append('|')
			.Append(m.daySinceInspection)
			.Append(',')
			.Append(m.dayUntilInspection)
			.Append(m.inspectionSeeded ? 'Z' : 'z');
		ulong num = 14695981039346656037uL;
		string text = stringBuilder.ToString();
		foreach (char c in text)
		{
			num ^= c;
			num *= 1099511628211L;
		}
		return num.ToString("x16");
	}

	public static string Diff(MsgSecState a, MsgSecState b)
	{
		List<string> d = new List<string>();
		C("crimes", string.Join(",", a.crimeTypes) + ":" + string.Join(",", a.crimeAmount), string.Join(",", b.crimeTypes) + ":" + string.Join(",", b.crimeAmount));
		C("crimeTotals", string.Join(",", a.crimeAmountTotal), string.Join(",", b.crimeAmountTotal));
		C("evidence", a.evidence, b.evidence);
		C("officerState", a.secState, b.secState);
		C("trackers", string.Join(",", a.trackers ?? new int[0]), string.Join(",", b.trackers ?? new int[0]));
		C("wrongful", a.wrongful, b.wrongful);
		C("wanted", WantedText(a.wanted), WantedText(b.wanted));
		C("augPresence", a.augPresence, b.augPresence);
		C("augFlags", a.augIntroduced.ToString() + a.augEnded + a.nextAugDay, b.augIntroduced.ToString() + b.augEnded + b.nextAugDay);
		C("shotgun", a.shootingUnlocked, b.shootingUnlocked);
		C("vandalism", a.vandalized.ToString() + a.daySinceVandalism, b.vandalized.ToString() + b.daySinceVandalism);
		C("inspectionLoss", a.inspLossDay + "/" + a.inspLossValue, b.inspLossDay + "/" + b.inspLossValue);
		C("theft", a.theftDay + "/" + a.theftValue, b.theftDay + "/" + b.theftValue);
		C("alarm", a.alarmActive, b.alarmActive);
		C("cartel", a.cartelCount + "," + a.cartelPurity + "," + a.cartelStage + "," + a.cartelExited, b.cartelCount + "," + b.cartelPurity + "," + b.cartelStage + "," + b.cartelExited);
		C("poster", a.poster, b.poster);
		C("arrested", string.Join(",", a.arrested), string.Join(",", b.arrested));
		C("inspectionTimer", a.daySinceInspection + "," + a.dayUntilInspection + "," + a.inspectionSeeded, b.daySinceInspection + "," + b.dayUntilInspection + "," + b.inspectionSeeded);
		C("arresting", a.arresting, b.arresting);
		C("shootout", a.shootout, b.shootout);
		C("shot", a.shot, b.shot);
		return string.Join(", ", d);
		void C(string n, object x, object y)
		{
			if (!object.Equals(x, y))
			{
				d.Add(n + " " + x?.ToString() + "/" + y);
			}
		}
	}

	private static string WantedText(Dictionary<string, int> w)
	{
		List<string> list = new List<string>(w.Keys);
		list.Sort(StringComparer.Ordinal);
		List<string> list2 = new List<string>();
		foreach (string item in list)
		{
			list2.Add(item + "=" + w[item]);
		}
		return string.Join(";", list2);
	}

	public static bool Idle(bool strict)
	{
		PlayerStore store = Game.Store;
		StoreUIManager instance = StoreUIManager.Instance;
		if (!Game.ShopLoaded || store == null || (Object)(object)instance == (Object)null)
		{
			return false;
		}
		if (store.currentClientInstance != null)
		{
			return false;
		}
		if (Net.IsClient)
		{
			if (Security.ClientOfficersHere)
			{
				return false;
			}
		}
		else if (Security.OfficerActive(1) || Security.OfficerActive(2))
		{
			return false;
		}
		if (!strict)
		{
			return true;
		}
		if (!store.isClientBeingArrested && !store.isHandlingShootout && !instance.isShot && !Net.Applying)
		{
			return !Snapshot.Capturing;
		}
		return false;
	}

	internal static void ServerTick(float now)
	{
		if (now < nextCheck)
		{
			return;
		}
		nextCheck = now + 0.5f;
		if (!Net.IsLive || Net.Players.Count == 0 || !Idle(strict: true))
		{
			return;
		}
		MsgSecState msgSecState = Capture();
		if (!(msgSecState.fp == lastSentFp))
		{
			msgSecState.idle = true;
			Net.ToAll("sec.state", msgSecState);
			if (sent++ < 30 || lastSentFp == null)
			{
				Log.Info("server: security record sent (day " + msgSecState.day + ", fp " + msgSecState.fp + ", wrongful " + msgSecState.wrongful + ", evidence " + msgSecState.evidence + ", aug " + msgSecState.augPresence + ")");
			}
			lastSentFp = msgSecState.fp;
		}
	}

	internal static void SendTo(int player)
	{
		if (Net.IsServer && Game.ShopLoaded)
		{
			MsgSecState msgSecState = Capture();
			msgSecState.idle = Idle(strict: true);
			Net.ToPlayer(player, "sec.state", msgSecState);
		}
	}

	internal static void ClientReceive(MsgSecState m)
	{
		if (!Game.ShopLoaded)
		{
			return;
		}
		MsgSecState msgSecState = Capture();
		bool flag = msgSecState.arresting != m.arresting || msgSecState.shootout != m.shootout || msgSecState.shot != m.shot;
		if (!m.idle || !Idle(strict: false))
		{
			if (msgSecState.fp != m.fp)
			{
				Log.Info("client: security record differs while busy (" + Diff(m, msgSecState) + "); the next idle record settles it");
			}
		}
		else if (!(msgSecState.fp == m.fp) || flag || msgSecState.reportedAug != m.reportedAug || msgSecState.glassClosed != m.glassClosed)
		{
			string text = Diff(m, msgSecState);
			Log.Info("client: taking the server's security record (" + ((text.Length > 0) ? text : "flags only") + ")");
			Apply(m);
			applied++;
		}
	}

	internal static void ClientTick()
	{
	}

	private static void Apply(MsgSecState m)
	{
		PlayerStore store = Game.Store;
		StoreUIManager instance = StoreUIManager.Instance;
		using (Net.Apply())
		{
			SecData obj = store.secData;
			if (obj == null)
			{
				SecData val = new SecData();
				SecData val2 = val;
				store.secData = val;
				obj = val2;
			}
			SecData val3 = obj;
			val3.crimeTypes = new List<string>(m.crimeTypes ?? new List<string>());
			val3.crimeAmount = new List<int>(m.crimeAmount ?? new List<int>());
			val3.crimeAmountTotal = new List<int>(m.crimeAmountTotal ?? new List<int>());
			val3.evidenceLevel = m.evidence;
			val3.state = m.secState;
			int[] array = m.trackers ?? new int[8];
			if (array.Length >= 8)
			{
				val3.dirtyWaterTracker = array[0];
				val3.mislabeledWaterTracker = array[1];
				val3.badChemTrackerFraud = array[2];
				val3.badChemTrackerEndangerment = array[3];
				val3.bribeTracker = array[4];
				val3.badAmmoTracker = array[5];
				val3.badAmmoTrackerLegal = array[6];
				val3.endangermentItemTracker = array[7];
			}
			store.wrongfulReportCount = m.wrongful;
			if (store.wantedStatus == null)
			{
				store.wantedStatus = new Dictionary<string, int>();
			}
			store.wantedStatus.Clear();
			if (m.wanted != null)
			{
				foreach (KeyValuePair<string, int> item in m.wanted)
				{
					store.wantedStatus[item.Key] = item.Value;
				}
			}
			store.augPresence = m.augPresence;
			store.isAugIntroduced = m.augIntroduced;
			store.isAugEnded = m.augEnded;
			store.nextAugSpawnDay = m.nextAugDay;
			bool flag = store.isShootingUnlocked != m.shootingUnlocked;
			store.isShootingUnlocked = m.shootingUnlocked;
			store.isVandalized = m.vandalized;
			store.daySinceLastVandalism = m.daySinceVandalism;
			store.lastInspectionLossDay = m.inspLossDay;
			store.lastInspectionLossValue = m.inspLossValue;
			store.lastTheftDay = m.theftDay;
			store.lastTheftValue = m.theftValue;
			store.isSecurityAlarmActive = m.alarmActive;
			store.cartelRequiredCount = m.cartelCount;
			store.cartelRequiredPurity = m.cartelPurity;
			store.cartelExitStage = m.cartelStage;
			store.isCartelExited = m.cartelExited;
			StoreClientManager storeClientManager = store.storeClientManager;
			if (storeClientManager != null)
			{
				storeClientManager.arrestedClients = new List<string>(m.arrested ?? new List<string>());
				storeClientManager.daySinceInspection = m.daySinceInspection;
				storeClientManager.dayUntilInspection = m.dayUntilInspection;
				storeClientManager.inspectionSeeded = m.inspectionSeeded;
			}
			try
			{
				if (flag && (Object)(object)instance != (Object)null)
				{
					Shotgun.LoadShotgun();
				}
			}
			catch (Exception ex)
			{
				Log.Warn("client: shotgun visual: " + ex.Message);
			}
			StorePoster instance2 = StorePoster.Instance;
			if ((Object)(object)instance2 != (Object)null && instance2.isPulled != m.poster)
			{
				if (m.poster)
				{
					instance2.PullUp(true);
				}
				else
				{
					instance2.PullDown(true);
				}
			}
			bool isClientBeingArrested = store.isClientBeingArrested;
			store.isClientBeingArrested = m.arresting;
			store.isHandlingShootout = m.shootout;
			if (isClientBeingArrested && !m.arresting)
			{
				store.lockNextBellFlag = m.bellFlag;
				store.onSecAlarmLeftAction = null;
			}
			if ((Object)(object)instance != (Object)null)
			{
				instance.isShot = m.shot;
				if (!m.shot)
				{
					instance.shotClientInstance = null;
				}
				instance.isReportedAug = m.reportedAug;
				if (!m.glassClosed && (Object)(object)instance.StoreGlass != (Object)null && instance.StoreGlass.IsClosed)
				{
					instance.StoreGlass.OpenGlassDoor();
				}
			}
			if ((Object)(object)DialogUIManager.Instance != (Object)null)
			{
				DialogUIManager.Instance.isSecMonologuePlaying = m.monologue;
			}
		}
	}
}
