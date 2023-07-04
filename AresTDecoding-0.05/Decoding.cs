global using AresGlobalMethods;
global using Corlib.NStar;
global using System;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using UnsafeFunctions;
global using G = System.Collections.Generic;
global using static AresGlobalMethods.Decoding;
global using static Corlib.NStar.Extents;
global using static System.Math;
global using static UnsafeFunctions.Global;

namespace AresTLib;

public static class Decoding
{
	public static byte[] Decode(byte[] compressedFile, byte encodingVersion)
	{
		if (compressedFile.Length <= 2)
			return Array.Empty<byte>();
		if (encodingVersion == 0)
			return compressedFile;
		else if (encodingVersion < ProgramVersion)
		{
			//return Outdated.Decode(encodingVersion, compressedFile);
		}
		int method = compressedFile[0];
		if (method == 0)
			return compressedFile[1..];
		else if (compressedFile.Length <= 2)
			throw new DecoderFallbackException();
		int misc = method >= 64 ? method % 64 % 7 : -1, hf = method % 64 % 7, rle = method % 64 % 21 / 7 * 7, lz = method % 64 % 42 / 21 * 21, bwt = method % 64 / 42 * 42;
		var hfw = hf is 2 or 3 or 5 or 6;
		if (method != 0 && compressedFile.Length <= 5)
			throw new DecoderFallbackException();
		NList<byte> byteList;
		if (misc == 2)
		{
			using ArithmeticDecoder arPPM = compressedFile[1..];
			var (encoding, maxLength, nullCount) = (arPPM.ReadEqual(3), arPPM.ReadCount(), arPPM.ReadCount((uint)BitsCount(FragmentLength)));
			if (maxLength is < 2 or > FragmentLength || nullCount > FragmentLength)
				throw new DecoderFallbackException();
			ListHashSet<int> nulls = new();
			for (var i = 0; i < nullCount; i++)
				nulls.Add((int)arPPM.ReadCount((uint)BitsCount(FragmentLength)) + (nulls.Length == 0 ? 0 : nulls[^1] + 1));
			Current[0] = 0;
			CurrentMaximum[0] = ProgressBarStep * 5;
			List<List<ShortIntervalList>> list = DecodePPM(arPPM, maxLength);
			list[0].Add(new() { new(encoding, 3) });
			Current[0] += ProgressBarStep;
			list.Add(DecodePPM(arPPM, ValuesInByte));
			Current[0] += ProgressBarStep;
			list.Add(DecodePPM(arPPM, (uint)list[0].Length - 1, true));
			Current[0] += ProgressBarStep;
			byteList = list.JoinWords(nulls);
		}
		else if (misc == 1)
			byteList = DecodePPM(compressedFile[1..], ValuesInByte).PNConvert(x => (byte)x[0].Lower);
		else
		{
			Current[0] = 0;
			CurrentMaximum[0] = ProgressBarStep * (bwt != 0 ? (hfw ? 8 : 4) : (hfw ? 7 : 3));
			using ArithmeticDecoder ar = compressedFile[1..];
			ListHashSet<int> nulls = new();
			byteList = hfw ? RedStarLinq.Fill(3, i => Decode2(hf, hfw, bwt, lz, ar, nulls, i)).JoinWords(nulls) : Decode2(hf, hfw, bwt, lz, ar).PNConvert(x => (byte)x[0].Lower);
		}
		Current[0] += ProgressBarStep;
		if (rle == 14)
			byteList = DecodeRLE3(byteList);
		Current[0] += ProgressBarStep;
		if (rle == 7)
			byteList = DecodeRLE(byteList);
		return byteList.ToArray();
	}

	private static NList<byte> JoinWords(this List<List<ShortIntervalList>> input, ListHashSet<int> nulls) => input.Wrap(tl =>
	{
		var encoding = tl[0][^1][0].Lower;
		Encoding encoding2 = (encoding == 1) ? Encoding.Unicode : (encoding == 2) ? Encoding.UTF8 : Encoding.GetEncoding(1251);
		var a = 0;
		var wordsList = tl[0].AsSpan(..^1).Convert(l => encoding2.GetString(tl[1][a..(a += (int)l[0].Lower)].ToArray(x => (byte)x[0].Lower)));
		var result = encoding2.GetBytes(tl[2].ConvertAndJoin(l => wordsList[(int)l[0].Lower].Wrap(x => l[1].Lower == 1 ? new List<char>(x).Add(' ') : x)).ToArray()).ToNList();
		foreach (var x in nulls)
			if (encoding == 0)
				result.Insert(x, 0);
			else
				result.Insert(x, new byte[] { 0, 0 });
		return result;
	});

	private static List<ShortIntervalList> Decode2(int hf, bool hfw, int bwt, int lz, ArithmeticDecoder ar, ListHashSet<int> nulls = default!, int n = 0)
	{
		var counter = (int)ar.ReadCount() - (hfw && n == 0 ? 2 : 1);
		uint lzRDist, lzMaxDist, lzThresholdDist = 0, lzRLength, lzMaxLength, lzThresholdLength = 0, lzUseSpiralLengths = 0, lzRSpiralLength, lzMaxSpiralLength, lzThresholdSpiralLength = 0;
		MethodDataUnit lzDist = new(), lzLength = new(), lzSpiralLength = new();
		int maxFrequency = 0, frequencyCount = 0;
		List<uint> arithmeticMap = new();
		List<Interval> uniqueList = new();
		List<int> skipped = new();
		var (encoding, maxLength, nullsCount) = hfw && n == 0 ? (ar.ReadEqual(3), ar.ReadCount(), ar.ReadCount((uint)BitsCount(FragmentLength))) : (0, 0, 0);
		if (hfw && n == 0 && nulls != null)
		{
			var counter2 = 1;
			if (maxLength is < 2 or > FragmentLength || nullsCount > FragmentLength)
				throw new DecoderFallbackException();
			for (var i = 0; i < nullsCount; i++)
			{
				var value = ar.ReadCount((uint)BitsCount(FragmentLength));
				if (value > FragmentLength)
					throw new DecoderFallbackException();
				nulls.Add((int)value + (nulls.Length == 0 ? 0 : nulls[^1] + 1));
				counter2++;
			}
			counter -= GetArrayLength(counter2, 4);
		}
		if (lz != 0)
		{
			var counter2 = 7;
			lzRDist = ar.ReadEqual(3);
			lzMaxDist = ar.ReadCount(16);
			if (lzRDist != 0)
			{
				lzThresholdDist = ar.ReadEqual(lzMaxDist + 1);
				counter2++;
			}
			lzDist = (lzRDist, lzMaxDist, lzThresholdDist);
			lzRLength = ar.ReadEqual(3);
			lzMaxLength = ar.ReadCount(16);
			if (lzRLength != 0)
			{
				lzThresholdLength = ar.ReadEqual(lzMaxLength + 1);
				counter2++;
			}
			lzLength = (lzRLength, lzMaxLength, lzThresholdLength);
			if (lzMaxDist == 0 && lzMaxLength == 0 && ar.ReadEqual(2) == 0)
			{
				lz = 0;
				goto l0;
			}
			lzUseSpiralLengths = ar.ReadEqual(2);
			if (lzUseSpiralLengths == 1)
			{
				lzRSpiralLength = ar.ReadEqual(3);
				lzMaxSpiralLength = ar.ReadCount(16);
				counter2 += 3;
				if (lzRSpiralLength != 0)
				{
					lzThresholdSpiralLength = ar.ReadEqual(lzMaxSpiralLength + 1);
					counter2++;
				}
				lzSpiralLength = (lzRSpiralLength, lzMaxSpiralLength, lzThresholdSpiralLength);
			}
			l0:
			counter -= GetArrayLength(counter2, 8);
		}
		LZData lzData = (lzDist, lzLength, lzUseSpiralLengths, lzSpiralLength);
		List<ShortIntervalList> compressedList;
		if (hf >= 4)
		{
			compressedList = ar.DecodeAdaptive(hfw, bwt, skipped, lzData, lz, counter, n);
			goto l1;
		}
		if (hf != 0)
		{
			var counter2 = 4;
			maxFrequency = (int)ar.ReadCount() + 1;
			frequencyCount = (int)ar.ReadCount() + 1;
			if (maxFrequency > FragmentLength || frequencyCount > FragmentLength)
				throw new DecoderFallbackException();
			Status[0] = 0;
			StatusMaximum[0] = frequencyCount;
			var @base = hfw && n == 0 ? maxLength + 1 : hfw && n == 2 ? (uint)frequencyCount : 256;
			if (maxFrequency > frequencyCount * 2 || frequencyCount <= 256)
			{
				arithmeticMap.Add((uint)maxFrequency);
				var prev = (uint)maxFrequency;
				for (var i = 0; i < frequencyCount; i++, Status[0]++)
				{
					counter2++;
					uniqueList.Add(new(ar.ReadEqual(@base), @base));
					if (i == 0) continue;
					prev = ar.ReadEqual(prev) + 1;
					counter2++;
					arithmeticMap.Add(arithmeticMap[^1] + prev);
				}
			}
			else
				for (var i = 0; i < frequencyCount; i++, Status[0]++)
				{
					uniqueList.Add(new((uint)i, hfw && n == 0 ? maxLength + 1 : (uint)frequencyCount));
					counter2++;
					arithmeticMap.Add((arithmeticMap.Length == 0 ? 0 : arithmeticMap[^1]) + ar.ReadEqual((uint)maxFrequency) + 1);
				}
			if (lz != 0)
				arithmeticMap.Add(GetBaseWithBuffer(arithmeticMap[^1]));
			counter -= GetArrayLength(counter2, 8);
			if (bwt != 0 && !(hfw && n != 1))
			{
				var skippedCount = (int)ar.ReadCount();
				for (var i = 0; i < skippedCount; i++)
					skipped.Add((int)ar.ReadEqual(@base));
				counter -= (skippedCount + 9) / 8;
			}
		}
		else
		{
			uniqueList.AddRange(RedStarLinq.Fill(256, index => new Interval((uint)index, 256)));
			arithmeticMap.AddRange(RedStarLinq.Fill(256, index => (uint)(index + 1)));
			if (lz != 0)
				arithmeticMap.Add(269);
		}
		if (counter is < 0 or > FragmentLength)
			throw new DecoderFallbackException();
		HuffmanData huffmanData = (maxFrequency, frequencyCount, arithmeticMap, uniqueList);
		Current[0] += ProgressBarStep;
		compressedList = ar.ReadCompressedList(huffmanData, bwt, lzData, lz, counter, n == 2);
	l1:
		if (hfw && n == 0)
			compressedList.Add(new() { new(encoding, 3) });
		if (bwt != 0 && !(hfw && n != 1))
		{
			Current[0]++;
			compressedList = compressedList.DecodeBWT(skipped);
		}
		if (hfw && n != 2)
			Current[0]++;
		return compressedList;
	}

	private static List<ShortIntervalList> DecodeAdaptive(this ArithmeticDecoder ar, bool hfw, int bwt, List<int> skipped, LZData lzData, int lz, int counter, int n)
	{
		if (bwt != 0 && !(hfw && n != 1))
		{
			var skippedCount = (int)ar.ReadCount();
			var @base = skippedCount == 0 ? 1 : ar.ReadCount();
			if (skippedCount > @base || @base > FragmentLength)
				throw new DecoderFallbackException();
			for (var i = 0; i < skippedCount; i++)
				skipped.Add((int)ar.ReadEqual(@base));
			counter -= skippedCount == 0 ? 1 : (skippedCount + 11) / 8;
		}
		var fileBase = ar.ReadCount();
		if (counter is < 0 or > FragmentLength)
			throw new DecoderFallbackException();
		Status[0] = 0;
		StatusMaximum[0] = counter;
		SumSet<uint> set = new() { (uint.MaxValue, 1) };
		List<Interval> uniqueList = new();
		if (lz != 0)
		{
			set.Add((fileBase - 1, 1));
			uniqueList.Add(new(fileBase - 1, fileBase));
		}
		List<ShortIntervalList> result = new();
		var fullLength = 0;
		uint nextWordLink = 0;
		for (; counter > 0; counter--, Status[0]++)
		{
			var readIndex = ar.ReadPart(set);
			if (readIndex == set.Length - 1)
			{
				var actualIndex = n == 2 ? nextWordLink++ : ar.ReadEqual(fileBase);
				if (!set.TryAdd((actualIndex, 1), out readIndex))
					throw new DecoderFallbackException();
				uniqueList.Insert(readIndex, new Interval(actualIndex, fileBase));
			}
			else
				set.Increase(uniqueList[readIndex].Lower);
			set.Update(uint.MaxValue, (int)GetBufferInterval((uint)set.GetLeftValuesSum(uint.MaxValue, out _)));
			if (!(lz != 0 && uniqueList[readIndex].Lower == fileBase - 1))
			{
				result.Add(n == 2 ? new() { uniqueList[readIndex], new(ar.ReadEqual(2), 2) } : new() { uniqueList[readIndex] });
				fullLength++;
				continue;
			}
			result.Add(new() { uniqueList[^1] });
			uint dist, length, spiralLength = 0;
			if (lzData.Length.R == 0)
				length = ar.ReadEqual(lzData.Length.Max + 1);
			else if (lzData.Length.R == 1)
			{
				length = ar.ReadEqual(lzData.Length.Threshold + 2);
				if (length == lzData.Length.Threshold + 1)
					length += ar.ReadEqual(lzData.Length.Max - lzData.Length.Threshold);
			}
			else
			{
				length = ar.ReadEqual(lzData.Length.Max - lzData.Length.Threshold + 2) + lzData.Length.Threshold;
				if (length == lzData.Length.Max + 1)
					length = ar.ReadEqual(lzData.Length.Threshold);
			}
			result[^1].Add(new(length, lzData.Length.Max + 1));
			if (length > result.Length - 2)
				throw new DecoderFallbackException();
			var maxDist = Min(lzData.Dist.Max, (uint)(fullLength - length - 2));
			if (lzData.Dist.R == 0 || maxDist < lzData.Dist.Threshold)
				dist = ar.ReadEqual(maxDist + lzData.UseSpiralLengths + 1);
			else if (lzData.Dist.R == 1)
			{
				dist = ar.ReadEqual(lzData.Dist.Threshold + 2);
				if (dist == lzData.Dist.Threshold + 1)
					dist += ar.ReadEqual(maxDist - lzData.Dist.Threshold + lzData.UseSpiralLengths);
			}
			else
			{
				dist = ar.ReadEqual(maxDist - lzData.Dist.Threshold + 2) + lzData.Dist.Threshold;
				if (dist == maxDist + 1)
				{
					dist = ar.ReadEqual(lzData.Dist.Threshold + lzData.UseSpiralLengths);
					if (dist == lzData.Dist.Threshold)
						dist = maxDist + 1;
				}
			}
			result[^1].Add(new(dist, maxDist + lzData.UseSpiralLengths + 1));
			if (dist == maxDist + 1)
			{
				if (lzData.SpiralLength.R == 0)
					spiralLength = ar.ReadEqual(lzData.SpiralLength.Max + 1);
				else if (lzData.SpiralLength.R == 1)
				{
					spiralLength = ar.ReadEqual(lzData.SpiralLength.Threshold + 2);
					if (spiralLength == lzData.SpiralLength.Threshold + 1)
						spiralLength += ar.ReadEqual(lzData.SpiralLength.Max - lzData.SpiralLength.Threshold);
				}
				else
				{
					spiralLength = ar.ReadEqual(lzData.SpiralLength.Max - lzData.SpiralLength.Threshold + 2) + lzData.SpiralLength.Threshold;
					if (spiralLength == lzData.SpiralLength.Max + 1)
						spiralLength = ar.ReadEqual(lzData.SpiralLength.Threshold);
				}
				result[^1].Add(new(spiralLength, lzData.SpiralLength.Max + 1));
			}
			fullLength += (int)((length + 2) * (spiralLength + 1));
		}
		return DecodeLempelZiv(result, lz != 0, 0, 0, 0, 0, lzData.UseSpiralLengths, 0, 0, 0);
	}

	private static List<ShortIntervalList> ReadCompressedList(this ArithmeticDecoder ar, HuffmanData huffmanData, int bwt, LZData lzData, int lz, int counter, bool spaceCodes)
	{
		Status[0] = 0;
		StatusMaximum[0] = counter;
		List<ShortIntervalList> result = new();
		var startingArithmeticMap = lz == 0 ? huffmanData.ArithmeticMap : huffmanData.ArithmeticMap[..^1];
		var uniqueLists = spaceCodes ? RedStarLinq.Fill(2, i => huffmanData.UniqueList.PConvert(x => new ShortIntervalList { x, new((uint)i, 2) })) : huffmanData.UniqueList.Convert(x => new ShortIntervalList() { x });
		for (; counter > 0; counter--, Status[0]++)
		{
			var readIndex = ar.ReadPart(result.Length < 2 || bwt != 0 && (result.Length < 4 || (result.Length + 0) % (BWTBlockSize + 2) is 0 or 1) ? startingArithmeticMap : huffmanData.ArithmeticMap);
			if (!(lz != 0 && readIndex == huffmanData.ArithmeticMap.Length - 1))
			{
				result.Add(uniqueLists[spaceCodes ? (int)ar.ReadEqual(2) * 1 : 0][readIndex]);
				continue;
			}
			uint dist, length, spiralLength = 0;
			if (lzData.Length.R == 0)
				length = ar.ReadEqual(lzData.Length.Max + 1);
			else if (lzData.Length.R == 1)
			{
				length = ar.ReadEqual(lzData.Length.Threshold + 2);
				if (length == lzData.Length.Threshold + 1)
					length += ar.ReadEqual(lzData.Length.Max - lzData.Length.Threshold);
			}
			else
			{
				length = ar.ReadEqual(lzData.Length.Max - lzData.Length.Threshold + 2) + lzData.Length.Threshold;
				if (length == lzData.Length.Max + 1)
					length = ar.ReadEqual(lzData.Length.Threshold);
			}
			if (length > result.Length - 2)
				throw new DecoderFallbackException();
			var maxDist = Min(lzData.Dist.Max, (uint)(result.Length - length - 2));
			if (lzData.Dist.R == 0 || maxDist < lzData.Dist.Threshold)
				dist = ar.ReadEqual(maxDist + lzData.UseSpiralLengths + 1);
			else if (lzData.Dist.R == 1)
			{
				dist = ar.ReadEqual(lzData.Dist.Threshold + 2);
				if (dist == lzData.Dist.Threshold + 1)
					dist += ar.ReadEqual(maxDist - lzData.Dist.Threshold + lzData.UseSpiralLengths);
			}
			else
			{
				dist = ar.ReadEqual(maxDist - lzData.Dist.Threshold + 2) + lzData.Dist.Threshold;
				if (dist == maxDist + 1)
				{
					dist = ar.ReadEqual(lzData.Dist.Threshold + lzData.UseSpiralLengths);
					if (dist == lzData.Dist.Threshold)
						dist = maxDist + 1;
				}
			}
			if (dist == maxDist + 1)
			{
				if (lzData.SpiralLength.R == 0)
					spiralLength = ar.ReadEqual(lzData.SpiralLength.Max + 1);
				else if (lzData.SpiralLength.R == 1)
				{
					spiralLength = ar.ReadEqual(lzData.SpiralLength.Threshold + 2);
					if (spiralLength == lzData.SpiralLength.Threshold + 1)
						spiralLength += ar.ReadEqual(lzData.SpiralLength.Max - lzData.SpiralLength.Threshold);
				}
				else
				{
					spiralLength = ar.ReadEqual(lzData.SpiralLength.Max - lzData.SpiralLength.Threshold + 2) + lzData.SpiralLength.Threshold;
					if (spiralLength == lzData.SpiralLength.Max + 1)
						spiralLength = ar.ReadEqual(lzData.SpiralLength.Threshold);
				}
				dist = 0;
			}
			var start = (int)(result.Length - dist - length - 2);
			if (start < 0)
				throw new DecoderFallbackException();
			var fullLength = (int)((length + 2) * (spiralLength + 1));
			for (var i = fullLength; i > 0; i -= (int)length + 2)
			{
				var length2 = (int)Min(length + 2, i);
				result.AddRange(result.GetRange(start, length2));
			}
		}
		return result;
	}

	private static List<ShortIntervalList> DecodePPM(this ArithmeticDecoder ar, uint inputBase, bool spaces = false)
	{
		uint counter = ar.ReadCount(), dicsize = ar.ReadCount();
		if (counter is < 0 or > FragmentLength || dicsize is < 0 or > FragmentLength)
			throw new DecoderFallbackException();
		Status[0] = 0;
		StatusMaximum[0] = (int)counter;
		List<ShortIntervalList> result = new();
		SumSet<uint> globalSet = new(), newItemsSet = spaces ? new() : new(new Chain((int)inputBase).Convert(x => ((uint)x, 1)));
		var maxDepth = 12;
		var comparer = new NListEComparer<uint>();
		FastDelHashSet<NList<uint>> contextHS = new(comparer);
		List<SumSet<uint>> sumSets = new();
		List<uint> preLZMap = new(2, 1, 2), spacesMap = new(2, 1, 2);
		uint nextWordLink = 0;
		for (; (int)counter > 0; counter--, Status[0]++)
		{
			var context = result.AsSpan(Max(result.Length - maxDepth, 0)..).NConvert(x => x[0].Lower).Reverse();
			var context2 = context.Copy();
			var index = -1;
			SumSet<uint>? set = null, excludingSet = new();
			uint item;
			if (context.Length == maxDepth && counter > maxDepth)
			{
				if (ar.ReadPart(preLZMap) == 1)
				{
					ProcessLZ(result.Length);
					continue;
				}
				else
				{
					preLZMap[0]++;
					preLZMap[1]++;
				}
			}
			for (; context.Length > 0 && !contextHS.TryGetIndexOf(context, out index); context.RemoveAt(^1)) ;
			var arithmeticIndex = -1;
			for (; context.Length > 0 && contextHS.TryGetIndexOf(context, out index) && (arithmeticIndex = (set = sumSets[index].Copy().ExceptWith(excludingSet)).Length == 0 ? 1 : ar.ReadPart(new List<uint>(2, (uint)set.ValuesSum, (uint)(set.ValuesSum + set.Length * 100)))) == 1; context.RemoveAt(^1), excludingSet.UnionWith(set)) ;
			if (set == null || context.Length == 0)
			{
				set = globalSet.Copy().ExceptWith(excludingSet);
				if (set.Length != 0 && (arithmeticIndex = ar.ReadPart(new List<uint>(2, (uint)set.ValuesSum, (uint)(set.ValuesSum + set.Length * 100)))) != 1)
				{
					if (set.Length != 0) arithmeticIndex = ar.ReadPart(set);
					item = set[arithmeticIndex].Key;
				}
				else if (spaces)
					item = nextWordLink++;
				else
				{
					item = newItemsSet[ar.ReadPart(newItemsSet)].Key;
					newItemsSet.RemoveValue(item);
				}
			}
			else
			{
				if (set.Length != 0) arithmeticIndex = ar.ReadPart(set);
				item = set[arithmeticIndex].Key;
			}
			result.Add(new() { new(item, inputBase) });
			if (spaces)
			{
				var space = (uint)ar.ReadPart(spacesMap);
				result[^1].Add(new(space, 2));
				spacesMap[0] += 1 - space;
				spacesMap[1]++;
			}
			Increase(context2, item);
			context.Dispose();
			context2.Dispose();
		}
		void ProcessLZ(int curPos)
		{
			var dist = ar.ReadEqual(Min((uint)result.Length, dicsize - 1));
			var oldPos = (int)(result.Length - dist - 2);
			if (!ar.ReadFibonacci(out var length) || length + maxDepth - 1 > counter)
				throw new DecoderFallbackException();
			for (var i = 0; i < length + maxDepth - 1; i++)
			{
				result.Add(result[oldPos + i]);
				Increase(result.AsSpan(result.Length - maxDepth - 1, maxDepth).NConvert(x => x[0].Lower).Reverse(), result[^1][0].Lower);
			}
			preLZMap[1]++;
			var decrease = length + maxDepth - 2;
			counter -= (uint)decrease;
			Status[0] += (int)decrease;
		}
		void Increase(NList<uint> context, uint item)
		{
			for (; context.Length > 0 && !contextHS.TryGetIndexOf(context, out var index); context.RemoveAt(^1))
			{
				contextHS.TryAdd(context.Copy(), out index);
				sumSets.SetOrAdd(index, new() { (item, 100) });
			}
			var successLength = context.Length;
			for (; context.Length > 0 && contextHS.TryGetIndexOf(context, out var index); context.RemoveAt(^1))
			{
				if (!sumSets[index].TryGetValue(item, out var itemValue))
				{
					sumSets[index].Add(item, 100);
					continue;
				}
				else if (context.Length == 1 || itemValue > 100)
				{
					sumSets[index].Update(item, itemValue + (int)Max(Round((double)100 / (successLength - context.Length + 1)), 1));
					continue;
				}
				var successContext = context.Copy().RemoveAt(^1);
				var successIndex = contextHS.IndexOf(successContext);
				if (!sumSets[successIndex].TryGetValue(item, out var successValue))
					successValue = 100;
				var step = (double)(sumSets[index].ValuesSum + sumSets[index].Length * 100) * successValue / (sumSets[index].ValuesSum + sumSets[successIndex].ValuesSum + sumSets[successIndex].Length * 100 - successValue);
				sumSets[index].Update(item, (int)(Max(Round(step), 1) + itemValue));
			}
			if (globalSet.TryGetValue(item, out var globalValue))
				globalSet.Update(item, globalValue + (int)Max(Round((double)100 / (successLength + 1)), 1));
			else
				globalSet.Add(item, 100);
		}
		return result;
	}

	private static List<ShortIntervalList> DecodeBWT(this List<ShortIntervalList> input, List<int> skipped)
	{
		Status[0] = 0;
		StatusMaximum[0] = GetArrayLength(input.Length, BWTBlockSize + 2);
		var hs = input.Convert(x => (int)x[0].Lower).FilterInPlace((x, index) => index % (BWTBlockSize + 2) is not (0 or 1)).ToHashSet().Concat(skipped).Sort().ToHashSet();
		List<ShortIntervalList> result = new(input.Length);
		for (var i = 0; i < input.Length; i += BWTBlockSize + 2, Status[0]++)
		{
			if (input.Length - i < 3)
				throw new DecoderFallbackException();
			var length = Min(BWTBlockSize, input.Length - i - 2);
			var firstPermutation = (int)(input[i][0].Lower * input[i + 1][0].Base + input[i + 1][0].Lower);
			result.AddRange(input.AsSpan(i + 2, length).DecodeBWT2(hs, firstPermutation));
		}
		return result;
	}

	private static List<ShortIntervalList> DecodeBWT2(this Span<ShortIntervalList> input, ListHashSet<int> hs, int firstPermutation)
	{
		var indexCodes = input.Convert(x => (int)x[0].Lower);
		var mtfMemory = hs.ToArray();
		for (var i = 0; i < indexCodes.Length; i++)
		{
			var index = hs.IndexOf(indexCodes[i]);
			indexCodes[i] = mtfMemory[index];
			Array.Copy(mtfMemory, 0, mtfMemory, 1, index);
			mtfMemory[0] = indexCodes[i];
		}
		var sorted = indexCodes.ToArray((elem, index) => (elem, index)).NSort(x => (uint)x.elem);
		var convert = sorted.ToArray(x => x.index);
		List<ShortIntervalList> result = RedStarLinq.EmptyList<ShortIntervalList>(indexCodes.Length);
		var it = firstPermutation;
		for (var i = 0; i < indexCodes.Length; i++)
		{
			it = convert[it];
			result[i] = new() { new((uint)indexCodes[it], input[i][0].Base) };
			input[i].AsSpan(1).ForEach(x => result[i].Add(x));
		}
		return result;
	}

	private static uint ReadCount(this ArithmeticDecoder ar, uint maxT = 31)
	{
		var temp = (int)ar.ReadEqual(maxT);
		var read = ar.ReadEqual((uint)1 << Max(temp, 1));
		return read + ((temp == 0) ? 0 : (uint)1 << Max(temp, 1));
	}
}
