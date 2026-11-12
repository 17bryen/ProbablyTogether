using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ProbablyTogether;

/// A player's side of a difference with the server, saved next to the server's side in <shop folder>\desync-reports.
internal static class DesyncReport
{
	private const string Mark = "\n=== ";

	internal static int Built;

	internal static int Received;

	internal static string LastFile = "";

	internal static string LastSummary = "";

	[SystemInit]
	internal static void Init()
	{
		Digest.AddExplain("core", delegate
		{
			PlayerStore store = Game.Store;
			return (store == null) ? "" : ("day=" + Game.Day + ";state=" + store.storeState.ToString() + ";cash=" + store.playerCash + ";favor=" + store.wildFavor + ";visits=" + store.visitLeftTonight);
		});
		Digest.AddExplain("customers", () => Systems.Customers.DigestText());
		Digest.AddExplain("economy", () => string.Join(";", from p in Economy.RawParts()
			orderby p.Key
			select p.Key + "=" + (p.Value ?? "").Replace(';', ' ').Replace(',', ' ')));
		Digest.AddExplain("items", delegate
		{
			Dictionary<long, string> dictionary = Snapshot.ItemTable();
			return (dictionary == null) ? "" : string.Join(";", from p in dictionary
				orderby p.Key
				select p.Key.ToString(CultureInfo.InvariantCulture) + "=" + (p.Value ?? "").Replace(';', ' ').Replace(',', ' '));
		});
		Digest.AddExplain("nego", () => Negotiation.Explain());
		// The two conversations (counter, phone): which step each copy is at and the line it is showing.
		Digest.AddExplain("dialogue", delegate
		{
			List<string> list = new List<string>();
			string[] array = new string[2] { "counter", "phone" };
			foreach (string text in array)
			{
				DialogueChannel dialogueChannel = DialogueSystem.Chan(text);
				if (dialogueChannel != null)
				{
					Dialogue current = dialogueChannel.Current;
					string text2 = (current == null) ? "(nobody talking)" : ((current.title ?? "") + ": " + (current.plainText ?? "(a line without plain text)"));
					text2 = text2.Replace(';', ' ').Replace(',', ' ').Replace('=', ' ');
					list.Add(text + ".step=" + dialogueChannel.Seq);
					list.Add(text + ".line=" + ((text2.Length > 160) ? text2.Substring(0, 160) : text2));
				}
			}
			return string.Join(";", list);
		});
		// Every machine with its own fingerprint, so a "machines" difference names the machine.
		Digest.AddExplain("machines", delegate
		{
			List<string> list = new List<string>();
			if (Items.Ready)
			{
				foreach (GameItem item in Items.All())
				{
					if (item != null && Systems.Specs.Machine(item))
					{
						list.Add(Ids.Of(item).ToString(CultureInfo.InvariantCulture) + "=" + item.identifier + ":" + Machines.Digest(item));
					}
				}
			}
			list.Sort(StringComparer.Ordinal);
			return string.Join(";", list);
		});
		Digest.AddExplain("members", () => Systems.Members.Describe());
	}

	private static void Section(StringBuilder sb, string name, string text)
	{
		sb.Append(Mark).Append(name).Append(" ===\n").Append(text ?? "").Append('\n');
	}

	internal static string Build(string reason)
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder(8192);
			Section(stringBuilder, "reason", reason);
			Section(stringBuilder, "when", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " UTC; " + Net.PlayerName(Net.LocalPlayer) + " (player " + Net.LocalPlayer + "); mod " + Mod.Version + "; somebody out: " + Systems.Outings.AnyOut + "; on a private trip: " + Systems.Outings.Private);
			try
			{
				Section(stringBuilder, "digest", Digest.Compute().Describe());
			}
			catch (Exception ex)
			{
				Section(stringBuilder, "digest", "threw " + ex.Message);
			}
			foreach (KeyValuePair<string, string> item in Digest.Explain())
			{
				Section(stringBuilder, "part " + item.Key, item.Value);
			}
			Section(stringBuilder, "log", Log.Tail(160));
			Built++;
			string text = stringBuilder.ToString();
			return (text.Length > 300000) ? text.Substring(0, 300000) : text;
		}
		catch (Exception ex2)
		{
			return Mark + "reason ===\n" + reason + "\n(the report could not be built: " + ex2.Message + ")\n";
		}
	}

	private static Dictionary<string, string> Parse(string report)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		string[] array = (report ?? "").Split(new string[1] { Mark }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text in array)
		{
			int num = text.IndexOf(" ===\n", StringComparison.Ordinal);
			if (num > 0)
			{
				dictionary[text.Substring(0, num)] = text.Substring(num + 5).TrimEnd('\n');
			}
		}
		return dictionary;
	}

	private static string Short(string s)
	{
		return (s.Length > 110) ? (s.Substring(0, 110) + "...") : s;
	}

	/// Entry by entry: positional for lists, by key for "key=value" lists.
	internal static string Compare(string theirs, string ours, int most = 6)
	{
		if (theirs == ours)
		{
			return "same";
		}
		char[] separator = new char[2] { ';', ',' };
		string[] array = (theirs ?? "").Split(separator, StringSplitOptions.RemoveEmptyEntries);
		string[] array2 = (ours ?? "").Split(separator, StringSplitOptions.RemoveEmptyEntries);
		List<string> list = new List<string>();
		int num = 0;
		bool flag = array.Length + array2.Length > 0 && array.Concat(array2).Count((string e) => e.IndexOf('=') > 0) * 5 >= (array.Length + array2.Length) * 4;
		if (flag)
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>(StringComparer.Ordinal);
			string[] array3 = array;
			foreach (string text in array3)
			{
				int num2 = text.IndexOf('=');
				dictionary[(num2 > 0) ? text.Substring(0, num2) : text] = (num2 > 0) ? text.Substring(num2 + 1) : "";
			}
			array3 = array2;
			foreach (string text2 in array3)
			{
				int num3 = text2.IndexOf('=');
				dictionary2[(num3 > 0) ? text2.Substring(0, num3) : text2] = (num3 > 0) ? text2.Substring(num3 + 1) : "";
			}
			foreach (string item in dictionary.Keys.Union(dictionary2.Keys).OrderBy((string k) => k, StringComparer.Ordinal))
			{
				string value;
				bool flag2 = dictionary.TryGetValue(item, out value);
				string value2;
				bool flag3 = dictionary2.TryGetValue(item, out value2);
				if (!flag2 || !flag3 || value != value2)
				{
					num++;
					if (list.Count < most)
					{
						list.Add(item + ": theirs " + (flag2 ? Short(value) : "(none)") + " / ours " + (flag3 ? Short(value2) : "(none)"));
					}
				}
			}
			return num + " of " + dictionary.Keys.Union(dictionary2.Keys).Count() + " differ: " + string.Join("  |  ", list) + ((num > list.Count) ? "  | ..." : "");
		}
		int num4 = Math.Max(array.Length, array2.Length);
		for (int j = 0; j < num4; j++)
		{
			string text3 = (j < array.Length) ? array[j] : "(nothing)";
			string text4 = (j < array2.Length) ? array2[j] : "(nothing)";
			if (text3 != text4)
			{
				num++;
				if (list.Count < most)
				{
					list.Add("[" + j + "] theirs " + Short(text3) + " / ours " + Short(text4));
				}
			}
		}
		return num + " of " + num4 + " differ: " + string.Join("  |  ", list) + ((num > list.Count) ? "  | ..." : "");
	}

	private static string Slug(string s)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (char c in s ?? "")
		{
			stringBuilder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
			if (stringBuilder.Length >= 40)
			{
				break;
			}
		}
		return stringBuilder.ToString().Trim('-');
	}

	/// Server: compares a player's report with this side, saves both to a file, logs a one-line comparison.
	internal static void Receive(string player, string reason, string report, bool satOut)
	{
		if (string.IsNullOrEmpty(report))
		{
			return;
		}
		try
		{
			Received++;
			Dictionary<string, string> dictionary = Parse(report);
			Dictionary<string, string> dictionary2 = Digest.Explain();
			StringBuilder stringBuilder = new StringBuilder(report.Length + 8192);
			List<string> list = new List<string>();
			stringBuilder.Append("Desync report: ").Append(player).Append(satOut ? " sat out a difference (no reload)" : " asked for a reload").Append(": ")
				.Append(reason)
				.Append('\n');
			stringBuilder.Append("Written by the server ").Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append(" UTC, mod ")
				.Append(Mod.Version)
				.Append('\n');
			stringBuilder.Append("\n##### COMPARISON (theirs = the player, ours = the server, taken when the report arrived)\n");
			foreach (KeyValuePair<string, string> item in dictionary2)
			{
				if (dictionary.TryGetValue("part " + item.Key, out var value))
				{
					string text = Compare(value, item.Value, 40);
					stringBuilder.Append(item.Key).Append(": ").Append(text)
						.Append('\n');
					if (text != "same")
					{
						list.Add(item.Key + ": " + Compare(value, item.Value, 3));
					}
				}
			}
			stringBuilder.Append("\n##### THE SERVER'S SIDE\n");
			try
			{
				Section(stringBuilder, "digest", Digest.Compute().Describe());
			}
			catch
			{
			}
			foreach (KeyValuePair<string, string> item2 in dictionary2)
			{
				Section(stringBuilder, "part " + item2.Key, item2.Value);
			}
			stringBuilder.Append("\n##### THE PLAYER'S SIDE\n").Append(report);
			string text2 = "";
			try
			{
				string text3 = Paths.Under("desync-reports");
				System.IO.Directory.CreateDirectory(text3);
				text2 = Path.Combine(text3, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Slug(player) + "-" + Slug(reason) + ".txt");
				File.WriteAllText(text2, stringBuilder.ToString());
				// Keep the newest 60.
				foreach (string item3 in (from f in System.IO.Directory.GetFiles(text3, "*.txt")
					orderby f descending
					select f).Skip(60))
				{
					File.Delete(item3);
				}
			}
			catch (Exception ex)
			{
				text2 = "(not written: " + ex.Message + ")";
			}
			LastFile = text2;
			LastSummary = (list.Count == 0) ? "every readable part is the same on both sides (the difference is in a part without readable content, or has passed)" : string.Join("  ||  ", list);
			Log.Info("server: desync report from " + player + (satOut ? " (sat out, no reload)" : "") + " for '" + Text.Clip(reason, 100) + "': " + Text.Clip(LastSummary, 900) + "; full report: " + text2);
		}
		catch (Exception ex2)
		{
			Log.Warn("server: a desync report from " + player + " could not be handled: " + ex2.Message);
		}
	}
}
