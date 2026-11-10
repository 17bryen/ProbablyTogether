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

	public static string Root => Override ?? Original;

	public static string GameDataPath()
	{
		return Override ?? Application.persistentDataPath;
	}

	public static string Under(string relative)
	{
		return Root + "/" + relative.Replace('\\', '/').TrimStart('/');
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

	internal static List<MethodBase> FindCallers()
	{
		List<MethodBase> list = new List<MethodBase>();
		List<string> list2 = new List<string>();
		ScanModule(typeof(SaveFiles).Assembly.ManifestModule, list, list2);
		ScanModule(typeof(ES3).Assembly.ManifestModule, list, list2);
		List<string> list3 = new List<string>();
		foreach (MethodBase item in list)
		{
			list3.Add(item.DeclaringType?.Name + "." + item.Name);
		}
		Log.Info("data folder: " + list.Count + " game method(s) read the account folder and are redirected: " + string.Join(", ", list3) + ((list2.Count > 0) ? ("; left alone: " + string.Join(", ", list2)) : ""));
		return list;
	}

	private static void ScanModule(Module module, List<MethodBase> found, List<string> skipped)
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
			if (memberInfo is MethodInfo { Name: "get_persistentDataPath" } methodInfo && methodInfo.DeclaringType == typeof(Application))
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
