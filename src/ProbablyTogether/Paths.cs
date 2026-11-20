using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ProbablyTogether;

public static class Paths
{
	private static string original;

	private static string over;

	private static bool overDone;

	internal static int RewrittenSites;

	/// Host mode: the game options file stays in the account folder (the host is a normal player).
	internal static bool KeepSettings;

	private static readonly HashSet<string> SkippedTypes = new HashSet<string>(StringComparer.Ordinal);

	public static string Original
	{
		get
		{
			if (original == null)
			{
				original = Norm(Application.persistentDataPath);
			}
			return original;
		}
	}

	public static string Override
	{
		get
		{
			if (!overDone)
			{
				overDone = true;
				string data = Args.Data;
				if (!string.IsNullOrEmpty(data))
				{
					try
					{
						over = Norm(Path.GetFullPath(data));
						System.IO.Directory.CreateDirectory(over);
					}
					catch (Exception ex)
					{
						over = null;
						Log.Error("--br-data " + data + " is not usable, staying on the account folder: " + ex.Message);
					}
				}
			}
			return over;
		}
	}

	public static bool Redirecting => Override != null;

	internal static void ClearOverride()
	{
		over = null;
		overDone = true;
		Log.Info("data folder: back to the account folder");
	}

	/// Host mode from the menu: point the shop's saves at a folder before the shop loads.
	internal static bool SetOverride(string folder)
	{
		try
		{
			string text = Norm(Path.GetFullPath(folder));
			System.IO.Directory.CreateDirectory(text);
			over = text;
			overDone = true;
			Log.Info("data folder: now " + text);
			return true;
		}
		catch (Exception ex)
		{
			Log.Error("data folder " + folder + " is not usable: " + ex.Message);
			return false;
		}
	}

	public static string Root => Override ?? Original;

	public static string GameDataPath()
	{
		return Override ?? Application.persistentDataPath;
	}

	public static string Under(string relative)
	{
		// A name made while redirected is already a full path (see File).
		if (!string.IsNullOrEmpty(relative) && Path.IsPathRooted(relative))
		{
			return Norm(relative);
		}
		return Root + "/" + relative.Replace('\\', '/').TrimStart('/');
	}

	/// A save file's full path in the folder in use, or the bare name when none is.
	/// The save library keeps full paths as given and puts bare names in the account folder.
	public static string File(string name)
	{
		string text = Override;
		if (text == null || string.IsNullOrEmpty(name) || Path.IsPathRooted(name))
		{
			return name;
		}
		return text + "/" + name.Replace('\\', '/').TrimStart('/');
	}

	internal const string IndexName = "saves_index.es3";

	/// SaveFiles.IndexLoad / IndexSave for the folder in use; the game's calls are redirected here
	/// (IndexCallSites), since generic methods cannot be patched.
	public static T IndexLoad<T>(string key, T defaultValue)
	{
		if (Override == null)
		{
			return SaveFiles.IndexLoad(key, defaultValue);
		}
		T result = defaultValue;
		try
		{
			SaveFiles.WithIORetry(delegate
			{
				result = ES3.Load(key, File(IndexName), defaultValue);
			});
		}
		catch (Exception ex)
		{
			if (Log.Allow("paths|indexload", 30000L, out var skipped))
			{
				Log.Warn("data folder: reading '" + key + "' from the save index threw" + Log.More(skipped) + ": " + ex.Message);
			}
			return defaultValue;
		}
		return result;
	}

	public static void IndexSave<T>(string key, T value)
	{
		if (Override == null)
		{
			SaveFiles.IndexSave(key, value);
			return;
		}
		try
		{
			SaveFiles.WithIORetry(delegate
			{
				ES3.Save(key, value, File(IndexName));
			});
		}
		catch (Exception ex)
		{
			Log.Error("data folder: writing '" + key + "' to the save index threw: " + ex.Message);
		}
	}

	internal static int IndexSites;

	/// Game methods that use the save index; generic ones cannot be patched and are skipped.
	internal static List<MethodBase> FindIndexUsers()
	{
		List<MethodBase> list = new List<MethodBase>();
		List<string> list2 = new List<string>();
		Module manifestModule = typeof(SaveFiles).Assembly.ManifestModule;
		int num = 0;
		for (int i = 1; i < 1000000 && num < 64; i++)
		{
			MethodBase methodBase;
			try
			{
				methodBase = manifestModule.ResolveMethod(0x6000000 | i);
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
			if (methodBase == null || methodBase.IsAbstract)
			{
				continue;
			}
			byte[] array;
			try
			{
				array = methodBase.GetMethodBody()?.GetILAsByteArray();
			}
			catch
			{
				array = null;
			}
			if (array == null || !UsesIndex(manifestModule, array))
			{
				continue;
			}
			Type declaringType = methodBase.DeclaringType;
			string text = (declaringType?.Name ?? "?") + "." + methodBase.Name;
			if (declaringType != null && IsStudioSecurityType(declaringType))
			{
				list2.Add(text + " (mod security)");
			}
			else if (methodBase is ConstructorInfo && methodBase.IsStatic)
			{
				list2.Add(text + " (static constructor, already ran)");
			}
			else if (methodBase.ContainsGenericParameters || (declaringType != null && declaringType.ContainsGenericParameters))
			{
				list2.Add(text + " (generic)");
			}
			else
			{
				list.Add(methodBase);
			}
		}
		Log.Info("data folder: " + list.Count + " game method(s) use the save index and are pointed at the folder in use" + ((list2.Count > 0) ? ("; left alone: " + string.Join(", ", list2)) : ""));
		return list;
	}

	private static bool UsesIndex(Module module, byte[] il)
	{
		for (int i = 0; i + 4 < il.Length; i++)
		{
			byte b = il[i];
			if (b != 40 && b != 111 && b != 114)
			{
				continue;
			}
			int num = il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
			int num2 = (num >> 24) & 0xFF;
			try
			{
				if (b == 114)
				{
					if (num2 == 112 && module.ResolveString(num) == IndexName)
					{
						return true;
					}
				}
				else if (num2 == 43)
				{
					// A generic method call with its types filled in.
					MethodBase methodBase = module.ResolveMethod(num);
					if (methodBase != null && methodBase.DeclaringType == typeof(SaveFiles) && (methodBase.Name == "IndexLoad" || methodBase.Name == "IndexSave"))
					{
						return true;
					}
				}
			}
			catch
			{
			}
		}
		return false;
	}

	internal static string Norm(string p)
	{
		return p?.Replace('\\', '/').TrimEnd('/');
	}

	internal static string Rewrite(string full)
	{
		string text = Override;
		if (text == null || full == null)
		{
			return full;
		}
		string text2 = Original;
		if (KeepSettings && (full.EndsWith("/SaveFile.es3", StringComparison.OrdinalIgnoreCase) || full.EndsWith("\\SaveFile.es3", StringComparison.OrdinalIgnoreCase)))
		{
			return full;
		}
		if (full.Length > text2.Length && (full[text2.Length] == '/' || full[text2.Length] == '\\') && string.Compare(full, 0, text2, 0, text2.Length, StringComparison.OrdinalIgnoreCase) == 0)
		{
			return text + full.Substring(text2.Length);
		}
		return full;
	}

	private static bool IsStudioSecurityType(Type t)
	{
		Type type = t;
		while (type != null)
		{
			if (type.Namespace == null && type.Name.StartsWith("Mod", StringComparison.Ordinal) && !type.Name.StartsWith("Module", StringComparison.Ordinal))
			{
				return true;
			}
			type = type.DeclaringType;
		}
		return false;
	}

	/// The game's methods that call AudioSource.Play() (QuietAudioCallSites).
	internal static List<MethodBase> FindSoundPlayers()
	{
		List<MethodBase> list = new List<MethodBase>();
		List<string> list2 = new List<string>();
		ScanModule(typeof(SaveFiles).Assembly.ManifestModule, list, list2, (MemberInfo m) => m is MethodInfo methodInfo && QuietAudioCallSites.IsSoundCall(methodInfo));
		Log.Info("items-use: " + list.Count + " game method(s) play a sound source directly; those calls are quiet while another player's action is replayed" + ((list2.Count > 0) ? (" (" + list2.Count + " left alone)") : ""));
		return list;
	}

	internal static List<MethodBase> FindCallers()
	{
		List<MethodBase> list = new List<MethodBase>();
		List<string> list2 = new List<string>();
		ScanModule(typeof(SaveFiles).Assembly.ManifestModule, list, list2, (MemberInfo m) => m is MethodInfo methodInfo && methodInfo.Name == "get_persistentDataPath" && methodInfo.DeclaringType == typeof(Application));
		List<string> list3 = new List<string>();
		foreach (MethodBase item in list)
		{
			list3.Add(item.DeclaringType?.Name + "." + item.Name);
		}
		Log.Info("data folder: " + list.Count + " game method(s) read the account folder and are redirected: " + string.Join(", ", list3) + ((list2.Count > 0) ? ("; left alone: " + string.Join(", ", list2)) : ""));
		return list;
	}

	private static void ScanModule(Module module, List<MethodBase> found, List<string> skipped, Func<MemberInfo, bool> wanted)
	{
		HashSet<int> hashSet = new HashSet<int>();
		int num = 0;
		for (int i = 1; i < 1000000 && num < 64; i++)
		{
			int num2 = 0xA000000 | i;
			MemberInfo memberInfo;
			try
			{
				memberInfo = module.ResolveMember(num2);
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
			if (wanted(memberInfo))
			{
				hashSet.Add(num2);
			}
		}
		if (hashSet.Count == 0)
		{
			return;
		}
		num = 0;
		for (int j = 1; j < 1000000 && num < 64; j++)
		{
			MethodBase methodBase;
			try
			{
				methodBase = module.ResolveMethod(0x6000000 | j);
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
			if (methodBase == null || methodBase.IsAbstract)
			{
				continue;
			}
			byte[] array;
			try
			{
				array = methodBase.GetMethodBody()?.GetILAsByteArray();
			}
			catch
			{
				array = null;
			}
			if (array != null && CallsAny(array, hashSet))
			{
				Type declaringType = methodBase.DeclaringType;
				string text = (declaringType?.Name ?? "?") + "." + methodBase.Name;
				if (declaringType != null && IsStudioSecurityType(declaringType))
				{
					skipped.Add(text + " (mod security)");
				}
				else if (methodBase is ConstructorInfo && methodBase.IsStatic)
				{
					skipped.Add(text + " (static constructor, already ran)");
				}
				else if (methodBase.ContainsGenericParameters || (declaringType != null && declaringType.ContainsGenericParameters))
				{
					skipped.Add(text + " (generic)");
				}
				else
				{
					found.Add(methodBase);
				}
			}
		}
	}

	private static bool CallsAny(byte[] il, HashSet<int> tokens)
	{
		for (int i = 0; i + 4 < il.Length; i++)
		{
			byte b = il[i];
			if (b == 40 || b == 111)
			{
				int item = il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
				if (tokens.Contains(item))
				{
					return true;
				}
			}
		}
		return false;
	}
}
