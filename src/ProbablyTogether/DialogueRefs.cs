using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ProbablyTogether;

internal static class DialogueRefs
{
	private sealed class Offer
	{
		public Dialogue Node;

		public string Fp;

		public int Speaker;

		public float At;

		public string Ch;
	}

	internal const int MaxNodes = 512;

	private static readonly string[] BlueprintFields = new string[11]
	{
		"mainDialogue", "acceptDealDialogue", "acceptDealDialogueLastLine", "placedWrongItemWhenSellingToDialogue", "placeRightItemWhenSellingToDialogue", "allDoneDialogue", "repeatDialogue", "interogationDialogue", "glasseDialogue", "customOnArrestDialogue",
		"resumeAfterConfrontDialogue"
	};

	private static readonly Dictionary<string, Func<Dialogue>> factories = new Dictionary<string, Func<Dialogue>>(StringComparer.Ordinal);

	private static readonly Dictionary<string, Dialogue> named = new Dictionary<string, Dialogue>(StringComparer.Ordinal);

	private static readonly HashSet<Dialogue> startedNamed = new HashSet<Dialogue>();

	private static readonly List<Offer> offers = new List<Offer>();

	internal static string Fp(Dialogue d)
	{
		if (d == null)
		{
			return "-";
		}
		ulong h = 14695981039346656037uL;
		int num = 0;
		Dialogue val = d;
		while (val != null && num < 16)
		{
			Mix(ref h, Speaker(val.title));
			Mix(ref h, val.choices?.Count ?? 0);
			Mix(ref h, (val.startAction != null) ? 1 : 0);
			Mix(ref h, (val.endAction != null) ? 1 : 0);
			Mix(ref h, val.priority);
			val = val.nextDialogue;
			num++;
		}
		Mix(ref h, num);
		return (h & 0xFFFFFFFFu).ToString("x8");
	}

	internal static int Speaker(string title)
	{
		if (!(title == "PLAYER"))
		{
			if (!(title == "SEC"))
			{
				return 79;
			}
			return 83;
		}
		return 80;
	}

	private static void Mix(ref ulong h, long v)
	{
		for (int i = 0; i < 8; i++)
		{
			h ^= (byte)(v >> i * 8);
			h *= 1099511628211uL;
		}
	}

	internal static void Register(string name, Func<Dialogue> factory)
	{
		factories[name] = factory;
	}

	internal static Dialogue BuildNamed(string name)
	{
		if (!factories.TryGetValue(name, out var value))
		{
			return null;
		}
		Dialogue val = value();
		if (val != null)
		{
			named[name] = val;
		}
		return val;
	}

	private static List<KeyValuePair<string, Dialogue>> RootList(DialogueChannel ch)
	{
		List<KeyValuePair<string, Dialogue>> list = new List<KeyValuePair<string, Dialogue>>();
		foreach (KeyValuePair<string, Dialogue> item in named)
		{
			list.Add(new KeyValuePair<string, Dialogue>("t:" + item.Key, item.Value));
		}
		if (ch is CounterChannel)
		{
			StoreClient val = null;
			try
			{
				object obj;
				if (!PlayerStore.HasInstance)
				{
					obj = null;
				}
				else
				{
					StoreClientInstance currentClientInstance = PlayerStore.Instance.currentClientInstance;
					obj = ((currentClientInstance != null) ? currentClientInstance.GetClientBlueprint() : null);
				}
				val = (StoreClient)obj;
			}
			catch
			{
			}
			if (val != null)
			{
				list.Add(new KeyValuePair<string, Dialogue>("f:mainDialogue", val.mainDialogue));
				list.Add(new KeyValuePair<string, Dialogue>("f:acceptDealDialogue", val.acceptDealDialogue));
				list.Add(new KeyValuePair<string, Dialogue>("f:acceptDealDialogueLastLine", val.acceptDealDialogueLastLine));
				list.Add(new KeyValuePair<string, Dialogue>("f:placedWrongItemWhenSellingToDialogue", val.placedWrongItemWhenSellingToDialogue));
				list.Add(new KeyValuePair<string, Dialogue>("f:placeRightItemWhenSellingToDialogue", val.placeRightItemWhenSellingToDialogue));
				list.Add(new KeyValuePair<string, Dialogue>("f:allDoneDialogue", val.allDoneDialogue));
				list.Add(new KeyValuePair<string, Dialogue>("f:repeatDialogue", val.repeatDialogue));
				list.Add(new KeyValuePair<string, Dialogue>("f:interogationDialogue", val.interogationDialogue));
				list.Add(new KeyValuePair<string, Dialogue>("f:glasseDialogue", val.glasseDialogue));
				list.Add(new KeyValuePair<string, Dialogue>("f:customOnArrestDialogue", val.customOnArrestDialogue));
				list.Add(new KeyValuePair<string, Dialogue>("f:resumeAfterConfrontDialogue", val.resumeAfterConfrontDialogue));
			}
		}
		for (int i = 0; i < ch.Roots.Count; i++)
		{
			list.Add(new KeyValuePair<string, Dialogue>("r" + i, ch.Roots[i]));
		}
		for (int j = 0; j < ch.ShownLines.Count; j++)
		{
			list.Add(new KeyValuePair<string, Dialogue>("s" + j, ch.ShownLines[j]));
		}
		list.Add(new KeyValuePair<string, Dialogue>("cur", ch.Current));
		return list;
	}

	internal static string Of(DialogueChannel ch, Dialogue node, bool preferShown = false)
	{
		if (node == null)
		{
			return null;
		}
		List<KeyValuePair<string, Dialogue>> list = RootList(ch);
		if (preferShown)
		{
			foreach (KeyValuePair<string, Dialogue> item2 in list)
			{
				if (item2.Key[0] == 's' && item2.Value == node)
				{
					return item2.Key;
				}
			}
		}
		foreach (KeyValuePair<string, Dialogue> item3 in list)
		{
			if (item3.Value == node)
			{
				return item3.Key;
			}
		}
		HashSet<Dialogue> hashSet = new HashSet<Dialogue>();
		int num = 512;
		foreach (KeyValuePair<string, Dialogue> item4 in list)
		{
			if (item4.Value == null || hashSet.Contains(item4.Value))
			{
				continue;
			}
			Queue<KeyValuePair<Dialogue, string>> queue = new Queue<KeyValuePair<Dialogue, string>>();
			queue.Enqueue(new KeyValuePair<Dialogue, string>(item4.Value, item4.Key));
			hashSet.Add(item4.Value);
			while (queue.Count > 0 && num-- > 0)
			{
				KeyValuePair<Dialogue, string> keyValuePair = queue.Dequeue();
				Dialogue key = keyValuePair.Key;
				if (key == node)
				{
					return keyValuePair.Value;
				}
				if (key.nextDialogue != null && hashSet.Add(key.nextDialogue))
				{
					queue.Enqueue(new KeyValuePair<Dialogue, string>(key.nextDialogue, keyValuePair.Value + "/n"));
				}
				if (key.choices == null)
				{
					continue;
				}
				for (int i = 0; i < key.choices.Count; i++)
				{
					Dialogue item = key.choices[i].Item2;
					if (item != null && hashSet.Add(item))
					{
						queue.Enqueue(new KeyValuePair<Dialogue, string>(item, keyValuePair.Value + "/c" + i));
					}
				}
			}
			if (num <= 0)
			{
				break;
			}
		}
		return null;
	}

	internal static Dialogue Resolve(DialogueChannel ch, string rf)
	{
		if (string.IsNullOrEmpty(rf))
		{
			return null;
		}
		string[] array = rf.Split('/');
		Dialogue value = null;
		string text = array[0];
		if (text.StartsWith("t:", StringComparison.Ordinal))
		{
			string text2 = text.Substring(2);
			bool num = text2.EndsWith("!", StringComparison.Ordinal);
			if (num)
			{
				text2 = text2.Substring(0, text2.Length - 1);
			}
			if (num || !named.TryGetValue(text2, out value) || value == null)
			{
				value = BuildNamed(text2);
			}
		}
		else
		{
			foreach (KeyValuePair<string, Dialogue> item in RootList(ch))
			{
				if (item.Key == text)
				{
					value = item.Value;
					break;
				}
			}
		}
		for (int i = 1; i < array.Length; i++)
		{
			if (value == null)
			{
				break;
			}
			string text3 = array[i];
			value = ((!(text3 == "n")) ? ((text3.Length <= 1 || text3[0] != 'c' || !int.TryParse(text3.Substring(1), out var result)) ? null : ((value.choices != null && result >= 0 && result < value.choices.Count) ? value.choices[result].Item2 : null)) : value.nextDialogue);
		}
		return value;
	}

	internal static string NamedOf(Dialogue d)
	{
		foreach (KeyValuePair<string, Dialogue> item in named)
		{
			if (item.Value == d)
			{
				return "t:" + item.Key + (startedNamed.Add(d) ? "!" : "");
			}
		}
		return null;
	}

	internal static MsgDlgInline Describe(Dialogue d)
	{
		MsgDlgInline msgDlgInline = new MsgDlgInline
		{
			fp = Fp(d),
			lines = new List<MsgDlgLine>()
		};
		int num = 0;
		Dialogue val = d;
		bool plain = true;
		while (val != null && num < 16)
		{
			if (val.startAction != null || val.endAction != null || (val.choices != null && val.choices.Count > 0) || val.isMainDialog)
			{
				plain = false;
			}
			MsgDlgLine msgDlgLine = new MsgDlgLine
			{
				title = (val.title ?? ""),
				text = Clip(val.plainText),
				prio = val.priority
			};
			if (val.choices != null && val.choices.Count > 0)
			{
				msgDlgLine.choices = new List<string>();
				foreach (var choice in val.choices)
				{
					msgDlgLine.choices.Add(Clip(choice.Item2?.plainText));
				}
			}
			msgDlgInline.lines.Add(msgDlgLine);
			val = val.nextDialogue;
			num++;
		}
		msgDlgInline.plain = plain && val == null;
		return msgDlgInline;
	}

	private static string Clip(string s)
	{
		if (s != null)
		{
			if (s.Length <= 400)
			{
				return s;
			}
			return s.Substring(0, 400);
		}
		return "";
	}

	internal static Dialogue TextOnly(MsgDlgInline m)
	{
		if (m?.lines == null || m.lines.Count == 0)
		{
			return new Dialogue().SetText("", "...");
		}
		Dialogue val = null;
		Dialogue val2 = null;
		foreach (MsgDlgLine line in m.lines)
		{
			Dialogue val3 = new Dialogue();
			val3.SetText(line.title ?? "", line.text ?? "");
			val3.priority = line.prio;
			if (line.choices != null)
			{
				foreach (string choice in line.choices)
				{
					val3.choices.Add((null, new Dialogue().SetText("PLAYER", choice ?? ""), null));
				}
			}
			if (val == null)
			{
				val = val3;
			}
			else
			{
				val2.nextDialogue = val3;
			}
			val2 = val3;
		}
		return val;
	}

	internal static void AddOffer(DialogueChannel ch, Dialogue d)
	{
		if (d != null)
		{
			Expire();
			offers.Add(new Offer
			{
				Node = d,
				Fp = Fp(d),
				Speaker = Speaker(d.title),
				At = Time.realtimeSinceStartup,
				Ch = ch.Name
			});
			if (offers.Count > 16)
			{
				offers.RemoveAt(0);
			}
		}
	}

	internal static Dialogue TakeOffer(DialogueChannel ch, MsgDlgInline m)
	{
		Expire();
		int num = ((m?.lines != null && m.lines.Count > 0) ? Speaker(m.lines[0].title) : 79);
		for (int i = 0; i < offers.Count; i++)
		{
			Offer offer = offers[i];
			if (offer.Ch == ch.Name && offer.Fp == m?.fp && offer.Speaker == num)
			{
				offers.RemoveAt(i);
				return offer.Node;
			}
		}
		return null;
	}

	internal static void ClearOffers()
	{
		offers.Clear();
	}

	private static void Expire()
	{
		float now = Time.realtimeSinceStartup;
		offers.RemoveAll((Offer o) => now - o.At > 5f);
	}

	internal static string Short(Dialogue d)
	{
		if (d == null)
		{
			return "-";
		}
		string text = d.plainText ?? "";
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append((char)Speaker(d.title)).Append(':').Append((text.Length > 24) ? (text.Substring(0, 24) + "...") : text);
		return stringBuilder.ToString();
	}
}
