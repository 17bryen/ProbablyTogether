using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ProbablyTogether.Bots;
using Steamworks;
using UnityEngine;

namespace ProbablyTogether;

internal static class GameBuild
{
	private static string id;

	private static string beta;

	private static bool betaRead;

	public static string Id
	{
		get
		{
			if (id == null)
			{
				id = Compute();
			}
			return id;
		}
	}

	public static string Beta
	{
		get
		{
			if (!betaRead)
			{
				betaRead = true;
				beta = ReadBeta();
			}
			return beta ?? "";
		}
	}

	private static string Compute()
	{
		try
		{
			using FileStream inputStream = File.OpenRead(Path.Combine(Path.Combine(Application.dataPath, "Managed"), "Assembly-CSharp.dll"));
			using SHA256 sHA = SHA256.Create();
			byte[] array = sHA.ComputeHash(inputStream);
			StringBuilder stringBuilder = new StringBuilder(12);
			for (int i = 0; i < 6; i++)
			{
				stringBuilder.Append(array[i].ToString("x2"));
			}
			Log.Info("build: game code " + stringBuilder?.ToString() + ((Beta.Length > 0) ? (", Steam beta '" + Beta + "'") : ""));
			return stringBuilder.ToString();
		}
		catch (Exception ex)
		{
			Log.Warn("build: the game's code could not be read (" + ex.Message + "); builds are not compared");
			return "";
		}
	}

	private static string ReadBeta()
	{
		try
		{
			string text = Args.Get("--br-beta");
			if (text != null && text.Length > 0 && BotRunner.LabAllowed(out var _))
			{
				return Clean(text);
			}
			if (!SteamManager.Initialized)
			{
				return "";
			}
			string name = default(string);
			return SteamApps.GetCurrentBetaName(out name, 128) ? Clean(name) : "";
		}
		catch (Exception ex)
		{
			Log.Warn("build: Steam beta branch not read: " + ex.Message);
			return "";
		}
	}

	internal static string Clean(string name)
	{
		string text = Regex.Replace(name ?? "", "[^A-Za-z0-9_.-]", "");
		if (text.Length <= 40)
		{
			return text;
		}
		return text.Substring(0, 40);
	}

	internal static string CleanId(string s)
	{
		s = Regex.Replace(s ?? "", "[^0-9a-f]", "");
		if (s.Length <= 16)
		{
			return s;
		}
		return s.Substring(0, 16);
	}
}
