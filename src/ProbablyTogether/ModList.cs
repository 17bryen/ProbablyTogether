using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ProbablyTogether;

internal static class ModList
{
	internal sealed class Info
	{
		public string Id = "";

		public string Name = "";

		public string Version = "";

		public string Hash = "";

		public ulong Ws;

		public string Why = "";

		public string Folder;
	}

	internal sealed class Diff
	{
		public readonly List<Info> Missing = new List<Info>();

		public readonly List<Info> Different = new List<Info>();

		public readonly List<Info> Extra = new List<Info>();

		public bool NeedsSomething
		{
			get
			{
				if (Missing.Count <= 0)
				{
					return Different.Count > 0;
				}
				return true;
			}
		}
	}

	private const int MaxMods = 64;

	private const int MaxIdChars = 64;

	private const int MaxNameChars = 40;

	private const int MaxVersionChars = 24;

	private const int MaxNamesShown = 4;

	private const int MaxHashFiles = 4000;

	private const long MaxHashBytes = 1073741824L;

	private static readonly List<Info> mods = new List<Info>();

	private static bool hooked;

	internal static List<string> Ids
	{
		get
		{
			List<string> list = new List<string>();
			foreach (Info mod in mods)
			{
				list.Add(mod.Id);
			}
			return list;
		}
	}

	internal static Info Own => Find(mods, "humangenome_accomplice");

	internal static void Enable()
	{
		if (!hooked)
		{
			hooked = true;
			ModHook.OnModAssetsLoaded += OnAssetsLoaded;
		}
	}

	internal static void Disable()
	{
		if (hooked)
		{
			hooked = false;
			ModHook.OnModAssetsLoaded -= OnAssetsLoaded;
		}
	}

	private static void OnAssetsLoaded(LoadedMod mod)
	{
		ModManifest val = mod?.Manifest;
		if (val != null)
		{
			string text = CleanId(val.ID);
			if (text.Length != 0 && Find(mods, text) == null && mods.Count < 64)
			{
				string text2 = Text.Clip((val.Name ?? "").Trim(), 40);
				Info info = new Info
				{
					Id = text,
					Name = ((text2.Length > 0) ? text2 : text),
					Version = Text.Clip((val.ModVersion ?? "").Trim(), 24),
					Ws = mod.WorkshopId,
					Folder = mod.FolderPath
				};
				mods.Add(info);
				Log.Info("mods: enabled mod " + text + ((text2.Length > 0 && text2 != text) ? (" (" + text2 + ")") : "") + ((info.Version.Length > 0) ? (" v" + info.Version) : "") + ((info.Ws != 0L) ? (", Workshop " + info.Ws) : "") + "; " + mods.Count + " so far");
			}
		}
	}

	internal static string NameOf(string id)
	{
		return Find(mods, id)?.Name ?? id ?? "";
	}

	private static string CleanId(string id)
	{
		return Text.Clip((id ?? "").Trim(), 64).Trim();
	}

	private static Info Find(List<Info> list, string id)
	{
		if (list == null || id == null)
		{
			return null;
		}
		foreach (Info item in list)
		{
			if (string.Equals(item.Id, id, StringComparison.Ordinal))
			{
				return item;
			}
		}
		return null;
	}

	private static bool IsOurs(string id)
	{
		return string.Equals(id, "humangenome_accomplice", StringComparison.Ordinal);
	}

	internal static string FolderHash(string folder)
	{
		try
		{
			if (string.IsNullOrEmpty(folder) || !System.IO.Directory.Exists(folder))
			{
				return "";
			}
			string text = Path.GetFullPath(folder).TrimEnd('\\', '/');
			string[] files = System.IO.Directory.GetFiles(text, "*", SearchOption.AllDirectories);
			if (files.Length == 0 || files.Length > 4000)
			{
				return "";
			}
			List<string> list = new List<string>();
			long num = 0L;
			string[] array = files;
			foreach (string text2 in array)
			{
				num += new FileInfo(text2).Length;
				if (num > 1073741824)
				{
					return "";
				}
				list.Add(text2.Substring(text.Length + 1).Replace('\\', '/'));
			}
			list.Sort(StringComparer.Ordinal);
			using SHA256 sHA = SHA256.Create();
			using SHA256 sHA2 = SHA256.Create();
			StringBuilder stringBuilder = new StringBuilder();
			foreach (string item in list)
			{
				byte[] b;
				using (FileStream inputStream = File.OpenRead(Path.Combine(text, item.Replace('/', Path.DirectorySeparatorChar))))
				{
					b = sHA2.ComputeHash(inputStream);
				}
				stringBuilder.Append(item).Append('\n').Append(Hex(b))
					.Append('\n');
			}
			return Hex(sHA.ComputeHash(Encoding.UTF8.GetBytes(stringBuilder.ToString())));
		}
		catch (Exception ex)
		{
			Log.Warn("mods: could not hash " + folder + ": " + ex.Message);
			return "";
		}
	}

	private static string Hex(byte[] b)
	{
		StringBuilder stringBuilder = new StringBuilder(b.Length * 2);
		foreach (byte b2 in b)
		{
			stringBuilder.Append(b2.ToString("x2"));
		}
		return stringBuilder.ToString();
	}

	internal static void HashAll()
	{
		foreach (Info mod in mods)
		{
			if (!IsOurs(mod.Id) && mod.Hash.Length <= 0 && mod.Folder != null)
			{
				mod.Hash = FolderHash(mod.Folder);
				Log.Info("mods: " + mod.Id + " hash " + ((mod.Hash.Length > 0) ? mod.Hash.Substring(0, 12) : "(none: compared by version)"));
			}
		}
	}

	private static JObject ToJson(Info i)
	{
		return new JObject
		{
			["id"] = (JToken)(i.Id),
			["name"] = (JToken)(i.Name),
			["version"] = (JToken)(i.Version),
			["ws"] = (JToken)(i.Ws.ToString()),
			["hash"] = (JToken)(i.Hash)
		};
	}

	internal static JArray Describe(Func<string, ulong> wsFor = null)
	{
		HashAll();
		JArray val = new JArray();
		foreach (Info mod in mods)
		{
			if (IsOurs(mod.Id))
			{
				continue;
			}
			JObject val2 = ToJson(mod);
			if (mod.Ws == 0L && wsFor != null)
			{
				ulong num = wsFor(mod.Id);
				if (num != 0L)
				{
					val2["ws"] = (JToken)(num.ToString());
				}
			}
			val.Add((JToken)(object)val2);
		}
		return val;
	}

	internal static JArray ForWelcome()
	{
		object[] array = Ids.ToArray();
		return new JArray(array);
	}

	internal static List<Info> Read(JToken field)
	{
		JArray val = (JArray)(object)((field is JArray) ? field : null);
		if (val == null)
		{
			return null;
		}
		List<Info> list = new List<Info>();
		int num = 0;
		foreach (JToken item in val)
		{
			if (++num > 128)
			{
				break;
			}
			JObject val2 = (JObject)(object)((item is JObject) ? item : null);
			if (val2 == null)
			{
				continue;
			}
			string text = CleanId(Str(val2, "id"));
			if (text.Length != 0 && !IsOurs(text) && Find(list, text) == null)
			{
				ulong.TryParse(Str(val2, "ws"), out var result);
				string text2 = Str(val2, "hash").ToLowerInvariant();
				if (text2.Length != 64)
				{
					text2 = "";
				}
				string text3 = Text.Clip(Str(val2, "name").Trim(), 40);
				string text4 = Str(val2, "why");
				if (text4 != "missing" && text4 != "different" && text4 != "extra")
				{
					text4 = "";
				}
				list.Add(new Info
				{
					Id = text,
					Name = ((text3.Length > 0) ? text3 : text),
					Version = Text.Clip(Str(val2, "version").Trim(), 24),
					Ws = result,
					Hash = text2,
					Why = text4
				});
			}
		}
		return list;
	}

	private static string Str(JObject o, string k)
	{
		JToken obj = o[k];
		JValue val = (JValue)(object)((obj is JValue) ? obj : null);
		if (val == null || val.Value == null)
		{
			return "";
		}
		return Text.Clip(val.Value.ToString(), 128);
	}

	internal static HashSet<string> ReadServerMods(JToken field)
	{
		JArray val = (JArray)(object)((field is JArray) ? field : null);
		if (val == null)
		{
			return null;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		int num = 0;
		foreach (JToken item in val)
		{
			if (++num > 128)
			{
				break;
			}
			JValue val2 = (JValue)(object)((item is JValue) ? item : null);
			if (val2 != null && (int)((JToken)val2).Type == 8)
			{
				string text = CleanId((string)(JToken)(object)val2);
				if (text.Length > 0)
				{
					hashSet.Add(text);
				}
			}
		}
		return hashSet;
	}

	internal static Diff Compare(List<Info> server, List<Info> client)
	{
		Diff diff = new Diff();
		foreach (Info item in server)
		{
			Info info = Find(client, item.Id);
			if (info == null)
			{
				diff.Missing.Add(item);
			}
			else if (!((item.Hash.Length > 0 && info.Hash.Length > 0) ? (item.Hash == info.Hash) : string.Equals(item.Version, info.Version, StringComparison.Ordinal)))
			{
				diff.Different.Add(item);
			}
		}
		foreach (Info item2 in client)
		{
			if (Find(server, item2.Id) == null)
			{
				diff.Extra.Add(item2);
			}
		}
		return diff;
	}

	internal static string Names(List<Info> list)
	{
		List<string> list2 = new List<string>();
		foreach (Info item in list)
		{
			list2.Add(item.Name);
		}
		List<string> list3 = ((list2.Count <= 4) ? list2 : list2.GetRange(0, 3));
		string text = string.Join(", ", list3);
		if (list3.Count < list2.Count)
		{
			text = text + " and " + (list2.Count - list3.Count) + " more";
		}
		return text;
	}

	internal static string NeedsSentence(Diff d)
	{
		List<string> list = new List<string>();
		if (d.Missing.Count > 0)
		{
			list.Add(((d.Missing.Count == 1) ? "It runs a mod you do not have: " : ("It runs " + d.Missing.Count + " mods you do not have: ")) + Names(d.Missing) + ".");
		}
		if (d.Different.Count > 0)
		{
			list.Add(((d.Different.Count == 1) ? "Your copy of " : "Your copies of ") + Names(d.Different) + ((d.Different.Count == 1) ? " differs" : " differ") + " from the server's.");
		}
		return "You need the same mods as this server. " + string.Join(" ", list);
	}

	internal static string DifferentLine(Diff d)
	{
		return ((d.Different.Count == 1) ? ("Your copy of " + Names(d.Different) + " is not the same as the server's") : ("Your copies of " + Names(d.Different) + " are not the same as the server's")) + ", so parts of the game may not match the other players.";
	}

	internal static string ExtraSentence(Diff d)
	{
		return "This server only allows its own mods. Turn off " + Names(d.Extra) + " in Mods, restart the game, then join.";
	}

	internal static JArray NeedsList(Diff d)
	{
		JArray val = new JArray();
		foreach (Info item in d.Missing)
		{
			JObject val2 = ToJson(item);
			val2["why"] = (JToken)("missing");
			val.Add((JToken)(object)val2);
		}
		foreach (Info item2 in d.Different)
		{
			JObject val3 = ToJson(item2);
			val3["why"] = (JToken)("different");
			val.Add((JToken)(object)val3);
		}
		return val;
	}

	internal static JArray ExtraList(Diff d)
	{
		JArray val = new JArray();
		foreach (Info item in d.Extra)
		{
			JObject val2 = ToJson(item);
			val2["why"] = (JToken)("extra");
			val.Add((JToken)(object)val2);
		}
		return val;
	}

	internal static List<Info> ServerSet(Func<string, ulong> wsFor)
	{
		return Read((JToken)(object)Describe(wsFor)) ?? new List<Info>();
	}

	internal static string ExtraModsLine(HashSet<string> serverMods)
	{
		if (serverMods == null)
		{
			return null;
		}
		List<string> list = new List<string>();
		foreach (Info mod in mods)
		{
			if (!IsOurs(mod.Id) && !serverMods.Contains(mod.Id))
			{
				list.Add(mod.Name);
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		List<string> list2 = ((list.Count <= 4) ? list : list.GetRange(0, 3));
		string text = string.Join(", ", list2);
		if (list2.Count < list.Count)
		{
			text = text + " and " + (list.Count - list2.Count) + " more";
		}
		if (list.Count != 1)
		{
			return "Your mods " + text + " are not on this server, so parts of the game may not match the other players.";
		}
		return "Your mod " + text + " is not on this server, so parts of the game may not match the other players.";
	}
}
