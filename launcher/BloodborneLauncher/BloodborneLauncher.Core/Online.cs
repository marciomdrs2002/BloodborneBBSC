using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BloodborneLauncher.Core;

public static class Online
{
	public sealed record Result(string Name, string Address, bool Ok, string Detail);

	private const string Host = "thehuntersdream.com";

	private const int SessionPort = 18671;

	private const int StunPort = 3478;

	public static string GameServer(string text)
	{
		text = text.Trim().TrimEnd('/');
		if (text.Length == 0)
		{
			return "https://thehuntersdream.com";
		}
		if (!text.Contains("://"))
		{
			text = "https://" + text;
		}
		if (!Uri.TryCreate(text, UriKind.Absolute, out Uri result) || !(result.Host == "srv.shadps4.net"))
		{
			return text;
		}
		return "https://thehuntersdream.com";
	}

	public static async Task PrepareTrustAsync()
	{
		try
		{
			using HttpClient http = new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(4.0)
			};
			using (await http.GetAsync($"https://{"thehuntersdream.com"}:{18671}/"))
			{
			}
		}
		catch (Exception)
		{
		}
	}

	public static async Task<List<Result>> CheckAsync(string gameServer)
	{
		List<Result> list = new List<Result>();
		List<Result> list2 = list;
		list2.Add(await CheckGameServerAsync(gameServer));
		List<Result> results = list;
		string session = $"{"thehuntersdream.com"}:{18671}";
		try
		{
			using TcpClient tcp = new TcpClient();
			using CancellationTokenSource cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5.0));
			await tcp.ConnectAsync("thehuntersdream.com", 18671, cancel.Token);
			results.Add(new Result("Co-op sessions", session, Ok: true, ""));
		}
		catch (Exception ex)
		{
			results.Add(new Result("Co-op sessions", session, Ok: false, ex.GetBaseException().Message));
		}
		list = results;
		list.Add(await CheckStunAsync());
		return results;
	}

	private static async Task<Result> CheckStunAsync()
	{
		string address = $"{"thehuntersdream.com"}:{3478} (UDP)";
		try
		{
			IPAddress address2 = (await Dns.GetHostAddressesAsync("thehuntersdream.com")).First((IPAddress a) => a.AddressFamily == AddressFamily.InterNetwork);
			using UdpClient udp = new UdpClient(0);
			byte[] request = new byte[20]
			{
				0, 1, 0, 0, 33, 18, 164, 66, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0
			};
			Random.Shared.NextBytes(request.AsSpan(8, 12));
			IPEndPoint server = new IPEndPoint(address2, 3478);
			int attempt = 0;
			while (attempt < 3)
			{
				await udp.SendAsync(request, request.Length, server);
				using (CancellationTokenSource cancel = new CancellationTokenSource(TimeSpan.FromSeconds(2.0)))
				{
					_ = 2;
					try
					{
						byte[] buffer = (await udp.ReceiveAsync(cancel.Token)).Buffer;
						if (buffer.Length >= 20 && buffer[0] == 1 && buffer[1] == 1 && buffer.AsSpan(8, 12).SequenceEqual(request.AsSpan(8, 12)))
						{
							for (int num = 20; num + 12 <= buffer.Length; num += 4 + (((buffer[num + 2] << 8) | buffer[num + 3]) + 3) / 4 * 4)
							{
								int num2 = (buffer[num] << 8) | buffer[num + 1];
								bool flag = ((num2 == 1 || num2 == 32 || num2 == 32800) ? true : false);
								if (flag && buffer[num + 5] == 1)
								{
									bool flag2 = num2 != 1;
									int value = ((buffer[num + 6] << 8) | buffer[num + 7]) ^ (flag2 ? 8466 : 0);
									byte[] array = new byte[4];
									for (int num3 = 0; num3 < 4; num3++)
									{
										array[num3] = (byte)(buffer[num + 8 + num3] ^ (flag2 ? request[4 + num3] : 0));
									}
									return new Result("Co-op relay", address, Ok: true, $"the server sees you at {new IPAddress(array)}:{value}");
								}
							}
							return new Result("Co-op relay", address, Ok: true, "");
						}
					}
					catch (OperationCanceledException)
					{
						goto end_IL_0207;
					}
					end_IL_0207:;
				}
				int num4 = attempt + 1;
				attempt = num4;
			}
			return new Result("Co-op relay", address, Ok: false, "no answer (a firewall or VPN may block UDP)");
		}
		catch (Exception ex2)
		{
			return new Result("Co-op relay", address, Ok: false, ex2.GetBaseException().Message);
		}
	}

	private static async Task<Result> CheckGameServerAsync(string gameServer)
	{
		try
		{
			UriBuilder uriBuilder = new UriBuilder(gameServer);
			if (new Uri(gameServer).IsDefaultPort)
			{
				uriBuilder.Port = 20443;
			}
			uriBuilder.Path = "/bb-eu/ss.info";
			using HttpClient http = new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(7.0)
			};
			string text = await http.GetStringAsync(uriBuilder.Uri);
			bool flag = Encoding.UTF8.GetString(Convert.FromBase64String(text.Trim())).Contains("<api_", StringComparison.Ordinal);
			return new Result("Game server", gameServer, flag, flag ? "" : "not a Bloodborne game server");
		}
		catch (FormatException)
		{
			return new Result("Game server", gameServer, Ok: false, "not a Bloodborne game server");
		}
		catch (Exception ex2)
		{
			return new Result("Game server", gameServer, Ok: false, ex2.GetBaseException().Message);
		}
	}
}
