using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ProbablyTogether.UI;
using HarmonyLib;
using UnityEngine;

namespace ProbablyTogether.Bots.Scripts;

internal static class UiBot
{
	internal const string LabLock = "bot.lock";

	internal const string LockPhrase = "is testing the lock label";

	internal static int Other()
	{
		foreach (int key in Net.Players.Keys)
		{
			if (key != Net.LocalPlayer)
			{
				return key;
			}
		}
		return -1;
	}

	internal static Presence.Remote OtherRemote()
	{
		int num = Other();
		if (num <= 0 || !Presence.Remotes.TryGetValue(num, out var value))
		{
			return null;
		}
		return value;
	}

	internal static float XFor(int player)
	{
		return 0.2f + 0.1f * (float)Mathf.Clamp(player, 1, 6);
	}

	internal static string Describe(Presence.Remote r)
	{
		if (r != null)
		{
			return string.Format(CultureInfo.InvariantCulture, "p{0} at ({1:F3},{2:F3}) view '{3}' visible {4} anchor ({5:F3},{6:F3}) label '{7}'", r.Id, r.X, r.Y, r.View, r.Visible, r.Anchor.x, r.Anchor.y, r.LabelText);
		}
		return "no remote yet";
	}

	internal static string Lines()
	{
		return string.Join(" / ", Chat.Lines.Select((Chat.Line l) => ((l.N > 0) ? ("#" + l.N) : "local") + (l.History ? "(log)" : "") + " " + (l.System ? "sys" : l.Name) + ": " + l.Text));
	}

	internal static string SaveHash()
	{
		try
		{
			string root = Paths.Root;
			List<string> list = System.IO.Directory.GetFiles(root, "save_*.es3").Concat(System.IO.Directory.GetFiles(root, "saves_index.es3")).OrderBy<string, string>((string f) => f, StringComparer.Ordinal)
				.ToList();
			using SHA256 sHA = SHA256.Create();
			StringBuilder stringBuilder = new StringBuilder();
			foreach (string item in list)
			{
				stringBuilder.Append(Path.GetFileName(item)).Append('=').Append(BitConverter.ToString(sHA.ComputeHash(File.ReadAllBytes(item))).Replace("-", "").Substring(0, 12))
					.Append(';');
			}
			return list.Count + " file(s) " + stringBuilder;
		}
		catch (Exception ex)
		{
			return "unreadable: " + ex.Message;
		}
	}

	internal static GameItem AnyItem()
	{
		GameItem val = null;
		foreach (GameItem item in Ids.All())
		{
			if (item != null && Ids.Of(item) > 0 && (val == null || Ids.Of(item) < Ids.Of(val)))
			{
				val = item;
			}
		}
		return val;
	}

	internal static bool Patched(MethodBase original, string patchClass)
	{
		Patches val = ((original == null) ? null : Harmony.GetPatchInfo(original));
		return val?.Prefixes.Concat(val.Postfixes).Any((Patch p) => p.PatchMethod.DeclaringType != null && p.PatchMethod.DeclaringType.Name == patchClass) ?? false;
	}
}
