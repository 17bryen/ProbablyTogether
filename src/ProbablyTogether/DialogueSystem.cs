using System;
using System.Collections.Generic;
using System.Globalization;
using ProbablyTogether.Bots;
using ProbablyTogether.Systems;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProbablyTogether;

internal static class DialogueSystem
{
	internal sealed class Step : IDisposable
	{
		private readonly DialogueChannel ch;

		private readonly int seq;

		private readonly IDisposable apply;

		private readonly DialogueChannel prevChannel;

		private bool done;

		private Step(DialogueChannel ch, int seq, IDisposable apply)
		{
			this.ch = ch;
			this.seq = seq;
			this.apply = apply;
			prevChannel = serverStepChannel;
			serverStepChannel = ch;
		}

		internal static Step Begin(DialogueChannel ch, string op, Action<MsgDlgStep> fill = null)
		{
			if (!Net.IsServer || Net.Applying || !Game.ShopLoaded)
			{
				return null;
			}
			ch.CheckScene();
			ch.SyncMorning();
			MsgDlgStep msgDlgStep = new MsgDlgStep
			{
				ch = ch.Name,
				seq = ++ch.Seq,
				op = op,
				epoch = ch.Epoch
			};
			try
			{
				fill?.Invoke(msgDlgStep);
			}
			catch (Exception ex)
			{
				Log.Warn("dialogue: describing step " + op + " failed: " + ex.Message);
			}
			Seed seed;
			IDisposable disposable = Replay.Server(out seed);
			msgDlgStep.seed = seed;
			serverStepDepth++;
			Net.ToAll("dialogue.step", msgDlgStep);
			return new Step(ch, msgDlgStep.seq, disposable);
		}

		public void Dispose()
		{
			if (!done)
			{
				done = true;
				long rd = 0L;
				try
				{
					rd = Replay.Digest();
				}
				catch
				{
				}
				apply.Dispose();
				serverStepDepth--;
				serverStepChannel = prevChannel;
				PlayerStore store = Game.Store;
				string fp = DialogueRefs.Fp(ch.Current);
				ch.Remember(seq, fp);
				Net.ToAll("dialogue.stepDone", new MsgDlgDone
				{
					ch = ch.Name,
					seq = seq,
					cash = (store?.playerCash ?? 0),
					favor = (store?.wildFavor ?? 0),
					fp = fp,
					rd = rd
				});
			}
		}
	}

	internal static readonly CounterChannel Counter = new CounterChannel();

	internal static readonly PhoneChannel Phone = new PhoneChannel();

	private static bool inited;

	internal const float ShownTimeout = 8f;

	private static int serverStepDepth;

	/// The channel of the innermost server step (host mode: who paces it decides whose UI it is).
	private static DialogueChannel serverStepChannel;

	internal static int FreshPhones;

	private static bool optionsSet;

	private static object optionsFor;

	private static float phoneIdle;

	internal static int ShownTimeouts;

	private static readonly Queue<object> inbox = new Queue<object>();

	private static float headWaitSince = -1f;

	private static bool uiForOthers;

	private static DialogueChannel applyingFor;

	internal static int RefusedCount;

	internal static string LastRefused = "";

	internal static Action<string, string, string> refused;

	internal static int Mismatches;

	internal static int MainBlocked;

	internal static int QuickDials;

	internal static int MorningSkipped;

	private static float lastResyncAsk = -1000f;

	private static float lastMismatchLog = -1000f;

	internal static bool ServerLive
	{
		get
		{
			if (Net.IsServer)
			{
				return Game.ShopLoaded;
			}
			return false;
		}
	}

	internal static bool CustomerAtCounter
	{
		get
		{
			try
			{
				return Game.ShopLoaded && Game.Store?.currentClientInstance != null;
			}
			catch
			{
				return false;
			}
		}
	}

	internal static bool UiForOthers
	{
		get
		{
			if (!Net.IsServer)
			{
				return uiForOthers;
			}
			if (Net.IsHost)
			{
				return serverStepDepth > 0 && (serverStepChannel == null || Locks.HeldBy(serverStepChannel.LockName) != Net.LocalPlayer);
			}
			return serverStepDepth > 0;
		}
	}

	internal static bool CanRequest
	{
		get
		{
			if (Net.IsClient && Net.IsLive && !Snapshot.IsLoading)
			{
				return !DayLog.Replaying;
			}
			return false;
		}
	}

	internal static DialogueChannel Chan(string name)
	{
		if (!(name == "phone"))
		{
			if (!(name == "counter"))
			{
				return null;
			}
			return Counter;
		}
		return Phone;
	}

	[SystemInit]
	private static void Init()
	{
		if (!inited)
		{
			inited = true;
			Net.On<MsgDlgStep>("dialogue.step", null, OnStep);
			Net.On<MsgDlgDone>("dialogue.stepDone", null, OnStepDone);
			Net.On("dialogue.shown", delegate(MsgDlgReq m, int p)
			{
				ServerLine(m, p, "shown");
			}, null);
			Net.On("dialogue.advance", delegate(MsgDlgReq m, int p)
			{
				ServerLine(m, p, "advance");
			}, null);
			Net.On("dialogue.choose", delegate(MsgDlgReq m, int p)
			{
				ServerLine(m, p, "choose");
			}, null);
			Net.On<MsgPhoneCall>("phone.call", ServerCall, null);
			Net.On<MsgPhoneHangup>("phone.hangup", ServerHangup, null);
			Net.On<MsgDlgRefused>("dialogue.refused", null, OnRefused);
			DialogueEvents.Register();
			Suppress.Hook();
			Net.Disconnected += delegate
			{
				ResetClient();
			};
			Snapshot.BeforeMorningCapture += ServerFreshPhone;
			ProbablyTogether.Digest.AddPart("dialogue", DigestPart);
			ProbablyTogether.Digest.AddPart("story", DialogueEvents.StoryHash);
			ProbablyTogether.Digest.AddPart("events", DialogueEvents.EventsHash);
			ProbablyTogether.Digest.AddPart("requests", DialogueEvents.RequestsHash);
		}
	}

	internal static void DescribeStart(DialogueChannel ch, MsgDlgStep m, Dialogue d)
	{
		m.rf = DialogueRefs.NamedOf(d) ?? DialogueRefs.Of(ch, d);
		if (m.rf == null)
		{
			m.inline = DialogueRefs.Describe(d);
		}
	}

	internal static void ServerEndActionTimer(DialogueChannel ch, Dialogue node)
	{
		if (!Net.IsServer)
		{
			return;
		}
		using (Step.Begin(ch, "endAction", delegate(MsgDlgStep m)
		{
			m.rf = DialogueRefs.Of(ch, node, preferShown: true);
			if (m.rf == null)
			{
				m.inline = DialogueRefs.Describe(node);
			}
		}))
		{
			ch.FireEndAction(node);
		}
	}

	private static void RunShown(DialogueChannel ch, bool skip, string why)
	{
		Dialogue cur = ch.Current;
		using (Step step = Step.Begin(ch, "shown", delegate(MsgDlgStep m)
		{
			m.skip = skip;
			m.fp = DialogueRefs.Fp(cur);
		}))
		{
			if (step == null)
			{
				return;
			}
			ch.SkipCurrent = skip;
			ch.Shown(server: true, pacer: false);
		}
		if (why != null)
		{
			Log.Info("dialogue: " + ch.Name + " line shown by the server (" + why + "): " + DialogueRefs.Short(cur));
		}
	}

	private static void Refuse(int player, DialogueChannel ch, string what, string why, bool quiet = false)
	{
		if (!quiet)
		{
			Log.Info("dialogue: refused " + what + " from " + Net.PlayerName(player) + " on " + (ch?.Name ?? "?") + ": " + why);
		}
		Net.ToPlayer(player, "dialogue.refused", new MsgDlgRefused
		{
			ch = (ch?.Name ?? ""),
			what = what,
			why = why
		});
	}

	internal static string HolderText(DialogueChannel ch, int holder)
	{
		return Net.PlayerName(holder) + ((ch is PhoneChannel) ? " is on the phone" : " is at the counter.");
	}

	private static bool TakeOrHold(DialogueChannel ch, int player, out string why)
	{
		why = null;
		if (ch is CounterChannel && !DayNightOuting.IsOut)
		{
			return Customers.TryCounter(player, out why);
		}
		if (ch is CounterChannel && !CustomerAtCounter)
		{
			return true;
		}
		int num = Locks.Holder(ch.LockName);
		if (num == player)
		{
			Locks.Touch(ch.LockName, player);
			return true;
		}
		if (num >= 0)
		{
			if (ch is CounterChannel && Locks.TakeOver(ch.LockName, player))
			{
				return true;
			}
			why = HolderText(ch, num);
			if (ch is CounterChannel)
			{
				Locks.Wanted(ch.LockName, player);
			}
			return false;
		}
		if (!Locks.TryTake(ch.LockName, player))
		{
			int num2 = Locks.Holder(ch.LockName);
			why = ((num2 >= 0 && num2 != player) ? HolderText(ch, num2) : ("the " + ch.Name + " is busy"));
			return false;
		}
		return true;
	}

	private static void ServerLine(MsgDlgReq m, int player, string kind)
	{
		DialogueChannel dialogueChannel = Chan(m.ch);
		if (dialogueChannel == null || !dialogueChannel.Ready || !Game.ShopLoaded)
		{
			Refuse(player, dialogueChannel, kind, "no conversation here");
			return;
		}
		dialogueChannel.CheckScene();
		Dialogue current = dialogueChannel.Current;
		if (current == null)
		{
			Refuse(player, dialogueChannel, kind, "nobody is talking", kind == "shown");
			return;
		}
		if (m.fp != DialogueRefs.Fp(current))
		{
			Refuse(player, dialogueChannel, kind, "that line is over", kind == "shown");
			return;
		}
		string why;
		if (kind == "shown")
		{
			if (Locks.Holder(dialogueChannel.LockName) != player)
			{
				return;
			}
			Locks.Touch(dialogueChannel.LockName, player);
		}
		else if (!TakeOrHold(dialogueChannel, player, out why))
		{
			Refuse(player, dialogueChannel, kind, why);
			return;
		}
		switch (kind)
		{
		case "shown":
			if (dialogueChannel.LineUnfinished)
			{
				RunShown(dialogueChannel, m.skip, null);
			}
			break;
		case "advance":
			if (dialogueChannel.IsChoiceNow)
			{
				Refuse(player, dialogueChannel, kind, "pick a choice");
			}
			else if (!dialogueChannel.LineUnfinished)
			{
				if (dialogueChannel.EndPending)
				{
					dialogueChannel.Finish();
				}
				else if (dialogueChannel.WaitingManual || dialogueChannel.AutoPending)
				{
					dialogueChannel.Continue();
				}
			}
			break;
		case "choose":
			if (!dialogueChannel.IsChoiceNow)
			{
				Refuse(player, dialogueChannel, kind, "no choice here");
			}
			else if (dialogueChannel.LineUnfinished)
			{
				Refuse(player, dialogueChannel, kind, "the choices are not up yet");
			}
			else if (m.index < 0 || m.index >= current.choices.Count)
			{
				Refuse(player, dialogueChannel, kind, "no such choice");
			}
			else if (!current.IsChoiceEnabledAt(m.index))
			{
				Refuse(player, dialogueChannel, kind, "that choice is not available");
			}
			else if (m.cfp != DialogueRefs.Fp(current.choices[m.index].Item2))
			{
				Refuse(player, dialogueChannel, kind, "the choices changed");
			}
			else
			{
				dialogueChannel.Select(m.index);
			}
			break;
		}
	}

	private static void ServerCall(MsgPhoneCall m, int player)
	{
		PhoneUIManager instance = PhoneUIManager.Instance;
		if ((Object)(object)instance == (Object)null || !Phone.Ready || !Game.ShopLoaded)
		{
			Refuse(player, Phone, "call", "no phone here");
			return;
		}
		int num = Locks.Holder(Phone.LockName);
		if (num >= 0 && num != player)
		{
			Refuse(player, Phone, "call", HolderText(Phone, num));
			return;
		}
		if (PhoneChannel.Talking)
		{
			Refuse(player, Phone, "call", "the line is busy");
			return;
		}
		if (!StorePhoneClient.IsNumberExist(m.number))
		{
			Refuse(player, Phone, "call", "no such number");
			return;
		}
		if (!PhoneUIManager.WillAnswerCall(m.number))
		{
			Refuse(player, Phone, "call", "no answer");
			return;
		}
		if (!Locks.TryTake(Phone.LockName, player))
		{
			Refuse(player, Phone, "call", "the line is busy");
			return;
		}
		PhoneChannel.SetQuick(m.quick);
		try
		{
			instance.StartPhoneDialog(m.number);
		}
		finally
		{
			PhoneChannel.SetQuick(v: false);
		}
		if (!PhoneChannel.Talking)
		{
			Locks.Release(Phone.LockName, player);
			Refuse(player, Phone, "call", "no answer");
		}
	}

	private static void ServerHangup(MsgPhoneHangup m, int player)
	{
		if (Locks.Holder(Phone.LockName) != player)
		{
			Refuse(player, Phone, "hangup", "you are not on the phone", quiet: true);
			return;
		}
		PhoneUIManager instance = PhoneUIManager.Instance;
		if (instance != null)
		{
			instance.StopCall();
		}
		Locks.Release(Phone.LockName, player);
	}

	internal static void ServerFreshPhone(string reason)
	{
		if (!Net.IsServer || !Game.ShopLoaded)
		{
			return;
		}
		if (BotRunner.Enabled && (Args.Get("--br-no-freshphone") ?? "").Trim() == "1")
		{
			Log.Info("dialogue: lab --br-no-freshphone: phone leftovers kept (" + reason + "): " + DigestPart());
			return;
		}
		PhoneUIManager instance = PhoneUIManager.Instance;
		DialoguePhoneUIManager instance2 = DialoguePhoneUIManager.Instance;
		if ((Object)(object)instance == (Object)null || (Object)(object)instance2 == (Object)null || PhoneChannel.Talking)
		{
			return;
		}
		int num = Locks.Holder(Phone.LockName);
		bool flag = reason == "night";
		if (num < 0 || flag)
		{
			if (num >= 0)
			{
				Locks.Release(Phone.LockName, num);
				Log.Info("dialogue: the phone line was still held by " + Net.PlayerName(num) + " after the night; freed before the morning snapshot");
			}
			if (instance2.currentDialog != null || Phone.Uncompleted != null || instance.currentPhoneClient != null)
			{
				string text = DigestPart();
				string text2 = ((instance2.currentDialog != null) ? ("the last call's line " + DialogueRefs.Fp(instance2.currentDialog)) : "") + ((instance.currentPhoneClient != null) ? (((instance2.currentDialog != null) ? " and " : "") + "the caller " + instance.currentPhoneClient.dialogFuncId) : "");
				instance2.currentDialog = null;
				Phone.Uncompleted = null;
				instance.currentPhoneClient = null;
				FreshPhones++;
				Log.Info("dialogue: the phone kept " + ((text2.Length > 0) ? text2 : "an unfinished line") + " from before; cleared before the morning snapshot (" + reason + "), as every client's fresh scene has it: " + text + " -> " + DigestPart());
			}
		}
	}

	internal static void ServerCallEnded()
	{
		if (Net.IsServer)
		{
			int num = Locks.Holder(Phone.LockName);
			if (num >= 0)
			{
				Locks.Release(Phone.LockName, num);
			}
		}
	}

	private static void ServerTick()
	{
		if (!Game.ShopLoaded)
		{
			return;
		}
		EmporiumOption instance = EmporiumOption.Instance;
		if (Net.IsDedicated && !Net.HostAway && (Object)(object)instance != (Object)null && (!optionsSet || optionsFor != instance))
		{
			instance.enableTypewriterEffect = false;
			instance.autoNextDialog = false;
			instance.dialogSpeed = 1f;
			optionsSet = true;
			optionsFor = instance;
			Log.Info("dialogue: server dialogue options: no typewriter, no auto-advance, speed 1");
		}
		DialogueChannel[] array = new DialogueChannel[2] { Counter, Phone };
		foreach (DialogueChannel dialogueChannel in array)
		{
			if (!dialogueChannel.Ready)
			{
				continue;
			}
			dialogueChannel.CheckScene();
			if (dialogueChannel.LineUnfinished)
			{
				if (dialogueChannel.WatchNode != dialogueChannel.Current)
				{
					dialogueChannel.WatchNode = dialogueChannel.Current;
					dialogueChannel.WatchSince = Time.realtimeSinceStartup;
				}
				int num = Locks.Holder(dialogueChannel.LockName);
				float num2 = Time.realtimeSinceStartup - dialogueChannel.WatchSince;
				if (num >= 0 && num2 > 8f)
				{
					ShownTimeouts++;
				}
				if (num < 0 || num2 > 8f)
				{
					RunShown(dialogueChannel, skip: false, (num < 0) ? null : ("no word from " + Net.PlayerName(num) + " in " + 8f.ToString("F0", CultureInfo.InvariantCulture) + " s"));
				}
			}
			else
			{
				dialogueChannel.WatchNode = null;
			}
		}
		int num3 = Locks.Holder(Phone.LockName);
		if (num3 >= 0 && !PhoneChannel.Talking && Phone.Current == null)
		{
			phoneIdle += Time.unscaledDeltaTime;
			if (phoneIdle > 3f)
			{
				Locks.Release(Phone.LockName, num3);
				phoneIdle = 0f;
			}
		}
		else
		{
			phoneIdle = 0f;
		}
	}

	internal static bool IsPacer(DialogueChannel ch)
	{
		if (ch == null || !Net.IsClient)
		{
			return false;
		}
		return Locks.HeldBy(ch.LockName) == Net.LocalPlayer;
	}

	private static bool MayPace(DialogueChannel ch, out int holder)
	{
		holder = -1;
		if (ch is CounterChannel && !DayNightOuting.IsOut)
		{
			if (Customers.CounterHeldByOther(Net.LocalPlayer) == null)
			{
				return true;
			}
			holder = Locks.HeldBy(ch.LockName);
			return false;
		}
		if (ch is CounterChannel && !CustomerAtCounter)
		{
			return true;
		}
		int num = Locks.HeldBy(ch.LockName);
		if (num < 0 || num == Net.LocalPlayer)
		{
			return true;
		}
		holder = num;
		return false;
	}

	internal static bool MayPaceQuiet(DialogueChannel ch)
	{
		int holder;
		if (ch != null)
		{
			return MayPace(ch, out holder);
		}
		return false;
	}

	internal static bool SendShown(DialogueChannel ch, bool skip)
	{
		if (ch == null || !IsPacer(ch))
		{
			return false;
		}
		return SendLine(ch, "dialogue.shown", skip, -1);
	}

	internal static bool SendLine(DialogueChannel ch, string kind, bool skip, int index)
	{
		if (ch == null || !CanRequest || !ch.Ready)
		{
			return false;
		}
		if (!MayPace(ch, out var holder))
		{
			NoteRefused(ch.Name, kind.Substring(kind.IndexOf('.') + 1), HolderText(ch, holder), local: true);
			if (ch is CounterChannel && holder > 0)
			{
				Customers.NoteWaiting();
			}
			return false;
		}
		Dialogue current = ch.Current;
		if (current == null)
		{
			return false;
		}
		MsgDlgReq msgDlgReq = new MsgDlgReq
		{
			ch = ch.Name,
			fp = DialogueRefs.Fp(current),
			skip = skip,
			index = index
		};
		if (index >= 0)
		{
			if (current.choices == null || index >= current.choices.Count)
			{
				return false;
			}
			msgDlgReq.cfp = DialogueRefs.Fp(current.choices[index].Item2);
		}
		Net.ToServer(kind, msgDlgReq);
		return true;
	}

	internal static bool SendCall(long number, bool quick)
	{
		if (!CanRequest)
		{
			return false;
		}
		if (!MayPace(Phone, out var holder))
		{
			NoteRefused(Phone.Name, "call", HolderText(Phone, holder), local: true);
			return false;
		}
		Net.ToServer("phone.call", new MsgPhoneCall
		{
			number = number,
			quick = quick
		});
		return true;
	}

	internal static bool SendHangup()
	{
		if (!CanRequest || !IsPacer(Phone))
		{
			return false;
		}
		Net.ToServer("phone.hangup", new MsgPhoneHangup());
		return true;
	}

	private static void OnRefused(MsgDlgRefused m)
	{
		NoteRefused(m?.ch ?? "", m?.what ?? "", m?.why ?? "", local: false);
	}

	internal static void NoteRefused(string ch, string what, string why, bool local)
	{
		RefusedCount++;
		LastRefused = what + ": " + why;
		Log.Info("dialogue: " + (local ? "not sent: " : "the server refused ") + what + " on " + ch + " (" + why + ")");
		if (ch == "phone" && what == "call" && (Object)(object)PhoneUIManager.Instance != (Object)null && (Object)(object)PhoneUIManager.Instance.statusDisplay != (Object)null)
		{
			try
			{
				((TMP_Text)PhoneUIManager.Instance.statusDisplay).text = LocHelper.GetLocalizedUI("ui_phone_no_answer", Array.Empty<object>());
			}
			catch
			{
			}
		}
		Action<string, string, string> action = refused;
		if (action == null)
		{
			return;
		}
		try
		{
			action(ch, what, why);
		}
		catch (Exception ex)
		{
			Log.Warn("dialogue: a Refused handler threw: " + ex.Message);
		}
	}

	private static void OnStep(MsgDlgStep m)
	{
		inbox.Enqueue(m);
		Pump();
	}

	private static void OnStepDone(MsgDlgDone m)
	{
		inbox.Enqueue(m);
		Pump();
	}

	private static void Pump()
	{
		int num = 0;
		while (inbox.Count > 0 && num++ < 256)
		{
			object obj = inbox.Peek();
			if (obj is MsgDlgStep { op: "start", rf: null, inline: not null } msgDlgStep)
			{
				DialogueChannel dialogueChannel = Chan(msgDlgStep.ch);
				Dialogue val = ((dialogueChannel != null) ? DialogueRefs.TakeOffer(dialogueChannel, msgDlgStep.inline) : null);
				if (val == null && !DayLog.Replaying)
				{
					if (headWaitSince < 0f)
					{
						headWaitSince = Time.realtimeSinceStartup;
					}
					if (Time.realtimeSinceStartup - headWaitSince < 3f)
					{
						break;
					}
				}
				headWaitSince = -1f;
				inbox.Dequeue();
				ApplyStep(msgDlgStep, val);
			}
			else
			{
				headWaitSince = -1f;
				inbox.Dequeue();
				if (obj is MsgDlgStep m)
				{
					ApplyStep(m, null);
				}
				else if (obj is MsgDlgDone m2)
				{
					ApplyDone(m2);
				}
			}
		}
	}

	private static void ApplyStep(MsgDlgStep m, Dialogue offer)
	{
		DialogueChannel dialogueChannel = Chan(m.ch);
		if (dialogueChannel == null)
		{
			return;
		}
		if (!dialogueChannel.Ready)
		{
			Log.Warn("dialogue: step " + m.op + " #" + m.seq + " on " + m.ch + " but no conversation manager here");
			return;
		}
		dialogueChannel.CheckScene();
		if (m.seq != dialogueChannel.Seq + 1)
		{
			Log.Warn("dialogue: " + dialogueChannel.Name + " step #" + m.seq + " after #" + dialogueChannel.Seq + ((m.seq > dialogueChannel.Seq + 1) ? (" (missed " + (m.seq - dialogueChannel.Seq - 1) + ")") : " (numbering differs)"));
		}
		dialogueChannel.Seq = m.seq;
		bool flag = IsPacer(dialogueChannel) && !DayLog.Replaying;
		DialogueChannel dialogueChannel2 = applyingFor;
		bool flag2 = uiForOthers;
		applyingFor = dialogueChannel;
		uiForOthers = !flag;
		try
		{
			using (Replay.Client(m.seed ?? new Seed()))
			{
				switch (m.op)
				{
				case "start":
				{
					Dialogue val = offer;
					if (val == null && m.rf != null)
					{
						val = DialogueRefs.Resolve(dialogueChannel, m.rf);
					}
					if (val == null)
					{
						Log.Warn("dialogue: " + dialogueChannel.Name + " start #" + m.seq + " (" + (m.rf ?? ("inline " + m.inline?.fp)) + ") has no line here; showing the server's text and asking for a resync");
						val = DialogueRefs.TextOnly(m.inline);
						AskResync("dialogue line not found");
					}
					dialogueChannel.Start(val, m.forced);
					break;
				}
				case "main":
				{
					StoreClient val2 = null;
					try
					{
						PlayerStore store = Game.Store;
						object obj;
						if (store == null)
						{
							obj = null;
						}
						else
						{
							StoreClientInstance currentClientInstance = store.currentClientInstance;
							obj = ((currentClientInstance != null) ? currentClientInstance.GetClientBlueprint() : null);
						}
						val2 = (StoreClient)obj;
					}
					catch
					{
					}
					if (val2 == null || val2.identifier != m.rf)
					{
						Log.Warn("dialogue: main dialogue #" + m.seq + " of " + m.rf + " but the customer here is " + (val2?.identifier ?? "nobody"));
						AskResync("main dialogue customer differs");
					}
					else
					{
						val2.StartMainDialogue();
					}
					break;
				}
				case "next":
					dialogueChannel.Continue();
					break;
				case "finish":
					dialogueChannel.Finish();
					break;
				case "choose":
				{
					Dialogue current = dialogueChannel.Current;
					if (current == null || current.choices == null || current.choices.Count != m.count || DialogueRefs.Fp(current) != m.fp)
					{
						Log.Warn("dialogue: " + dialogueChannel.Name + " choose #" + m.seq + " does not match the line here (" + DialogueRefs.Fp(current) + "/" + (current?.choices?.Count).GetValueOrDefault() + " vs " + m.fp + "/" + m.count + ")");
						AskResync("dialogue choices differ");
					}
					else
					{
						int countBefore = dialogueChannel.Coroutines?.Count ?? 0;
						dialogueChannel.Select(m.index);
						dialogueChannel.DropNewestCoroutineSince(countBefore);
					}
					break;
				}
				case "shown":
					dialogueChannel.SkipCurrent = m.skip;
					dialogueChannel.Shown(server: false, flag);
					break;
				case "endAction":
				{
					Dialogue val3 = ((m.rf != null) ? DialogueRefs.Resolve(dialogueChannel, m.rf) : null);
					if (val3 == null)
					{
						Log.Warn("dialogue: " + dialogueChannel.Name + " end action #" + m.seq + " (" + (m.rf ?? "inline") + ") has no line here");
						AskResync("dialogue end action not found");
					}
					else
					{
						dialogueChannel.FireEndAction(val3);
					}
					break;
				}
				case "end":
					dialogueChannel.End();
					break;
				case "interrupt":
					dialogueChannel.Interrupt();
					break;
				case "flush":
				{
					DialogUIManager instance4 = DialogUIManager.Instance;
					if (instance4 != null)
					{
						instance4.FlushScheduledEndActions();
					}
					break;
				}
				case "checkOwned":
				{
					DialogUIManager instance3 = DialogUIManager.Instance;
					if (instance3 != null)
					{
						instance3.CheckOwned();
					}
					break;
				}
				case "call":
					PhoneChannel.SetQuick(m.quick);
					try
					{
						PhoneUIManager instance2 = PhoneUIManager.Instance;
						if (instance2 != null)
						{
							instance2.StartPhoneDialog(m.number);
						}
						break;
					}
					finally
					{
						PhoneChannel.SetQuick(v: false);
					}
				case "hangup":
				{
					PhoneUIManager instance = PhoneUIManager.Instance;
					if (instance != null)
					{
						instance.StopCall();
					}
					break;
				}
				default:
					Log.Warn("dialogue: unknown step op " + m.op);
					break;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error("dialogue: applying " + dialogueChannel.Name + " step " + m.op + " #" + m.seq + " threw: " + ex);
			AskResync("dialogue step failed");
		}
		finally
		{
			applyingFor = dialogueChannel2;
			uiForOthers = flag2;
		}
	}

	private static void ApplyDone(MsgDlgDone m)
	{
		DialogueChannel dialogueChannel = Chan(m.ch);
		if (dialogueChannel == null || !dialogueChannel.Ready)
		{
			return;
		}
		string text = DialogueRefs.Fp(dialogueChannel.Current);
		if (m.seq == dialogueChannel.Seq)
		{
			dialogueChannel.Remember(m.seq, text);
		}
		PlayerStore store = Game.Store;
		bool flag = text == m.fp;
		long num = 0L;
		try
		{
			num = Replay.Digest();
		}
		catch
		{
		}
		bool flag2 = m.rd == 0L || num == 0L || num == m.rd;
		bool flag3 = store == null || (store.playerCash == m.cash && store.wildFavor == m.favor);
		if (flag && flag2 && flag3)
		{
			return;
		}
		Mismatches++;
		if (!(Time.realtimeSinceStartup - lastMismatchLog < 10f && flag && flag2))
		{
			lastMismatchLog = Time.realtimeSinceStartup;
			Log.Warn("dialogue: after " + dialogueChannel.Name + " step #" + m.seq + " this client differs from the server: line " + text + "/" + m.fp + ", cash " + (store?.playerCash ?? 0) + "/" + m.cash + ", favor " + (store?.wildFavor ?? 0) + "/" + m.favor + (flag2 ? "" : (", replay digest " + num + "/" + m.rd)));
			if (!flag || !flag2)
			{
				AskResync("dialogue state differs after step " + m.seq);
			}
		}
	}

	private static void AskResync(string why)
	{
		if (Net.IsClient && !DayLog.Replaying && !Snapshot.IsLoading && !(Time.realtimeSinceStartup - lastResyncAsk < 30f))
		{
			lastResyncAsk = Time.realtimeSinceStartup;
			Log.Info("dialogue: asking for a resync: " + why);
			Snapshot.RequestResync("dialogue: " + why);
		}
	}

	private static void ResetClient()
	{
		inbox.Clear();
		headWaitSince = -1f;
		DialogueRefs.ClearOffers();
		Counter.Seq = 0;
		Phone.Seq = 0;
		Counter.History.Clear();
		Phone.History.Clear();
		uiForOthers = false;
	}

	[SystemTick]
	private static void Tick()
	{
		if (!Net.IsOnline)
		{
			return;
		}
		try
		{
			if (Net.IsServer)
			{
				ServerTick();
			}
			else if ((Object)(object)StoreUIManager.Instance != (Object)null)
			{
				if (Counter.CheckScene() | Phone.CheckScene())
				{
					DialogueRefs.ClearOffers();
				}
				if (inbox.Count > 0)
				{
					Pump();
				}
			}
			Suppress.Tick();
		}
		catch (Exception ex)
		{
			Log.Error("dialogue tick: " + ex);
		}
	}

	internal static string Digest()
	{
		string text = PhoneChannel.Caller?.dialogFuncId ?? "-";
		return Part(Counter) + " " + Part(Phone) + "/" + text + (PhoneChannel.Talking ? "/talking" : "");
		static string Part(DialogueChannel ch)
		{
			return ch.Name + ":" + ch.SeqNow + "/" + DialogueRefs.Fp(ch.Current) + (ch.LineUnfinished ? "/typing" : "");
		}
	}

	internal static string DigestPart()
	{
		if (!Game.ShopLoaded)
		{
			return "";
		}
		int num = Counter.Scheduled?.Count ?? 0;
		string text = PhoneChannel.Caller?.dialogFuncId ?? "-";
		return "c:" + Line(Counter) + "/e" + num + " p:" + Line(Phone) + "/" + text + (PhoneChannel.Talking ? "/talking" : "");
		static string Line(DialogueChannel ch)
		{
			return ch.SeqNow + "/" + DialogueRefs.Fp(ch.Current) + (ch.LineUnfinished ? "/typing" : "");
		}
	}

	internal static bool AtMatches(string mine, string serverHistory, out bool pending, out string detail)
	{
		pending = false;
		detail = "";
		int num = mine?.IndexOf(':') ?? (-1);
		if (num <= 0 || !int.TryParse(mine.Substring(0, num), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			pending = true;
			detail = "no step read here (" + mine + ")";
			return false;
		}
		string text = mine.Substring(num + 1);
		if (text == "?")
		{
			pending = true;
			detail = "step " + result + "'s check not applied here yet";
			return false;
		}
		int num2 = int.MinValue;
		int num3 = int.MaxValue;
		string[] array = (serverHistory ?? "").Split(',');
		foreach (string text2 in array)
		{
			int num4 = text2.IndexOf(':');
			if (num4 > 0 && int.TryParse(text2.Substring(0, num4), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
			{
				if (result2 > num2)
				{
					num2 = result2;
				}
				if (result2 < num3)
				{
					num3 = result2;
				}
				if (result2 == result)
				{
					string text3 = text2.Substring(num4 + 1);
					detail = "at step " + result + " here " + text + ", server " + text3;
					return text3 == text;
				}
			}
		}
		pending = true;
		detail = ((num2 == int.MinValue) ? "no server history" : ((result > num2) ? ("step " + result + " here is newer than the server's answer (" + num2 + ")") : ("step " + result + " is older than the server's history (" + num3 + "-" + num2 + ")")));
		return false;
	}
}
