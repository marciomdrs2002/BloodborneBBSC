using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace BloodborneLauncher.Core;

public static class HuntersDream
{
	public sealed record Link(string DeviceCode, string UserCode, string Page, int Interval, int ExpiresIn);

	public const string Server = "https://thehuntersdream.com";

	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(30.0)
	};

	public static bool OnlinePaused { get; } = Environment.GetEnvironmentVariable("BB_ONLINE_PAUSED") == "1";

	private static string DeviceName()
	{
		string machineName = Environment.MachineName;
		if (machineName.Length != 0)
		{
			return "BloodBorneButPc on " + machineName.Substring(0, Math.Min(48, machineName.Length));
		}
		return "BloodBorneButPc";
	}

	private static async Task<JsonObject> Call(string path, JsonObject body, CancellationToken cancel)
	{
		using HttpResponseMessage response = await Http.PostAsJsonAsync("https://thehuntersdream.com" + path, body, cancel);
		string json = await response.Content.ReadAsStringAsync(cancel);
		JsonObject jsonObject = null;
		try
		{
			jsonObject = JsonNode.Parse(json) as JsonObject;
		}
		catch (Exception)
		{
		}
		if (jsonObject?["ResKind"] == null)
		{
			throw new InvalidOperationException($"The Hunter's Dream answered HTTP {response.StatusCode} without an account reply");
		}
		if (jsonObject["ResKind"].GetValue<int>() != 0)
		{
			throw new InvalidOperationException(jsonObject["Message"]?.GetValue<string>() ?? "refused");
		}
		return jsonObject;
	}

	public static async Task<Link> StartLinkAsync(CancellationToken cancel)
	{
		JsonObject jsonObject = await Call("/auth/device/start", new JsonObject { ["DeviceName"] = DeviceName() }, cancel);
		Link link = new Link(jsonObject["DeviceCode"]?.GetValue<string>() ?? "", jsonObject["UserCode"]?.GetValue<string>() ?? "", jsonObject["VerificationUri"]?.GetValue<string>() ?? "https://thehuntersdream.com/link", Math.Max(1, jsonObject["Interval"]?.GetValue<int>() ?? 5), jsonObject["ExpiresIn"]?.GetValue<int>() ?? 600);
		if (link.DeviceCode.Length == 0 || link.UserCode.Length == 0)
		{
			throw new InvalidOperationException("no code in the reply");
		}
		return link;
	}

	public static async Task<(string Name, string Token)> WaitLinkAsync(Link link, CancellationToken cancel)
	{
		int interval = link.Interval;
		DateTime until = DateTime.UtcNow.AddSeconds(link.ExpiresIn);
		while (DateTime.UtcNow < until)
		{
			await Task.Delay(TimeSpan.FromSeconds(interval), cancel);
			JsonObject jsonObject = await Call("/auth/device/poll", new JsonObject { ["DeviceCode"] = link.DeviceCode }, cancel);
			switch (jsonObject["Status"]?.GetValue<string>())
			{
			case "approved":
			{
				string text = jsonObject["Token"]?.GetValue<string>() ?? "";
				string text2 = jsonObject["OnlineId"]?.GetValue<string>() ?? "";
				if (text.Length == 0 || text2.Length == 0)
				{
					throw new InvalidOperationException("no token in the reply");
				}
				return (Name: text2, Token: text);
			}
			case "slow_down":
				interval += 5;
				break;
			case "denied":
				throw new InvalidOperationException("the link was denied on the website");
			case "expired":
				throw new InvalidOperationException("the code expired: start again");
			}
		}
		throw new InvalidOperationException("the code expired: start again");
	}

	public static async Task<(string Name, string Token, string RecoveryCode)> CreateAccountAsync(string name, CancellationToken cancel)
	{
		return Account(await Call("/auth/account/create", new JsonObject
		{
			["Name"] = name,
			["DeviceName"] = DeviceName()
		}, cancel));
	}

	public static async Task<(string Name, string Token, string RecoveryCode)> RecoverAsync(string name, string code, CancellationToken cancel)
	{
		return Account(await Call("/auth/recover", new JsonObject
		{
			["Name"] = name,
			["RecoveryCode"] = code
		}, cancel));
	}

	private static (string Name, string Token, string RecoveryCode) Account(JsonObject reply)
	{
		string text = reply["Token"]?.GetValue<string>() ?? "";
		string text2 = reply["OnlineId"]?.GetValue<string>() ?? "";
		if (text.Length == 0 || text2.Length == 0)
		{
			throw new InvalidOperationException("no token in the reply");
		}
		return (Name: text2, Token: text, RecoveryCode: reply["RecoveryCode"]?.GetValue<string>() ?? "");
	}

	public static async Task LogoutAsync(string token)
	{
		try
		{
			await Call("/auth/logout", new JsonObject { ["Token"] = token }, CancellationToken.None);
		}
		catch (Exception)
		{
		}
	}
}
