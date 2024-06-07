global using AresGlobalMethods;
global using Corlib.NStar;
global using System;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using UnsafeFunctions;
global using G = System.Collections.Generic;
global using static AresTLib005.DecodingExtents;
global using static AresTLib.Global;
global using static Corlib.NStar.Extents;
global using static System.Math;
global using static UnsafeFunctions.Global;
global using String = Corlib.NStar.String;
global using ArithmeticDecoder = AresGlobalMethods005.ArithmeticDecoder;
global using HuffmanData = AresGlobalMethods005.HuffmanData;
global using LZData = AresGlobalMethods005.LZData;
using System.Text.RegularExpressions;

namespace AresTLib;

public enum UsedMethods
{
	None = 0,
	CS1 = 1,
	LZ1 = 1 << 1,
	HF1 = 1 << 2,
	//Dev1 = 1 << 3,
	//Dev1_2 = 1 << 4,
	CS2 = 1 << 5,
	LZ2 = 1 << 6,
	COMB2 = 1 << 7,
	FAB2 = 1 << 8,
	CS3 = 1 << 9,
	AHF3 = 1 << 10,
	//Dev3 = 1 << 11,
	CS4 = 1 << 12,
	COMB4 = 1 << 13,
	FAB4 = 1 << 14,
	CS5 = 1 << 15,
	//Dev5 = 1 << 16,
	CS6 = 1 << 17,
	//Dev6 = 1 << 18,
	CS7 = 1 << 19,
	COMB7 = 1 << 20,
	FAB7 = 1 << 21,
	CS8 = 1 << 22,
}

public static class Global
{
	public const byte ProgramVersion = 3;
	public const int WordsListActualParts = 3;
	public static int BWTBlockSize { get; set; } = 50000;
#pragma warning disable CS0652 // Сравнение с константой интеграции бесполезно: константа находится за пределами диапазона типа
	public static int BWTBlockExtraSize => BWTBlockSize <= 0x8000 ? 2 : BWTBlockSize <= 0x800000 ? 3 : BWTBlockSize <= 0x80000000 ? 4 : BWTBlockSize <= 0x8000000000 ? 5 : BWTBlockSize <= 0x800000000000 ? 6 : BWTBlockSize <= 0x80000000000000 ? 7 : 8;
#pragma warning restore CS0652 // Сравнение с константой интеграции бесполезно: константа находится за пределами диапазона типа
	public static int FragmentLength { get; set; } = 16000000;
	public static int PreservedFragmentLength { get; set; } = FragmentLength;
	public static UsedMethods PresentMethods { get; set; } = UsedMethods.CS1 | UsedMethods.HF1 | UsedMethods.LZ1 | UsedMethods.CS2 | UsedMethods.LZ2;
	public static Encoding Encoding1251 { get; } = Encoding.GetEncoding(1251);
	public static Dictionary<(byte, byte), byte>[] UnicodeDic { get; } = new[] { Encoding.Unicode, Encoding.UTF8 }.ToArray(l => new Chain(0x0400, 0x60).ToDictionary(x => ((byte, byte))l.GetBytes("" + (char)x).ToNList(), x => (byte)(x - 0x0400 + 0x80)));
	public static Dictionary<byte, byte[]>[] UnicodeDicRev { get; } = new[] { Encoding.Unicode, Encoding.UTF8 }.ToArray(l => new Chain(0x0400, 0x60).ToDictionary(x => (byte)(x - 0x0400 + 0x80), x => l.GetBytes("" + (char)x)));
}

public class Decoding : AresTLib007.Decoding
{
	protected bool fab;

	public override byte[] Decode(byte[] compressedFile, byte encodingVersion)
	{
		if (compressedFile.Length <= 2)
			return [];
		if (encodingVersion == 0)
			return compressedFile;
		else if (encodingVersion < ProgramVersion)
			return encodingVersion switch
			{
				1 => new AresTLib005.Decoding().Decode(compressedFile, encodingVersion),
				2 => new AresTLib007.Decoding().Decode(compressedFile, encodingVersion),
				_ => throw new DecoderFallbackException(),
			};
		if (ProcessMethod(compressedFile) is byte[] bytes)
			return bytes;
		var byteList = ProcessMisc(compressedFile);
		Current[0] += ProgressBarStep;
		if (rle == 14)
			byteList = new RLEDec(byteList).DecodeRLE3();
		Current[0] += ProgressBarStep;
		if (rle == 7)
			byteList = new RLEDec(byteList).Decode();
		return [.. byteList.Repeat(repeatsCount)];
	}

	protected override void SplitMethod(int method)
	{
		base.SplitMethod(method);
		fab = misc == 3 || hf is 3 or 6;
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

	protected override NList<byte> JoinWords(List<List<ShortIntervalList>> input, ListHashSet<int> nulls) => input.Wrap(tl =>
	{
		var encoding = tl[0][^1][0].Lower;
		var encoding2 = (encoding == 1) ? Encoding.Unicode : (encoding == 2) ? Encoding.UTF8 : Encoding.GetEncoding(1251);
		var a = 0;
		var joinedWords = new Decoding().DecodeUnicode(DecodeFAB(tl[1].NConvert(x => (byte)x[0].Lower), fab), encoding, encoding2).ToArray();
		var wordsList = tl[0].GetSlice(..^1).Convert(l => encoding2.GetString(joinedWords[a..(a += (int)l[0].Lower)]));
		var result = encoding2.GetBytes(RedStarLinq.ToString(tl[2].ConvertAndJoin(l => wordsList[(int)l[0].Lower].Wrap(x => DecodeCOMB(l[1].Lower == 1 ? [.. x, ' '] : [.. x], true))))).Wrap(bl => encoding == 2 ? bl.ToNList().Insert(0, tl[3][1..CreateVar((int)(tl[3][0][0].Lower + 1), out var rightStart)].ToArray(x => (byte)x[0].Lower)).AddRange(tl[3][(rightStart + 1)..(int)(rightStart + tl[3][rightStart][0].Lower + 1)].ToArray(x => (byte)x[0].Lower)) : bl.ToNList());
		foreach (var x in nulls)
			if (encoding == 0)
				result.Insert(x, 0);
			else
				result.Insert(x, [0, 0]);
		return result;
	});

	public override uint GetFragmentLength() => (uint)FragmentLength;

	public override List<ShortIntervalList> ReadCompressedList(HuffmanData huffmanData, int bwt, LZData lzData, int lz, int counter, bool spaceCodes)
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

	public override void ProcessLZDist(LZData lzData, SumList distsSL, int fullLength, out int readIndex, out uint dist, uint length, out uint maxDist)
	{
		maxDist = Min(lzData.Dist.Max, (uint)(fullLength - length - 2));
		readIndex = ar.ReadPart(distsSL);
		distsSL.Increase(readIndex);
		if (lzData.Dist.R == 1)
		{
			dist = (uint)readIndex;
			if (dist == lzData.Dist.Threshold + 1)
				dist += ar.ReadEqual(maxDist - lzData.Dist.Threshold + lzData.UseSpiralLengths);
		}
		else
			dist = (uint)readIndex;
	}

	protected override List<List<ShortIntervalList>> FillHFWTripleList(ListHashSet<int> nulls) => ProcessUTF8(CreateVar(base.FillHFWTripleList(nulls), out var list), list[0][^1][0].Lower == 2);

	protected override List<List<ShortIntervalList>> PPMWFillTripleList(uint encoding, uint maxLength)
	{
		var list = base.PPMWFillTripleList(encoding, maxLength);
		ProcessUTF8(list, encoding == 2);
		return list;
	}

	protected override Decoding2 CreateDecoding2(ListHashSet<int> nulls, int i) => new(this, ar, nulls, hf, bwt, lz, n = i, hfw);

	protected override PPMDec CreatePPM(uint @base, int n = -1) => new(this, ar, @base, n);

	public override List<ShortIntervalList> DecodeBWT(List<ShortIntervalList> input, NList<byte> skipped)
	{
		Status[0] = 0;
		StatusMaximum[0] = GetArrayLength(input.Length, BWTBlockSize + BWTBlockExtraSize);
		var bytes = input.NConvert(x => (byte)x[0].Lower);
		NList<byte> bytes2 = [];
		for (var i = 0; i < bytes.Length;)
		{
			var rle = bytes[i] & ValuesInByte >> 1;
			bytes2.AddRange(bytes.GetSlice(i..(i += BWTBlockExtraSize)));
			bytes2.AddRange(rle == 0 ? DecodeRLEAfterBWT(bytes, ref i) : bytes.GetRange(i..Min(i += BWTBlockSize, bytes.Length)));
		}
		var hs = bytes2.Filter((x, index) => index % (BWTBlockSize + BWTBlockExtraSize) >= BWTBlockExtraSize).ToHashSet().Concat(skipped).Sort().ToHashSet();
		List<ShortIntervalList> result = new(bytes2.Length);
		for (var i = 0; i < bytes2.Length; i += BWTBlockSize, Status[0]++)
		{
			if (bytes2.Length - i <= BWTBlockExtraSize)
				throw new DecoderFallbackException();
			var length = Min(BWTBlockSize, bytes2.Length - i - BWTBlockExtraSize);
			bytes2[i] &= (ValuesInByte >> 1) - 1;
			var firstPermutation = (int)bytes2.GetSlice(i, BWTBlockExtraSize).Progression(0L, (x, y) => (x << BitsPerByte) + y);
			i += BWTBlockExtraSize;
			result.AddRange(DecodeBWT2(bytes2.GetRange(i, length), hs, firstPermutation));
		}
		return result;
	}

	protected override List<ShortIntervalList> DecodeBWT2(NList<byte> input, ListHashSet<byte> hs, int firstPermutation)
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
			result[i] = [new(input[it], ValuesInByte)];
		}
		return result;
	}

	public override NList<byte> DecodeRLEAfterBWT(NList<byte> byteList, ref int i)
	{
		if (i >= byteList.Length)
			throw new DecoderFallbackException();
		var zero = byteList[i++];
		NList<byte> result = [];
		int length, serie, l;
		byte temp;
		for (; i < byteList.Length && result.Length < BWTBlockSize;)
		{
			result.Add(byteList[i++]);
			if (i >= byteList.Length || result.Length >= BWTBlockSize)
				break;
			temp = byteList[i++];
			if (temp >= ValuesInByte >> 1)
				serie = 2;
			else
				serie = 1;
			if (temp % (ValuesInByte >> 1) != (ValuesInByte >> 1) - 1)
				length = temp % (ValuesInByte >> 1) + 1;
			else
			{
				if (i >= byteList.Length - 1 || result.Length >= BWTBlockSize - 1)
					break;
				length = (byteList[i++] << BitsPerByte) + byteList[i++] + (ValuesInByte >> 1);
			}
			if (result.Length + length > BWTBlockSize)
				throw new DecoderFallbackException();
			if (serie == 1)
			{
				for (var j = 0; j < length; j++)
					result.Add(zero);
				continue;
			}
			l = Min(length, byteList.Length - i);
			result.AddRange(byteList.GetRange(i, l));
			i += l;
			if (l >= ValuesIn2Bytes)
				continue;
			if (i >= byteList.Length || result.Length >= BWTBlockSize)
				break;
			temp = byteList[i++];
			if (temp >= ValuesInByte >> 1)
				throw new DecoderFallbackException();
			if (temp % (ValuesInByte >> 1) != (ValuesInByte >> 1) - 1)
				length = temp % (ValuesInByte >> 1) + 1;
			else
			{
				if (i >= byteList.Length - 1 || result.Length >= BWTBlockSize - 1)
					break;
				length = (byteList[i++] << BitsPerByte) + byteList[i++] + (ValuesInByte >> 1);
			}
			if (result.Length + length > BWTBlockSize)
				throw new DecoderFallbackException();
			for (var j = 0; j < length; j++)
				result.Add(zero);
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

	public virtual G.IList<byte> DecodeUnicode(G.IList<byte> input, uint encoding, Encoding encoding2)
	{
		if (input.Count == 0)
			return [];
		if (encoding == 0)
			return input;
		NList<byte> result = new(input.Count << 1);
		if (encoding == 1)
		{
			for (var i = 0; i < input.Count; i++)
			{
				if (input[i] is >= 1 and < ValuesInByte >> 1)
					result.AddRange([input[i], 0]);
				else if (UnicodeDicRev[0].TryGetValue(input[i], out var value))
					result.AddRange(value);
				else if (input[i] == 0 && i < input.Count - 2)
					result.AddRange([input[++i], input[++i]]);
				else
					throw new DecoderFallbackException();
			}
			return result;
		}
		if (encoding != 2)
			return input;
		for (var i = 0; i < input.Count; i++)
		{
			if (input[i] is >= 1 and < ValuesInByte >> 1)
				result.Add(input[i]);
			else if (UnicodeDicRev[1].TryGetValue(input[i], out var value))
				result.AddRange(value);
			else if (input[i] == 0 && i < input.Count - 2)
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

	public virtual NList<byte> DecodeFAB(NList<byte> joinedWords, bool decoding)
	{
		if (!decoding)
			return joinedWords;
		var pos = 0;
		if (pos >= joinedWords.Length)
			return joinedWords;
		var length = joinedWords[pos++];
		if (pos >= joinedWords.Length - length * 4)
			return joinedWords;
		var first = joinedWords[pos];
		Dictionary<byte, G.IEnumerable<byte>> dic = new(length);
		for (var i = 0; i < length; i++)
			if (!dic.TryAdd(joinedWords[pos++], [joinedWords[pos++], joinedWords[pos++], joinedWords[pos++]]))
				throw new DecoderFallbackException();
		var length2 = joinedWords[pos++];
		if (pos >= joinedWords.Length - (length2 == ValuesInByte - 1 ? 1 : length2 * 4))
			return joinedWords;
		if (length2 == ValuesInByte - 1)
		{
			Dictionary<byte, G.IEnumerable<byte>> dic2 = new(ValuesInByte >> 1);
			for (var i = 0; i < length; i++)
				if (joinedWords[pos++] == first)
					break;
				else if (!dic2.TryAdd(joinedWords[pos - 1], [joinedWords[pos++], joinedWords[pos++]]))
					throw new DecoderFallbackException();
				else if (pos >= joinedWords.Length - 3)
					return joinedWords;
			return joinedWords.GetRange(pos).Replace(dic2).Replace(dic);
		}
		else
		{
			var threshold = first;
			for (var i = 0; i < length2; i++)
				if (!dic.TryAdd(joinedWords[pos++], [joinedWords[pos++], joinedWords[pos++], joinedWords[pos++]]))
					throw new DecoderFallbackException();
			NList<byte> joinedWords2 = new(joinedWords.Length + GetArrayLength(joinedWords.Length, 5));
			var isThreshold = false;
			for (; pos < joinedWords.Length; pos++)
			{
				if (!isThreshold && joinedWords[pos] == threshold && !(pos < joinedWords.Length - 1 && !dic.ContainsKey(joinedWords[pos + 1])))
				{
					isThreshold = true;
					continue;
				}
				if ((!isThreshold || joinedWords[pos] == threshold) && dic.TryGetValue(joinedWords[pos], out var newCollection))
					joinedWords2.AddRange(newCollection);
				else
					joinedWords2.Add(joinedWords[pos]);
				isThreshold = false;
			}
			return joinedWords2;
		}
	}
}
