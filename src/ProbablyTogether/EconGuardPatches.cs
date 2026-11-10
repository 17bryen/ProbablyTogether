using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ProbablyTogether;

[HarmonyPatch]
internal static class EconGuardPatches
{
	private static readonly string[] names = new string[17]
	{
		"PlayerStore.OnRentPaid", "PlayerStore.OnRentNotPaid", "PlayerStore.BuyOutStore", "PlayerStore.TakeMortgage", "PlayerStore.PayWeeklyMortgage", "PlayerStore.OnMortgagePaidOff", "PlayerStore.Repo", "PlayerStore.ModCash", "PlayerStore.PayMoney", "NetworkUpgrade.Unlock",
		"StoreRequest.DispenseReward", "StorePhoneClient.UseService", "PermitHelper.GrantedFromPermit", "PermitHelper.OnPermitGranted", "PermitHelper.OnPermitRevoked", "StoreReputation.ModReputation", "StoreReputation.ModReputationRaw"
	};

	private static MethodBase[] targets;

	private static readonly Dictionary<MethodBase, string> nameOf = new Dictionary<MethodBase, string>();

	private static MethodBase[] Targets()
	{
		if (targets != null)
		{
			return targets;
		}
		List<MethodBase> l = new List<MethodBase>();
		Add(() => AccessTools.Method(typeof(PlayerStore), "OnRentPaid", Type.EmptyTypes, (Type[])null), names[0]);
		Add(() => AccessTools.Method(typeof(PlayerStore), "OnRentNotPaid", Type.EmptyTypes, (Type[])null), names[1]);
		Add(() => AccessTools.Method(typeof(PlayerStore), "BuyOutStore", Type.EmptyTypes, (Type[])null), names[2]);
		Add(() => AccessTools.Method(typeof(PlayerStore), "TakeMortgage", Type.EmptyTypes, (Type[])null), names[3]);
		Add(() => AccessTools.Method(typeof(PlayerStore), "PayWeeklyMortgage", Type.EmptyTypes, (Type[])null), names[4]);
		Add(() => AccessTools.Method(typeof(PlayerStore), "OnMortgagePaidOff", Type.EmptyTypes, (Type[])null), names[5]);
		Add(() => AccessTools.Method(typeof(PlayerStore), "Repo", Type.EmptyTypes, (Type[])null), names[6]);
		Add(() => AccessTools.Method(typeof(PlayerStore), "ModCash", new Type[2]
		{
			typeof(int),
			typeof(bool)
		}, (Type[])null), names[7]);
		Add(() => AccessTools.Method(typeof(PlayerStore), "PayMoney", new Type[2]
		{
			typeof(int),
			typeof(bool)
		}, (Type[])null), names[8]);
		Add(() => AccessTools.Method(typeof(NetworkUpgrade), "Unlock", Type.EmptyTypes, (Type[])null), names[9]);
		Add(() => AccessTools.Method(typeof(StoreRequest), "DispenseReward", Type.EmptyTypes, (Type[])null), names[10]);
		Add(() => AccessTools.Method(typeof(StorePhoneClient), "UseService", Type.EmptyTypes, (Type[])null), names[11]);
		Add(() => AccessTools.Method(typeof(PermitHelper), "GrantedFromPermit", new Type[1] { typeof(GameItem) }, (Type[])null), names[12]);
		Add(() => AccessTools.Method(typeof(PermitHelper), "OnPermitGranted", new Type[2]
		{
			typeof(int),
			typeof(string)
		}, (Type[])null), names[13]);
		Add(() => AccessTools.Method(typeof(PermitHelper), "OnPermitRevoked", new Type[2]
		{
			typeof(int),
			typeof(string)
		}, (Type[])null), names[14]);
		Add(() => AccessTools.Method(typeof(StoreReputation), "ModReputation", new Type[3]
		{
			typeof(string),
			typeof(int),
			typeof(bool)
		}, (Type[])null), names[15]);
		Add(() => AccessTools.Method(typeof(StoreReputation), "ModReputationRaw", new Type[3]
		{
			typeof(string),
			typeof(int),
			typeof(bool)
		}, (Type[])null), names[16]);
		return targets = l.ToArray();
		void Add(Func<MethodBase> f, string n)
		{
			MethodBase methodBase = EconomyHook.Find(f, n);
			if (methodBase != null)
			{
				l.Add(methodBase);
				nameOf[methodBase] = n;
			}
		}
	}

	private static bool Prepare()
	{
		return Targets().Length != 0;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return Targets();
	}

	private static bool Prefix(MethodBase __originalMethod)
	{
		if (!Net.IsClient || Net.Applying)
		{
			return true;
		}
		string value;
		return Economy.Guard((__originalMethod != null && nameOf.TryGetValue(__originalMethod, out value)) ? value : "an economy commit");
	}
}
