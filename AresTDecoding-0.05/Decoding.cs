global using AresGlobalMethods005;
global using Corlib.NStar;
global using System;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using UnsafeFunctions;
global using G = System.Collections.Generic;
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
	protected ArithmeticDecoder ar = default!;
	protected int misc, hf, rle, lz, bwt, n;
	protected bool hfw;

	public virtual byte[] Decode(byte[] compressedFile, byte encodingVersion)
	{
		if (compressedFile.Length <= 2)
			return [];
		if (encodingVersion == 0)
			return compressedFile;
		else if (encodingVersion < ProgramVersion)
		{
			return encodingVersion switch
			{
				_ => throw new DecoderFallbackException(),
			};
		}
		if (ProcessMethod(compressedFile) is byte[] bytes)
			return bytes;
		var byteList = ProcessMisc(compressedFile);
		Current[0] += ProgressBarStep;
		if (rle == 14)
			byteList = new RLEDec(byteList).DecodeRLE3();
		Current[0] += ProgressBarStep;
		if (rle == 7)
			byteList = new RLEDec(byteList).Decode();
		return [.. byteList];
	}

	protected virtual byte[]? ProcessMethod(byte[] compressedFile)
	{
		int method = compressedFile[0];
		if (method == 0)
			return compressedFile[1..];
		else if (compressedFile.Length <= 2)
			throw new DecoderFallbackException();
		SplitMethod(method);
		if (method != 0 && compressedFile.Length <= 5)
			throw new DecoderFallbackException();
		return null;
	}

	protected virtual void SplitMethod(int method)
	{
		misc = method >= 64 ? method % 64 % 7 : -1;
		hf = method % 64 % 7;
		rle = method % 64 % 21 / 7 * 7;
		lz = method % 64 % 42 / 21 * 21;
		bwt = method % 64 / 42 * 42;
		hfw = hf is 2 or 3 or 5 or 6;
	}

	protected virtual NList<byte> ProcessMisc(byte[] compressedFile) => misc switch
	{
		2 => ProcessMisc2(compressedFile, out _),
		1 => ProcessMisc1(compressedFile),
		_ => ProcessNonMisc(compressedFile)
	};

	protected virtual NList<byte> ProcessMisc2(byte[] compressedFile, out uint encoding)
	{
		ar = compressedFile[1..];
		PPMWGetEncodingAndNulls(out encoding, out var maxLength, out var nulls);
		var list = PPMWFillTripleList(encoding, maxLength);
		return JoinWords(list, nulls);
	}

	protected virtual NList<byte> ProcessMisc1(byte[] compressedFile)
	{
		ar = compressedFile[1..];
		return CreatePPM(ValuesInByte).Decode().PNConvert(x => (byte)x[0].Lower);
	}

	protected virtual NList<byte> ProcessNonMisc(byte[] compressedFile)
	{
		Current[0] = 0;
		CurrentMaximum[0] = ProgressBarStep * (bwt != 0 ? (hfw ? 8 : 4) : (hfw ? 7 : 3));
		ar = compressedFile[1..];
		ListHashSet<int> nulls = [];
		return hfw ? JoinWords(FillHFWTripleList(nulls), nulls) : CreateDecoding2(nulls, 0).Decode().PNConvert(x => (byte)x[0].Lower);
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
				result.Insert(x, [0, 0]);
		return result;
	});

	public virtual uint GetFragmentLength() => FragmentLength;

	public virtual List<ShortIntervalList> ReadCompressedList(HuffmanData huffmanData, int bwt, LZData lzData, int lz, int counter, bool spaceCodes)
	{
		Status[0] = 0;
		StatusMaximum[0] = counter;
		List<ShortIntervalList> result = [];
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
			ProcessLZLength(lzData, out var length);
			if (length > result.Length - 2)
				throw new DecoderFallbackException();
			ProcessLZDist(lzData, result.Length, out var dist, length, out var maxDist);
			if (ProcessLZSpiralLength(lzData, ref dist, out var spiralLength, maxDist))
				dist = 0;
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

	protected virtual List<List<ShortIntervalList>> FillHFWTripleList(ListHashSet<int> nulls) => RedStarLinq.Fill(3, i => CreateDecoding2(nulls, i).Decode());

	public virtual void ProcessLZLength(LZData lzData, out uint length)
	{
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
	}

	public virtual void ProcessLZDist(LZData lzData, int fullLength, out uint dist, uint length, out uint maxDist)
	{
		maxDist = Min(lzData.Dist.Max, (uint)(fullLength - length - 2));
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
	}

	public virtual bool ProcessLZSpiralLength(LZData lzData, ref uint dist, out uint spiralLength, uint maxDist)
	{
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
			return true;
		}
		spiralLength = 0;
		return false;
	}

	protected virtual void PPMWGetEncodingAndNulls(out uint encoding, out uint maxLength, out ListHashSet<int> nulls)
	{
		(encoding, maxLength, var nullCount) = (ar.ReadEqual(3), ar.ReadCount(), ar.ReadCount((uint)BitsCount(GetFragmentLength())));
		if (maxLength < 2 || maxLength > GetFragmentLength() || nullCount > GetFragmentLength())
			throw new DecoderFallbackException();
		nulls = [];
		for (var i = 0; i < nullCount; i++)
			nulls.Add((int)ar.ReadCount((uint)BitsCount(GetFragmentLength())) + (nulls.Length == 0 ? 0 : nulls[^1] + 1));
	}

	protected virtual List<List<ShortIntervalList>> PPMWFillTripleList(uint encoding, uint maxLength)
	{
		Current[0] = 0;
		CurrentMaximum[0] = ProgressBarStep * 5;
		List<List<ShortIntervalList>> list = CreatePPM(maxLength, 0).Decode();
		list[0].Add([new(encoding, 3)]);
		Current[0] += ProgressBarStep;
		list.Add(CreatePPM(ValuesInByte, 1).Decode());
		Current[0] += ProgressBarStep;
		list.Add(CreatePPM((uint)list[0].Length - 1, 2).Decode());
		Current[0] += ProgressBarStep;
		return list;
	}

	protected virtual Decoding2 CreateDecoding2(ListHashSet<int> nulls, int i) => new(this, ar, nulls, hf, bwt, lz, n = i, hfw);

	protected virtual PPMDec CreatePPM(uint @base, int n = -1) => new(this, ar, @base, n);

	public virtual List<ShortIntervalList> DecodeBWT(List<ShortIntervalList> input, NList<byte> skipped)
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
			result[i] = [new(indexCodes[it], input[i][0].Base)];
			input[i].GetSlice(1).ForEach(x => result[i].Add(x));
		}
		return result;
	}
}
