global using AresGlobalMethods007;
global using Corlib.NStar;
global using System;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using UnsafeFunctions;
global using G = System.Collections.Generic;
global using static AresGlobalMethods007.Decoding;
global using static AresTLib005.DecodingExtents;
global using static Corlib.NStar.Extents;
global using static System.Math;
global using static UnsafeFunctions.Global;
global using ArithmeticDecoder = AresGlobalMethods005.ArithmeticDecoder;
global using HuffmanData = AresGlobalMethods005.HuffmanData;
global using MethodDataUnit = AresGlobalMethods005.MethodDataUnit;
global using LZData = AresGlobalMethods005.LZData;

namespace AresTLib007;

public class Decoding : AresTLib005.Decoding
{
	internal const byte ProgramVersion = 2;
	internal const int FragmentLength = 8000000;
	internal const int BWTBlockSize = 50000;
	internal static int BWTBlockExtraSize => BWTBlockSize <= 0x8000 ? 2 : BWTBlockSize <= 0x800000 ? 3 : BWTBlockSize <= 0x80000000 ? 4 : BWTBlockSize <= 0x8000000000 ? 5 : BWTBlockSize <= 0x800000000000 ? 6 : BWTBlockSize <= 0x80000000000000 ? 7 : 8;
	protected int repeatsCount = 1;

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
				_ => throw new DecoderFallbackException(),
			};
		if (ProcessMethod(compressedFile) is byte[] bytes)
			return bytes;
		var byteList = ProcessMisc(compressedFile);
		Current[0] += ProgressBarStep;
		if (rle == 14)
			byteList = byteList.DecodeRLE3();
		Current[0] += ProgressBarStep;
		if (rle == 7)
			byteList = byteList.DecodeRLE();
		return [.. byteList.Repeat(repeatsCount)];
	}

	protected override NList<byte> ProcessMisc1(byte[] compressedFile) => new PPM(this, ar = compressedFile[1..], ValuesInByte).Decode().PNConvert(x => (byte)x[0].Lower);

	protected override NList<byte> ProcessNonMisc(byte[] compressedFile)
	{
		if (hf + lz + bwt != 0)
		{
			Current[0] = 0;
			CurrentMaximum[0] = ProgressBarStep * (bwt != 0 ? (hfw ? 8 : 4) : (hfw ? 7 : 3));
			ar = compressedFile[1..];
			ListHashSet<int> nulls = [];
			return hfw ? JoinWords(FillHFWTripleList(nulls), nulls) : CreateDecoding2(nulls, 0).Decode().PNConvert(x => (byte)x[0].Lower);
		}
		else
			return compressedFile.GetSlice(1).ToNList();
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

	protected override void PPMWGetEncodingAndNulls(out uint encoding, out uint maxLength, out ListHashSet<int> nulls)
	{
		GetRepeatsCount();
		base.PPMWGetEncodingAndNulls(out encoding, out maxLength, out nulls);
	}

	protected override Decoding2 CreateDecoding2(ListHashSet<int> nulls, int i) => new(this, ar, nulls, hf, bwt, lz, n = i, hfw);

	protected override PPM CreatePPM(uint @base, int n = -1) => new(this, ar, @base, n);

	public virtual void GetRepeatsCount()
	{
		var repeats = ar.ReadPart(new List<uint>(2, 224, 225));
		repeatsCount = repeats == 0 ? 1 : (int)ar.ReadCount() + 2;
		if (repeatsCount > GetFragmentLength() >> 1)
			throw new DecoderFallbackException();
	}

	public override List<ShortIntervalList> DecodeBWT(List<ShortIntervalList> input, List<byte> skipped)
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

	protected override List<ShortIntervalList> DecodeBWT2(Slice<ShortIntervalList> input, ListHashSet<byte> hs, int firstPermutation) => DecodeBWT2(input.NConvert(x => (byte)x[0].Lower), hs, firstPermutation);

	protected virtual List<ShortIntervalList> DecodeBWT2(NList<byte> input, ListHashSet<byte> hs, int firstPermutation)
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

	public virtual NList<byte> DecodeRLEAfterBWT(NList<byte> byteList, ref int i)
	{
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
					result.Add(0);
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
				result.Add(0);
		}
		return result;
	}
}
