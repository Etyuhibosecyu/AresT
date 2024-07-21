global using AresGlobalMethods;
global using Corlib.NStar;
global using System;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using UnsafeFunctions;
global using G = System.Collections.Generic;
global using static AresGlobalMethods.DecodingExtents;
global using static AresTLib.Global;
global using static Corlib.NStar.Extents;
global using static System.Math;
global using static UnsafeFunctions.Global;
global using String = Corlib.NStar.String;
using System.Text.RegularExpressions;
using Mpir.NET;

namespace AresTLib;

public enum UsedMethodsT
{
	None = 0,
	CS1 = 1,
	LZ1 = 1 << 1,
	COMB1 = 1 << 2,
	FAB1 = 1 << 3,
	CS2 = 1 << 4,
	COMB2 = 1 << 5,
	//Dev2 = 1 << 6,
	CS3 = 1 << 7,
	//Dev3 = 1 << 8,
	//Dev3_2 = 1 << 9,
}

public static class Global
{
	public const byte ProgramVersion = 1;
	public static UsedMethodsT PresentMethodsT { get; set; } = UsedMethodsT.CS1 | UsedMethodsT.LZ1;
	public static Encoding Encoding1251 { get; } = Encoding.GetEncoding(1251);
	public static Dictionary<(byte, byte), byte>[] UnicodeDic { get; } = new[] { Encoding.Unicode, Encoding.UTF8 }.ToArray(l => new Chain(0x0400, 0x60).ToDictionary(x => ((byte, byte))l.GetBytes("" + (char)x).ToNList(), x => unchecked((byte)(x - 0x0400 + 0x80))));
	public static Dictionary<byte, byte[]>[] UnicodeDicRev { get; } = new[] { Encoding.Unicode, Encoding.UTF8 }.ToArray(l => new Chain(0x0400, 0x60).ToDictionary(x => unchecked((byte)(x - 0x0400 + 0x80)), x => l.GetBytes("" + (char)x)));
}

public class DecodingT
{
	protected GlobalDecoding globalDecoding = default!;
	protected ArithmeticDecoder ar = default!;
	protected int misc, lz, bwt, n;
	protected int repeatsCount = 1;

	public virtual byte[] Decode(byte[] compressedFile, byte encodingVersion)
	{
		if (compressedFile.Length <= 2)
			return [];
		if (encodingVersion == 0)
			return compressedFile;
		else if (encodingVersion < ProgramVersion)
			return encodingVersion switch
			{
				_ => throw new DecoderFallbackException(),
			};
		if (ProcessMethod(compressedFile) is byte[] bytes)
			return bytes;
		var byteList = ProcessMisc(compressedFile);
		return [.. byteList.Repeat(repeatsCount)];
	}

	protected virtual byte[]? ProcessMethod(byte[] compressedFile)
	{
		int method = compressedFile[0];
		if (method == 0)
			return compressedFile[1..];
		else if (compressedFile.Length <= 2)
			throw new DecoderFallbackException();
		SplitMethod(--method);
		if (method != 0 && compressedFile.Length <= 5)
			throw new DecoderFallbackException();
		return null;
	}

	protected virtual NList<byte> ProcessMisc(byte[] compressedFile) => misc switch
	{
		0 => ProcessMisc0(compressedFile, out _),
		-1 => ProcessNonMisc(compressedFile),
		_ => throw new DecoderFallbackException(),
	};

	protected virtual NList<byte> ProcessMisc0(byte[] compressedFile, out uint encoding)
	{
		ar = compressedFile[1..];
		globalDecoding = CreateGlobalDecoding();
		PPMWGetEncodingAndNulls(out encoding, out var maxLength, out var nulls);
		var list = PPMWFillTripleList(encoding, maxLength);
		return JoinWords(list, nulls);
	}

	protected virtual NList<byte> ProcessNonMisc(byte[] compressedFile)
	{
		Current[0] = 0;
		CurrentMaximum[0] = ProgressBarStep * (bwt != 0 ? 6 : 5);
		ar = compressedFile[1..];
		globalDecoding = CreateGlobalDecoding();
		ListHashSet<int> nulls = [];
		return JoinWords(FillHFWTripleList(nulls), nulls);
	}

	protected virtual void SplitMethod(int method)
	{
		misc = method >= 4 ? method % 4 : -1;
		lz = method % 4 % 2;
		bwt = method % 4 / 2;
	}

	public virtual void ProcessLZLength(LZData lzData, SumList lengthsSL, out int readIndex, out uint length)
	{
		readIndex = ar.ReadPart(lengthsSL);
		lengthsSL.Increase(readIndex);
		if (lzData.Length.R == 0)
			length = (uint)readIndex;
		else if (lzData.Length.R == 1)
		{
			length = (uint)readIndex;
			if (length == lzData.Length.Threshold + 1)
				length += ar.ReadEqual(lzData.Length.Max - lzData.Length.Threshold);
		}
		else
		{
			length = (uint)readIndex + lzData.Length.Threshold;
			if (length == lzData.Length.Max + 1)
				length = ar.ReadEqual(lzData.Length.Threshold);
		}
	}

	public virtual void ProcessLZDist(LZData lzData, SumList distsSL, int fullLength, out int readIndex, out uint dist, uint length, out uint maxDist)
	{
		maxDist = Min(lzData.Dist.Max, (uint)(fullLength - length - 2));
		readIndex = ar.ReadPart(distsSL);
		distsSL.Increase(readIndex);
		if (lzData.Dist.R == 0 || maxDist < lzData.Dist.Threshold)
			dist = (uint)readIndex;
		else if (lzData.Dist.R == 1)
		{
			dist = (uint)readIndex;
			if (dist == lzData.Dist.Threshold + 1)
				dist += ar.ReadEqual(maxDist - lzData.Dist.Threshold + lzData.UseSpiralLengths);
		}
		else
			dist = (uint)readIndex;
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
		(encoding, maxLength, var nullCount) = (ar.ReadEqual(3), ar.ReadCount(), ar.ReadCount());
		if (maxLength < 2 || maxLength > GetFragmentLength() || nullCount > GetFragmentLength())
			throw new DecoderFallbackException();
		nulls = [];
		for (var i = 0; i < nullCount; i++)
			nulls.Add((int)ar.ReadCount() + (nulls.Length == 0 ? 0 : nulls[^1] + 1));
	}

	protected virtual List<List<ShortIntervalList>> ProcessUTF8(List<List<ShortIntervalList>> list, bool utf8)
	{
		if (utf8)
		{
			var leftOffset = ar.ReadCount();
			list.Add([[new(leftOffset, 0xffffffff)]]);
			for (var i = 0; i < leftOffset; i++)
				list[^1].Add([new(ar.ReadEqual(ValuesInByte), ValuesInByte)]);
			var rightOffset = ar.ReadCount();
			list[^1].Add([new(rightOffset, 0xffffffff)]);
			for (var i = 0; i < rightOffset; i++)
				list[^1].Add([new(ar.ReadEqual(ValuesInByte), ValuesInByte)]);
		}
		return list;
	}

	protected virtual NList<byte> JoinWords(List<List<ShortIntervalList>> input, ListHashSet<int> nulls) => input.Wrap(tl =>
	{
		var encoding = tl[0][^1][0].Lower;
		var encoding2 = (encoding == 1) ? Encoding.Unicode : (encoding == 2) ? Encoding.UTF8 : Encoding.GetEncoding(1251);
		var a = 0;
		var joinedWords = new DecodingT().DecodeUnicode(tl[1].ToNList(x => (byte)x[0].Lower), encoding, encoding2).ToArray();
		var wordsList = tl[0].GetSlice(..^1).ToList(l => encoding2.GetString(joinedWords[a..(a += (int)l[0].Lower)]));
		var result = encoding2.GetBytes(RedStarLinq.ToString(tl[2].ConvertAndJoin(l => wordsList[(int)l[0].Lower].Wrap(x => DecodeCOMB(l[1].Lower == 1 ? [.. x, ' '] : [.. x], true))))).Wrap(bl => encoding == 2 ? bl.ToNList().Insert(0, tl[3][1..CreateVar((int)(tl[3][0][0].Lower + 1), out var rightStart)].ToArray(x => (byte)x[0].Lower)).AddRange(tl[3][(rightStart + 1)..(int)(rightStart + tl[3][rightStart][0].Lower + 1)].ToArray(x => (byte)x[0].Lower)) : bl.ToNList());
		foreach (var x in nulls)
			if (encoding == 0)
				result.Insert(x, 0);
			else
				result.Insert(x, [0, 0]);
		return result;
	});

	public virtual uint GetFragmentLength() => (uint)FragmentLength;

	protected virtual List<List<ShortIntervalList>> FillHFWTripleList(ListHashSet<int> nulls)
	{
		var bwtBlockSize = 0;
		return ProcessUTF8(CreateVar(RedStarLinq.Fill(3, i => CreateDecoding2(nulls, i, ref bwtBlockSize).Decode()), out var list), list[0][^1][0].Lower == 2);
	}

	protected virtual List<List<ShortIntervalList>> PPMWFillTripleList(uint encoding, uint maxLength)
	{
		Current[0] = 0;
		CurrentMaximum[0] = ProgressBarStep * 3;
		List<List<ShortIntervalList>> list = globalDecoding.CreatePPM(maxLength, 0).Decode();
		list[0].Add([new(encoding, 3)]);
		Current[0] += ProgressBarStep;
		list.Add(globalDecoding.CreatePPM(ValuesInByte, 1).Decode());
		Current[0] += ProgressBarStep;
		list.Add(globalDecoding.CreatePPM((uint)list[0].Length - 1, 2).Decode());
		Current[0] += ProgressBarStep;
		ProcessUTF8(list, encoding == 2);
		return list;
	}

	public virtual uint GetNullsCount() => ar.ReadCount();

	public virtual GlobalDecoding CreateGlobalDecoding() => new(ar);

	protected virtual Decoding2T CreateDecoding2(ListHashSet<int> nulls, int i, ref int bwtBlockSize) => new(this, ar, nulls, bwt, bwt == 0 || i == 2 ? lz : 0, n = i, ref bwtBlockSize);

	public virtual List<ShortIntervalList> DecodeBWT(List<ShortIntervalList> input, NList<uint> skipped, int bwtBlockSize)
	{
		var bwtBlockExtraSize = bwtBlockSize <= 0x4000 ? 2 : bwtBlockSize <= 0x400000 ? 3 : bwtBlockSize <= 0x40000000 ? 4 : 5;
		Status[0] = 0;
		StatusMaximum[0] = GetArrayLength(input.Length, bwtBlockSize + bwtBlockExtraSize);
		var bytes = input.Convert(x => x.Length > 1 ? x[0].Lower * 2 + x[1].Lower : x[0].Lower);
		NList<uint> bytes2 = [];
		for (var i = 0; i < bytes.Length;)
		{
			var zle = bytes[i] & ValuesInByte >> 1;
			bytes2.AddRange(bytes.GetSlice(i..(i += bwtBlockExtraSize)));
			bytes2.AddRange(zle != 0 ? DecodeZLE(bytes, ref i, bwtBlockSize) : bytes.GetRange(i..Min(i += bwtBlockSize, bytes.Length)));
		}
		var hs = bytes2.Filter((x, index) => index % (bwtBlockSize + bwtBlockExtraSize) >= bwtBlockExtraSize).ToHashSet().Concat(skipped).Sort().ToHashSet();
		var @base = hs.Max() + 1;
		List<ShortIntervalList> result = new(bytes2.Length);
		for (var i = 0; i < bytes2.Length; i += bwtBlockSize, Status[0]++)
		{
			if (bytes2.Length - i <= bwtBlockExtraSize)
				throw new DecoderFallbackException();
			var length = Min(bwtBlockSize, bytes2.Length - i - bwtBlockExtraSize);
			bytes2[i] &= (ValuesInByte >> 1) - 1;
			var firstPermutation = (int)bytes2.GetSlice(i, bwtBlockExtraSize).Progression(0L, (x, y) => unchecked((x << BitsPerByte) + y));
			i += bwtBlockExtraSize;
			result.AddRange(DecodeBWT2(bytes2.GetRange(i, length), hs, @base, firstPermutation));
		}
		return result;
	}

	protected virtual List<ShortIntervalList> DecodeBWT2(NList<uint> input, ListHashSet<uint> hs, uint @base, int firstPermutation)
	{
		var mtfMemory = hs.ToArray();
		for (var i = 0; i < input.Length; i++)
		{
			var index = hs.IndexOf(input[i]);
			input[i] = mtfMemory[index];
			Array.Copy(mtfMemory, 0, mtfMemory, 1, index);
			mtfMemory[0] = input[i];
		}
		var sorted = input.ToArray((elem, index) => (elem, index)).NSort(x => x.elem);
		var convert = sorted.ToArray(x => x.index);
		var result = RedStarLinq.EmptyList<ShortIntervalList>(input.Length);
		var it = firstPermutation;
		for (var i = 0; i < input.Length; i++)
		{
			it = convert[it];
			result[i] = @base > ValuesInByte ? [new(input[it] / 2, (@base + 1) / 2), new(input[it] % 2, 2)] : [new(input[it], ValuesInByte)];
		}
		return result;
	}

	public virtual NList<uint> DecodeZLE(Slice<uint> byteList, ref int i, int bwtBlockSize)
	{
		if (i >= byteList.Length)
			throw new DecoderFallbackException();
		uint zero = byteList[i++], zeroB = byteList[i++];
		NList<uint> result = [];
		String zeroCode = ['1'];
		int length;
		for (; i < byteList.Length && result.Length < bwtBlockSize;)
		{
			while (i < byteList.Length && result.Length < bwtBlockSize && byteList[i] != zero && byteList[i] != zeroB)
				result.Add(byteList[i++]);
			if (i >= byteList.Length || result.Length >= bwtBlockSize)
				break;
			zeroCode.Remove(1);
			length = 0;
			while (i < byteList.Length && result.Length + length < bwtBlockSize && (byteList[i] == zero || byteList[i] == zeroB))
			{
				zeroCode.Add(byteList[i++] == zero ? '0' : '1');
				length = (int)(new MpzT(zeroCode.ToString(), 2) - 1);
			}
			result.AddRange(RedStarLinq.NFill(zero, length));
		}
		return result;
	}

	public virtual List<char> DecodeCOMB(List<char> word, bool comb = true)
	{
		if (!comb || word.Length <= 2)
			return word;
		var result = new List<char>();
		var pos = 0;
		for (var i = 0; i < 3; i++)
		{
			if (pos < word.Length && word[pos] is ',' or '.')
			{
				result.Add(word[pos++]);
				if (pos < word.Length && word[pos] == ' ')
					result.Add(word[pos++]);
			}
			else
				for (; pos < word.Length && word[pos] is not (',' or '.'); pos++)
					result.Add(word[pos]);
			if (pos < word.Length && word[pos] is ',')
			{
				result.Add(' ');
				pos++;
			}
			else if (pos < word.Length - 1 && word[pos] == '.' && word[pos + 1] is ',' or '.')
				pos++;
		}
		return result;
	}

	public virtual NList<byte> DecodeUnicode(NList<byte> input, uint encoding, Encoding encoding2)
	{
		if (input.Length == 0)
			return [];
		if (encoding == 0)
			return input;
		NList<byte> result = new(input.Length << 1);
		if (encoding == 1)
		{
			for (var i = 0; i < input.Length; i++)
			{
				if (input[i] is >= 1 and < ValuesInByte >> 1)
					result.AddRange([input[i], 0]);
				else if (UnicodeDicRev[0].TryGetValue(input[i], out var value))
					result.AddRange(value);
				else if (input[i] == 0 && i < input.Length - 2)
					result.AddRange([input[++i], input[++i]]);
				else
					throw new DecoderFallbackException();
			}
			return result;
		}
		if (encoding != 2)
			return input;
		for (var i = 0; i < input.Length; i++)
		{
			if (input[i] is >= 1 and < ValuesInByte >> 1)
				result.Add(input[i]);
			else if (UnicodeDicRev[1].TryGetValue(input[i], out var value))
				result.AddRange(value);
			else if (input[i] == 0 && i < input.Length - 2)
			{
				var charLength = input[++i] switch
				{
					<= 0b10111111 => throw new DecoderFallbackException(),
					<= 0b11011111 => 0,
					<= 0b11101111 => 1,
					_ => 2,
				};
				result.AddRange([input[i], input[++i]]);
				for (; charLength > 0; charLength--)
					result.Add(input[++i]);
			}
			else
				throw new DecoderFallbackException();
		}
		return result;
	}
}
