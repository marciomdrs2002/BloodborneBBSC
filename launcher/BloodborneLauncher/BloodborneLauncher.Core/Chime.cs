using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;

namespace BloodborneLauncher.Core;

public static class Chime
{
	private const int Rate = 44100;

	public static void Play(int volumePercent, string customFile = "")
	{
		try
		{
			if (customFile.Length > 0 && File.Exists(customFile))
			{
				new SoundPlayer(customFile).Play();
			}
			else
			{
				new SoundPlayer(new MemoryStream(Wave((double)Math.Clamp(volumePercent, 0, 100) / 100.0))).Play();
			}
		}
		catch
		{
		}
	}

	private static byte[] Wave(double volume)
	{
		double num = 1.6;
		int num2 = (int)(44100.0 * num);
		double[] array = new double[num2];
		Bell(array, 0.0, 659.25, 0.55);
		Bell(array, 0.14, 987.77, 0.65);
		double num3 = ((IEnumerable<double>)array).Max((Func<double, double>)Math.Abs);
		using MemoryStream memoryStream = new MemoryStream();
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write("RIFF"u8);
		binaryWriter.Write(36 + num2 * 2);
		binaryWriter.Write("WAVEfmt "u8);
		binaryWriter.Write(16);
		binaryWriter.Write((short)1);
		binaryWriter.Write((short)1);
		binaryWriter.Write(44100);
		binaryWriter.Write(88200);
		binaryWriter.Write((short)2);
		binaryWriter.Write((short)16);
		binaryWriter.Write("data"u8);
		binaryWriter.Write(num2 * 2);
		double num4 = ((num3 > 0.0) ? (0.8 * volume / num3) : 0.0);
		double[] array2 = array;
		foreach (double num5 in array2)
		{
			binaryWriter.Write((short)Math.Clamp(num5 * num4 * 32767.0, -32768.0, 32767.0));
		}
		binaryWriter.Flush();
		return memoryStream.ToArray();
	}

	private static void Bell(double[] samples, double start, double frequency, double amplitude)
	{
		(double, double, double)[] array = new(double, double, double)[4]
		{
			(1.0, 1.0, 2.6),
			(2.0, 0.35, 4.0),
			(2.76, 0.18, 6.0),
			(5.4, 0.06, 9.0)
		};
		int num = (int)(start * 44100.0);
		for (int i = num; i < samples.Length; i++)
		{
			double num2 = (double)(i - num) / 44100.0;
			double num3 = Math.Min(1.0, num2 / 0.006);
			double num4 = 0.0;
			(double, double, double)[] array2 = array;
			for (int j = 0; j < array2.Length; j++)
			{
				(double, double, double) tuple = array2[j];
				double item = tuple.Item1;
				double item2 = tuple.Item2;
				double item3 = tuple.Item3;
				num4 += item2 * Math.Exp((0.0 - item3) * num2) * Math.Sin(Math.PI * 2.0 * frequency * item * num2);
			}
			samples[i] += amplitude * num3 * num4;
		}
	}
}
