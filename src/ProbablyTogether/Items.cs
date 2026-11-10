using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

public static class Items
{
	private sealed class Scope : IDisposable
	{
		private Action end;

		public Scope(Action e)
		{
			end = e;
		}

		public void Dispose()
		{
			Action action = end;
			end = null;
			action?.Invoke();
		}
	}

	private static readonly KeyValuePair<string, Func<EmporiumEntry, GraphNodeStorage>>[] roots = new KeyValuePair<string, Func<EmporiumEntry, GraphNodeStorage>>[41]
	{
		R("inv", (EmporiumEntry e) => (GraphNodeStorage)(object)e.invElement),
		R("back", (EmporiumEntry e) => Systems.HostTrip.Back(e)),
		R("front", (EmporiumEntry e) => Systems.HostTrip.Front(e)),
		R("showcase", (EmporiumEntry e) => (GraphNodeStorage)(object)e.showcaseElement),
		R("hidden", (EmporiumEntry e) => (GraphNodeStorage)(object)e.hiddenElement),
		R("sold", (EmporiumEntry e) => (GraphNodeStorage)(object)e.soldElement),
		R("hireling", (EmporiumEntry e) => (GraphNodeStorage)(object)e.hirelingInv),
		R("vendingMachine", (EmporiumEntry e) => (GraphNodeStorage)(object)e.vendingMachineElement),
		R("vendingFountain", (EmporiumEntry e) => (GraphNodeStorage)(object)e.vendingFountainElement),
		R("cassette", (EmporiumEntry e) => (GraphNodeStorage)(object)e.cassettePlayerElement),
		R("docSlot", (EmporiumEntry e) => (GraphNodeStorage)(object)e.docInvElement),
		R("trashSlot", (EmporiumEntry e) => (GraphNodeStorage)(object)e.trashInvElement),
		R("drain", (EmporiumEntry e) => (GraphNodeStorage)(object)e.drainInvElement),
		R("faucet", (EmporiumEntry e) => (GraphNodeStorage)(object)e.faucetElement),
		R("afterhour", (EmporiumEntry e) => Systems.HostTrip.Carried((GraphNodeStorage)(object)e.afterhourInventory)),
		R("backc", (EmporiumEntry e) => Systems.HostTrip.Holding(e)),
		R("pocketL", (EmporiumEntry e) => Systems.HostTrip.Carried((GraphNodeStorage)(object)e.afterhourPocketSlotInvLeft)),
		R("pocketB", (EmporiumEntry e) => Systems.HostTrip.Carried((GraphNodeStorage)(object)e.afterhourPocketSlotInvBackpack)),
		R("pocketR", (EmporiumEntry e) => Systems.HostTrip.Carried((GraphNodeStorage)(object)e.afterhourPocketSlotInvRight)),
		R("bazar", (EmporiumEntry e) => (GraphNodeStorage)(object)e.bazarLeftinvElement),
		R("swap", (EmporiumEntry e) => (GraphNodeStorage)(object)e.swapBufferElement),
		R("response", (EmporiumEntry e) => (GraphNodeStorage)(object)e.responseInventory),
		R("responseC", (EmporiumEntry e) => (GraphNodeStorage)(object)e.responseInventoryClosable),
		R("trash", delegate(EmporiumEntry e)
		{
			GameItem trashcan = e.trashcan;
			if (trashcan == null)
			{
				return (GraphNodeStorage)null;
			}
			PixelWindow contentWindow = trashcan.contentWindow;
			return (contentWindow == null) ? null : contentWindow.child;
		}),
		R("inv.w", (EmporiumEntry e) => (GraphNodeStorage)(object)e.invWindow),
		R("back.w", (EmporiumEntry e) => Systems.HostTrip.CountersBorrowed && Systems.HostTrip.HomeView ? Systems.HostTrip.Back(e) : (GraphNodeStorage)(object)e.backInvWindow),
		R("front.w", (EmporiumEntry e) => Systems.HostTrip.FrontAway && Systems.HostTrip.HomeView ? Systems.HostTrip.Front(e) : (GraphNodeStorage)(object)e.frontInvWindow),
		R("showcase.w", (EmporiumEntry e) => (GraphNodeStorage)(object)e.showcaseWindow),
		R("hidden.w", (EmporiumEntry e) => (GraphNodeStorage)(object)e.hiddenWindow),
		R("afterhour.w", (EmporiumEntry e) => Systems.HostTrip.Carried((GraphNodeStorage)(object)e.afterhourWindow)),
		R("hireling.w", (EmporiumEntry e) => (GraphNodeStorage)(object)e.hirelingWindow),
		R("vendingMachine.w", (EmporiumEntry e) => (GraphNodeStorage)(object)e.vendingMachineWindow),
		R("vendingFountain.w", (EmporiumEntry e) => (GraphNodeStorage)(object)e.vendingFountainWindow),
		R("docSlot.w", (EmporiumEntry e) => (GraphNodeStorage)(object)e.docInvWindow),
		R("trashSlot.w", (EmporiumEntry e) => (GraphNodeStorage)(object)e.trashInvWindow),
		R("drain.w", (EmporiumEntry e) => (GraphNodeStorage)(object)e.drainInvWindow),
		R("faucet.w", (EmporiumEntry e) => (GraphNodeStorage)(object)e.faucetWindow),
		R("cassette.w", (EmporiumEntry e) => (GraphNodeStorage)(object)e.cassettePlayerWindow),
		R("bazar.w", (EmporiumEntry e) => (GraphNodeStorage)(object)e.bazarLeftInvWindow),
		R("response.w", (EmporiumEntry e) => (GraphNodeStorage)(object)e.responseWindow),
		R("responseC.w", (EmporiumEntry e) => (GraphNodeStorage)(object)e.responseWindowClosable)
	};

	public static readonly string[] InventoryRoots = new string[19]
	{
		"inv", "back", "front", "showcase", "hidden", "sold", "hireling", "vendingMachine", "vendingFountain", "cassette",
		"docSlot", "trashSlot", "drain", "faucet", "afterhour", "backc", "pocketL", "pocketB", "pocketR"
	};

	private static int recordDepth;

	private static int playDepth;

	private static int suspend;

	private static Tape recording;

	private static Tape playing;

	private static int playIndex;

	private static readonly List<GameItem> created = new List<GameItem>();

	private static bool warnedExhausted;

	private static readonly FieldInfo heapField = AccessTools.Field(typeof(SaveManager), "itemHeap");

	private static readonly FieldInfo dictField = AccessTools.Field(typeof(SaveManager), "itemDict");

	private static bool inited;

	public static bool Ready => Game.ShopLoaded;

	public static bool CustomerPresent
	{
		get
		{
			PlayerStore store = Game.Store;
			try
			{
				return store != null && store.currentClientInstance != null;
			}
			catch
			{
				return false;
			}
		}
	}

	public static int TapeMismatches { get; private set; }

	private static KeyValuePair<string, Func<EmporiumEntry, GraphNodeStorage>> R(string n, Func<EmporiumEntry, GraphNodeStorage> f)
	{
		return new KeyValuePair<string, Func<EmporiumEntry, GraphNodeStorage>>(n, f);
	}

	private static GraphNodeStorage Root(EmporiumEntry e, string name)
	{
		if ((Object)(object)e == (Object)null || name == null)
		{
			return null;
		}
		KeyValuePair<string, Func<EmporiumEntry, GraphNodeStorage>>[] array = roots;
		for (int i = 0; i < array.Length; i++)
		{
			KeyValuePair<string, Func<EmporiumEntry, GraphNodeStorage>> keyValuePair = array[i];
			if (keyValuePair.Key == name)
			{
				try
				{
					return keyValuePair.Value(e);
				}
				catch
				{
					return null;
				}
			}
		}
		return null;
	}

	private static string RootNameOf(EmporiumEntry e, GraphNodeStorage node)
	{
		if ((Object)(object)e == (Object)null || node == null)
		{
			return null;
		}
		KeyValuePair<string, Func<EmporiumEntry, GraphNodeStorage>>[] array = roots;
		for (int i = 0; i < array.Length; i++)
		{
			KeyValuePair<string, Func<EmporiumEntry, GraphNodeStorage>> keyValuePair = array[i];
			GraphNodeStorage val;
			try
			{
				val = keyValuePair.Value(e);
			}
			catch
			{
				continue;
			}
			if (val != null && val == node)
			{
				return keyValuePair.Key;
			}
		}
		return null;
	}

	public static ItemRef RefOf(GraphNodeStorage node)
	{
		if (node == null)
		{
			return null;
		}
		EmporiumEntry instance = EmporiumEntry.Instance;
		GameItem val = (GameItem)(object)((node is GameItem) ? node : null);
		if (val != null)
		{
			long num = Ids.Of(val);
			if (num == 0L)
			{
				return null;
			}
			return new ItemRef
			{
				i = num
			};
		}
		string text = RootNameOf(instance, node);
		if (text != null)
		{
			return new ItemRef
			{
				r = text
			};
		}
		GameItem val2 = GraphUtils.FindParentType<GameItem>(node, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null);
		if (val2 == null)
		{
			return null;
		}
		long num2 = Ids.Of(val2);
		if (num2 == 0L)
		{
			return null;
		}
		int num3 = 0;
		foreach (GraphNodeStorage item in NodesOf(val2))
		{
			if (item == node)
			{
				return new ItemRef
				{
					o = num2,
					n = num3
				};
			}
			num3++;
		}
		return null;
	}

	public static GraphNodeStorage Resolve(ItemRef r)
	{
		if (r == null || !Ready)
		{
			return null;
		}
		if (r.r != null)
		{
			return Root(EmporiumEntry.Instance, r.r);
		}
		if (r.i != 0L)
		{
			return (GraphNodeStorage)(object)Find(r.i);
		}
		if (r.o == 0L || r.n < 0)
		{
			return null;
		}
		GameItem val = Find(r.o);
		if (val == null)
		{
			return null;
		}
		int num = 0;
		foreach (GraphNodeStorage item in NodesOf(val))
		{
			if (num == r.n)
			{
				return item;
			}
			num++;
		}
		return null;
	}

	public static GameInventory ResolveInventory(ItemRef r)
	{
		GraphNodeStorage obj = Resolve(r);
		return (GameInventory)(object)((obj is GameInventory) ? obj : null);
	}

	private static IEnumerable<GraphNodeStorage> NodesOf(GameItem owner)
	{
		HashSet<GraphNodeStorage> seen = new HashSet<GraphNodeStorage>();
		Queue<GraphNodeStorage> q = new Queue<GraphNodeStorage>();
		foreach (GraphNodeStorage child in owner.children)
		{
			q.Enqueue(child);
		}
		while (q.Count > 0)
		{
			GraphNodeStorage x = q.Dequeue();
			if (x == null || x is GameItem || !seen.Add(x))
			{
				continue;
			}
			yield return x;
			foreach (GraphNodeStorage child2 in x.children)
			{
				q.Enqueue(child2);
			}
		}
	}

	public static string RootOf(GraphNodeStorage node)
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return null;
		}
		GraphNodeStorage val = node;
		int num = 0;
		while (val != null && num < 64)
		{
			string text = RootNameOf(instance, val);
			if (text != null)
			{
				if (!text.EndsWith(".w", StringComparison.Ordinal))
				{
					return text;
				}
				return text.Substring(0, text.Length - 2);
			}
			GameItem val2 = (GameItem)(object)((val is GameItem) ? val : null);
			if (val2 != null)
			{
				val = (GraphNodeStorage)(object)val2.parentInventory;
			}
			else
			{
				List<GraphNodeStorage> parents = val.parents;
				val = ((parents != null && parents.Count > 0) ? parents[0] : null);
			}
			num++;
		}
		return null;
	}

	public static bool IsCounterArea(GraphNodeStorage node)
	{
		string text = RootOf(node);
		if (!(text == "front"))
		{
			return text == "back";
		}
		return true;
	}

	public static GameItem Find(long id)
	{
		GameItem val = Ids.Find(id);
		if (val == null && id != 0L && id >= int.MinValue && id <= int.MaxValue && Ready)
		{
			foreach (GameItem item in All())
			{
				if (item.uniqueId == (int)id && !IsGone(item))
				{
					val = item;
					Ids.Of(item);
					break;
				}
			}
		}
		GameItemElement val2 = (GameItemElement)(object)((val is GameItemElement) ? val : null);
		if (val2 != null && val2.IsDestroyed())
		{
			return null;
		}
		return val;
	}

	public static bool IsGone(GameItem it)
	{
		if (it != null)
		{
			GameItemElement val = (GameItemElement)(object)((it is GameItemElement) ? it : null);
			if (val != null)
			{
				return val.IsDestroyed();
			}
			return false;
		}
		return true;
	}

	public static GridShape Shape(GameItem item, int x, int y, bool flipped, int orientation)
	{
		return (GridShape)(object)new GridShapeBuilder(item.modifiedShape).SetTransform(x, y, flipped, orientation);
	}

	public static List<GameItem> All()
	{
		EmporiumEntry instance = EmporiumEntry.Instance;
		HashSet<GameItem> hashSet = new HashSet<GameItem>();
		List<GameItem> list = new List<GameItem>();
		if ((Object)(object)instance == (Object)null)
		{
			return list;
		}
		string[] inventoryRoots = InventoryRoots;
		foreach (string name in inventoryRoots)
		{
			GraphNodeStorage val = Root(instance, name);
			if (val == null)
			{
				continue;
			}
			foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>(val, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null))
			{
				if (hashSet.Add(item))
				{
					list.Add(item);
				}
			}
		}
		return list;
	}

	public static IDisposable Record(out Tape tape)
	{
		if (recordDepth++ == 0)
		{
			recording = new Tape();
			created.Clear();
		}
		tape = recording;
		return new Scope(EndRecord);
	}

	private static void EndRecord()
	{
		if (--recordDepth > 0)
		{
			return;
		}
		Tape tape = recording;
		recording = null;
		if (tape == null)
		{
			return;
		}
		foreach (GameItem item in created)
		{
			tape.ids.Add(IsGone(item) ? 0 : Ids.Of(item));
		}
		created.Clear();
		tape.after = Game.Store?.currentUniqueId ?? 0;
	}

	public static IDisposable Play(Tape tape)
	{
		if (playDepth++ == 0)
		{
			playing = tape ?? new Tape();
			playIndex = 0;
			created.Clear();
			warnedExhausted = false;
		}
		return new Scope(EndPlay);
	}

	private static void EndPlay()
	{
		if (--playDepth > 0)
		{
			return;
		}
		Tape tape = playing;
		playing = null;
		if (tape == null)
		{
			return;
		}
		int num = 0;
		for (int i = 0; i < created.Count && i < tape.ids.Count; i++)
		{
			GameItem val = created[i];
			long num2 = tape.ids[i];
			if (num2 != 0L && !IsGone(val) && Ids.Of(val) != num2)
			{
				Ids.Bind(val, num2);
				num++;
			}
		}
		if (playIndex != tape.uids.Count || created.Count != tape.ids.Count)
		{
			TapeMismatches++;
			Log.Warn("items: replay made " + created.Count + " item(s) and used " + playIndex + " id(s); the server made " + tape.ids.Count + " and used " + tape.uids.Count);
		}
		created.Clear();
		PlayerStore store = Game.Store;
		if (store != null && tape.after > 0)
		{
			store.currentUniqueId = tape.after;
		}
	}

	internal static long TapeMark()
	{
		if (recording != null)
		{
			return ((long)recording.uids.Count << 32) | (uint)created.Count;
		}
		return -1L;
	}

	internal static void TapeRewind(long mark)
	{
		if (mark >= 0 && recording != null)
		{
			int num = (int)(mark >> 32);
			int num2 = (int)(mark & 0xFFFFFFFFu);
			if (recording.uids.Count > num)
			{
				recording.uids.RemoveRange(num, recording.uids.Count - num);
			}
			if (created.Count > num2)
			{
				created.RemoveRange(num2, created.Count - num2);
			}
		}
	}

	internal static IDisposable Suspend()
	{
		suspend++;
		return new Scope(delegate
		{
			suspend--;
		});
	}

	internal static bool OnGetUniqueId(PlayerStore store, ref int result)
	{
		if (suspend > 0 || playing == null)
		{
			return true;
		}
		if (playIndex < playing.uids.Count)
		{
			result = playing.uids[playIndex++];
			store.currentUniqueId = result;
			return false;
		}
		if (!warnedExhausted)
		{
			warnedExhausted = true;
			Log.Warn("items: replay asked for more new ids than the server used; counting on locally");
		}
		return true;
	}

	internal static void OnGotUniqueId(int result)
	{
		if (suspend == 0 && recording != null && !ItemsMoveRecorder.AtTop)
		{
			recording.uids.Add(result);
		}
	}

	internal static void OnItemCreated(GameItem item)
	{
		if (item != null && suspend <= 0)
		{
			if (Net.IsServer && ItemsMoveRecorder.AtTop)
			{
				ItemsMoveRecorder.OnCreated(item);
			}
			else if (recording != null || playing != null)
			{
				created.Add(item);
			}
		}
	}

	public static Subtree Encode(GameItem item)
	{
		if (item == null)
		{
			return null;
		}
		PlayerStore store = Game.Store;
		if (store == null)
		{
			return null;
		}
		if (store.savedItemFeatureList == null)
		{
			store.savedItemFeatureList = new List<ItemFeature>();
		}
		List<ItemFeature> savedItemFeatureList = store.savedItemFeatureList;
		int count = savedItemFeatureList.Count;
		List<ItemFeature> list = null;
		List<long> list2 = new List<long>();
		string json;
		try
		{
			json = store.EncodeItem(item);
			if (heapField?.GetValue(null) is Dictionary<GameItem, long> dictionary)
			{
				SortedDictionary<long, GameItem> sortedDictionary = new SortedDictionary<long, GameItem>();
				foreach (KeyValuePair<GameItem, long> item2 in dictionary)
				{
					sortedDictionary[item2.Value] = item2.Key;
				}
				foreach (KeyValuePair<long, GameItem> item3 in sortedDictionary)
				{
					list2.Add(Ids.Of(item3.Value));
				}
			}
		}
		finally
		{
			if (savedItemFeatureList.Count > count)
			{
				list = savedItemFeatureList.GetRange(count, savedItemFeatureList.Count - count);
				savedItemFeatureList.RemoveRange(count, savedItemFeatureList.Count - count);
			}
		}
		string feats = "";
		if (list != null && list.Count > 0)
		{
			try
			{
				feats = Convert.ToBase64String(ES3.Serialize<List<ItemFeature>>(list, (ES3Settings)null));
			}
			catch (Exception ex)
			{
				Log.Warn("items: features of " + item.identifier + " not encoded: " + ex.Message);
			}
		}
		return new Subtree
		{
			json = json,
			feats = feats,
			ids = list2.ToArray()
		};
	}

	public static GameItem Decode(Subtree s)
	{
		if (s == null || string.IsNullOrEmpty(s.json))
		{
			return null;
		}
		PlayerStore store = Game.Store;
		if (store == null)
		{
			return null;
		}
		using (Suspend())
		{
			try
			{
				store.DecodeSaveItem(s.json, (GameInventory)null);
			}
			catch (Exception ex)
			{
				Log.Warn("items: subtree did not decode: " + ex.Message);
				return null;
			}
			if (!(dictField?.GetValue(null) is Dictionary<long, GameItem> dictionary) || !dictionary.TryGetValue(0L, out var value) || value == null)
			{
				return null;
			}
			RunLoadHooks(value);
			if (s.ids != null)
			{
				for (int i = 0; i < s.ids.Length; i++)
				{
					if (s.ids[i] != 0L && dictionary.TryGetValue(i, out var value2) && value2 != null)
					{
						GameItem val = Ids.Find(s.ids[i]);
						if (val != null && val != value2)
						{
							Ids.Forget(val);
						}
						Ids.Bind(value2, s.ids[i]);
					}
				}
			}
			List<GameItem> list = new List<GameItem> { value };
			list.AddRange(GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)value, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null));
			if (!string.IsNullOrEmpty(s.feats))
			{
				try
				{
					List<ItemFeature> list2 = ES3.Deserialize<List<ItemFeature>>(Convert.FromBase64String(s.feats), (ES3Settings)null);
					Dictionary<int, GameItem> dictionary2 = new Dictionary<int, GameItem>();
					foreach (GameItem item in list)
					{
						if (!dictionary2.ContainsKey(item.uniqueId))
						{
							dictionary2[item.uniqueId] = item;
						}
					}
					foreach (GameItem item2 in list)
					{
						item2.RemoveAllFeature();
					}
					if (list2 != null)
					{
						foreach (ItemFeature item3 in list2)
						{
							if (item3 != null && dictionary2.TryGetValue(item3.parentItemUniqueId, out var value3))
							{
								value3.itemFeatures.Add(item3);
							}
						}
					}
				}
				catch (Exception ex2)
				{
					Log.Warn("items: features not decoded: " + ex2.Message);
				}
			}
			return value;
		}
	}

	private static void RunLoadHooks(GameItem it)
	{
		try
		{
			MachineHelper.LoadUpgrade(it);
			SpriteHelper.UpdateWaterContainerSprite(it);
			GeneralHelper.LoadLockItem(it);
			PlantTraitList.LoadEdibleProduce(it);
			PlantTraitList.LoadTasteFeature(it);
			GeneralHelper.LoadNonOwnedToolTag(it);
			GeneralHelper.LoadHazardousWasteTag(it);
			LockHelper.LoadLockedContainer(it);
			MachineBrokenHelper.LoadBrokenMachine(it);
			MachineHelper.OnLoad(it);
			MachineTurboBoosterAdv.LoadTurboBoostEligibleTag(it);
			CurrentConverterHelper.OnLoad(it);
			((GameItemFunc)it).onLoaded?.Invoke(it);
		}
		catch (Exception ex)
		{
			Log.Warn("items: load hooks for " + it.identifier + ": " + ex.Message);
		}
	}

	public static GameItem ReplaceSubtree(string encodedJson, long[] ids)
	{
		return ReplaceSubtree(new Subtree
		{
			json = encodedJson,
			feats = "",
			ids = ids
		}, null);
	}

	public static GameItem ReplaceSubtree(Subtree s, GameInventory into)
	{
		if (s == null)
		{
			return null;
		}
		GameItem val = null;
		if (s.ids != null && s.ids.Length != 0)
		{
			val = Find(s.ids[0]);
		}
		if (val == null)
		{
			try
			{
				JToken obj = JObject.Parse(s.json)["saveItems"];
				object obj2;
				if (obj == null)
				{
					obj2 = null;
				}
				else
				{
					JToken obj3 = obj[(object)0];
					obj2 = ((obj3 != null) ? obj3[(object)"uniqueId"] : null);
				}
				JToken val2 = (JToken)obj2;
				int num = ((val2 != null) ? ((int)val2) : 0);
				if (num != 0)
				{
					foreach (GameItem item in All())
					{
						if (item.uniqueId == num)
						{
							val = item;
							break;
						}
					}
				}
			}
			catch
			{
			}
		}
		GameInventory val3 = ((val != null) ? val.parentInventory : null) ?? into;
		if (val != null)
		{
			DestroyTree(val);
		}
		GameItem val4 = Decode(s);
		if (val4 == null)
		{
			Log.Warn("items: subtree replace failed to decode");
			return null;
		}
		if (val3 != null && !val3.UncheckedAccept(val4))
		{
			Log.Warn("items: " + ((object)val3)?.ToString() + " refused the replaced " + val4.identifier + " at " + val4.modifiedShape.minX + "," + val4.modifiedShape.minY);
		}
		return val4;
	}

	internal static void DestroyTree(GameItem item)
	{
		if (item == null)
		{
			return;
		}
		List<GameItem> list = GraphUtils.FindAllChildrenType<GameItem>((GraphNodeStorage)(object)item, (Func<GameItem, bool>)null, (Func<GameItem, bool>)null);
		for (int num = list.Count - 1; num >= 0; num--)
		{
			Ids.Forget(list[num]);
			if (!IsGone(list[num]))
			{
				list[num].Destroy();
			}
		}
		Ids.Forget(item);
		if (!IsGone(item))
		{
			item.Destroy();
		}
	}

	public static void BroadcastSubtree(GameItem item, string why = "repair")
	{
		if (Net.IsServer && item != null)
		{
			ItemsMove.BroadcastSubtree(item, why);
		}
	}

	public static bool ServerAction(string what, Action body, int actor = 0)
	{
		if (Net.IsClient)
		{
			return false;
		}
		if (!Net.IsServer)
		{
			body();
			return true;
		}
		return ItemsMove.RunServerAction(what, body, actor);
	}

	[SystemInit]
	internal static void Init()
	{
		if (!inited)
		{
			inited = true;
			ItemsMove.Init();
			Digest.AddPart("items", ItemsMove.Digest);
		}
	}
}
