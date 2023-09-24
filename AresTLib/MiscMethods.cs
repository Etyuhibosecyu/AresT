using System;
using System.Text;
using System.Text.RegularExpressions;

namespace AresTLib;

internal partial class Compression
{
	public static (NList<byte> RepeatingBytes, int RepeatsCount) Repeats(NList<byte> input)
	{
		if (input.Length <= 2)
			return (input, 1);
		var findIndex = 1;
		while ((findIndex = input.IndexOf(input[0], findIndex + 1)) >= 0 && findIndex < input.Length >> 1)
		{
			var (quotient, remainder) = DivRem(input.Length, findIndex);
			if (remainder != 0)
				continue;
			if (new Chain(quotient - 1).All(x => RedStarLinq.Equals(input.GetSlice(0, findIndex), input.GetSlice(findIndex * (x + 1), findIndex))))
				return (input.GetRange(0, findIndex), quotient);
		}
		return (input, 1);
	}

	private BitList ArchaicHuffman(List<ShortIntervalList> input)
	{
		if (!(input.Length >= (CreateVar(input[0].Length >= 1 && input[0][0] == LengthsApplied, out var lengths) ? 4 : 3) && input.GetSlice(CreateVar(lengths ? (int)input[0][1].Base + 1 : 1, out var startPos)).All(x => x.Length >= 1 && x.All(y => y.Length == 1)) && input.GetSlice(startPos + 1).All(x => x[0].Base == input[startPos][0].Base)))
			throw new EncoderFallbackException();
		var frequencyTable = input.GetSlice(startPos).FrequencyTable(x => x[0].Lower).NSort(x => ~(uint)x.Count);
		var nodes = frequencyTable.Convert(x => new ArchaicHuffmanNode(x.Key, x.Count));
		var maxFrequency = nodes[0].Count;
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 2;
		Status[tn] = 0;
		StatusMaximum[tn] = nodes.Length - 1;
		Comparer<ArchaicHuffmanNode> comparer = new((x, y) => (~x.Count).CompareTo(~y.Count));
		var dic = frequencyTable.ToDictionary(x => x.Key, x => new BitList());
		while (nodes.Length > 1)
		{
			ArchaicHuffmanNode node = new(nodes[^1], nodes[^2]);
			nodes.Remove(^2);
			var pos = nodes.BinarySearch(node, comparer);
			if (pos < 0)
				pos = ~pos;
			nodes.Insert(pos, node);
			foreach (var x in node.Left!)
				dic[x].Add(false);
			foreach (var x in node.Right!)
				dic[x].Add(true);
			Status[tn]++;
		}
		dic.ForEach(x => x.Value.Reverse());
		BitList result = new((input.Length - startPos) * BitsPerByte);
		result.AddRange(EncodeFibonacci((uint)maxFrequency));
		result.AddRange(EncodeFibonacci((uint)frequencyTable.Length));
		Status[tn] = 0;
		StatusMaximum[tn] = frequencyTable.Length;
		Current[tn] += ProgressBarStep;
		for (var i = 0; i < frequencyTable.Length; i++, Status[tn]++)
		{
			result.AddRange(EncodeEqual(frequencyTable[i].Key, input[startPos][0].Base));
			if (i != 0)
				result.AddRange(EncodeEqual((uint)frequencyTable[i].Count - 1, (uint)frequencyTable[i - 1].Count));
		}
		Status[tn] = 0;
		StatusMaximum[tn] = input.Length;
		Current[tn] += ProgressBarStep;
		for (var i = startPos; i < input.Length; i++, Status[tn]++)
		{
			result.AddRange(dic[input[i][0].Lower]);
			for (var j = 1; j < input[i].Length; j++)
				result.AddRange(EncodeEqual(input[i][j].Lower, input[i][j].Base));
		}
		return result;
	}

	public List<ShortIntervalList> BWT(List<ShortIntervalList> input, bool words = false)
	{
		if (input.Length == 0)
			throw new EncoderFallbackException();
		if (input[0].Contains(HuffmanApplied) || input[0].Contains(BWTApplied))
			return input;
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 2;
		var lz = CreateVar(input[0].IndexOf(LempelZivApplied), out var lzIndex) != -1;
		var startPos = (lz ? (input[0].Length >= lzIndex + BWTBlockExtraSize && input[0][lzIndex + 1] == LempelZivSubdivided ? 3 : 2) : 1) + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base : 0);
		if (!(input.Length >= startPos + BWTBlockExtraSize && input.GetSlice(startPos).All(x => x.Length == 1 && x[0].Base == ValuesInByte)))
			throw new EncoderFallbackException();
		result.Replace(input.GetSlice(0, startPos));
		result[0] = new(result[0]);
		Status[tn] = 0;
		StatusMaximum[tn] = 7;
		var byteInput = input.GetSlice(startPos).NConvert(x => (byte)x[0].Lower);
		Status[tn]++;
		var uniqueElems = byteInput.ToHashSet();
		Status[tn]++;
		var uniqueElems2 = uniqueElems.ToNList().Sort();
		Status[tn]++;
		var inputPos = startPos;
		NList<byte> byteResult;
		Status[tn] = 0;
		StatusMaximum[tn] = byteInput.Length;
		Current[tn] += ProgressBarStep;
		byteResult = byteInput.Copy().AddRange(new byte[GetArrayLength(byteInput.Length, BWTBlockSize) * BWTBlockExtraSize]);
		BWTInternal();
		byteInput.Clear();
		for (var i = 0; i < byteResult.Length; i += BWTBlockSize)
			byteInput.AddRange(byteResult.GetRange(i..(i += BWTBlockExtraSize))).AddRange(RLEAfterBWT(byteResult.Skip(i).Take(BWTBlockSize), byteInput.GetRange(^BWTBlockExtraSize..)));
		uniqueElems2 = byteResult.Filter((x, index) => index % (BWTBlockSize + BWTBlockExtraSize) >= BWTBlockExtraSize).ToHashSet().ToNList().Sort();
		result.AddRange(byteInput.Convert(x => new ShortIntervalList() { new(x, ValuesInByte) }));
		result[0].Add(BWTApplied);
		uniqueElems.ExceptWith(uniqueElems2);
#if DEBUG
		var input2 = input.Skip(startPos);
		var decoded = result.GetRange(startPos).DecodeBWT(uniqueElems.ToList());
		for (var i = 0; i < input2.Length && i < decoded.Length; i++)
			for (var j = 0; j < input2[i].Length && j < decoded[i].Length; j++)
			{
				var x = input2[i][j];
				var y = decoded[i][j];
				if (!(x.Equals(y) || GetBaseWithBuffer(x.Base) == y.Base && x.Lower == y.Lower && x.Length == y.Length))
					throw new DecoderFallbackException();
			}
		if (input2.Length != decoded.Length)
			throw new DecoderFallbackException();
#endif
		var c = uniqueElems.PConvert(x => new Interval(x, ValuesInByte));
		c.Insert(0, GetCountList((uint)uniqueElems.Length));
		var cSplit = c.SplitIntoEqual(8);
		c.Dispose();
		var cLength = (uint)cSplit.Length;
		result[0].Add(new(0, cLength, cLength));
		result.Insert(startPos, cSplit.PConvert(x => new ShortIntervalList(x)));
		cSplit.Dispose();
		return result;
		void BWTInternal()
		{
			var buffer = RedStarLinq.FillArray(Environment.ProcessorCount, index => byteInput.Length < BWTBlockSize * (index + 1) ? default! : new byte[BWTBlockSize * 2 - 1]);
			var currentBlock = RedStarLinq.FillArray(buffer.Length, index => byteInput.Length < BWTBlockSize * (index + 1) ? default! : new byte[BWTBlockSize]);
			var indexes = RedStarLinq.FillArray(buffer.Length, index => byteInput.Length < BWTBlockSize * (index + 1) ? default! : new int[BWTBlockSize]);
			var tasks = new Task[buffer.Length];
			var MTFMemory = RedStarLinq.FillArray<byte[]>(buffer.Length, _ => default!);
			for (var i = 0; i < GetArrayLength(byteInput.Length, BWTBlockSize); i++)
			{
				tasks[i % buffer.Length]?.Wait();
				int i2 = i * BWTBlockSize, length = Min(BWTBlockSize, byteInput.Length - i2);
				MTFMemory[i % buffer.Length] = uniqueElems2.ToArray();
				if (byteInput.Length - i2 < BWTBlockSize)
				{
					buffer[i % buffer.Length] = default!;
					currentBlock[i % buffer.Length] = default!;
					indexes[i % buffer.Length] = default!;
					GC.Collect();
					buffer[i % buffer.Length] = new byte[(byteInput.Length - i2) * 2 - 1];
					currentBlock[i % buffer.Length] = new byte[byteInput.Length - i2];
					indexes[i % buffer.Length] = new int[byteInput.Length - i2];
				}
				for (var j = 0; j < length; j++)
					currentBlock[i % buffer.Length][j] = byteInput[i2 + j];
				var i3 = i;
				tasks[i % buffer.Length] = Task.Factory.StartNew(() => BWTMain(i3));
			}
			tasks.ForEach(x => x?.Wait());
			void BWTMain(int blockIndex)
			{
				var firstPermutation = 0;
				//Сортировка контекстов с обнаружением, в какое место попал первый
				GetBWT(currentBlock[blockIndex % buffer.Length]!, buffer[blockIndex % buffer.Length]!, indexes[blockIndex % buffer.Length], currentBlock[blockIndex % buffer.Length]!, ref firstPermutation);
				for (var i = BWTBlockExtraSize - 1; i >= 0; i--)
				{
					byteResult[(BWTBlockSize + BWTBlockExtraSize) * blockIndex + i] = (byte)firstPermutation;
					firstPermutation >>= BitsPerByte;
				}
				WriteToMTF(blockIndex);
			}
			void GetBWT(byte[] source, byte[] buffer, int[] indexes, byte[] result, ref int firstPermutation)
			{
				CopyMemory(source, 0, buffer, 0, source.Length);
				CopyMemory(source, 0, buffer, source.Length, source.Length - 1);
				for (var i = 0; i < indexes.Length; i++)
					indexes[i] = i;
				var chainLength = buffer.BWTCompare(source.Length);
				new Chain(chainLength).ForEach(i => indexes.NSort(x => buffer[chainLength - 1 - i + x]));
#if DEBUG
				if (indexes.ToHashSet().Length != indexes.Length)
					throw new InvalidOperationException();
#endif
				firstPermutation = Array.IndexOf(indexes, 0);
				// Копирование результата
				for (var i = 0; i < source.Length; i++)
					result[i] = buffer[indexes[i] + indexes.Length - 1];
			}
			void WriteToMTF(int blockIndex)
			{
				for (var i = 0; i < currentBlock[blockIndex % buffer.Length].Length; i++)
				{
					var elem = currentBlock[blockIndex % buffer.Length][i];
					var index = Array.IndexOf(MTFMemory[blockIndex % buffer.Length]!, elem);
					byteResult[(BWTBlockSize + BWTBlockExtraSize) * blockIndex + i + BWTBlockExtraSize] = uniqueElems2[index];
					Array.Copy(MTFMemory[blockIndex % buffer.Length]!, 0, MTFMemory[blockIndex % buffer.Length]!, 1, index);
					MTFMemory[blockIndex % buffer.Length][0] = elem;
					Status[tn]++;
				}
			}
		}
	}

	private static Slice<byte> RLEAfterBWT(Slice<byte> input, NList<byte> firstPermutationRange)
	{
		var result = new NList<byte>(input.Length);
		for (var i = 0; i < input.Length;)
		{
			result.Add(input[i++]);
			if (i == input.Length)
				break;
			var j = i;
			while (i < input.Length && i - j < ValuesIn2Bytes && input[i] != 0)
				i++;
			if (i != j)
				result.AddRange(i - j < ValuesInByte >> 1 ? new[] { (byte)(i - j - 1 + (ValuesInByte >> 1)) } : new[] { (byte)(ValuesInByte - 1), (byte)((i - j - (ValuesInByte >> 1)) >> BitsPerByte), (byte)(i - j - (ValuesInByte >> 1)) }).AddRange(input.GetSlice(j..i));
			if (i - j >= ValuesIn2Bytes)
				continue;
			j = i;
			while (i < input.Length && i - j < ValuesIn2Bytes && input[i] == 0)
				i++;
			if (i != j)
				result.AddRange(i - j < ValuesInByte >> 1 ? new[] { (byte)(i - j - 1) } : new[] { (byte)((ValuesInByte >> 1) - 1), (byte)((i - j - (ValuesInByte >> 1)) >> BitsPerByte), (byte)(i - j - (ValuesInByte >> 1)) });
		}
#if DEBUG
		var input2 = input;
		var pos = 0;
		var decoded = result.DecodeRLEAfterBWT(ref pos);
		for (var i = 0; i < input2.Length && i < decoded.Length; i++)
		{
			var x = input2[i];
			var y = decoded[i];
			if (x != y)
				throw new DecoderFallbackException();
		}
		if (input2.Length != decoded.Length)
			throw new DecoderFallbackException();
#endif
		if (result.Length < input.Length)
			return result.GetSlice();
		else
		{
			firstPermutationRange[0] |= ValuesInByte >> 1;
			return input;
		}
	}

	private string SHET(string originalString, (char Starter, char Escape) specialSymbols)
	{
		Status[tn] = 0;
		StatusMaximum[tn] = originalString.Length;
		var pattern1 = @"(?<=[A-Za-zА-Яа-я])(" + string.Join('|', SHETEndinds.GetSlice(0, 3).ToArray(x => string.Join('|', x.Filter(x => x.Length > 2).SortDesc(x => x.Length).ToArray()))) + ")";
		var pattern2 = @"(?<![A-Za-zА-Яа-я])(" + string.Join('|', SHETEndinds[3].Filter(x => x.Length > 2).SortDesc(x => x.Length).ToArray()) + ")";
		return Regex.Replace(Regex.Replace(originalString.Replace("" + specialSymbols.Starter, "" + specialSymbols.Starter + specialSymbols.Escape), pattern1, x => GetSHETReplacer(x, specialSymbols, SHETDic1, SHETThreshold1)), pattern2, x => GetSHETReplacer(x, specialSymbols, SHETDic2, SHETThreshold2));
	}

	private string GetSHETReplacer(Match x, (char Starter, char Escape) specialSymbols, Dictionary<string, int> dic, int threshold)
	{
		Status[tn] = x.Index;
		if (!dic.TryGetValue(x.Value, out var index))
			return x.Value;
		var s = index < threshold ? "" + specialSymbols.Starter + (char)index : "" + specialSymbols.Starter + (char)(((index - threshold) >> BitsPerByte) + threshold) + (char)(byte)(index - threshold);
		return s.Length < x.Length ? s : x.Value;
	}

	private byte[] LZMA(List<ShortIntervalList> input)
	{
		if (!(input.Length >= 3 && CreateVar(input[1][0].Base, out var originalBase) <= ValuesInByte && input.GetSlice(2).All(x => x[0].Base == originalBase)))
			throw new EncoderFallbackException();
		using BitList bitList = new(input.GetSlice(1).NConvert(x => (byte)x[0].Lower));
		return LZMA(bitList);
	}

	private byte[] LZMA(BitList bitList)
	{
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 2;
		var indexCodes = RedStarLinq.PNFill(bitList.Length - 23, index => bitList.GetSmallRange(index, 24)).PGroup(tn).Filter(X => X.Group.Length >= 2).NSort(x => x.Key).PToArray(col => col.Group.Sort());
		var startKGlobal = 24;
		Dictionary<uint, (uint dist, uint length, uint spiralLength)> repeatsInfo = new();
		Status[tn] = 0;
		StatusMaximum[tn] = indexCodes.Sum(x => x.Length);
		Current[tn] += ProgressBarStep;
		TreeSet<int> maxReached = new();
		Dictionary<int, int> maxReachedLengths = new();
		uint useSpiralLengths = 0;
		var maxLevel = Max(BitsCount(LZDictionarySize * BitsPerByte) / 2 - 5, 0);
		object lockObj = new();
		Parallel.ForEach(indexCodes, x => FindMatchesRecursive(x, 0));
		var (blockStartValue, blockEscapeValue, bitsCount, blockStartIndexes) = LZMABlockStart(bitList);
		var blockStart = new BitList(bitsCount, blockStartValue).Convert(x => new ShortIntervalList() { new(x ? 1u : 0, 2) });
		LZRequisites(bitList.Length, BitsPerByte, repeatsInfo, out var boundIndex, out var repeatsInfoList2, out var starts, out var dists, out var lengths, out var spiralLengths, out var maxDist, out var maxLength, out var maxSpiralLength, out var rDist, out var thresholdDist, out var rLength, out var thresholdLength, out var rSpiralLength, out var thresholdSpiralLength, PrimitiveType.UIntType);
		var result = bitList.Convert(x => new ShortIntervalList() { new(x ? 1u : 0, 2) });
		Status[tn] = 0;
		StatusMaximum[tn] = starts.Length;
		Current[tn] += ProgressBarStep;
		BitList elementsReplaced = new(result.Length, false);
		WriteLZMatches(result, blockStart, useSpiralLengths, starts[..boundIndex], dists, lengths, spiralLengths, maxDist, maxLength, maxSpiralLength, rDist, thresholdDist, rLength, thresholdLength, rSpiralLength, thresholdSpiralLength, elementsReplaced);
		var sortedRepeatsInfo2 = repeatsInfoList2.PConvert(l => l.PNBreak(x => x.Key, x => (x.Value.dist, x.Value.length, x.Value.spiralLength)));
		repeatsInfoList2.ForEach(x => x.Dispose());
		repeatsInfoList2.Dispose();
		var brokenRepeatsInfo = sortedRepeatsInfo2.PConvert(l => (l.Item1, l.Item2.PNBreak()));
		sortedRepeatsInfo2.ForEach(x => x.Item2.Dispose());
		sortedRepeatsInfo2.Dispose();
		Parallel.ForEach(brokenRepeatsInfo, x => WriteLZMatches(result, blockStart, useSpiralLengths, x.Item1, x.Item2.Item1, x.Item2.Item2, x.Item2.Item3, maxDist, maxLength, maxSpiralLength, rDist, thresholdDist, rLength, thresholdLength, rSpiralLength, thresholdSpiralLength, elementsReplaced, false));
		if (blockStartIndexes.Length != 0)
		{
			var temp = result.GetRange(0, blockStartIndexes[0]);
			var tempReplaced = elementsReplaced.GetRange(0, blockStartIndexes[0]);
			var blockEscape = new BitList(bitsCount, blockEscapeValue).Convert(x => new ShortIntervalList() { new(x ? 1u : 0, 2) });
			BitList blockReplaced = new(bitsCount, 0);
			for (var i = 1; i < blockStartIndexes.Length; i++)
			{
				temp.AddRange(blockEscape);
				temp.AddRange(result.GetRange(blockStartIndexes[i - 1]..blockStartIndexes[i]));
				tempReplaced.AddRange(blockReplaced);
				tempReplaced.AddRange(elementsReplaced.GetRange(blockStartIndexes[i - 1]..blockStartIndexes[i]));
			}
			temp.AddRange(blockEscape);
			temp.AddRange(result.GetRange(blockStartIndexes[^1]..));
			tempReplaced.AddRange(blockReplaced);
			tempReplaced.AddRange(elementsReplaced.GetRange(blockStartIndexes[^1]..));
			blockEscape.Dispose();
			blockReplaced.Dispose();
			result.Dispose();
			elementsReplaced.Dispose();
			result = temp;
			elementsReplaced = tempReplaced;
		}
		result.FilterInPlace((x, index) => index == 0 || !elementsReplaced[index]);
		elementsReplaced.Dispose();
		List<Interval> c = new() { new Interval((uint)rDist, 3) };
		c.WriteCount(maxDist, 24);
		if (rDist != 0)
			c.Add(new(thresholdDist, maxDist + 1));
		c.Add(new Interval((uint)rLength, 3));
		c.WriteCount(maxLength, 24);
		if (rLength != 0)
			c.Add(new(thresholdLength, maxLength + 1));
		if (maxDist == 0 && maxLength == 0)
			c.Add(new(1, 2));
		c.Add(new Interval(useSpiralLengths, 2));
		if (useSpiralLengths == 1)
		{
			c.Add(new Interval((uint)rSpiralLength, 3));
			c.WriteCount(maxSpiralLength, 16);
			if (rSpiralLength != 0)
				c.Add(new(thresholdSpiralLength, maxSpiralLength + 1));
		}
		var cSplit = c.SplitIntoEqual(8).Convert(x => new ShortIntervalList(x));
		c.Dispose();
		result.Insert(0, cSplit);
		using ArithmeticEncoder ar = new();
		if (!new AdaptiveHuffmanBits(tn).Encode(ar, result, cSplit.Length))
			throw new EncoderFallbackException();
		cSplit.Dispose();
		return ar;
		void FindMatchesRecursive(uint[] ic, int level)
		{
			if (level < maxLevel)
			{
				var nextCodes = ic.GetSlice(..Max(ic.FindLastIndex(x => x <= bitList.Length - level * BitsPerByte - startKGlobal), 0)).GroupIndexes(iIC => bitList.GetSmallRange((int)iIC + startKGlobal, BitsPerByte)).FilterInPlace(x => x.Length >= 2).Convert(x => x.ToArray(index => ic[index]).Sort());
				var nextCodes2 = nextCodes.JoinIntoSingle().ToHashSet();
				ic = ic.Filter(x => !nextCodes2.Contains(x)).ToArray();
				nextCodes.ForEach(x => FindMatchesRecursive(x, level + 1));
			}
			if (ic.Length > 1)
				FindMatches(ic, level * BitsPerByte + startKGlobal);
		}
		void FindMatches(uint[] ic, int startK)
		{
			for (var i = 1; i < ic.Length; i++, Status[tn]++)
			{
				var iIC = (int)ic[i];
				if (maxReached.Length != 0 && Lock(lockObj, () => CreateVar(maxReached.IndexOfNotLess(iIC), out var mr) >= 1 && maxReachedLengths[maxReached[mr - 1]] > iIC - maxReached[mr - 1]))
					continue;
				var matches = ic.GetSlice((Array.FindLastIndex(ic, i - 1, x => iIC - x >= LZDictionarySize * BitsPerByte) + 1)..i).Filter(jIC => iIC - jIC >= 2 && RedStarLinq.Equals(bitList.GetRange(iIC, startK), bitList.GetRange((int)jIC, startK)));
				var ub = bitList.Length - iIC - 1;
				if (matches.Length == 0 || ub < startK)
					continue;
				var lastMatch = (int)matches[^1];
				var k = startK;
				for (; k <= ub && matches.Length > 1; k++)
				{
					lastMatch = (int)matches[^1];
					matches.FilterInPlace(jIC => bitList[iIC + k] == bitList[(int)(jIC + k)]);
					if (k == ub || matches.Length == 0 || iIC - matches[^1] >= (iIC - lastMatch) << 3 && lastMatch - matches[^1] >= 64) break;
				}
				if (matches.Length == 1)
				{
					lastMatch = (int)matches[^1];
					var ub2 = Min(ub, (int)Min((long)(iIC - lastMatch) * (ushort.MaxValue + 1) - 1, int.MaxValue));
					for (; k <= ub2 && bitList[iIC + k] == bitList[lastMatch + k]; k++) ;
				}
				if (k * Log(2) < Log(21) + Log(LZDictionarySize * BitsPerByte) + Log(k))
					continue;
				var sl = (ushort)Clamp(k / (iIC - lastMatch) - 1, 0, ushort.MaxValue);
				UpdateRepeatsInfo(repeatsInfo, lockObj, iIC, 0, (uint)Max(iIC - lastMatch - k, 0), (uint)Min(k - 2, iIC - lastMatch - 2), sl, PrimitiveType.UIntType);
				if (sl > 0)
					useSpiralLengths = 1;
				if (k > ub || sl == ushort.MaxValue)
					Lock(lockObj, () => (maxReached.Add(iIC), maxReachedLengths.TryAdd(iIC, k)));
			}
		}
	}

	private static (uint Value, uint Escape, int bitsCount, List<int> ValueIndexes) LZMABlockStart(BitList bitList)
	{
		using NList<int> ranges = new();
		for (var i = 2; i <= BitsPerByte; i++)
		{
			ranges.Replace(RedStarLinq.PNFill(bitList.Length - (i - 1), index => (int)bitList.GetSmallRange(index, i)));
			using var hs = new Chain(i).ToHashSet();
			hs.ExceptWith(ranges);
			if (hs.Length != 0)
				return ((uint)hs[0], uint.MaxValue, i, new());
		}
		using var groups = ranges.Group();
		return ((uint)CreateVar(groups.FindMin(x => x.Length)!.Key, out var value), (uint)groups.FilterInPlace(x => x.Key != value).FindMin(x => x.Length)!.Key, BitsPerByte, ranges.IndexesOf(value));
	}

	private void WriteLZMatches(List<ShortIntervalList> result, List<ShortIntervalList> blockStart, uint useSpiralLengths, NList<uint> starts, NList<uint> dists, NList<uint> lengths, NList<uint> spiralLengths, uint maxDist, uint maxLength, uint maxSpiralLength, int rDist, uint thresholdDist, int rLength, uint thresholdLength, int rSpiralLength, uint thresholdSpiralLength, BitList elementsReplaced, bool changeBase = true)
	{
		double statesNumLog1, statesNumLog2;
		for (var i = 0; i < starts.Length; i++, Status[tn]++)
		{
			uint iDist = dists[i], iLength = lengths[i], iSpiralLength = spiralLengths[i];
			var localMaxLength = (iLength + 2) * (iSpiralLength + 1) - 2;
			var iStart = (int)starts[i];
			if (elementsReplaced.IndexOf(true, iStart, Min((int)localMaxLength + 3, elementsReplaced.Length - iStart)) != -1)
				continue;
			statesNumLog1 = Log(2) * (localMaxLength + 2);
			statesNumLog2 = Log(2) * blockStart.Length;
			statesNumLog2 += StatesNumLogSum(iDist, rDist, maxDist, thresholdDist, useSpiralLengths);
			statesNumLog2 += StatesNumLogSum(iLength, rLength, maxLength, thresholdLength);
			if (useSpiralLengths == 1 && iLength < localMaxLength)
				statesNumLog2 += StatesNumLogSum(iSpiralLength, rSpiralLength, maxSpiralLength, thresholdSpiralLength);
			if (statesNumLog1 <= statesNumLog2)
				continue;
			var b = blockStart.Copy();
			b[^1] = new(b[^1]);
			WriteLZValue(b[^1], iLength, rLength, maxLength, thresholdLength);
			var maxDist2 = Min(maxDist, (uint)(iStart - iLength - 2));
			if (useSpiralLengths == 0)
				WriteLZValue(b[^1], iDist, rDist, maxDist2, thresholdDist);
			else if (iLength >= localMaxLength)
				WriteLZValue(b[^1], iDist, rDist, maxDist2, thresholdDist, 1);
			else
			{
				if (rDist == 0 || maxDist2 < thresholdDist)
					b[^1].Add(new(maxDist2 + 1, maxDist2 + 2));
				else if (rDist == 1)
				{
					b[^1].Add(new(thresholdDist + 1, thresholdDist + 2));
					b[^1].Add(new(maxDist2 - thresholdDist, maxDist2 - thresholdDist + 1));
				}
				else
				{
					b[^1].Add(new(maxDist2 - thresholdDist + 1, maxDist2 - thresholdDist + 2));
					b[^1].Add(new(thresholdDist, thresholdDist + 1));
				}
			}
			if (useSpiralLengths == 1 && iLength < localMaxLength)
				WriteLZValue(b[^1], iSpiralLength, rSpiralLength, maxSpiralLength, thresholdSpiralLength);
			result.SetRange(iStart, b);
			elementsReplaced.SetAll(true, iStart + b.Length, (int)localMaxLength + 2 - b.Length);
		}
		starts.Dispose();
		dists.Dispose();
		lengths.Dispose();
		spiralLengths.Dispose();
		if (!changeBase)
			return;
		Parallel.For(2, this.result.Length, i =>
		{
			if (!elementsReplaced[i] && (i == this.result.Length - 1 || !elementsReplaced[i + 1]))
			{
				var first = this.result[i][0];
				var newBase = GetBaseWithBuffer(first.Base);
				this.result[i] = newBase == 269 ? ByteIntervals2[(int)first.Lower] : new(this.result[i]) { [0] = new(first.Lower, first.Length, newBase) };
			}
		});
	}
}
