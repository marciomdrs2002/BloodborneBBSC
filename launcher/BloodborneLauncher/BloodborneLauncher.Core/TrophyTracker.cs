using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BloodborneLauncher.Core;

public sealed class TrophyTracker
{
	public const string LogName = "trophies.log";

	public const int PlatinumId = 0;

	public static readonly Trophy[] All = new Trophy[40]
	{
		new Trophy(0, "Bloodborne", "All trophies acquired. Hats off!", Grade.Platinum, Dlc: false),
		new Trophy(1, "Yharnam Sunrise", "You lived through the hunt, and saw another day.", Grade.Gold, Dlc: false),
		new Trophy(2, "Honoring Wishes", "Captivated by the moon presence, you pledge to watch over the hunter's dream.", Grade.Gold, Dlc: false),
		new Trophy(3, "Childhood's Beginning", "You became an infant Great One, lifting humanity into its next childhood.", Grade.Gold, Dlc: false),
		new Trophy(4, "Yharnam, Pthumerian Queen", "Defeat Yharnam, Blood Queen of the Old Labyrinth.", Grade.Gold, Dlc: false),
		new Trophy(5, "Hunter's Essence", "Acquire all hunter weapons.", Grade.Gold, Dlc: false),
		new Trophy(6, "Hunter's Craft", "Acquire all special hunter tools.", Grade.Gold, Dlc: false),
		new Trophy(7, "Weapon Master", "Acquire a weapon of the highest level.", Grade.Silver, Dlc: false),
		new Trophy(8, "Blood Gem Master", "Acquire an extremely precious blood gem.", Grade.Silver, Dlc: false),
		new Trophy(9, "Rune Master", "Acquire an extremely precious Caryll Rune.", Grade.Silver, Dlc: false),
		new Trophy(10, "Cainhurst", "Gain entry to Cainhurst, the lost and ruined castle.", Grade.Silver, Dlc: false),
		new Trophy(11, "The Choir", "Gain entry to the realm of the Choir, the high stratum of the Healing Church.", Grade.Silver, Dlc: false),
		new Trophy(12, "The Source of the Dream", "Discover the abandoned old workshop, the source of the hunter's dream.", Grade.Silver, Dlc: false),
		new Trophy(13, "Nightmare Lecture Building", "Gain entry into the Byrgenwerth lecture building, that drifts within the realm of nightmare.", Grade.Silver, Dlc: false),
		new Trophy(14, "Father Gascoigne", "Defeat the beast that once was Father Gascoigne.", Grade.Bronze, Dlc: false),
		new Trophy(15, "Vicar Amelia", "Defeat the beast that once was Vicar Amelia.", Grade.Bronze, Dlc: false),
		new Trophy(16, "Shadow of Yharnam", "Defeat the Shadow of Yharnam.", Grade.Bronze, Dlc: false),
		new Trophy(17, "Rom, the Vacuous Spider", "Defeat Great One: Rom, the Vacuous Spider.", Grade.Bronze, Dlc: false),
		new Trophy(18, "The One Reborn", "Defeat the One Reborn.", Grade.Bronze, Dlc: false),
		new Trophy(19, "Micolash, Host of the Nightmare", "Defeat Micolash, Host of the Nightmare.", Grade.Bronze, Dlc: false),
		new Trophy(20, "Mergo's Wet Nurse", "Defeat Great One: Mergo's Wet Nurse.", Grade.Bronze, Dlc: false),
		new Trophy(21, "Cleric Beast", "Defeat Cleric Beast.", Grade.Bronze, Dlc: false),
		new Trophy(22, "Blood-starved Beast", "Defeat Blood-starved Beast.", Grade.Bronze, Dlc: false),
		new Trophy(23, "The Witch of Hemwick", "Defeat the Witch of Hemwick.", Grade.Bronze, Dlc: false),
		new Trophy(24, "Darkbeast Paarl", "Defeat Darkbeast Paarl.", Grade.Bronze, Dlc: false),
		new Trophy(25, "Amygdala", "Defeat Great One: Amygdala.", Grade.Bronze, Dlc: false),
		new Trophy(26, "Martyr Logarius", "Defeat Martyr Logarius.", Grade.Bronze, Dlc: false),
		new Trophy(27, "Celestial Emissary", "Defeat Great One: Celestial Emissary.", Grade.Bronze, Dlc: false),
		new Trophy(28, "Ebrietas, Daughter of the Cosmos", "Defeat Great One: Ebrietas, Daughter of the Cosmos.", Grade.Bronze, Dlc: false),
		new Trophy(29, "Blood Gem Contact", "Acquire a blood gem that imbues hunter weapons with special strength.", Grade.Bronze, Dlc: false),
		new Trophy(30, "Rune Contact", "Acquire a Caryll Rune that endows hunters with special strength.", Grade.Bronze, Dlc: false),
		new Trophy(31, "Chalice of Pthumeru", "Acquire the Chalice of Pthumeru that seals the catacombs that form a web deep below Yharnam.", Grade.Bronze, Dlc: false),
		new Trophy(32, "Chalice of Ailing Loran", "Acquire the Chalice of Ailing Loran that seals the tragic land lost to the sands.", Grade.Bronze, Dlc: false),
		new Trophy(33, "Chalice of Isz", "Acquire the Great Chalice of Isz that seals the home of the cosmic kin.", Grade.Bronze, Dlc: false),
		new Trophy(34, "Old Hunter's Essence", "Acquire all old hunter weapons.", Grade.Gold, Dlc: true),
		new Trophy(35, "Orphan of Kos", "Defeat Great One: Orphan of Kos.", Grade.Silver, Dlc: true),
		new Trophy(36, "Ludwig, the Holy Blade", "Defeat the beast that was once Ludwig, the Holy Blade.", Grade.Bronze, Dlc: true),
		new Trophy(37, "Lady Maria of the Astral Clocktower", "Defeat Lady Maria of the Astral Clocktower.", Grade.Bronze, Dlc: true),
		new Trophy(38, "Living Failures", "Defeat the failed attempts to become Great Ones.", Grade.Bronze, Dlc: true),
		new Trophy(39, "Laurence, the First Vicar", "Defeat the beast that was once Laurence, the First Vicar.", Grade.Bronze, Dlc: true)
	};

	public static readonly Dictionary<int, Trophy> ById = All.ToDictionary((Trophy t) => t.Id);

	private static readonly int[] PlatinumNeeds = (from t in All
		where !t.Dlc && t.Id != 0
		select t.Id).ToArray();

	private static readonly Regex UnlockLine = new Regex("Runtime: trophy (\\d+) unlocked", RegexOptions.Compiled);

	private static readonly Regex SaveLine = new Regex("Runtime: trophy (\\d+) earned in this save", RegexOptions.Compiled);

	private string _userDir = "";

	private (long, DateTime)? _logStamp;

	private (long, DateTime)? _runStamp;

	private long _runOffset;

	private byte[] _runTail = Array.Empty<byte>();

	public Dictionary<int, long> Unlocked { get; } = new Dictionary<int, long>();

	public string LogPath => Path.Combine(_userDir, "trophies.log");

	private string RunLogPath => Path.Combine(_userDir, "last_run.log");

	public int Count => Unlocked.Keys.Count(ById.ContainsKey);

	public int TotalPoints => All.Sum((Trophy t) => t.Points);

	public int EarnedPoints => All.Where((Trophy t) => Unlocked.ContainsKey(t.Id)).Sum((Trophy t) => t.Points);

	public double Completion => (double)EarnedPoints / (double)TotalPoints;

	public void SetUserDir(string userDir)
	{
		if (!string.Equals(_userDir, userDir, StringComparison.OrdinalIgnoreCase))
		{
			_userDir = userDir;
			Unlocked.Clear();
			_logStamp = (_runStamp = null);
			_runOffset = 0L;
			_runTail = Array.Empty<byte>();
			Poll(readRunLog: true);
		}
	}

	public string? Reset()
	{
		string text = null;
		if (File.Exists(LogPath))
		{
			text = Path.Combine(_userDir, $"trophies.{DateTime.Now:yyyyMMdd-HHmmss}.log.bak");
			File.Move(LogPath, text);
		}
		Unlocked.Clear();
		_logStamp = Stamp(LogPath);
		try
		{
			byte[] array = (File.Exists(RunLogPath) ? File.ReadAllBytes(RunLogPath) : Array.Empty<byte>());
			_runOffset = array.Length;
			_runTail = array[Math.Max(0, array.Length - 64)..];
			_runStamp = Stamp(RunLogPath);
		}
		catch
		{
		}
		return text;
	}

	public (int Earned, int Total) Progress(bool dlc)
	{
		return (Earned: All.Count((Trophy t) => t.Dlc == dlc && Unlocked.ContainsKey(t.Id)), Total: All.Count((Trophy t) => t.Dlc == dlc));
	}

	public List<int> Poll(bool readRunLog)
	{
		List<int> list = RefreshLog();
		if (readRunLog)
		{
			list.AddRange(ReadRunLog());
		}
		list.AddRange(AwardPlatinum());
		return list;
	}

	public List<int> Observe(string line)
	{
		List<int> list = (from m in UnlockLine.Matches(line)
			select int.Parse(m.Groups[1].Value)).ToList();
		if (list.Count != 0)
		{
			return Record(list, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
		}
		return new List<int>();
	}

	public List<int> ObserveSave(string line)
	{
		List<int> list = (from m in SaveLine.Matches(line)
			select int.Parse(m.Groups[1].Value)).ToList();
		if (list.Count != 0)
		{
			return Record(list, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
		}
		return new List<int>();
	}

	private static (long, DateTime)? Stamp(string path)
	{
		try
		{
			FileInfo fileInfo = new FileInfo(path);
			return fileInfo.Exists ? new(long, DateTime)?((fileInfo.Length, fileInfo.LastWriteTimeUtc)) : (((long, DateTime)?)null);
		}
		catch
		{
			return null;
		}
	}

	private List<int> RefreshLog()
	{
		List<int> list = new List<int>();
		(long, DateTime)? tuple = Stamp(LogPath);
		(long, DateTime)? tuple2 = tuple;
		(long, DateTime)? logStamp = _logStamp;
		bool hasValue = tuple2.HasValue;
		if (hasValue == logStamp.HasValue)
		{
			if (hasValue)
			{
				(long, DateTime) valueOrDefault = tuple2.GetValueOrDefault();
				(long, DateTime) valueOrDefault2 = logStamp.GetValueOrDefault();
				if (valueOrDefault.Item1 != valueOrDefault2.Item1 || !(valueOrDefault.Item2 == valueOrDefault2.Item2))
				{
					goto IL_0076;
				}
			}
			return list;
		}
		goto IL_0076;
		IL_0076:
		_logStamp = tuple;
		Dictionary<int, long> dictionary = new Dictionary<int, long>();
		try
		{
			using FileStream stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
			while (true)
			{
				string text = streamReader.ReadLine();
				if (text != null)
				{
					string[] array = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					if (array.Length >= 2 && int.TryParse(array[0], out var result) && long.TryParse(array[1], out var result2) && (!dictionary.TryGetValue(result, out var value) || result2 < value))
					{
						dictionary[result] = result2;
					}
					continue;
				}
				break;
			}
		}
		catch
		{
			return list;
		}
		foreach (var (num3, num4) in dictionary.OrderBy((KeyValuePair<int, long> p) => p.Value))
		{
			if (!Unlocked.ContainsKey(num3))
			{
				list.Add(num3);
			}
			if (!Unlocked.TryGetValue(num3, out var value2) || num4 < value2)
			{
				Unlocked[num3] = num4;
			}
		}
		return list;
	}

	private List<int> ReadRunLog()
	{
		(long, DateTime)? tuple = Stamp(RunLogPath);
		if (tuple.HasValue)
		{
			(long, DateTime)? tuple2 = tuple;
			(long, DateTime)? runStamp = _runStamp;
			bool hasValue = tuple2.HasValue;
			if (hasValue != runStamp.HasValue)
			{
				goto IL_0079;
			}
			if (hasValue)
			{
				(long, DateTime) valueOrDefault = tuple2.GetValueOrDefault();
				(long, DateTime) valueOrDefault2 = runStamp.GetValueOrDefault();
				if (valueOrDefault.Item1 != valueOrDefault2.Item1 || !(valueOrDefault.Item2 == valueOrDefault2.Item2))
				{
					goto IL_0079;
				}
			}
		}
		return new List<int>();
		IL_0079:
		_runStamp = tuple;
		try
		{
			using FileStream fileStream = new FileStream(RunLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			if (_runOffset > 0 && _runOffset <= fileStream.Length)
			{
				fileStream.Seek(_runOffset - _runTail.Length, SeekOrigin.Begin);
				byte[] array = new byte[_runTail.Length];
				fileStream.ReadExactly(array);
				if (!array.AsSpan().SequenceEqual(_runTail))
				{
					_runOffset = 0L;
				}
			}
			else if (_runOffset > fileStream.Length)
			{
				_runOffset = 0L;
			}
			fileStream.Seek(_runOffset, SeekOrigin.Begin);
			using MemoryStream memoryStream = new MemoryStream();
			fileStream.CopyTo(memoryStream);
			byte[] array2 = memoryStream.ToArray();
			int num = Array.LastIndexOf(array2, (byte)10) + 1;
			if (num == 0)
			{
				return new List<int>();
			}
			_runOffset += num;
			_runTail = array2[Math.Max(0, num - 64)..num];
			string input = Encoding.UTF8.GetString(array2, 0, num);
			long when = new DateTimeOffset(tuple.Value.Item2).ToUnixTimeSeconds();
			List<int> list = new List<int>();
			List<int> list2 = (from m in SaveLine.Matches(input)
				select int.Parse(m.Groups[1].Value)).ToList();
			if (list2.Count > 0)
			{
				list.AddRange(from id in Record(list2, when)
					where id == 0
					select id);
			}
			List<int> list3 = (from m in UnlockLine.Matches(input)
				select int.Parse(m.Groups[1].Value)).ToList();
			if (list3.Count > 0)
			{
				list.AddRange(Record(list3, when));
			}
			return list;
		}
		catch
		{
			return new List<int>();
		}
	}

	private List<int> Record(IEnumerable<int> ids, long when)
	{
		List<int> list = RefreshLog();
		foreach (int id in ids)
		{
			if (!Unlocked.ContainsKey(id))
			{
				Unlocked[id] = when;
				Append(id, when);
				list.Add(id);
			}
		}
		list.AddRange(AwardPlatinum());
		return list;
	}

	private List<int> AwardPlatinum()
	{
		if (Unlocked.ContainsKey(0) || !PlatinumNeeds.All(Unlocked.ContainsKey))
		{
			return new List<int>();
		}
		long num = PlatinumNeeds.Max((int id) => Unlocked[id]);
		Unlocked[0] = num;
		Append(0, num);
		return new List<int> { 0 };
	}

	private void Append(int id, long when)
	{
		try
		{
			Directory.CreateDirectory(_userDir);
			File.AppendAllText(LogPath, $"{id} {when}\n");
			_logStamp = Stamp(LogPath);
		}
		catch
		{
		}
	}

	public static bool RuntimeReportsTrophies(string probe)
	{
		try
		{
			byte[] bytes = Encoding.ASCII.GetBytes("trophy %d unlocked");
			return File.ReadAllBytes(probe).AsSpan().IndexOf(bytes) >= 0;
		}
		catch
		{
			return false;
		}
	}
}
