using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

internal abstract class DialogueChannel
{
	internal readonly string Name;

	internal readonly string LockName;

	internal int Seq;

	internal int SeqMorning = int.MinValue;

	internal readonly List<KeyValuePair<int, string>> History = new List<KeyValuePair<int, string>>();

	internal readonly List<Dialogue> Roots = new List<Dialogue>();

	internal readonly List<Dialogue> ShownLines = new List<Dialogue>();

	internal Dialogue WatchNode;

	internal float WatchSince;

	internal MonoBehaviour LastManager;

	internal int SeqNow
	{
		get
		{
			SyncMorning();
			return Seq;
		}
	}

	internal abstract MonoBehaviour Manager { get; }

	internal bool Ready => (Object)(object)Manager != (Object)null;

	internal abstract Dialogue Current { get; }

	internal abstract Dialogue Uncompleted { get; set; }

	internal abstract int Epoch { get; }

	internal abstract bool EndPending { get; set; }

	internal abstract bool AutoPending { get; }

	internal abstract bool WaitingManual { get; set; }

	internal abstract bool SkipCurrent { get; set; }

	internal abstract List<Coroutine> Coroutines { get; }

	internal bool IsChoiceNow
	{
		get
		{
			Dialogue current = Current;
			if (current != null && current.choices != null)
			{
				return current.choices.Count > 0;
			}
			return false;
		}
	}

	internal bool LineUnfinished
	{
		get
		{
			Dialogue current = Current;
			if (current != null)
			{
				return Uncompleted == current;
			}
			return false;
		}
	}

	protected DialogueChannel(string name, string lockName)
	{
		Name = name;
		LockName = lockName;
	}

	internal void SyncMorning()
	{
		if (Net.IsServer && SeqMorning != DayLog.MorningSnapshot)
		{
			SeqMorning = DayLog.MorningSnapshot;
			Seq = 0;
			History.Clear();
		}
	}

	internal void Remember(int seq, string fp)
	{
		if (History.Count > 0 && History[History.Count - 1].Key >= seq)
		{
			History.Clear();
		}
		History.Add(new KeyValuePair<int, string>(seq, fp ?? "-"));
		if (History.Count > 48)
		{
			History.RemoveAt(0);
		}
	}

	internal string FpAt(int seq)
	{
		if (seq <= 0)
		{
			if (SeqNow > 0)
			{
				return null;
			}
			return DialogueRefs.Fp(Current);
		}
		for (int num = History.Count - 1; num >= 0; num--)
		{
			if (History[num].Key == seq)
			{
				return History[num].Value;
			}
		}
		return null;
	}

	internal string At()
	{
		int seqNow = SeqNow;
		return seqNow + ":" + (FpAt(seqNow) ?? "?");
	}

	internal string HistoryText()
	{
		StringBuilder stringBuilder = new StringBuilder();
		if (SeqNow <= 0)
		{
			stringBuilder.Append("0:").Append(DialogueRefs.Fp(Current));
		}
		for (int num = History.Count - 1; num >= 0; num--)
		{
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append(',');
			}
			stringBuilder.Append(History[num].Key).Append(':').Append(History[num].Value);
		}
		return stringBuilder.ToString();
	}

	internal abstract bool Start(Dialogue d, bool forced);

	internal abstract void Continue();

	internal abstract void Finish();

	internal abstract void Select(int index);

	internal abstract void End();

	internal abstract void Interrupt();

	internal abstract void ScheduleFlow(float delay, Action action);

	internal abstract void ScheduleAutoAdvance(float delay);

	internal abstract void DisplayChoice();

	internal abstract void Shown(bool server, bool pacer);

	internal abstract void FireEndAction(Dialogue node);

	internal void PushRoot(Dialogue d)
	{
		if (d != null)
		{
			Roots.Remove(d);
			Roots.Insert(0, d);
			if (Roots.Count > 8)
			{
				Roots.RemoveAt(Roots.Count - 1);
			}
		}
	}

	internal void PushShown(Dialogue d)
	{
		if (d != null)
		{
			ShownLines.Remove(d);
			ShownLines.Insert(0, d);
			if (ShownLines.Count > 16)
			{
				ShownLines.RemoveAt(ShownLines.Count - 1);
			}
		}
	}

	internal bool CheckScene()
	{
		MonoBehaviour manager = Manager;
		if (manager == LastManager)
		{
			return false;
		}
		LastManager = manager;
		Roots.Clear();
		ShownLines.Clear();
		WatchNode = null;
		if (!Net.IsServer)
		{
			Seq = 0;
			History.Clear();
		}
		return true;
	}

	internal void DropNewestCoroutineSince(int countBefore)
	{
		List<Coroutine> coroutines = Coroutines;
		MonoBehaviour manager = Manager;
		if (coroutines != null && !((Object)(object)manager == (Object)null) && coroutines.Count > countBefore)
		{
			Coroutine val = coroutines[coroutines.Count - 1];
			coroutines.RemoveAt(coroutines.Count - 1);
			if (val != null)
			{
				manager.StopCoroutine(val);
			}
		}
	}

	protected static T Get<T>(FieldInfo f, object o)
	{
		try
		{
			return (f != null && o != null) ? ((T)f.GetValue(o)) : default(T);
		}
		catch
		{
			return default(T);
		}
	}

	protected static void Set(FieldInfo f, object o, object v)
	{
		if (f != null && o != null)
		{
			f.SetValue(o, v);
		}
	}

	protected static void Call(MethodInfo m, object o, params object[] a)
	{
		if (m == null || o == null)
		{
			Log.Warn("dialogue: game method missing (" + (m?.Name ?? "?") + ")");
			return;
		}
		try
		{
			m.Invoke(o, a);
		}
		catch (TargetInvocationException ex)
		{
			throw ex.InnerException ?? ex;
		}
	}
}
