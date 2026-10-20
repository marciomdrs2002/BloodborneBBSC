using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace BloodborneLauncher.Core
{
    /// <summary>
    /// The BBSC local server: co-op among friends without a public server. The host runs both
    /// servers on its Tailscale address: shadNet (coop-server\shadnet.exe) and BBSC's game server
    /// (game-server\bbsc_gameserver.py, CLS_AMG, GPL-2.0-or-later, with its own embeddable Python),
    /// which answers the game's ss.info and sends the summons to shadNet. Guests run nothing: their
    /// game goes to the host's. Messages, bloodstains and ghosts stay among the group.
    /// </summary>
    public static class Bbsc
    {
        /// <summary>The launcher's "bbsc_online_server" value for this server (bbport.ini gets online_pool=shadnet).</summary>
        public const string ServerKey = "bbsc";
        public const int ShadnetPort = 31313;
        public const int WebApiPort = 31315;
        public const int GameServerPort = 20443;
        private const string InvitePrefix = "BBSC-";

        public static string CoopServerDir => Path.Combine(Paths.PortDir, "coop-server");
        public static string ShadnetExe => Path.Combine(CoopServerDir, "shadnet.exe");
        public static string AccountTool => Path.Combine(CoopServerDir, "shadnet-sample.exe");
        public static string GameServerDir => Path.Combine(Paths.PortDir, "game-server");
        public static string GameServerScript => Path.Combine(GameServerDir, "bbsc_gameserver.py");
        /// <summary>The Python that comes with the game server (python.org's embeddable package): nothing to install.</summary>
        public static string PythonExe => Path.Combine(GameServerDir, "python", "python.exe");
        public static string OverridesFile => Path.Combine(Paths.ConfigDir, "host_overrides.bbsc.json");

        public static bool Selected(LauncherSettings s) => s.Str("bbsc_online_server") == ServerKey;
        public static bool Hosting(LauncherSettings s) => s.Str("bbsc_role") != "join";

        /// <summary>The shadNet server's address: this PC's Tailscale address when hosting, else the invite's.</summary>
        public static string HostAddress(LauncherSettings s) =>
            Hosting(s) ? TailscaleAddress() ?? "" : s.Str("bbsc_host").Trim();

        private static readonly List<Process> Started = new();

        // ---------------------------------------------------------------- Tailscale and invites

        private static string? _tailscaleAddress;

        /// <summary>This PC's Tailscale IPv4 address (100.x.y.z), or null without Tailscale.</summary>
        public static string? TailscaleAddress(bool refresh = false)
        {
            if (_tailscaleAddress != null && !refresh) return _tailscaleAddress;
            var exe = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale.exe"),
                "tailscale.exe",
            }.FirstOrDefault(p => !Path.IsPathRooted(p) || File.Exists(p));
            if (exe == null) return null;
            var (code, output) = Run(exe, new[] { "ip", "-4" }, 5000);
            var address = output.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("100."));
            _tailscaleAddress = code == 0 ? address : null;
            return _tailscaleAddress;
        }

        public static string MakeInvite(string address) =>
            InvitePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { a = address })))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        /// <summary>The host address in an invite code (this launcher's BBSC-..., BBport's BBCOOP-...)
        /// or a plain address; null when it is neither.</summary>
        public static string? ParseInvite(string text)
        {
            text = text.Trim();
            if (System.Net.IPAddress.TryParse(text, out _)) return text;
            var at = text.IndexOf('-');
            if (at <= 0 || !(text.StartsWith(InvitePrefix) || text.StartsWith("BBCOOP-"))) return null;
            try
            {
                var b64 = text[(at + 1)..].Replace('-', '+').Replace('_', '/');
                b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
                using var json = JsonDocument.Parse(Convert.FromBase64String(b64));
                return json.RootElement.TryGetProperty("a", out var a) && a.GetString() is { Length: > 0 } address ? address : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---------------------------------------------------------------- what the game gets

        /// <summary>bbport.ini for a BBSC session (the engine's shadNet pool); saved before the game starts.</summary>
        public static void ApplyIni(LauncherSettings s, IniFile ini)
        {
            var host = HostAddress(s);
            ini.Set("online_pool", "shadnet");
            ini.Set("online_host", host);
            ini.Set("online_port", ShadnetPort.ToString());
            ini.Set("online_webapi", $"http://{host}:{WebApiPort}");
            ini.Set("online_game_api", $"http://{host}:{GameServerPort}");
            WriteOverrides(host);
        }

        /// <summary>The game's environment for a BBSC session.</summary>
        public static void ApplyEnvironment(LauncherSettings s, IniFile ini, Dictionary<string, string> env)
        {
            var host = HostAddress(s);
            var name = ini.Get("online_username").Trim();
            env["BB_ONLINE"] = "1";
            env["BB_SHADNET_SERVER"] = $"{host}:{ShadnetPort}";
            env["BB_SHADNET_WEBAPI"] = $"http://{host}:{WebApiPort}";
            env["BB_SHADNET_NPID"] = name;
            env["BB_USER_NAME"] = name;
            if (s.OnlinePassword is { Length: > 0 } password) env["BB_SHADNET_PASSWORD"] = password; // kept encrypted, never in bbport.ini
            env["SHADPS4_HTTP_HOST_OVERRIDES_JSON"] = OverridesFile;
            if (ModEnabled(s)) ApplyModEnvironment(host, env);
        }

        /// <summary>The game's servers (ss4, play logs) all go to the host's game server.</summary>
        private static void WriteOverrides(string host)
        {
            var local = $"http://{host}:{GameServerPort}";
            var map = new Dictionary<string, string>
            {
                ["https://ss4.scej-network.jp:20443"] = local,
                ["https://bb-playlog-prod.s3.amazonaws.com:443"] = local,
                ["https://bb-playlog-test.s3.amazonaws.com:443"] = local,
            };
            Directory.CreateDirectory(Paths.ConfigDir);
            File.WriteAllText(OverridesFile, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
        }

        // ---------------------------------------------------------------- servers

        /// <summary>Before the game: the host starts its two servers (shadNet and BBSC's game server,
        /// both on its Tailscale address); a guest only checks it can reach them. Returns null, or
        /// what is wrong.</summary>
        public static async Task<string?> PrepareAsync(LauncherSettings s, Action<string> log)
        {
            var host = HostAddress(s);
            if (host.Length == 0)
                return Hosting(s)
                    ? "Tailscale is not running on this PC. Sign in to Tailscale, then try again."
                    : "Paste the host's invite code on the Online page.";

            if (!Hosting(s))
                return Listening(host, ShadnetPort) && Listening(host, GameServerPort)
                    ? null
                    : $"The host ({host}) is not hosting right now. Ask them to start hosting in their launcher, and check you are both on Tailscale.";

            if (!File.Exists(ShadnetExe))
                return $"The co-op server is missing: {ShadnetExe}";
            if (!Listening(host, ShadnetPort))
            {
                SetShadnetHost(host);
                StartHidden(ShadnetExe, Array.Empty<string>(), CoopServerDir);
                log($"BBSC: co-op server started on {host}:{ShadnetPort}");
                if (!await WaitListeningAsync(host, ShadnetPort))
                    return "The co-op server did not start. Is another program using port 31313?";
            }
            if (!Listening(host, GameServerPort))
            {
                if (!File.Exists(GameServerScript) || !File.Exists(PythonExe))
                    return $"The game server is missing from {GameServerDir} (bbsc_gameserver.py and python\\python.exe).";
                // Bound to the Tailscale address only, without --advertise: every caller, this PC's game too,
                // then gets the ss.info with that address (--advertise gives this PC 127.0.0.1, which is not bound).
                StartHidden(PythonExe, new[] { GameServerScript, "--shadnet", host, "--host", host, "--no-bbhost" }, GameServerDir);
                log($"BBSC: game server started on {host}:{GameServerPort}");
                if (!await WaitListeningAsync(host, GameServerPort))
                    return "The game server did not start (game-server\\logs has its log).";
            }
            return null;
        }

        // ---------------------------------------------------------------- BBSC's mod (seamless co-op)

        /// <summary>BBSC's mod (CLS_AMG, GPL-2.0-or-later): one session through deaths, lamps, bosses
        /// and the Hunter's Dream, its own bells. It finds the game in bb-probe's memory by itself and
        /// leaves it alone when the bytes it patches are not the ones it expects.</summary>
        public static string ModDll => Path.Combine(Paths.PortDir, "bbsc", "bbsc.dll");

        public static bool ModEnabled(LauncherSettings s) => s.Str("bbsc_mod") != "0" && File.Exists(ModDll);

        /// <summary>The mod's switches, as bbsc-launch.exe sets them (the mod does the work, not the
        /// engine), and its relay: the host's game server.</summary>
        private static void ApplyModEnvironment(string host, Dictionary<string, string> env)
        {
            env["SHADPS4_BBSC_PERSIST"] = "0";
            env["SHADPS4_BLOODBORNE_SEAMLESS_COOP"] = "0";
            env["SHADPS4_BLOODBORNE_RE_TRACE"] = "0";
            env["BBSC_RELAY"] = $"{host}:{RelayPort}";
        }

        public const int RelayPort = 20444;

        /// <summary>Waits for the bb-probe.exe that bb-start.exe starts and loads the mod into it with
        /// a remote LoadLibraryW, as bbsc-launch.exe does (it starts the emulator suspended; here the
        /// engine is caught within milliseconds, while it still loads the game image).</summary>
        public static async Task InjectModAsync(DateTime after, Action<string, bool> log)
        {
            var probe = Path.Combine(Paths.BinariesDir, "bb-probe.exe");
            var deadline = DateTime.Now.AddMinutes(3);
            while (DateTime.Now < deadline)
            {
                foreach (var process in Process.GetProcessesByName("bb-probe"))
                {
                    try
                    {
                        if (process.StartTime < after.AddSeconds(-1) ||
                            !string.Equals(process.MainModule?.FileName, probe, StringComparison.OrdinalIgnoreCase))
                            continue;
                        var error = Inject(process.Id, ModDll);
                        log(error == null ? $"BBSC: seamless co-op mod loaded into the engine (pid {process.Id}); its log: bbsc\\logs"
                                          : $"BBSC: the mod could not be loaded: {error}", error == null);
                        return;
                    }
                    catch (Exception)
                    {
                        // Not ready to be read yet (still starting): look again.
                    }
                }
                await Task.Delay(15);
            }
            log("BBSC: the engine did not start, so the mod was not loaded", false);
        }

        private static string? Inject(int pid, string dll)
        {
            const uint access = 0x0002 | 0x0008 | 0x0010 | 0x0020 | 0x0400; // create thread, vm operation/read/write, query
            var process = Native.OpenProcess(access, false, pid);
            if (process == IntPtr.Zero) return "OpenProcess failed (" + Marshal.GetLastWin32Error() + ")";
            try
            {
                var bytes = Encoding.Unicode.GetBytes(dll + "\0");
                var remote = Native.VirtualAllocEx(process, IntPtr.Zero, (UIntPtr)bytes.Length, 0x3000, 0x04);
                if (remote == IntPtr.Zero) return "VirtualAllocEx failed (" + Marshal.GetLastWin32Error() + ")";
                try
                {
                    if (!Native.WriteProcessMemory(process, remote, bytes, (UIntPtr)bytes.Length, out _))
                        return "WriteProcessMemory failed (" + Marshal.GetLastWin32Error() + ")";
                    var loadLibrary = Native.GetProcAddress(Native.GetModuleHandle("kernel32.dll"), "LoadLibraryW");
                    var thread = Native.CreateRemoteThread(process, IntPtr.Zero, UIntPtr.Zero, loadLibrary, remote, 0, out _);
                    if (thread == IntPtr.Zero) return "CreateRemoteThread failed (" + Marshal.GetLastWin32Error() + ")";
                    Native.WaitForSingleObject(thread, 30000);
                    Native.GetExitCodeThread(thread, out var loaded);
                    Native.CloseHandle(thread);
                    return loaded == 0 ? "the engine refused the DLL (LoadLibraryW returned 0)" : null;
                }
                finally
                {
                    Native.VirtualFreeEx(process, remote, UIntPtr.Zero, 0x8000);
                }
            }
            finally
            {
                Native.CloseHandle(process);
            }
        }

        private static class Native
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, UIntPtr size, uint type, uint protect);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool VirtualFreeEx(IntPtr process, IntPtr address, UIntPtr size, uint type);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, UIntPtr size, out UIntPtr written);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr GetModuleHandle(string name);

            [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
            public static extern IntPtr GetProcAddress(IntPtr module, string name);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr CreateRemoteThread(IntPtr process, IntPtr attributes, UIntPtr stack, IntPtr start, IntPtr parameter, uint flags, out uint id);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern uint WaitForSingleObject(IntPtr handle, uint ms);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool GetExitCodeThread(IntPtr thread, out uint code);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CloseHandle(IntPtr handle);
        }

        /// <summary>Whether this PC is hosting now (its co-op server answers on the Tailscale address).</summary>
        public static bool HostingNow(LauncherSettings s) =>
            Hosting(s) && HostAddress(s) is { Length: > 0 } host && Listening(host, ShadnetPort);

        /// <summary>Stops the servers this launcher started (the host's friends lose the session).</summary>
        public static void StopAll()
        {
            lock (Started)
            {
                foreach (var process in Started)
                {
                    try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { /* gone */ }
                }
                Started.Clear();
            }
            // A co-op server from this folder that another launcher run left behind.
            foreach (var process in Process.GetProcessesByName("shadnet"))
            {
                try
                {
                    if (string.Equals(process.MainModule?.FileName, ShadnetExe, StringComparison.OrdinalIgnoreCase))
                        process.Kill(entireProcessTree: true);
                }
                catch { /* gone, or not ours to read */ }
            }
        }

        /// <summary>shadnet.cfg's Host: only the Tailscale address, never every interface (it has no encryption).</summary>
        private static void SetShadnetHost(string host)
        {
            var cfg = Path.Combine(CoopServerDir, "shadnet.cfg");
            var lines = File.Exists(cfg) ? File.ReadAllLines(cfg).ToList() : new List<string> { "[General]" };
            var at = lines.FindIndex(l => l.StartsWith("Host="));
            if (at >= 0) lines[at] = "Host=" + host;
            else lines.Insert(Math.Min(1, lines.Count), "Host=" + host);
            // BBSC's server setting (its own shadnet.cfg has it on): the seamless session's matching.
            var seamless = lines.FindIndex(l => l.StartsWith("BloodborneSeamlessCoop="));
            if (seamless >= 0) lines[seamless] = "BloodborneSeamlessCoop=true";
            else lines.Add("BloodborneSeamlessCoop=true");
            File.WriteAllLines(cfg, lines);
        }

        private static void StartHidden(string exe, IEnumerable<string> args, string dir)
        {
            var info = new ProcessStartInfo(exe) { WorkingDirectory = dir, UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in args) info.ArgumentList.Add(arg);
            var process = Process.Start(info);
            if (process != null) lock (Started) Started.Add(process);
        }

        public static bool Listening(string host, int port)
        {
            try
            {
                using var client = new TcpClient();
                return client.ConnectAsync(host, port).Wait(400) && client.Connected;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static async Task<bool> WaitListeningAsync(string host, int port)
        {
            for (var i = 0; i < 20; i++)
            {
                if (Listening(host, port)) return true;
                await Task.Delay(250);
            }
            return false;
        }

        // ---------------------------------------------------------------- accounts

        /// <summary>Creates an account on the host's shadNet server; returns null, or why not.</summary>
        public static Task<string?> RegisterAsync(string host, string name, string password) =>
            AccountAsync(host, "register", name, password, $"{name}@bbsc.local", "Account created");

        /// <summary>Signs in once outside the game; returns null, or why it failed.</summary>
        public static Task<string?> LoginAsync(string host, string name, string password) =>
            AccountAsync(host, "login", name, password, null, "[login] OK");

        private static Task<string?> AccountAsync(string host, string command, string name, string password, string? email, string success) =>
            Task.Run(() =>
            {
                if (!File.Exists(AccountTool)) return (string?)$"The account tool is missing: {AccountTool}";
                var args = new List<string> { host, ShadnetPort.ToString(), command, name, password };
                if (email != null) args.Add(email);
                var (_, output) = Run(AccountTool, args, 15000);
                if (output.Contains(success)) return null;
                var reason = output.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0) ?? "no answer";
                return $"The server said: {reason}";
            });

        private static (int Code, string Output) Run(string exe, IEnumerable<string> args, int timeoutMs)
        {
            try
            {
                var info = new ProcessStartInfo(exe)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                };
                foreach (var arg in args) info.ArgumentList.Add(arg);
                using var process = Process.Start(info)!;
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); } catch { /* gone */ }
                    return (-1, "timed out");
                }
                return (process.ExitCode, output.Result + error.Result);
            }
            catch (Exception e)
            {
                return (-1, e.Message);
            }
        }
    }
}
