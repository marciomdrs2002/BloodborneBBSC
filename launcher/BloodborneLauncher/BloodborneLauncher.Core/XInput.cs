using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BloodborneLauncher.Core;

public static class XInput
{
	private struct State
	{
		public uint PacketNumber;

		public ushort Buttons;

		public byte LeftTrigger;

		public byte RightTrigger;

		public short ThumbLX;

		public short ThumbLY;

		public short ThumbRX;

		public short ThumbRY;
	}

	[DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
	private static extern uint GetState(uint user, out State state);

	public static List<int> Connected()
	{
		List<int> list = new List<int>();
		try
		{
			for (uint num = 0u; num < 4; num++)
			{
				if (GetState(num, out var _) == 0)
				{
					list.Add((int)(num + 1));
				}
			}
		}
		catch
		{
		}
		return list;
	}
}
