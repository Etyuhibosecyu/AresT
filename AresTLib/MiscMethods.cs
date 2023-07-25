using System;
using System.Text;
using System.Text.RegularExpressions;

namespace AresTLib;

internal partial class Compression
{
	private readonly byte[] originalFile;
	private readonly List<ShortIntervalList> input, result = new();
	private readonly int tn;
	private const int LZDictionarySize = 32767;

	public Compression(byte[] originalFile, List<ShortIntervalList> input, int tn)
	{
		this.originalFile = originalFile;
		this.input = input;
		this.tn = tn;
	}

	internal List<ShortIntervalList> PreEncode(ref int rle, out byte[] originalFile2)
	{
		List<ShortIntervalList> cdl;
		byte[] string1, string2, cstring;
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 11;
		cstring = originalFile;
		Subtotal[tn] += ProgressBarStep;
		string1 = new RLE(originalFile, tn).Encode();
		Subtotal[tn] += ProgressBarStep;
		string2 = new RLE(originalFile, tn).RLE3();
		Subtotal[tn] += ProgressBarStep;
		Current[tn] = 0;
		Status[tn] = 0;
		if (string1.Length < cstring.Length * 0.5 && string1.Length < string2.Length)
		{
			rle = 7;
			cstring = string1;
		}
		else if (string2.Length < cstring.Length * 0.5)
		{
			rle = 14;
			cstring = string2;
		}
		(originalFile2, var repeatsCount) = Repeats(cstring);
		cdl = new ShortIntervalList[originalFile2.Length + 1];
		cdl[0] = new() { repeatsCount == 1 ? RepeatsNotApplied : RepeatsApplied };
		if (repeatsCount > 1)
			cdl[0].WriteCount((uint)repeatsCount - 2);
		var originalFile2_ = originalFile2;
		Parallel.For(0, originalFile2.Length, i => cdl[i + 1] = ByteIntervals[originalFile2_[i]]);
		Subtotal[tn] += ProgressBarStep;
		return cdl;
	}

	internal void Encode1(ref byte[] cs, ref int hf, ref int lz)
	{
		byte[] s;
		List<ShortIntervalList> dl1, cdl = input;
		LZData lzData = new();
		if ((PresentMethods & UsedMethods.LZ1) != 0)
		{
			dl1 = new(new LempelZiv(cdl, result, tn).Encode(out lzData));
			Subtotal[tn] += ProgressBarStep;
			s = WorkUpDoubleList(dl1, tn);
		}
		else
		{
			Subtotal[tn] += ProgressBarStep;
			dl1 = cdl;
			s = cs;
		}
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < cs.Length && s.Length > 0)
		{
			lz = 21;
			cdl = dl1;
			cs = s;
		}
		if ((PresentMethods & UsedMethods.HF1) != 0)
		{
			if ((PresentMethods & UsedMethods.AHF) != 0)
			{
				s = AdaptiveHuffman(cdl, lzData);
				Subtotal[tn] += ProgressBarStep;
			}
			else
			{
				dl1 = new(new Huffman(cdl, result, tn).Encode());
				Subtotal[tn] += ProgressBarStep;
				s = WorkUpDoubleList(dl1, tn);
			}
		}
		else
			Subtotal[tn] += ProgressBarStep;
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < cs.Length && s.Length > 0)
		{
			hf = (PresentMethods & UsedMethods.AHF) != 0 ? 4 : 1;
			cdl = dl1;
			cs = s;
		}
		if ((PresentMethods & UsedMethods.PSLZ1) != 0 && (PresentMethods & UsedMethods.LZ1) == 0)
		{
			dl1 = new(new LempelZiv(cdl, result, tn).Encode(out _));
			Subtotal[tn] += ProgressBarStep;
			s = WorkUpDoubleList(dl1, tn);
		}
		else
			Subtotal[tn] += ProgressBarStep;
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < cs.Length && s.Length > 0)
		{
			lz = 21;
			cs = s;
		}
	}

	internal void Encode2(ref byte[] cs, ref int hf, ref int lz)
	{
		byte[] s, cs2 = cs;
		List<List<ShortIntervalList>> tl1, ctl;
		LZData[] lzData = { new(), new(), new() };
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 9;
		ctl = MakeWordsSplit((PresentMethods & UsedMethods.SHET2) != 0);
		if (ctl.Length == 0)
			throw new EncoderFallbackException();
		Subtotal[tn] += ProgressBarStep;
		tl1 = RedStarLinq.Fill(ctl.Length, _ => new List<ShortIntervalList>());
		if ((PresentMethods & UsedMethods.LZ2) != 0)
		{
			tl1 = RedStarLinq.Fill(ctl.Length, _ => new List<ShortIntervalList>());
			for (var i = 0; i < ctl.Length; i++)
			{
				tl1[i] = new(new LempelZiv(ctl[i], result, tn).Encode(out lzData[i]));
				Subtotal[tn] += ProgressBarStep;
			}
			s = WorkUpTripleList(tl1, tn);
		}
		else
		{
			tl1 = ctl;
			Subtotal[tn] += ProgressBarStep * 3;
			s = cs2;
		}
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < cs2.Length && s.Length > 0)
		{
			lz = 21;
			ctl = tl1;
			cs2 = s;
		}
		if ((PresentMethods & UsedMethods.AHF) != 0)
		{
			s = AdaptiveHuffman(ctl, lzData);
			Subtotal[tn] += ProgressBarStep;
		}
		else
		{
			for (var i = 0; i < ctl.Length; i++)
			{
				tl1[i] = new(new Huffman(ctl[i], result, tn).Encode());
				if (tl1[i].Length == 0)
					throw new EncoderFallbackException();
				Subtotal[tn] += ProgressBarStep;
			}
			s = WorkUpTripleList(tl1, tn);
		}
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < cs2.Length && s.Length > 0)
		{
			hf = ((PresentMethods & UsedMethods.AHF) != 0 ? 4 : 1) + ((PresentMethods & UsedMethods.SHET2) != 0 ? 2 : 1);
			cs = s;
		}
	}

	internal void Encode3(ref byte[] cs, ref int hf)
	{
		byte[] s;
		List<ShortIntervalList> dl1, cdl = input;
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 4;
		Subtotal[tn] += ProgressBarStep;
		dl1 = new(BWT(cdl));
		Subtotal[tn] += ProgressBarStep;
		if ((PresentMethods & UsedMethods.AHF) != 0)
		{
			s = AdaptiveHuffman(dl1, new());
			Subtotal[tn] += ProgressBarStep;
		}
		else
		{
			dl1 = new(new Huffman(dl1, result, tn).Encode());
			Subtotal[tn] += ProgressBarStep;
			s = WorkUpDoubleList(dl1, tn);
		}
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < cs.Length && s.Length > 0)
		{
			hf = (PresentMethods & UsedMethods.AHF) != 0 ? 4 : 1;
			cs = s;
		}
	}

	internal void Encode4(ref byte[] cs, ref int hf)
	{
		byte[] s;
		List<List<ShortIntervalList>> tl1, ctl;
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 7;
		var isBWTApplicable = IsBWTApplicable();
		Subtotal[tn] += ProgressBarStep;
		if (!isBWTApplicable)
			throw new EncoderFallbackException();
		ctl = MakeWordsSplit((PresentMethods & UsedMethods.SHET4) != 0);
		if (ctl.Length != 3)
			throw new EncoderFallbackException();
		Subtotal[tn] += ProgressBarStep;
		ctl[1] = new(BWT(ctl[1], true));
		Subtotal[tn] += ProgressBarStep;
		if ((PresentMethods & UsedMethods.AHF) != 0)
		{
			s = AdaptiveHuffman(ctl, Array.Empty<LZData>());
			Subtotal[tn] += ProgressBarStep;
		}
		else
		{
			tl1 = RedStarLinq.Fill(ctl.Length, _ => new List<ShortIntervalList>());
			for (var i = 0; i < ctl.Length; i++)
			{
				tl1[i] = new(new Huffman(ctl[i], result, tn).Encode());
				if (tl1[i].Length == 0)
					throw new EncoderFallbackException();
				Subtotal[tn] += ProgressBarStep;
			}
			s = WorkUpTripleList(tl1, tn);
		}
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < cs.Length && s.Length > 0)
		{
			hf = ((PresentMethods & UsedMethods.AHF) != 0 ? 4 : 1) + ((PresentMethods & UsedMethods.SHET2) != 0 ? 2 : 1);
			cs = s;
		}
	}

	internal void Encode5(ref byte[] cs, ref int indicator)
	{
		byte[] s;
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 4;
		ArchaicHuffman(input);
		s = LZMA(input);
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < originalFile.Length && s.Length > 0)
		{
			indicator = 64;
			cs = s;
		}
	}

	internal void Encode6(ref byte[] cs, ref int indicator)
	{
		byte[] s;
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 2;
		s = PPM(input);
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < originalFile.Length && s.Length > 0)
		{
			indicator = 65;
			cs = s;
		}
	}

	internal void Encode7(ref byte[] cs, ref int indicator)
	{
		byte[] s;
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 2;
		var ctl = MakeWordsSplit((PresentMethods & UsedMethods.SHET7) != 0);
		if (ctl.Length == 0)
			throw new EncoderFallbackException();
		Subtotal[tn] += ProgressBarStep;
		s = PPM(ctl);
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < originalFile.Length && s.Length > 0)
		{
			indicator = 66;
			cs = s;
		}
	}

	public static (byte[] RepeatingBytes, int RepeatsCount) Repeats(byte[] input)
	{
		if (input.Length <= 2)
			return (input, 1);
		var findIndex = 1;
		while ((findIndex = Array.IndexOf(input, input[0], findIndex + 1)) >= 0 && findIndex < input.Length >> 1)
		{
			var (quotient, remainder) = DivRem(input.Length, findIndex);
			if (remainder != 0)
				continue;
			if (new Chain(quotient - 1).All(x => RedStarLinq.Equals(input.AsSpan(0, findIndex), input.AsSpan(findIndex * (x + 1), findIndex))))
				return (input.AsSpan(0, findIndex).ToArray(), quotient);
		}
		return (input, 1);
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
		var startPos = (lz ? (input[0].Length >= lzIndex + 2 && input[0][lzIndex + 1] == LempelZivSubdivided ? 3 : 2) : 1) + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base : 0);
		if (input.Length < startPos + 2)
			throw new EncoderFallbackException();
		result.Replace(RedStarLinq.EmptyList<ShortIntervalList>(input.Length + GetArrayLength(input.Length - startPos, BWTBlockSize) * 2));
		for (var i =  0; i < startPos; i++)
			result[i] = input[i];
		result[0] = new(result[0]);
		var spaces = input[0].Length >= 2 && input[0][1] == SpacesApplied;
		var innerCount = spaces ? 2 : 1;
		Status[tn] = 0;
		StatusMaximum[tn] = 7;
		var maxFrequency = 1;
		var groups = input.AsSpan(startPos).Convert((x, index) => (elem: x[0], index)).Wrap(l => lz ? l.FilterInPlace(x => x.index < 2 || x.elem.Lower + x.elem.Length != x.elem.Base) : l).Group(x => x.elem.Lower).Wrap(l => CreateVar(l.Max(x => x.Length), out maxFrequency) > input[startPos][0].Base * 2 || input[startPos][0].Base <= 256 ? l.NSort(x => 4294967295 - (uint)x.Length) : l);
		Status[tn]++;
		var uniqueElems = groups.PConvert(x => x[0].elem.Lower);
		Status[tn]++;
		var indexCodes = input.AsSpan(startPos).Convert(x => (int)x[0].Lower);
		Status[tn]++;
		NList<(int elem, int freq)> frequencyTable = groups.PNConvert((x, index) => (index, x.Length));
		groups.Dispose();
		Status[tn]++;
		var frequency = frequencyTable.PNConvert(x => x.freq);
		Status[tn]++;
		var bound = Max(uniqueElems.Length, ValuesInByte);
		var intervalsBase = (uint)bound;
		Status[tn] = 0;
		StatusMaximum[tn] = input.Length - startPos;
		Current[tn] += ProgressBarStep;
		var inputPos = startPos;
		if (intervalsBase == ValuesInByte)
			BWTInternal<byte>();
		else
			BWTInternal<int>();
		result[0].Add(BWTApplied);
		if (intervalsBase == ValuesInByte)
		{
			var hs = uniqueElems.ToHashSet();
			hs.ExceptWith(result.AsSpan(startPos).Convert(x => x[0].Lower));
			var c = hs.PConvert(x => new Interval(x, intervalsBase));
			c.Insert(0, GetCountList((uint)hs.Length));
			var cSplit = c.SplitIntoEqual(8);
			c.Dispose();
			var cLength = (uint)cSplit.Length;
			result[0].Add(new(0, cLength, cLength));
			result.Insert(startPos, cSplit.PConvert(x => new ShortIntervalList(x)));
			cSplit.Dispose();
		}
		return result;
		void BWTInternal<T>() where T : unmanaged, IComparable<T>
		{
			var buffer = RedStarLinq.FillArray(Environment.ProcessorCount, index => indexCodes.Length < BWTBlockSize * (index + 1) ? default! : new T[BWTBlockSize * 2 - 1]);
			var currentBlock = RedStarLinq.FillArray(buffer.Length, index => indexCodes.Length < BWTBlockSize * (index + 1) ? default! : new T[BWTBlockSize]);
			var indexes = RedStarLinq.FillArray(buffer.Length, index => indexCodes.Length < BWTBlockSize * (index + 1) ? default! : new int[BWTBlockSize]);
			var tasks = new Task[buffer.Length];
			var MTFMemory = RedStarLinq.FillArray<T[]>(buffer.Length, _ => default!);
			var isByteType = typeof(T).Equals(typeof(byte));
			for (var i = 0; i < GetArrayLength(indexCodes.Length, BWTBlockSize); i++)
			{
				tasks[i % buffer.Length]?.Wait();
				int i2 = i * BWTBlockSize, length = Min(BWTBlockSize, indexCodes.Length - i2);
				MTFMemory[i % buffer.Length] = uniqueElems.Sort().ToArray(x => isByteType ? (T)(object)(byte)x : (T)(object)x);
				if (indexCodes.Length - i2 < BWTBlockSize)
				{
					buffer[i % buffer.Length] = default!;
					currentBlock[i % buffer.Length] = default!;
					indexes[i % buffer.Length] = default!;
					GC.Collect();
					buffer[i % buffer.Length] = new T[(indexCodes.Length - i2) * 2 - 1];
					currentBlock[i % buffer.Length] = new T[indexCodes.Length - i2];
					indexes[i % buffer.Length] = new int[indexCodes.Length - i2];
				}
				for (var j = 0; j < length; j++)
					currentBlock[i % buffer.Length][j] = isByteType ? (T)(object)(byte)indexCodes[i2 + j] : (T)(object)indexCodes[i2 + j];
				var i3 = i;
				tasks[i % buffer.Length] = Task.Factory.StartNew(() => BWTMain(i3));
			}
			tasks.ForEach(x => x?.Wait());
			void BWTMain(int blockIndex)
			{
				var firstPermutation = 0;
				//Сортировка контекстов с обнаружением, в какое место попал первый
				GetBWT(currentBlock[blockIndex % buffer.Length]!, buffer[blockIndex % buffer.Length]!, indexes[blockIndex % buffer.Length], currentBlock[blockIndex % buffer.Length]!, ref firstPermutation);
				uint firstHeaderSymbol = (uint)(firstPermutation / intervalsBase), secondHeaderSymbol = (uint)(firstPermutation % intervalsBase);
				result[startPos + (BWTBlockSize + 2) * blockIndex] = new() { new(firstHeaderSymbol, intervalsBase) };
				result[startPos + (BWTBlockSize + 2) * blockIndex + 1] = new() { new(secondHeaderSymbol, intervalsBase) };
				WriteToMTF(blockIndex);
			}
			void GetBWT(T[] source, T[] buffer, int[] indexes, T[] result, ref int firstPermutation)
			{
				CopyMemory(source, 0, buffer, 0, source.Length);
				CopyMemory(source, 0, buffer, source.Length, source.Length - 1);
				for (var i = 0; i < indexes.Length; i++)
					indexes[i] = i;
				BWTSortClass2<T>.BWTSort(buffer, indexes);
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
				var bufferBase = GetBaseWithBuffer(intervalsBase);
				for (var i = 0; i < currentBlock[blockIndex % buffer.Length].Length; i++)
				{
					var elem = currentBlock[blockIndex % buffer.Length][i];
					var index = Array.IndexOf(MTFMemory[blockIndex % buffer.Length]!, elem);
					result[startPos + (BWTBlockSize + 2) * blockIndex + i + 2] = new() { new(uniqueElems[index], inputPos < startPos + 2 ? intervalsBase : bufferBase) };
					input[startPos + BWTBlockSize * blockIndex + i].AsSpan(1).ForEach(x => result[^1].Add(x));
					Array.Copy(MTFMemory[blockIndex % buffer.Length]!, 0, MTFMemory[blockIndex % buffer.Length]!, 1, index);
					MTFMemory[blockIndex % buffer.Length][0] = elem;
					Status[tn]++;
				}
			}
		}
	}

	private string SHET(string originalString, (char Starter, char Escape) specialSymbols)
	{
		Status[tn] = 0;
		StatusMaximum[tn] = originalString.Length;
		var pattern1 = @"(?<=[A-Za-zА-Яа-я])(" + string.Join('|', SHETEndinds.AsSpan(0, 3).ToArray(x => string.Join('|', x.Filter(x => x.Length > 2).SortDesc(x => x.Length).ToArray()))) + ")";
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

	private bool IsBWTApplicable()
	{
		int prev1, dist1 = 0, prevDist1 = 0, prev2, dist2 = 0, prevDist2 = 0, prev3, dist3 = 0, prevDist3 = 0, length = 0;
		Status[tn] = 0;
		StatusMaximum[tn] = originalFile.Length - 1;
		for (var i = 1; i < originalFile.Length; i++, Status[tn]++)
		{
			if (!(prevDist1 != 0 && originalFile[i - prevDist1] == originalFile[i] || prevDist2 != 0 && originalFile[i - prevDist2] == originalFile[i] || prevDist3 != 0 && originalFile[i - prevDist3] == originalFile[i]))
			{
				prev1 = Array.LastIndexOf(originalFile, originalFile[i], i - 1);
				if (prev1 == -1)
					dist1 = 0;
				else if (prev1 > 0)
				{
					dist1 = i - prev1;
					prev2 = Array.LastIndexOf(originalFile, originalFile[i], prev1 - 1);
					if (prev2 == -1)
						dist2 = 0;
					else if (prev2 > 0)
					{
						dist2 = i - prev2;
						prev3 = Array.LastIndexOf(originalFile, originalFile[i], prev2 - 1);
						if (prev3 == -1)
							dist3 = 0;
						else
							dist3 = i - prev3;
					}
				}
			}
			if (dist1 == prevDist1 || dist1 != 0 && dist3 == prevDist3 || dist2 != 0 && dist3 == prevDist3)
				length++;
			else
				length = 0;
			if (length >= 1000 && (dist1 != 0 && (dist1 <= 500 || dist1 <= length) || dist1 != 0 && dist2 != 0 && (dist2 <= 500 || dist2 <= length) || dist2 != 0 && dist3 != 0 && (dist3 <= 500 || dist3 <= length)))
				return false;
			prevDist1 = dist1;
			prevDist2 = dist2;
			prevDist3 = dist3;
		}
		return true;
	}

	private byte[] LZMA(List<ShortIntervalList> input)
	{
		if (!(input.Length >= 3 && CreateVar(input[1][0].Base, out var originalBase) <= ValuesInByte && input.AsSpan(2).All(x => x[0].Base == originalBase)))
			throw new EncoderFallbackException();
		using BitList bitList = new(input.AsSpan(1).NConvert(x => (byte)x[0].Lower));
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
		if (!AdaptiveHuffmanBits(ar, result, cSplit.Length))
			throw new EncoderFallbackException();
		cSplit.Dispose();
		return ar;
		void FindMatchesRecursive(uint[] ic, int level)
		{
			if (level < maxLevel)
			{
				var nextCodes = ic.AsSpan(..Max(ic.FindLastIndex(x => x <= bitList.Length - level * BitsPerByte - startKGlobal), 0)).GroupIndexes(iIC => bitList.GetSmallRange((int)iIC + startKGlobal, BitsPerByte)).FilterInPlace(x => x.Length >= 2).Convert(x => x.ToArray(index => ic[index]).Sort());
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
				var matches = ic.AsSpan((Array.FindLastIndex(ic, i - 1, x => iIC - x >= LZDictionarySize * BitsPerByte) + 1)..i).Filter(jIC => iIC - jIC >= 2 && RedStarLinq.Equals(bitList.GetRange(iIC, startK), bitList.GetRange((int)jIC, startK)));
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

public static class Executions
{
	public static byte[] Encode(byte[] originalFile)
	{
		var s = RedStarLinq.FillArray(ProgressBarGroups, _ => originalFile);
		var cs = originalFile;
		Total = 0;
		TotalMaximum = ProgressBarStep * 6;
		int hf = 0, bwt = 0, rle = 0, lz = 0, misc = 0, hfP1 = 0, lzP1 = 0, hfP2 = 0, lzP2 = 0, hfP3 = 0, lzP3 = 0, hfP4 = 0, lzP4 = 0, miscP5 = 0, miscP6 = 0, miscP7 = 0;
		var mainInput = new Compression(originalFile, new(), 0).PreEncode(ref rle, out var originalFile2);
		Total += ProgressBarStep;
		Threads[0] = new Thread(() =>
		{
			try
			{
				if ((PresentMethods & UsedMethods.CS1) != 0)
					new Compression(originalFile2, mainInput, 0).Encode1(ref s[0], ref hfP1, ref lzP1);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[1] = new Thread(() =>
		{
			try
			{
				if ((PresentMethods & UsedMethods.CS2) != 0 && rle == 0)
					new Compression(originalFile2, mainInput, 1).Encode2(ref s[1], ref hfP2, ref lzP2);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[2] = new Thread(() =>
		{
			try
			{
				if ((PresentMethods & UsedMethods.CS3) != 0)
					new Compression(originalFile2, mainInput, 2).Encode3(ref s[2], ref hfP3);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[3] = new Thread(() =>
		{
			try
			{
				if ((PresentMethods & UsedMethods.CS4) != 0 && rle == 0)
					new Compression(originalFile2, mainInput, 3).Encode4(ref s[3], ref hfP4);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[4] = new Thread(() =>
		{
			try
			{
				if ((PresentMethods & UsedMethods.CS5) != 0)
					new Compression(originalFile2, mainInput, 4).Encode5(ref s[4], ref miscP5);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[5] = new Thread(() =>
		{
			try
			{
				if ((PresentMethods & UsedMethods.CS6) != 0)
					new Compression(originalFile2, mainInput, 5).Encode6(ref s[5], ref miscP6);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[6] = new Thread(() =>
		{
			try
			{
				if ((PresentMethods & UsedMethods.CS7) != 0)
					new Compression(originalFile2, mainInput, 6).Encode7(ref s[6], ref miscP7);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[0].Name = "Процесс классического сжатия";
		Threads[1].Name = "Процесс сжатия для слов";
		Threads[2].Name = "Процесс сжатия с BWT";
		Threads[3].Name = "Процесс сжатия для слов с BWT";
		Threads[4].Name = "Процесс сжатия LZMA";
		Threads[5].Name = "Процесс сжатия PPM";
		Threads[6].Name = "Процесс сжатия PPM для слов";
		Threads[0].Priority = ThreadPriority.Normal;
		Threads[1].Priority = ThreadPriority.Normal;
		Threads[2].Priority = ThreadPriority.AboveNormal;
		Threads[3].Priority = ThreadPriority.Highest;
		Threads[4].Priority = ThreadPriority.Normal;
		Threads[5].Priority = ThreadPriority.Normal;
		Threads[6].Priority = ThreadPriority.Normal;
		Threads.ForEach(x => _ = x == null || (x.IsBackground = true));
		Thread.CurrentThread.Priority = ThreadPriority.Lowest;
		Threads.ForEach(x => x?.Start());
		Threads.ForEach(x => x?.Join());
		if ((PresentMethods & UsedMethods.CS7) != 0 && s[6].Length < cs.Length && s[6].Length > 0 && s.AsSpan(0, 6).All(x => s[6].Length < x.Length))
		{
			misc = miscP7;
			cs = s[6];
		}
		else if ((PresentMethods & UsedMethods.CS6) != 0 && s[5].Length < cs.Length && s[5].Length > 0 && s.AsSpan(0, 5).All(x => s[5].Length < x.Length))
		{
			misc = miscP6;
			cs = s[5];
		}
		else if ((PresentMethods & UsedMethods.CS5) != 0 && s[4].Length < cs.Length && s[4].Length > 0 && s.AsSpan(0, 4).All(x => s[4].Length < x.Length))
		{
			misc = miscP5;
			cs = s[4];
		}
		else if ((PresentMethods & UsedMethods.CS4) != 0 && s[3].Length < cs.Length && s[3].Length > 0 && s.AsSpan(0, 3).All(x => s[3].Length < x.Length))
		{
			hf = hfP4;
			bwt = 42;
			lz = lzP4;
			cs = s[3];
		}
		else if ((PresentMethods & UsedMethods.CS3) != 0 && s[2].Length < cs.Length && s[2].Length > 0 && s[2].Length < s[1].Length && s[2].Length < s[0].Length)
		{
			hf = hfP3;
			bwt = 42;
			lz = lzP3;
			cs = s[2];
		}
		else if ((PresentMethods & UsedMethods.CS2) != 0 && s[1].Length < cs.Length && s[1].Length > 0 && s[1].Length < s[0].Length)
		{
			hf = hfP2;
			bwt = 0;
			lz = lzP2;
			cs = s[1];
		}
		else if ((PresentMethods & UsedMethods.CS1) != 0 && s[0].Length < cs.Length && s[0].Length > 0)
		{
			hf = hfP1;
			bwt = 0;
			lz = lzP1;
			cs = s[0];
		}
		else
			return new byte[] { 0 }.Concat(originalFile).ToArray();
		var compressedFile = new[] { (byte)(misc + lz + bwt + rle + hf) }.Concat(cs).ToArray();
#if DEBUG
		if (!(originalFile.TryWrap(x => RedStarLinq.Equals(x, Decoding.Decode(compressedFile, ProgramVersion)), out var success) && success))
			throw new DecoderFallbackException();
#endif
		return compressedFile;
	}
}
