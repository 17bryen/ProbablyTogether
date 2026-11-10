using System;
using System.Collections.Generic;
using System.Reflection;

namespace ProbablyTogether;

internal static class SystemHooks
{
	private sealed class Hook
	{
		public string Name;

		public Action Call;

		public long LastErrorMs = -100000L;
	}

	private static readonly List<Hook> ticks = new List<Hook>();

	private static bool initDone;

	internal static int TickCount => ticks.Count;

	internal static void Init()
	{
		if (initDone)
		{
			return;
		}
		initDone = true;
		List<KeyValuePair<int, Hook>> list = new List<KeyValuePair<int, Hook>>();
		Module module = typeof(SystemHooks).Module;
		int num = 0;
		for (int i = 1; i < 1000000 && num < 64; i++)
		{
			MethodBase methodBase;
			try
			{
				methodBase = module.ResolveMethod(0x6000000 | i);
				num = 0;
			}
			catch (ArgumentOutOfRangeException)
			{
				num++;
				continue;
			}
			catch (Exception)
			{
				num = 0;
				continue;
			}
			if (!(methodBase is MethodInfo { IsStatic: not false, ContainsGenericParameters: false } methodInfo) || methodInfo.GetParameters().Length != 0)
			{
				continue;
			}
			Type declaringType = methodInfo.DeclaringType;
			if (declaringType == null || declaringType.ContainsGenericParameters)
			{
				continue;
			}
			SystemInitAttribute customAttribute = methodInfo.GetCustomAttribute<SystemInitAttribute>();
			SystemTickAttribute customAttribute2 = methodInfo.GetCustomAttribute<SystemTickAttribute>();
			if (customAttribute != null || customAttribute2 != null)
			{
				Action call;
				try
				{
					call = (Action)Delegate.CreateDelegate(typeof(Action), methodInfo);
				}
				catch (Exception ex3)
				{
					Log.Warn("system hook " + declaringType.Name + "." + methodInfo.Name + " skipped: " + ex3.Message);
					continue;
				}
				Hook hook = new Hook
				{
					Name = declaringType.Name + "." + methodInfo.Name,
					Call = call
				};
				if (customAttribute != null)
				{
					list.Add(new KeyValuePair<int, Hook>(customAttribute.Order, hook));
				}
				if (customAttribute2 != null)
				{
					ticks.Add(new Hook
					{
						Name = hook.Name,
						Call = call
					});
				}
			}
		}
		list.Sort((KeyValuePair<int, Hook> a, KeyValuePair<int, Hook> b) => (a.Key == b.Key) ? string.CompareOrdinal(a.Value.Name, b.Value.Name) : a.Key.CompareTo(b.Key));
		ticks.Sort((Hook a, Hook b) => string.CompareOrdinal(a.Name, b.Name));
		foreach (KeyValuePair<int, Hook> item in list)
		{
			try
			{
				item.Value.Call();
			}
			catch (Exception ex4)
			{
				Log.Error("system init " + item.Value.Name + ": " + ex4);
			}
		}
		Log.Info("systems: " + list.Count + " init hook(s), " + ticks.Count + " tick hook(s)");
	}

	internal static void Tick()
	{
		for (int i = 0; i < ticks.Count; i++)
		{
			Hook hook = ticks[i];
			try
			{
				hook.Call();
			}
			catch (Exception ex)
			{
				long ms = Clock.Ms;
				if (ms - hook.LastErrorMs >= 10000)
				{
					hook.LastErrorMs = ms;
					Log.Error("system tick " + hook.Name + ": " + ex);
				}
			}
		}
	}
}
