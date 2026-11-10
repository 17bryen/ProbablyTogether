using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ProbablyTogether;

internal static class Wire
{
	internal sealed class Limits
	{
		public string Name;

		public int MaxFrame;

		public int MaxJson;

		public int MaxDepth;

		public int MaxValues;
	}

	internal sealed class OverBudget : IOException
	{
		public OverBudget()
			: base("frame expands past this connection's byte budget")
		{
		}
	}

	private sealed class FiniteFloats : JsonConverter
	{
		public override bool CanWrite => false;

		public override bool CanConvert(Type t)
		{
			if (!(t == typeof(float)) && !(t == typeof(double)) && !(t == typeof(float?)))
			{
				return t == typeof(double?);
			}
			return true;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			throw new NotSupportedException();
		}

		public override object ReadJson(JsonReader r, Type t, object existing, JsonSerializer s)
		{
			bool flag = t == typeof(float?) || t == typeof(double?);
			JsonToken tokenType = r.TokenType;
			double result;
			switch ((int)tokenType - 7)
			{
			case 4:
				if (flag)
				{
					return null;
				}
				throw new JsonSerializationException("null where a number is needed");
			case 0:
			case 1:
				result = ((r.Value is IConvertible convertible) ? convertible.ToDouble(CultureInfo.InvariantCulture) : double.Parse(Convert.ToString(r.Value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture));
				break;
			case 2:
			{
				string text = (string)r.Value;
				if (string.IsNullOrEmpty(text) && flag)
				{
					return null;
				}
				if (text == null || text.Length > 64 || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
				{
					throw new JsonSerializationException("not a number");
				}
				break;
			}
			case 3:
				result = (((bool)r.Value) ? 1 : 0);
				break;
			default:
				throw new JsonSerializationException("not a number");
			}
			if (double.IsNaN(result) || double.IsInfinity(result))
			{
				throw new JsonSerializationException("not a finite number");
			}
			if (t == typeof(float) || t == typeof(float?))
			{
				if (result > 3.4028234663852886E+38 || result < -3.4028234663852886E+38)
				{
					throw new JsonSerializationException("out of range for a float");
				}
				return (float)result;
			}
			return result;
		}
	}

	internal const int GzipOver = 8192;

	internal static readonly byte[] Magic = new byte[4] { 66, 82, 67, 49 };

	internal const uint GzipFlag = 2147483648u;

	internal const int ClientToServerMax = 1048576;

	internal const int ClientToServerMaxJson = 4194304;

	internal const int ServerToClientMax = 8388608;

	internal const int ServerToClientMaxJson = 100663296;

	internal const int PreJoinMax = 16384;

	internal const int MaxKindLength = 64;

	internal const int MaxNumberChars = 64;

	internal static readonly Limits FromClient = new Limits
	{
		Name = "client",
		MaxFrame = 1048576,
		MaxJson = 4194304,
		MaxDepth = 32,
		MaxValues = 200000
	};

	internal static readonly Limits FromServer = new Limits
	{
		Name = "server",
		MaxFrame = 8388608,
		MaxJson = 100663296,
		MaxDepth = 64,
		MaxValues = 4000000
	};

	private static readonly UTF8Encoding Utf8In = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

	private static readonly UTF8Encoding Utf8Out = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

	internal static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
	{
		TypeNameHandling = (TypeNameHandling)0,
		ReferenceLoopHandling = (ReferenceLoopHandling)1,
		MaxDepth = 64,
		DateParseHandling = (DateParseHandling)0,
		NullValueHandling = (NullValueHandling)0
	};

	private static readonly JsonSerializerSettings UntrustedSettings = new JsonSerializerSettings
	{
		TypeNameHandling = (TypeNameHandling)0,
		ReferenceLoopHandling = (ReferenceLoopHandling)1,
		MaxDepth = 32,
		DateParseHandling = (DateParseHandling)0,
		NullValueHandling = (NullValueHandling)0,
		MetadataPropertyHandling = (MetadataPropertyHandling)2,
		Converters = { (JsonConverter)(object)new FiniteFloats() }
	};

	private static readonly JsonLoadSettings LoadSettings = new JsonLoadSettings
	{
		CommentHandling = (CommentHandling)0,
		LineInfoHandling = (LineInfoHandling)0,
		DuplicatePropertyNameHandling = (DuplicatePropertyNameHandling)2
	};

	[ThreadStatic]
	private static JsonSerializer serializer;

	[ThreadStatic]
	private static JsonSerializer untrusted;

	private static int warnedBig;

	internal static JsonSerializer Serializer => serializer ?? (serializer = JsonSerializer.Create(Settings));

	internal static JsonSerializer Untrusted => untrusted ?? (untrusted = JsonSerializer.Create(UntrustedSettings));

	internal static byte[] Encode(string kind, object payload)
	{
		string json;
		return Encode(kind, payload, out json);
	}

	internal static byte[] Encode(string kind, object payload, out string json)
	{
		StringWriter stringWriter = new StringWriter(new StringBuilder(256), CultureInfo.InvariantCulture);
		JsonTextWriter val = new JsonTextWriter((TextWriter)stringWriter);
		try
		{
			((JsonWriter)val).WriteStartObject();
			((JsonWriter)val).WritePropertyName("k");
			((JsonWriter)val).WriteValue(kind);
			((JsonWriter)val).WritePropertyName("d");
			if (payload == null)
			{
				((JsonWriter)val).WriteNull();
			}
			else
			{
				JToken val2 = (JToken)((payload is JToken) ? payload : null);
				if (val2 != null)
				{
					val2.WriteTo((JsonWriter)(object)val, Array.Empty<JsonConverter>());
				}
				else
				{
					Serializer.Serialize((JsonWriter)(object)val, payload);
				}
			}
			((JsonWriter)val).WriteEndObject();
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		json = stringWriter.ToString();
		byte[] array = Utf8Out.GetBytes(json);
		bool flag = array.Length > 8192;
		if (flag)
		{
			array = Gzip(array);
		}
		if (array.Length > 8388608 && Interlocked.Exchange(ref warnedBig, 1) == 0)
		{
			Log.Error("wire: a " + kind + " frame is " + (array.Length >> 10) + " KB on the wire, over the " + 8 + " MB a client accepts: that client will drop the connection (logged once)");
		}
		byte[] array2 = new byte[4 + array.Length];
		uint num = (uint)(array.Length | (flag ? int.MinValue : 0));
		array2[0] = (byte)(num >> 24);
		array2[1] = (byte)(num >> 16);
		array2[2] = (byte)(num >> 8);
		array2[3] = (byte)num;
		Buffer.BlockCopy(array, 0, array2, 4, array.Length);
		return array2;
	}

	internal static void ParseHeader(byte[] h, out int length, out bool gz)
	{
		uint num = (uint)((h[0] << 24) | (h[1] << 16) | (h[2] << 8) | h[3]);
		gz = (num & 0x80000000u) != 0;
		length = (int)(num & 0x7FFFFFFF);
	}

	internal static void Decode(byte[] body, int length, bool gz, Limits lim, int jsonCap, out string kind, out JToken data, out int jsonBytes)
	{
		if (jsonCap > lim.MaxJson)
		{
			jsonCap = lim.MaxJson;
		}
		byte[] array;
		int num;
		if (gz)
		{
			array = Gunzip(body, length, jsonCap, jsonCap < lim.MaxJson);
			num = array.Length;
		}
		else
		{
			array = body;
			num = length;
			if (num > jsonCap)
			{
				if (jsonCap < lim.MaxJson)
				{
					throw new OverBudget();
				}
				throw new InvalidDataException("frame of " + num + " bytes is over the " + lim.MaxJson + " byte limit");
			}
		}
		jsonBytes = num;
		Scan(array, num, lim);
		JsonTextReader val = new JsonTextReader((TextReader)new StringReader(Utf8In.GetString(array, 0, num)))
		{
			DateParseHandling = (DateParseHandling)0,
			FloatParseHandling = (FloatParseHandling)0,
			MaxDepth = lim.MaxDepth,
			CloseInput = true
		};
		JObject val2;
		try
		{
			val2 = JObject.Load((JsonReader)(object)val, LoadSettings);
			if (((JsonReader)val).Read())
			{
				throw new InvalidDataException("data after the frame's object");
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		JToken val3 = val2["k"];
		if (val3 == null || (int)val3.Type != 8)
		{
			throw new InvalidDataException("frame without a kind");
		}
		kind = (string)val3;
		if (!KindOk(kind))
		{
			throw new InvalidDataException("bad kind");
		}
		data = val2["d"];
	}

	internal static bool KindOk(string kind)
	{
		if (string.IsNullOrEmpty(kind) || kind.Length > 64)
		{
			return false;
		}
		foreach (char c in kind)
		{
			if ((c < 'a' || c > 'z') && (c < 'A' || c > 'Z') && (c < '0' || c > '9') && c != '.' && c != '_' && c != ':' && c != '-')
			{
				return false;
			}
		}
		return true;
	}

	internal static void Scan(byte[] b, int n, Limits lim)
	{
		int num = 0;
		int num2 = 0;
		int i;
		for (i = 0; i < n && IsWs(b[i]); i++)
		{
		}
		if (i >= n || b[i] != 123)
		{
			throw new InvalidDataException("a frame must be one JSON object");
		}
		bool flag = false;
		int num3 = -1;
		for (; i < n; i++)
		{
			byte b2 = b[i];
			if (num3 >= 0)
			{
				if (!IsDelim(b2))
				{
					continue;
				}
				CheckBare(b, num3, i);
				num3 = -1;
			}
			if (flag && !IsWs(b2))
			{
				throw new InvalidDataException("data after the frame's object");
			}
			switch (b2)
			{
			case 91:
			case 123:
				if (++num > lim.MaxDepth)
				{
					throw new InvalidDataException("nested deeper than " + lim.MaxDepth);
				}
				if (++num2 > lim.MaxValues)
				{
					throw new InvalidDataException("more than " + lim.MaxValues + " values");
				}
				break;
			case 93:
			case 125:
				if (--num < 0)
				{
					throw new InvalidDataException("unbalanced brackets");
				}
				if (num == 0)
				{
					flag = true;
				}
				break;
			case 34:
				if (++num2 > lim.MaxValues)
				{
					throw new InvalidDataException("more than " + lim.MaxValues + " values");
				}
				i = SkipString(b, i + 1, n);
				break;
			default:
				if (++num2 > lim.MaxValues)
				{
					throw new InvalidDataException("more than " + lim.MaxValues + " values");
				}
				num3 = i;
				break;
			case 9:
			case 10:
			case 13:
			case 32:
			case 44:
			case 58:
				break;
			}
		}
		if (num3 >= 0)
		{
			CheckBare(b, num3, n);
		}
		if (num != 0 || !flag)
		{
			throw new InvalidDataException("truncated JSON");
		}
	}

	private static bool IsWs(byte c)
	{
		if (c != 32 && c != 9 && c != 10)
		{
			return c == 13;
		}
		return true;
	}

	private static bool IsDelim(byte c)
	{
		if (!IsWs(c) && c != 44 && c != 58 && c != 123 && c != 125 && c != 91 && c != 93)
		{
			return c == 34;
		}
		return true;
	}

	private static void CheckBare(byte[] b, int from, int to)
	{
		int num = to - from;
		if ((num == 4 && b[from] == 116 && b[from + 1] == 114 && b[from + 2] == 117 && b[from + 3] == 101) || (num == 4 && b[from] == 110 && b[from + 1] == 117 && b[from + 2] == 108 && b[from + 3] == 108) || (num == 5 && b[from] == 102 && b[from + 1] == 97 && b[from + 2] == 108 && b[from + 3] == 115 && b[from + 4] == 101))
		{
			return;
		}
		if (num > 64)
		{
			throw new InvalidDataException("a number longer than " + 64 + " characters");
		}
		for (int i = from; i < to; i++)
		{
			byte b2 = b[i];
			if ((b2 < 48 || b2 > 57) && b2 != 45 && b2 != 43 && b2 != 46 && b2 != 101 && b2 != 69)
			{
				throw new InvalidDataException("not plain JSON (a bare word, comment, single quote, NaN or Infinity)");
			}
		}
	}

	private static int SkipString(byte[] b, int i, int n)
	{
		while (i < n)
		{
			byte b2 = b[i];
			if (b2 == 34)
			{
				return i;
			}
			if (b2 < 32)
			{
				throw new InvalidDataException("a raw control character inside a string");
			}
			if (b2 == 92)
			{
				if (++i >= n)
				{
					break;
				}
				switch (b[i])
				{
				case 117:
				{
					int num = Hex4(b, i + 1, n);
					i += 4;
					if (num >= 56320 && num <= 57343)
					{
						throw new InvalidDataException("a lone low surrogate escape");
					}
					if (num >= 55296 && num <= 56319)
					{
						if (i + 6 >= n || b[i + 1] != 92 || b[i + 2] != 117)
						{
							throw new InvalidDataException("a lone high surrogate escape");
						}
						int num2 = Hex4(b, i + 3, n);
						if (num2 < 56320 || num2 > 57343)
						{
							throw new InvalidDataException("a lone high surrogate escape");
						}
						i += 6;
					}
					break;
				}
				default:
					throw new InvalidDataException("a bad escape inside a string");
				case 34:
				case 47:
				case 92:
				case 98:
				case 102:
				case 110:
				case 114:
				case 116:
					break;
				}
			}
			i++;
		}
		throw new InvalidDataException("an unterminated string");
	}

	private static int Hex4(byte[] b, int i, int n)
	{
		if (i + 4 > n)
		{
			throw new InvalidDataException("a short \\u escape");
		}
		int num = 0;
		for (int j = i; j < i + 4; j++)
		{
			int num2 = b[j];
			int num3;
			if (num2 >= 48 && num2 <= 57)
			{
				num3 = num2 - 48;
			}
			else if (num2 >= 97 && num2 <= 102)
			{
				num3 = num2 - 97 + 10;
			}
			else
			{
				if (num2 < 65 || num2 > 70)
				{
					throw new InvalidDataException("a bad \\u escape");
				}
				num3 = num2 - 65 + 10;
			}
			num = (num << 4) | num3;
		}
		return num;
	}

	internal static byte[] Gzip(byte[] data)
	{
		using MemoryStream memoryStream = new MemoryStream(data.Length / 4 + 64);
		using (GZipStream gZipStream = new GZipStream(memoryStream, CompressionLevel.Fastest, leaveOpen: true))
		{
			gZipStream.Write(data, 0, data.Length);
		}
		return memoryStream.ToArray();
	}

	internal static byte[] Gunzip(byte[] data, int length, int max, bool budget = false)
	{
		using MemoryStream stream = new MemoryStream(data, 0, length, writable: false);
		using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress);
		using MemoryStream memoryStream = new MemoryStream((int)Math.Min((long)length * 4L, max) + 16);
		byte[] array = new byte[16384];
		int num;
		while ((num = gZipStream.Read(array, 0, array.Length)) > 0)
		{
			if (memoryStream.Length + num > max)
			{
				if (budget)
				{
					throw new OverBudget();
				}
				throw new InvalidDataException("frame expands past " + max + " bytes");
			}
			memoryStream.Write(array, 0, num);
		}
		return memoryStream.ToArray();
	}

	internal static byte[] Gunzip(byte[] data, int max)
	{
		return Gunzip(data, data.Length, max);
	}

	internal static T To<T>(JToken d) where T : class
	{
		if (d != null && (int)d.Type != 10)
		{
			return d.ToObject<T>(Net.IsServer ? Untrusted : Serializer);
		}
		return null;
	}

	internal static object To(JToken d, Type t)
	{
		if (d != null && (int)d.Type != 10)
		{
			return d.ToObject(t, Net.IsServer ? Untrusted : Serializer);
		}
		return null;
	}
}
