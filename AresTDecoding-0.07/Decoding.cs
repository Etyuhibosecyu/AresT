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
			return Array.Empty<byte>();
		if (encodingVersion == 0)
			return compressedFile;
		else if (encodingVersion < ProgramVersion)
			return encodingVersion switch
			{
				1 => new AresTLib005.Decoding().Decode(compressedFile, encodingVersion),
				_ => throw new DecoderFallbackException(),
			};
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
			using ArithmeticDecoder ar = compressedFile[1..];
			var repeats = ar.ReadPart(new List<uint>(2, 224, 225));
			repeatsCount = repeats == 0 ? 1 : (int)ar.ReadCount() + 2;
			if (repeatsCount > FragmentLength >> 1)
				throw new DecoderFallbackException();
			var (encoding, maxLength, nullCount) = (ar.ReadEqual(3), ar.ReadCount(), ar.ReadCount((uint)BitsCount(FragmentLength)));
			if (maxLength is < 2 or > FragmentLength || nullCount > FragmentLength)
				throw new DecoderFallbackException();
			ListHashSet<int> nulls = new();
			for (var i = 0; i < nullCount; i++)
				nulls.Add((int)ar.ReadCount((uint)BitsCount(FragmentLength)) + (nulls.Length == 0 ? 0 : nulls[^1] + 1));
			Current[0] = 0;
			CurrentMaximum[0] = ProgressBarStep * 5;
			List<List<ShortIntervalList>> list = DecodePPM(ar, maxLength, 0);
			list[0].Add(new() { new(encoding, 3) });
			Current[0] += ProgressBarStep;
			list.Add(DecodePPM(ar, ValuesInByte, 1));
			Current[0] += ProgressBarStep;
			list.Add(DecodePPM(ar, (uint)list[0].Length - 1, 2));
			Current[0] += ProgressBarStep;
			byteList = JoinWords(list, nulls);
		}
		else if (misc == 1)
			byteList = DecodePPM(compressedFile[1..], ValuesInByte).PNConvert(x => (byte)x[0].Lower);
		else if (hf + lz + bwt != 0)
		{
			Current[0] = 0;
			CurrentMaximum[0] = ProgressBarStep * (bwt != 0 ? (hfw ? 8 : 4) : (hfw ? 7 : 3));
			using ArithmeticDecoder ar = compressedFile[1..];
			ListHashSet<int> nulls = new();
			byteList = hfw ? JoinWords(RedStarLinq.Fill(3, i => (n = i, Decode2(ar, nulls)).Item2), nulls) : Decode2(ar).PNConvert(x => (byte)x[0].Lower);
		}
		else
			byteList = compressedFile.GetSlice(1).ToNList();
		Current[0] += ProgressBarStep;
		if (rle == 14)
			byteList = byteList.DecodeRLE3();
		Current[0] += ProgressBarStep;
		if (rle == 7)
			byteList = byteList.DecodeRLE();
		return byteList.Repeat(repeatsCount).ToArray();
	}

	protected override List<ShortIntervalList> Decode2(ArithmeticDecoder ar, ListHashSet<int> nulls = default!)
	{
		var counter = (int)ar.ReadCount() - (hfw && n == 0 ? 2 : 1);
		uint lzRDist, lzMaxDist, lzThresholdDist = 0, lzRLength, lzMaxLength, lzThresholdLength = 0, lzUseSpiralLengths = 0, lzRSpiralLength, lzMaxSpiralLength, lzThresholdSpiralLength = 0;
		MethodDataUnit lzDist = new(), lzLength = new(), lzSpiralLength = new();
		int maxFrequency = 0, frequencyCount = 0;
		List<uint> arithmeticMap = new();
		List<Interval> uniqueList = new();
		List<byte> skipped = new();
		if (n == 0)
		{
			var repeats = ar.ReadPart(new List<uint>(2, 224, 225));
			repeatsCount = repeats == 0 ? 1 : (int)ar.ReadCount() + 2;
			if (repeatsCount > FragmentLength >> 1)
				throw new DecoderFallbackException();
		}
		ProcessNulls(ar, nulls, ref counter, out var encoding, out var maxLength);
		var lz = this.lz;
		if (lz != 0)
		{
			var counter2 = 7;
			lzRDist = ar.ReadEqual(3);
			lzMaxDist = ar.ReadCount();
			if (lzRDist != 0)
			{
				lzThresholdDist = ar.ReadEqual(lzMaxDist + 1);
				counter2++;
			}
			lzDist = new(lzRDist, lzMaxDist, lzThresholdDist);
			lzRLength = ar.ReadEqual(3);
			lzMaxLength = ar.ReadCount(16);
			if (lzRLength != 0)
			{
				lzThresholdLength = ar.ReadEqual(lzMaxLength + 1);
				counter2++;
			}
			lzLength = new(lzRLength, lzMaxLength, lzThresholdLength);
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
				lzSpiralLength = new(lzRSpiralLength, lzMaxSpiralLength, lzThresholdSpiralLength);
			}
		l0:
			counter -= GetArrayLength(counter2, 8);
		}
		LZData lzData = new(lzDist, lzLength, lzUseSpiralLengths, lzSpiralLength);
		return ProcessHuffman(ar, ref maxFrequency, ref frequencyCount, arithmeticMap, uniqueList, skipped, encoding, maxLength, lzData, lz, ref counter);
	}

	protected override List<ShortIntervalList> DecodeAdaptive(ArithmeticDecoder ar, List<byte> skipped, LZData lzData, int lz, int counter)
	{
		DecodeAdaptivePrerequisites(ar, skipped, ref counter, out var fileBase, out var set);
		SumList lengthsSL = lz != 0 ? new(RedStarLinq.Fill(1, (int)(lzData.Length.R == 0 ? lzData.Length.Max + 1 : lzData.Length.R == 1 ? lzData.Length.Threshold + 2 : lzData.Length.Max - lzData.Length.Threshold + 2))) : new(), distsSL = lz != 0 ? new(RedStarLinq.Fill(1, (int)lzData.UseSpiralLengths + 1)) : new();
		var firstIntervalDist = lz != 0 ? (lzData.Dist.R == 1 ? lzData.Dist.Threshold + 2 : lzData.Dist.Max + 1) + lzData.UseSpiralLengths : 0;
		DecodeAdaptivePrerequisites2(lz, fileBase, set, out var uniqueList, out var result, out var fullLength, out var nextWordLink);
		for (; counter > 0; counter--, Status[0]++)
		{
			var readIndex = DecodeAdaptiveReadFirst(ar, fileBase, set, uniqueList, ref nextWordLink);
			if (!(lz != 0 && uniqueList[readIndex].Lower == fileBase - 1))
			{
				result.Add(n == 2 ? new() { uniqueList[readIndex], new(ar.ReadEqual(2), 2) } : new() { uniqueList[readIndex] });
				fullLength++;
				if (lz != 0 && distsSL.Length < firstIntervalDist)
					distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1);
				continue;
			}
			result.Add(new() { uniqueList[^1] });
			uint dist, length, spiralLength = 0;
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
			result[^1].Add(new(length, lzData.Length.Max + 1));
			var maxDist = Min(lzData.Dist.Max, (uint)(fullLength - length - 2));
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
			ProcessAdaptiveDist(ar, lzData, result, ref fullLength, dist, length, ref spiralLength, maxDist);
			if (lz != 0 && distsSL.Length < firstIntervalDist)
				new Chain((int)Min(firstIntervalDist - distsSL.Length, (length + 2) * (spiralLength + 1))).ForEach(x => distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1));
		}
		Current[0] += ProgressBarStep;
		return result.DecodeLempelZiv(lz != 0, 0, 0, 0, 0, lzData.UseSpiralLengths, 0, 0, 0);
	}

	protected override List<ShortIntervalList> DecodePPM(ArithmeticDecoder ar, uint inputBase, bool spaces = false) => DecodePPM(ar, inputBase, spaces ? 2 : -1);

	protected virtual List<ShortIntervalList> DecodePPM(ArithmeticDecoder ar, uint inputBase, int n = -1)
	{
		if (n == -1)
		{
			var repeats = ar.ReadPart(new List<uint>(2, 224, 225));
			repeatsCount = repeats == 0 ? 1 : (int)ar.ReadCount() + 2;
			if (repeatsCount > FragmentLength >> 1)
				throw new DecoderFallbackException();
		}
		uint counter = ar.ReadCount(), dicsize = ar.ReadCount();
		if (counter > FragmentLength || dicsize > FragmentLength)
			throw new DecoderFallbackException();
		Status[0] = 0;
		StatusMaximum[0] = (int)counter;
		List<ShortIntervalList> result = new();
		SumSet<uint> globalSet = new(), newItemsSet = n == 2 ? new() : new(new Chain((int)inputBase).Convert(x => ((uint)x, 1)));
		var maxDepth = 12;
		var comparer = n == 2 ? (G.IEqualityComparer<NList<uint>>)new NListEComparer<uint>() : new EComparer<NList<uint>>((x, y) => x.Equals(y), x => (int)x.Progression((uint)x.Length, (x, y) => (x << 7 | x >> BitsPerInt - 7) ^ (uint)y.GetHashCode()));
		FastDelHashSet<NList<uint>> contextHS = new(comparer);
		List<SumSet<uint>> sumSets = new();
		SumList lzLengthsSL = new() { 1 };
		List<uint> preLZMap = new(2, 1, 2), spacesMap = new(2, 1, 2);
		NList<uint> context = new(maxDepth), context2 = new(maxDepth);
		SumSet<uint>? set = new(), excludingSet = new();
		uint nextWordLink = 0;
		for (; (int)counter > 0; counter--, Status[0]++)
		{
			result.GetSlice(Max(result.Length - maxDepth, 0)..).ForEach((x, index) => context.SetOrAdd(index, x[0].Lower));
			context.Reverse();
			context2.Replace(context);
			var index = -1;
			set.Clear();
			excludingSet.Clear();
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
			for (; context.Length > 0 && contextHS.TryGetIndexOf(context, out index) && (arithmeticIndex = set.Replace(sumSets[index]).ExceptWith(excludingSet).Length == 0 ? 1 : ar.ReadPart(new List<uint>(2, (uint)set.ValuesSum, (uint)(set.ValuesSum + set.Length * 100)))) == 1; context.RemoveAt(^1), excludingSet.UnionWith(set)) ;
			if (set.Length == 0 || context.Length == 0)
			{
				set.Replace(globalSet).ExceptWith(excludingSet);
				if (set.Length != 0 && (arithmeticIndex = ar.ReadPart(new List<uint>(2, (uint)set.ValuesSum, (uint)(set.ValuesSum + set.Length * 100)))) != 1)
				{
					if (set.Length != 0) arithmeticIndex = ar.ReadPart(set);
					item = set[arithmeticIndex].Key;
				}
				else if (n == 2)
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
			if (n == 2)
			{
				var space = (uint)ar.ReadPart(spacesMap);
				result[^1].Add(new(space, 2));
				spacesMap[0] += 1 - space;
				spacesMap[1]++;
			}
			Increase(context2, context, item);
		}
		void ProcessLZ(int curPos)
		{
			var dist = ar.ReadEqual(Min((uint)result.Length, dicsize - 1));
			var oldPos = (int)(result.Length - dist - 2);
			var readIndex = ar.ReadPart(lzLengthsSL);
			uint length;
			if (readIndex < lzLengthsSL.Length - 1)
			{
				length = (uint)readIndex + 1;
				lzLengthsSL.Increase(readIndex);
			}
			else if (ar.ReadFibonacci(out length) && length + maxDepth - 1 <= counter)
			{
				length += (uint)lzLengthsSL.Length - 1;
				lzLengthsSL.Increase(lzLengthsSL.Length - 1);
				new Chain((int)length - lzLengthsSL.Length).ForEach(x => lzLengthsSL.Insert(lzLengthsSL.Length - 1, 1));
			}
			else
				throw new DecoderFallbackException();
			for (var i = 0; i < length + maxDepth - 1; i++)
			{
				result.Add(result[oldPos + i]);
				Increase(result.GetSlice(result.Length - maxDepth - 1, maxDepth).NConvert(x => x[0].Lower).Reverse(), context, result[^1][0].Lower);
			}
			preLZMap[1]++;
			var decrease = length + maxDepth - 2;
			counter -= (uint)decrease;
			Status[0] += (int)decrease;
		}
		void Increase(NList<uint> context, NList<uint> successContext, uint item)
		{
			for (; context.Length > 0 && contextHS.TryAdd(context.Copy(), out var index); context.RemoveAt(^1))
				sumSets.SetOrAdd(index, new() { (item, 100) });
			var successLength = context.Length;
			_ = context.Length == 0 ? null : successContext.Replace(context).RemoveAt(^1);
			for (; context.Length > 0 && contextHS.TryGetIndexOf(context, out var index); context.RemoveAt(^1), _ = context.Length == 0 ? null : successContext.RemoveAt(^1))
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

	public override List<ShortIntervalList> DecodeBWT(List<ShortIntervalList> input, List<byte> skipped)
	{
		Status[0] = 0;
		StatusMaximum[0] = GetArrayLength(input.Length, BWTBlockSize + BWTBlockExtraSize);
		var bytes = input.NConvert(x => (byte)x[0].Lower);
		NList<byte> bytes2 = new();
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
			result[i] = new() { new(input[it], ValuesInByte) };
		}
		return result;
	}

	public virtual NList<byte> DecodeRLEAfterBWT(NList<byte> byteList, ref int i)
	{
		NList<byte> result = new();
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
