﻿
namespace AresTLib;

internal record class AdaptiveHuffman(int TN)
{
	private bool lz;
	private int bwtLength, startPos;
	private uint firstIntervalDist;
	private readonly SumSet<uint> set = new();
	private readonly SumList lengthsSL = new(), distsSL = new();

	public byte[] Encode(List<ShortIntervalList> input, LZData lzData)
	{
		if (input.Length < 2)
			throw new EncoderFallbackException();
		using ArithmeticEncoder ar = new();
		if (!AdaptiveHuffmanInternal(ar, input, lzData))
			throw new EncoderFallbackException();
		ar.WriteEqual(1234567890, 4294967295);
		return ar;
	}

	public byte[] Encode(List<List<ShortIntervalList>> input, LZData[] lzData)
	{
		if (input.GetSlice(..WordsListActualParts).Any(x => x.Length < 2))
			throw new EncoderFallbackException();
		using ArithmeticEncoder ar = new();
		for (var i = 0; i < WordsListActualParts; i++, _ = i < WordsListActualParts ? Subtotal[TN] += ProgressBarStep : 0)
			if (!AdaptiveHuffmanInternal(ar, input[i], lzData[i], i))
				throw new EncoderFallbackException();
		input.GetSlice(WordsListActualParts).ForEach(dl => dl.ForEach(l => l.ForEach(x => ar.WritePart(x.Lower, x.Length, x.Base))));
		ar.WriteEqual(1234567890, 4294967295);
		return ar;
	}

	private bool AdaptiveHuffmanInternal(ArithmeticEncoder ar, List<ShortIntervalList> input, LZData lzData, int n = 1)
	{
		Prerequisites(input);
		ar.WriteCount((uint)input.Length);
		for (var i = 0; i < startPos; i++)
			for (var j = 0; j < input[i].Length; j++)
			{
				var x = input[i][j];
				if (i == startPos - bwtLength && j == 2)
					ar.WriteCount(x.Base);
				ar.WritePart(x.Lower, x.Length, x.Base);
			}
		Status[TN]++;
		var newBase = input[startPos][0].Base + (lz ? 1u : 0);
		ar.WriteCount(newBase);
		Status[TN] = 0;
		StatusMaximum[TN] = input.Length - startPos;
		Current[TN] += ProgressBarStep;
		set.Clear();
		lengthsSL.Replace(lz ? RedStarLinq.Fill(1, (int)(lzData.Length.R == 0 ? lzData.Length.Max + 1 : lzData.Length.R == 1 ? lzData.Length.Threshold + 2 : lzData.Length.Max - lzData.Length.Threshold + 2)) : new());
		distsSL.Replace(lz ? RedStarLinq.Fill(1, (int)lzData.UseSpiralLengths + 1) : new());
		firstIntervalDist = lz ? lzData.Dist.R == 2 ? lzData.Dist.Max - lzData.Dist.Threshold + 2 : (lzData.Dist.R == 1 ? lzData.Dist.Threshold + 2 : lzData.Dist.Max + 1) + lzData.UseSpiralLengths : 0;
		if (lz)
			set.Add((newBase - 1, 1));
		new AdaptiveHuffmanMain(ar, input, lzData, n, startPos, lz, newBase, set, lengthsSL, distsSL, firstIntervalDist, TN).MainProcess();
		return true;
	}

	private void Prerequisites(List<ShortIntervalList> input)
	{
		var bwtIndex = input[0].IndexOf(BWTApplied);
		if (CreateVar(input[0].IndexOf(HuffmanApplied), out var huffmanIndex) != -1 && !(bwtIndex != -1 && huffmanIndex == bwtIndex + 1))
			throw new EncoderFallbackException();
		Current[TN] = 0;
		CurrentMaximum[TN] = ProgressBarStep * 2;
		Status[TN] = 0;
		StatusMaximum[TN] = 3;
		lz = CreateVar(input[0].IndexOf(LempelZivApplied), out var lzIndex) != -1 && (bwtIndex == -1 || lzIndex != bwtIndex + 1);
		var lzDummy = CreateVar(input[0].IndexOf(LempelZivDummyApplied), out var lzDummyIndex) != -1 && (bwtIndex == -1 || lzDummyIndex != bwtIndex + 1);
		bwtLength = bwtIndex != -1 ? (int)input[0][bwtIndex + 1].Base : 0;
		startPos = (lz || lzDummy ? (input[0].Length >= lzIndex + 2 && input[0][lzIndex + 1] == LempelZivSubdivided ? 3 : 2) : 1) + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base : 0) + bwtLength;
		Status[TN]++;
		var lzPos = bwtIndex != -1 ? 4 : 2;
		if (input.Length < startPos + lzPos + 1)
			throw new EncoderFallbackException();
		var originalBase = input[startPos + lzPos][0].Base;
		if (!input.GetSlice(startPos + lzPos + 1).All((x, index) => bwtIndex != -1 && (index + lzPos + 1) % (BWTBlockSize + 2) is 0 or 1 || x[0].Base == originalBase))
			throw new EncoderFallbackException();
		Status[TN]++;
	}
}

file sealed record class AdaptiveHuffmanMain(ArithmeticEncoder Ar, List<ShortIntervalList> Input, LZData LZData, int N, int StartPos, bool LZ, uint NewBase, SumSet<uint> Set, SumList LengthsSL, SumList DistsSL, uint FirstIntervalDist, int TN)
{
	private int lzLength, lzDist, lzSpiralLength, frequency;
	private uint bufferInterval;
	private long sum;
	private uint item;
	private bool lessThanThreshold = true;

	public void MainProcess()
	{
		for (var i = StartPos; i < Input.Length; i++, Status[TN]++)
		{
			item = Input[i][0].Lower;
			sum = Set.GetLeftValuesSum(item, out frequency);
			bufferInterval = Max((uint)Set.Length, 1);
			var fullBase = (uint)(Set.ValuesSum + bufferInterval);
			if (frequency == 0)
			{
				Ar.WritePart((uint)Set.ValuesSum, bufferInterval, fullBase);
				if (N != 2)
					Ar.WriteEqual(item, NewBase);
			}
			else
				Ar.WritePart((uint)sum, (uint)frequency, fullBase);
			Set.Increase(item);
			lzLength = lzDist = lzSpiralLength = 0;
			EncodeNextIntervals(i);
		}
	}

	private void EncodeNextIntervals(int i)
	{
		var j = 1;
		if (LZ && item == NewBase - 1)
		{
			item = Input[i][j].Lower;
			lzLength = (int)(item + (LZData.Length.R == 2 ? LZData.Length.Threshold : 0));
			sum = LengthsSL.GetLeftValuesSum((int)item, out frequency);
			Ar.WritePart((uint)sum, (uint)frequency, (uint)LengthsSL.ValuesSum);
			LengthsSL.Increase((int)item);
			j++;
			if (LZData.Length.R != 0 && item == LengthsSL.Length - 1)
			{
				Ar.WritePart(Input[i][j].Lower, Input[i][j].Length, Input[i][j].Base);
				lzLength = (int)(LZData.Length.R == 2 ? Input[i][j].Lower : Input[i][j].Lower + LZData.Length.Threshold + 1);
				j++;
			}
			EncodeDist(i, ref j);
			lzSpiralLength = LZData.UseSpiralLengths != 0 && Input[i][j - 1].Lower == Input[i][j - 1].Base - 1 ? LZData.SpiralLength.R == 0 ? (int)Input[i][^1].Lower : (int)(Input[i][^1].Lower + (LZData.SpiralLength.R == 1 ? LZData.SpiralLength.Threshold + 2 - LZData.SpiralLength.R : 0)) : 0;
			if (LZ && DistsSL.Length < FirstIntervalDist)
				new Chain(Min((int)FirstIntervalDist - DistsSL.Length, (lzLength + 2) * (lzSpiralLength + 1))).ForEach(x => DistsSL.Insert(DistsSL.Length - ((int)LZData.UseSpiralLengths + 1), 1));
		}
		else if (LZ && DistsSL.Length < FirstIntervalDist)
			DistsSL.Insert(DistsSL.Length - ((int)LZData.UseSpiralLengths + 1), 1);
		if (LZData.Dist.R == 2 && lessThanThreshold && DistsSL.Length - LZData.UseSpiralLengths - 1 >= LZData.Dist.Threshold)
		{
			DistsSL.Remove(0, (int)(LZData.Dist.Threshold + 1 - LZData.UseSpiralLengths));
			lessThanThreshold = false;
		}
		for (; j < Input[i].Length; j++)
			Ar.WritePart(Input[i][j].Lower, Input[i][j].Length, Input[i][j].Base);
	}

	private void EncodeDist(int i, ref int j)
	{
		item = Input[i][j].Lower;
		lzDist = (int)(item + (LZData.Dist.R == 2 && DistsSL.Length - LZData.UseSpiralLengths - lzLength - StartPos >= LZData.Dist.Threshold ? LZData.Dist.Threshold : 0));
		sum = DistsSL.GetLeftValuesSum((int)item, out frequency);
		Ar.WritePart((uint)sum, (uint)frequency, (uint)DistsSL.ValuesSum);
		DistsSL.Increase((int)item);
		j++;
		if (LZData.Dist.R != 0 && j < Input[i].Length && Input[i][j - 1].Lower == Input[i][j - 1].Base - 1)
		{
			Ar.WritePart(Input[i][j].Lower, Input[i][j].Length, Input[i][j].Base);
			j++;
		}
	}
}

public record class AdaptiveHuffmanBits(int TN)
{
	public bool Encode(ArithmeticEncoder ar, List<ShortIntervalList> input, int startPos)
	{
		if (!(input.Length >= startPos + 2 && input.GetSlice(startPos).All(x => x.Length > 0 && x[0].Base == 2)))
			throw new EncoderFallbackException();
		Status[TN] = 0;
		StatusMaximum[TN] = 3;
		Current[TN] += ProgressBarStep;
		Status[TN]++;
		ar.WriteCount((uint)input.Length);
		for (var i = 0; i < startPos; i++)
			for (var j = 0; j < input[i].Length; j++)
			{
				var x = input[i][j];
				ar.WritePart(x.Lower, x.Length, x.Base);
			}
		Status[TN]++;
		var newBase = input[startPos][0].Base;
		ar.WriteCount(newBase);
		Status[TN] = 0;
		StatusMaximum[TN] = input.Length - startPos;
		Current[TN] += ProgressBarStep;
		var windowSize = 1 << 13;
		uint zeroFreq = 1, totalFreq = 2;
		for (var i = startPos; i < input.Length; i++, Status[TN]++)
		{
			var item = input[i][0].Lower == 1;
			var sum = item ? zeroFreq : 0;
			ar.WritePart(sum, item ? totalFreq - zeroFreq : zeroFreq, totalFreq);
			if (i < windowSize + startPos)
			{
				if (!item)
					zeroFreq++;
				totalFreq++;
			}
			if (i >= windowSize + startPos && input[i - windowSize][0].Lower == (item ? 0u : 1))
			{
				if (item)
					zeroFreq--;
				else
					zeroFreq++;
			}
			for (var j = 1; j < input[i].Length; j++)
				ar.WritePart(input[i][j].Lower, input[i][j].Length, input[i][j].Base);
		}
		return true;
	}
}
