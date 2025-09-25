global using AresGlobalMethods;
global using NStar.Core;
global using NStar.Dictionaries;
global using NStar.ExtraHS;
global using NStar.Linq;
global using NStar.MathLib;
global using NStar.SumCollections;
global using System;
global using System.Text;
global using UnsafeFunctions;
global using G = System.Collections.Generic;
global using static AresGlobalMethods.DecodingExtents;
global using static AresTLib.Global;
global using static NStar.Core.Extents;
global using static System.Math;
global using static UnsafeFunctions.Global;
global using String = NStar.Core.String;
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

public class DecodingT : IDisposable
{
	protected GlobalDecoding globalDecoding = default!;
	protected ArithmeticDecoder ar = default!;
	protected int misc, lz, bwt, n;
	protected int repeatsCount = 1;
	protected uint encoding;

	public virtual NList<byte> Decode(NList<byte> compressedFile, byte encodingVersion)
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
		if (ProcessMethod(compressedFile) is NList<byte> bytes)
			return bytes;
		var byteList = ProcessMisc(compressedFile);
		return [.. byteList.Repeat(repeatsCount)];
	}

	public virtual void Dispose()
	{
		ar?.Dispose();
		GC.SuppressFinalize(this);
	}

	protected virtual NList<byte>? ProcessMethod(NList<byte> compressedFile)
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

	protected virtual NList<byte> ProcessMisc(NList<byte> compressedFile) => misc switch
	{
		0 => ProcessMisc0(compressedFile, out _),
		-1 => ProcessNonMisc(compressedFile),
		_ => throw new DecoderFallbackException(),
	};

	protected virtual NList<byte> ProcessMisc0(NList<byte> compressedFile, out uint encoding)
	{
		ar = compressedFile[1..];
		globalDecoding = CreateGlobalDecoding();
		PPMWGetEncodingAndNulls(out encoding, out var maxLength, out var nulls);
		var list = PPMWFillTripleList(encoding, maxLength);
		return JoinWords(list, nulls);
	}

	protected virtual NList<byte> ProcessNonMisc(NList<byte> compressedFile)
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

	protected virtual void PPMWGetEncodingAndNulls(out uint encoding, out uint maxLength, out ListHashSet<int> nulls)
	{
		(encoding, maxLength, var nullCount) = (ar.ReadEqual(3), ar.ReadNumber(), ar.ReadNumber());
		if (maxLength < 2 || maxLength > GetFragmentLength() || nullCount > GetFragmentLength())
			throw new DecoderFallbackException();
		nulls = [];
		for (var i = 0; i < nullCount; i++)
			nulls.Add((int)ar.ReadNumber() + (nulls.Length == 0 ? 0 : nulls[^1] + 1));
	}

	protected virtual NList<ShortIntervalList>[] ProcessUTF8(NList<ShortIntervalList>[] list, bool utf8)
	{
		if (utf8)
		{
			var leftOffset = ar.ReadNumber();
			list[3] = [[new(leftOffset, 0xffffffff)]];
			for (var i = 0; i < leftOffset; i++)
				list[^1].Add([new(ar.ReadEqual(ValuesInByte), ValuesInByte)]);
			var rightOffset = ar.ReadNumber();
			list[^1].Add([new(rightOffset, 0xffffffff)]);
			for (var i = 0; i < rightOffset; i++)
				list[^1].Add([new(ar.ReadEqual(ValuesInByte), ValuesInByte)]);
		}
		return list;
	}

	protected virtual NList<byte> JoinWords(NList<ShortIntervalList>[] input, ListHashSet<int> nulls)
	{
		var encoding2 = (encoding == 1) ? Encoding.Unicode : (encoding == 2) ? Encoding.UTF8 : Encoding.GetEncoding(1251);
		var a = 0;
		var joinedWords = new DecodingT().DecodeUnicode(input[1].ToNList(x => (byte)x[0].Lower), encoding, encoding2).ToArray();
		var wordsList = input[0].ToList(l => encoding2.GetString(joinedWords[a..(a += (int)l[0].Lower)]));
		if (a != joinedWords.Length)
			throw new DecoderFallbackException();
		var result = encoding2.GetBytes(RedStarLinq.ToString(input[2].ConvertAndJoin(l => wordsList[(int)l[0].Lower].Wrap(x => DecodeCOMB(l[1].Lower == 1 ? [.. x, ' '] : [.. x], true))))).Wrap(bl => encoding == 2 ? bl.ToNList().Insert(0, input[3][1..CreateVar((int)(input[3][0][0].Lower + 1), out var rightStart)].ToArray(x => (byte)x[0].Lower)).AddRange(input[3][(rightStart + 1)..(int)(rightStart + input[3][rightStart][0].Lower + 1)].ToArray(x => (byte)x[0].Lower)) : bl.ToNList());
		foreach (var x in nulls)
			if (encoding == 0)
				result.Insert(x, 0);
			else
				result.Insert(x, new byte[] { 0, 0 });
		return result;
	}

	public virtual uint GetFragmentLength() => (uint)FragmentLength;

	protected virtual NList<ShortIntervalList>[] FillHFWTripleList(ListHashSet<int> nulls)
	{
		var bwtBlockSize = 0;
		return ProcessUTF8(CreateVar(RedStarLinq.FillArray(encoding == 2 ? 4 : 3, i =>
		{
			using var dec = CreateDecoding2(nulls, i, ref bwtBlockSize);
			return dec.Decode();
		}), out var list), encoding == 2);
	}

	protected virtual NList<ShortIntervalList>[] PPMWFillTripleList(uint encoding, uint maxLength)
	{
		Current[0] = 0;
		CurrentMaximum[0] = ProgressBarStep * 3;
		var ppm = globalDecoding.CreatePPM(maxLength, 0);
		NList<ShortIntervalList>[] list = [ppm.Decode()];
		ppm.Dispose();
		list[0].Add([new(encoding, 3)]);
		Current[0] += ProgressBarStep;
		ppm = globalDecoding.CreatePPM(ValuesInByte, 1);
		list[1] = ppm.Decode();
		ppm.Dispose();
		Current[0] += ProgressBarStep;
		ppm = globalDecoding.CreatePPM((uint)list[0].Length - 1, 2);
		list[2] = ppm.Decode();
		ppm.Dispose();
		Current[0] += ProgressBarStep;
		ProcessUTF8(list, encoding == 2);
		return list;
	}

	public virtual uint GetNullsCount() => ar.ReadNumber();

	public virtual GlobalDecoding CreateGlobalDecoding() => new(ar);

	protected virtual Decoding2T CreateDecoding2(ListHashSet<int> nulls, int i, ref int bwtBlockSize) => new(this, globalDecoding, ar, nulls, bwt, bwt == 0 || i == 2 ? lz : 0, n = i, ref bwtBlockSize);

	public virtual void SetEncoding(uint encoding) => this.encoding = encoding;

	public virtual NList<ShortIntervalList> DecodeBWT(NList<ShortIntervalList> input, NList<uint> skipped, int bwtBlockSize)
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
		var hs = bytes2.Filter((x, index) => index % (bwtBlockSize + bwtBlockExtraSize) >= bwtBlockExtraSize).ToNHashSet().Concat(skipped).Sort().ToNHashSet();
		var @base = hs.Max() + 1;
		NList<ShortIntervalList> result = new(bytes2.Length);
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

	protected virtual NList<ShortIntervalList> DecodeBWT2(NList<uint> input, NListHashSet<uint> hs, uint @base, int firstPermutation)
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
		var result = RedStarLinq.NEmptyList<ShortIntervalList>(input.Length);
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
			zeroCode.RemoveEnd(1);
			length = 0;
			while (i < byteList.Length && result.Length + length < bwtBlockSize && (byteList[i] == zero || byteList[i] == zeroB))
			{
				zeroCode.Add(byteList[i++] == zero ? '0' : '1');
				length = (int)(new MpzT(zeroCode.ToString(), 2) - 1);
			}
			result.AddSeries(zero, length);
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
					result.AddRange(new byte[] { input[i], 0 });
				else if (UnicodeDicRev[0].TryGetValue(input[i], out var value))
					result.AddRange(value);
				else if (input[i] == 0 && i < input.Length - 2)
					result.AddRange(new byte[] { input[++i], input[++i] });
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
				result.AddRange(new byte[] { input[i], input[++i] });
				for (; charLength > 0; charLength--)
					result.Add(input[++i]);
			}
			else
				throw new DecoderFallbackException();
		}
		return result;
	}
}
