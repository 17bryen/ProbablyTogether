using System;
using UnityEngine;

namespace ProbablyTogether;

internal sealed class Driver : MonoBehaviour
{
	internal static Driver Instance;

	private void Awake()
	{
		Instance = this;
	}

	private void Update()
	{
		try
		{
			Core.Tick();
		}
		catch (Exception ex)
		{
			Log.Error("core tick: " + ex);
		}
	}

	private void OnApplicationQuit()
	{
		try
		{
			Core.Shutdown("game closing");
		}
		catch
		{
		}
	}
}
