global using AresGlobalMethods005;
global using Corlib.NStar;
global using System;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using UnsafeFunctions;
global using G = System.Collections.Generic;
global using static AresGlobalMethods005.Decoding;
global using static AresTLib005.DecodingExtents;
global using static Corlib.NStar.Extents;
global using static System.Math;
global using static UnsafeFunctions.Global;

namespace AresTLib005;

public class Decoding
{
	internal const byte ProgramVersion = 1;
	internal const int FragmentLength = 8000000;
	internal const int BWTBlockSize = 50000;
	protected int misc, hf, rle, lz, bwt, n;
	protected bool hfw;

	public virtual byte[] Decode(byte[] compressedFile, byte encodingVersion)
	{
		if (compressedFile.Length <= 2)
			return Array.Empty<byte>();
		if (encodingVersion == 0)
			return compressedFile;
		else if (encodingVersion < ProgramVersion)
		{
			return encodingVersion switch
			{
				_ => throw new DecoderFallbackException(),
			};
		}
		int method = compressedFile[0];
		if (method == 0)
			return compressedFile[1..];
		else if (compressedFile.Length <= 2)
			throw new DecoderFallbackException();
		ProcessMethod(method);
		if (method != 0 && compressedFile.Length <= 5)
			throw new DecoderFallbackException();
		NList<byte> byteList;
		if (misc == 2)
		{
			ProcessMisc(compressedFile, out var ar, out _, out var nulls, out var list);
			byteList = JoinWords(list, nulls);
		}
		else if (misc == 1)
			byteList = new PPM(this, compressedFile[1..], ValuesInByte).Decode().PNConvert(x => (byte)x[0].Lower);
		else
		{
			Current[0] = 0;
			CurrentMaximum[0] = ProgressBarStep * (bwt != 0 ? (hfw ? 8 : 4) : (hfw ? 7 : 3));
			using ArithmeticDecoder ar = compressedFile[1..];
			ListHashSet<int> nulls = new();
			byteList = hfw ? JoinWords(RedStarLinq.Fill(3, i => new Decoding2(this, ar, nulls, hf, bwt, lz, n = i, hfw).Decode()), nulls) : new Decoding2(this, ar, nulls, hf, bwt, lz, n = 0, hfw).Decode().PNConvert(x => (byte)x[0].Lower);
		}
		Current[0] += ProgressBarStep;
		if (rle == 14)
			byteList = DecodeRLE3(byteList);
		Current[0] += ProgressBarStep;
		if (rle == 7)
			byteList = DecodeRLE(byteList);
		return byteList.ToArray();
	}

	protected virtual void ProcessMisc(byte[] compressedFile, out ArithmeticDecoder ar, out uint encoding, out ListHashSet<int> nulls, out List<List<ShortIntervalList>> list)
	{
		ar = compressedFile[1..];
		(encoding, var maxLength, var nullCount) = (ar.ReadEqual(3), ar.ReadCount(), ar.ReadCount((uint)BitsCount(GetFragmentLength())));
		if (maxLength < 2 || maxLength > GetFragmentLength() || nullCount > GetFragmentLength())
			throw new DecoderFallbackException();
		nulls = new();
		for (var i = 0; i < nullCount; i++)
			nulls.Add((int)ar.ReadCount((uint)BitsCount(GetFragmentLength())) + (nulls.Length == 0 ? 0 : nulls[^1] + 1));
		Current[0] = 0;
		CurrentMaximum[0] = ProgressBarStep * 5;
		list = new PPM(this, ar, maxLength, 0).Decode();
		list[0].Add(new() { new(encoding, 3) });
		Current[0] += ProgressBarStep;
		list.Add(new PPM(this, ar, ValuesInByte, 1).Decode());
		Current[0] += ProgressBarStep;
		list.Add(new PPM(this, ar, (uint)list[0].Length - 1, 2).Decode());
		Current[0] += ProgressBarStep;
	}

	protected virtual void ProcessMethod(int method)
	{
		misc = method >= 64 ? method % 64 % 7 : -1;
		hf = method % 64 % 7;
		rle = method % 64 % 21 / 7 * 7;
		lz = method % 64 % 42 / 21 * 21;
		bwt = method % 64 / 42 * 42;
		hfw = hf is 2 or 3 or 5 or 6;
	}

	protected virtual NList<byte> JoinWords(List<List<ShortIntervalList>> input, ListHashSet<int> nulls) => input.Wrap(tl =>
	{
		var encoding = tl[0][^1][0].Lower;
		var encoding2 = (encoding == 1) ? Encoding.Unicode : (encoding == 2) ? Encoding.UTF8 : Encoding.GetEncoding(1251);
		var a = 0;
		var wordsList = tl[0].GetSlice(..^1).Convert(l => encoding2.GetString(tl[1][a..(a += (int)l[0].Lower)].ToArray(x => (byte)x[0].Lower)));
		var result = encoding2.GetBytes(tl[2].ConvertAndJoin(l => wordsList[(int)l[0].Lower].Wrap(x => l[1].Lower == 1 ? new List<char>(x).Add(' ') : x)).ToArray()).ToNList();
		foreach (var x in nulls)
			if (encoding == 0)
				result.Insert(x, 0);
			else
				result.Insert(x, new byte[] { 0, 0 });
		return result;
	});

	public virtual uint GetFragmentLength() => FragmentLength;

	public virtual List<ShortIntervalList> DecodeAdaptive(ArithmeticDecoder ar, List<byte> skipped, LZData lzData, int lz, int counter)
	{
		DecodeAdaptivePrerequisites(ar, skipped, ref counter, out var fileBase, out var set);
		DecodeAdaptivePrerequisites2(lz, fileBase, set, out var uniqueList, out var result, out var fullLength, out var nextWordLink);
		for (; counter > 0; counter--, Status[0]++)
		{
			var readIndex = DecodeAdaptiveReadFirst(ar, fileBase, set, uniqueList, ref nextWordLink);
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
			ProcessAdaptiveDist(ar, lzData, result, ref fullLength, dist, length, ref spiralLength, maxDist);
		}
		return DecodeLempelZiv(result, lz != 0, 0, 0, 0, 0, lzData.UseSpiralLengths, 0, 0, 0);
	}

	protected virtual void DecodeAdaptivePrerequisites(ArithmeticDecoder ar, List<byte> skipped, ref int counter, out uint fileBase, out SumSet<uint> set)
	{
		if (bwt != 0 && !(hfw && n != 1))
		{
			var skippedCount = (int)ar.ReadCount();
			var @base = skippedCount == 0 ? 1 : ar.ReadCount();
			if (skippedCount > @base || @base > GetFragmentLength())
				throw new DecoderFallbackException();
			for (var i = 0; i < skippedCount; i++)
				skipped.Add((byte)ar.ReadEqual(@base));
			counter -= skippedCount == 0 ? 1 : (skippedCount + 11) / 8;
		}
		fileBase = ar.ReadCount();
		if (counter < 0 || counter > GetFragmentLength() + (bwt == 0 ? 0 : GetFragmentLength() >> 8))
			throw new DecoderFallbackException();
		Status[0] = 0;
		StatusMaximum[0] = counter;
		set = new() { (uint.MaxValue, 1) };
	}

	protected virtual void DecodeAdaptivePrerequisites2(int lz, uint fileBase, SumSet<uint> set, out List<Interval> uniqueList, out List<ShortIntervalList> result, out int fullLength, out uint nextWordLink)
	{
		uniqueList = new();
		if (lz != 0)
		{
			set.Add((fileBase - 1, 1));
			uniqueList.Add(new(fileBase - 1, fileBase));
		}
		result = new();
		fullLength = 0;
		nextWordLink = 0;
	}

	protected virtual int DecodeAdaptiveReadFirst(ArithmeticDecoder ar, uint fileBase, SumSet<uint> set, List<Interval> uniqueList, ref uint nextWordLink)
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
		DecodeAdaptiveFirstUpdateSet(set);
		return readIndex;
	}

	protected virtual void DecodeAdaptiveFirstUpdateSet(SumSet<uint> set) => set.Update(uint.MaxValue, (int)GetBufferInterval((uint)set.GetLeftValuesSum(uint.MaxValue, out _)));

	protected virtual void ProcessAdaptiveDist(ArithmeticDecoder ar, LZData lzData, List<ShortIntervalList> result, ref int fullLength, uint dist, uint length, ref uint spiralLength, uint maxDist)
	{
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

	public virtual List<ShortIntervalList> ReadCompressedList(ArithmeticDecoder ar, HuffmanData huffmanData, int bwt, LZData lzData, int lz, int counter, bool spaceCodes)
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

	public virtual List<ShortIntervalList> DecodeBWT(List<ShortIntervalList> input, List<byte> skipped)
	{
		Status[0] = 0;
		StatusMaximum[0] = GetArrayLength(input.Length, BWTBlockSize + 2);
		var hs = input.Convert(x => (byte)x[0].Lower).FilterInPlace((x, index) => index % (BWTBlockSize + 2) is not (0 or 1)).ToHashSet().Concat(skipped).Sort().ToHashSet();
		List<ShortIntervalList> result = new(input.Length);
		for (var i = 0; i < input.Length; i += BWTBlockSize + 2, Status[0]++)
		{
			if (input.Length - i < 3)
				throw new DecoderFallbackException();
			var length = Min(BWTBlockSize, input.Length - i - 2);
			var firstPermutation = (int)(input[i][0].Lower * input[i + 1][0].Base + input[i + 1][0].Lower);
			result.AddRange(DecodeBWT2(input.GetSlice(i + 2, length), hs, firstPermutation));
		}
		return result;
	}

	protected virtual List<ShortIntervalList> DecodeBWT2(Slice<ShortIntervalList> input, ListHashSet<byte> hs, int firstPermutation)
	{
		var indexCodes = input.Convert(x => (byte)x[0].Lower);
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
		var result = RedStarLinq.EmptyList<ShortIntervalList>(indexCodes.Length);
		var it = firstPermutation;
		for (var i = 0; i < indexCodes.Length; i++)
		{
			it = convert[it];
			result[i] = new() { new((uint)indexCodes[it], input[i][0].Base) };
			input[i].GetSlice(1).ForEach(x => result[i].Add(x));
		}
		return result;
	}
}

public static class DecodingExtents
{
	public static uint ReadCount(this ArithmeticDecoder ar, uint maxT = 31)
	{
		var temp = (int)ar.ReadEqual(maxT);
		var read = ar.ReadEqual((uint)1 << Max(temp, 1));
		return read + ((temp == 0) ? 0 : (uint)1 << Max(temp, 1));
	}

	public static uint GetBaseWithBuffer(uint oldBase) => oldBase + GetBufferInterval(oldBase);
	public static uint GetBufferInterval(uint oldBase) => Max((oldBase + 10) / 20, 1);
}
