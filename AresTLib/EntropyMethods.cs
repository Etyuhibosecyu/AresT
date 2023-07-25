using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace AresTLib;

internal partial class Compression
{
	private List<List<ShortIntervalList>> MakeWordsSplit(bool shet)
	{
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 3;
		var s = AdaptEncoding(out var encoding, out var nulls);
		if (s == "")
			throw new EncoderFallbackException();
		var encoding2 = (encoding == 1) ? Encoding.Unicode : (encoding == 2) ? Encoding.UTF8 : Encoding.GetEncoding(1251);
		Current[tn] += ProgressBarStep;
		if (shet)
			s = SHET(s, ('\x99', '\x7F'));
		Current[tn] += ProgressBarStep;
		var words = DivideIntoWords(s);
		if (words.Length < 5)
			throw new EncoderFallbackException();
		Current[tn] += ProgressBarStep;
		Status[tn] = 0;
		StatusMaximum[tn] = 10;
		var wordsWithoutSpaces = words.PConvert(x => new Word(x.String, false));
		Status[tn]++;
		var uniqueWords = wordsWithoutSpaces.ToHashSet().ToArray();
		if (words.Length < uniqueWords.Length * 3)
			throw new EncoderFallbackException();
		var uniqueWords2 = uniqueWords.PConvert(x => encoding2.GetBytes(x.String));
		Status[tn]++;
		var uniqueIntervals = RedStarLinq.Fill(uniqueWords.Length, index => new Interval((uint)index, (uint)uniqueWords.Length));
		var uniqueLists = uniqueIntervals.ToArray(x => new[] { new ShortIntervalList { x, new Interval(0, 2) }, new ShortIntervalList { x, new(1, 2) } });
		var byteLists = RedStarLinq.Fill(ValuesInByte, index => new ShortIntervalList { new((uint)index, ValuesInByte) });
		Status[tn]++;
		var lengths = uniqueWords2.PNConvert(x => (uint)x.Length);
		Status[tn]++;
		var maxLength = lengths.Max();
		Status[tn]++;
		var lengthsSum = lengths.Sum();
		Status[tn]++;
		var indexCodes = wordsWithoutSpaces.RepresentIntoNumbers();
		Status[tn]++;
		List<List<ShortIntervalList>> result = new();
		List<Interval> c = new() { new(encoding, 3) };
		c.WriteCount(maxLength);
		result.Add(lengths.PConvert(x => new ShortIntervalList { new(x, maxLength + 1) }));
		Status[tn]++;
		result.Add(uniqueWords2.ConvertAndJoin(l => l.Convert(x => byteLists[x])));
		Status[tn]++;
		result.Add(indexCodes.PConvert((x, index) => uniqueLists[x][words[index].Space ? 1 : 0]));
		Status[tn]++;
		List<Interval> nullIntervals = new();
		nullIntervals.WriteCount((uint)nulls.Length, (uint)BitsCount(FragmentLength));
		for (var i = 0; i < nulls.Length; i++)
			nullIntervals.WriteCount((uint)(nulls[i] - CreateVar(i == 0 ? 0 : nulls[i - 1] + 1, out var prev)), (uint)BitsCount(FragmentLength));
		Status[tn]++;
		result[0].Insert(0, CreateVar(nullIntervals.SplitIntoEqual(8).Convert(x => new ShortIntervalList(x)), out var splitNullIntervals));
		result[0].Insert(0, new List<ShortIntervalList>() { new() { LengthsApplied, new(0, (uint)splitNullIntervals.Length + 1, (uint)splitNullIntervals.Length + 1) }, new(c) });
		input[0].ForEach(x => result[0][0].Add(x));
		result[1].Insert(0, new ShortIntervalList());
		result[2].Insert(0, new ShortIntervalList() { WordsApplied, SpacesApplied });
#if DEBUG
		if (!RedStarLinq.Equals(originalFile.Filter((x, index) => !nulls.Contains(index) && (encoding == 0 || !nulls.Contains(index - 1))).ToArray(), result.Wrap(tl =>
		{
			var a = 0;
			var wordsList = tl[0].AsSpan(GetArrayLength(nullIntervals.Length, 8) + 2).Convert(l => encoding2.GetString(tl[1].AsSpan(1)[a..(a += (int)l[0].Lower)].ToArray(x => (byte)x[0].Lower)));
			return encoding2.GetBytes(tl[2].AsSpan(1).ConvertAndJoin(l => wordsList[(int)l[0].Lower].Wrap(x => l[1].Lower == 1 ? new List<char>(x).Add(' ') : x.ToList())).ToArray());
		})))
			throw new InvalidOperationException();
#endif
		return result;
	}

	private string AdaptEncoding(out uint encoding, out ListHashSet<int> nulls)
	{
		int ansiLetters = 0, utf16Letters = 0, utf8Letters = 0;
		ListHashSet<int> singleNulls = new(), doubleNulls = new();
		if (originalFile.Length == 0)
		{
			encoding = 0;
			nulls = new();
			return "";
		}
		Status[tn] = 0;
		StatusMaximum[tn] = originalFile.Length;
		if (originalFile[0] is >= 0xC0 and <= 0xFF)
			ansiLetters++;
		object lockObj = new();
		Status[tn]++;
		Parallel.For(1, originalFile.Length, (i, pls) =>
		{
			if (originalFile[i] == 0)
			{
				lock (lockObj) singleNulls.Add(i);
				if (originalFile[i - 1] == 0)
					lock (lockObj) doubleNulls.Add(i - 1);
			}
			if (originalFile[i] is >= 0xC0 and <= 0xFF)
				lock (lockObj)
					ansiLetters++;
			else if (originalFile[i - 1] >= 0x10 && originalFile[i - 1] <= 0x4F && originalFile[i] == 0x04 || originalFile[i - 1] >= 0x20 && originalFile[i - 1] <= 0x7F && originalFile[i] == 0x00)
				lock (lockObj)
					utf16Letters++;
			else if (originalFile[i - 1] == 0xD0 && originalFile[i] >= 0x90 && originalFile[i] <= 0xBF || originalFile[i - 1] == 0xD1 && originalFile[i] >= 0x80 && originalFile[i] <= 0x8F)
				lock (lockObj)
					utf8Letters++;
			Status[tn]++;
		});
		var nullSequenceStart = -1;
		doubleNulls.FilterInPlace((x, index) => (index == 0 || doubleNulls[index - 1] != x - 1 || (index - nullSequenceStart) % 2 == 0) && (nullSequenceStart = index) >= 0);
		if (doubleNulls.Length * doubleNulls.Length * 400 >= originalFile.Length)
		{
			encoding = 0;
			nulls = new();
			return "";
		}
		if ((utf16Letters >= (originalFile.Length + 9) / 10 || utf16Letters > ansiLetters) && utf16Letters > utf8Letters)
		{
			encoding = 1;
			nulls = doubleNulls;
			return Encoding.Unicode.GetString(originalFile.Filter((x, index) => !doubleNulls.Contains(index) && !doubleNulls.Contains(index - 1)).ToArray());
		}
		else if (utf8Letters >= (originalFile.Length + 9) / 10 || utf8Letters > ansiLetters)
		{
			encoding = 2;
			nulls = doubleNulls;
			return Encoding.UTF8.GetString(originalFile.Filter((x, index) => !doubleNulls.Contains(index) && !doubleNulls.Contains(index - 1)).ToArray());
		}
		else
		{
			encoding = 0;
			if (singleNulls.Length * singleNulls.Length * 400 >= originalFile.Length)
			{
				nulls = new();
				return "";
			}
			else
			{
				nulls = singleNulls;
				return Encoding.GetEncoding(1251).GetString(originalFile.Filter((x, index) => !singleNulls.Contains(index)).ToArray());
			}
		}
	}

	/// <summary>Разделяет текст на слова. Для этой цели можно было бы использовать регулярные выражения, но они потребляют слишком много ресурсов.</summary>
	private List<Word> DivideIntoWords(string text)
	{
		List<Word> outputWords = new();
		var wordStart = 0;
		var state = 0; //0 - начальное состояние, 1 - прописные буквы, 2 - строчные буквы, 3 - цифры, 4 - пробел, 5 - перевод строки #1, 6 - перевод строки #2, 7 - управляющий символ SHET, 8 - второй символ SHET, 9 - управляющий символ предлога SHET, 10 - второй символ предлога SHET, 11 - прочие символы.
		var space = false;
		Status[tn] = 0;
		StatusMaximum[tn] = text.Length;
		for (var i = 0; i < text.Length; i++, Status[tn]++)
		{
			if (text[i] is >= 'A' and <= 'Z' or >= 'А' and <= 'Я')
			{
				switch (state)
				{
					case 0:
					case 1:
						break;
					default:
						outputWords.Add(new Word(text[wordStart..(i - (space ? 1 : 0))], space));
						wordStart = i;
						break;
				}
				state = 1;
			}
			else if (text[i] is >= 'a' and <= 'z' or >= 'а' and <= 'я')
			{
				switch (state)
				{
					case 0:
					case 1:
					case 2:
						break;
					default:
						outputWords.Add(new Word(text[wordStart..(i - (space ? 1 : 0))], space));
						wordStart = i;
						break;
				}
				state = 2;
			}
			else if (text[i] is >= '0' and <= '9')
			{
				switch (state)
				{
					case 0:
					case 3:
						break;
					default:
						outputWords.Add(new Word(text[wordStart..(i - (space ? 1 : 0))], space));
						wordStart = i;
						break;
				}
				state = 3;
			}
			else if (text[i] == ' ')
			{
				if (state == 4)
				{
					outputWords.Add(new Word(text[wordStart..(i - 1)], true));
					wordStart = i;
				}
				space = true;
				state = 4;
			}
			else if (text[i] == '\r')
			{
				state = 5;
				outputWords.Add(new Word(text[wordStart..(i - (space ? 1 : 0))], space));
				wordStart = i;
			}
			else if (text[i] == '\n')
			{
				if (state != 5)
				{
					outputWords.Add(new Word(text[wordStart..(i - (space ? 1 : 0))], space));
					wordStart = i;
				}
				state = 6;
			}
			else if (text[i] == '\x99')
			{
				if (state is 1 or 2)
					state = 7;
				else
				{
					outputWords.Add(new Word(text[wordStart..(i - (space ? 1 : 0))], space));
					wordStart = i;
					state = 9;
				}
			}
			else if (state is 7 or 9)
			{
				if (text[i] < (state == 7 ? SHETThreshold1 : SHETThreshold2))
					state = 2;
				else
					state++;
			}
			else if (state is 8 or 10)
				state = 2;
			else
			{
				state = 11;
				outputWords.Add(new Word(text[wordStart..(i - (space ? 1 : 0))], space));
				wordStart = i;
			}
			if (text[i] != ' ')
				space = false;
		}
		outputWords.Add(new Word(text[wordStart..^(space ? 1 : 0)], space));
		return outputWords.Filter(x => x.String != "" || x.Space);
	}

	private byte[] AdaptiveHuffman(List<ShortIntervalList> input, LZData lzData)
	{
		if (input.Length < 2)
			throw new EncoderFallbackException();
		using ArithmeticEncoder ar = new();
		if (!AdaptiveHuffmanInternal(ar, input, lzData))
			throw new EncoderFallbackException();
		ar.WriteEqual(1234567890, 4294967295);
		return ar;
	}

	private byte[] AdaptiveHuffman(List<List<ShortIntervalList>> input, LZData[] lzData)
	{
		if (input.Any(x => x.Length < 2))
			throw new EncoderFallbackException();
		using ArithmeticEncoder ar = new();
		for (var i = 0; i < input.Length; i++)
			if (!AdaptiveHuffmanInternal(ar, input[i], lzData[i], i))
				throw new EncoderFallbackException();
		ar.WriteEqual(1234567890, 4294967295);
		return ar;
	}

	private bool AdaptiveHuffmanInternal(ArithmeticEncoder ar, List<ShortIntervalList> input, LZData lzData, int n = 1)
	{
		var bwtIndex = input[0].IndexOf(BWTApplied);
		if (CreateVar(input[0].IndexOf(HuffmanApplied), out var huffmanIndex) != -1 && !(bwtIndex != -1 && huffmanIndex == bwtIndex + 1))
			throw new EncoderFallbackException();
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 2;
		Status[tn] = 0;
		StatusMaximum[tn] = 3;
		var lz = CreateVar(input[0].IndexOf(LempelZivApplied), out var lzIndex) != -1 && (bwtIndex == -1 || lzIndex != bwtIndex + 1);
		var lzDummy = CreateVar(input[0].IndexOf(LempelZivDummyApplied), out var lzDummyIndex) != -1 && (bwtIndex == -1 || lzDummyIndex != bwtIndex + 1);
		var bwtLength = bwtIndex != -1 ? (int)input[0][bwtIndex + 1].Base : 0;
		var startPos = (lz || lzDummy ? (input[0].Length >= lzIndex + 2 && input[0][lzIndex + 1] == LempelZivSubdivided ? 3 : 2) : 1) + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base : 0) + bwtLength;
		Status[tn]++;
		var lzPos = bwtIndex != -1 ? 4 : 2;
		if (input.Length < startPos + lzPos + 1)
			throw new EncoderFallbackException();
		var originalBase = input[startPos + lzPos][0].Base;
		if (!input.AsSpan(startPos + lzPos + 1).All((x, index) => bwtIndex != -1 && (index + lzPos + 1) % (BWTBlockSize + 2) is 0 or 1 || x[0].Base == originalBase))
			throw new EncoderFallbackException();
		Status[tn]++;
		ar.WriteCount((uint)input.Length);
		for (var i = 0; i < startPos; i++)
			for (var j = 0; j < input[i].Length; j++)
			{
				var x = input[i][j];
				if (i == startPos - bwtLength && j == 2)
					ar.WriteCount(x.Base);
				ar.WritePart(x.Lower, x.Length, x.Base);
			}
		Status[tn]++;
		var newBase = input[startPos][0].Base + (lz ? 1u : 0);
		ar.WriteCount(newBase);
		Status[tn] = 0;
		StatusMaximum[tn] = input.Length - startPos;
		Current[tn] += ProgressBarStep;
		SumSet<uint> set = new();
		SumList lengthsSL = lz ? new(RedStarLinq.Fill(1, (int)(lzData.Length.R == 0 ? lzData.Length.Max + 1 : lzData.Length.R == 1 ? lzData.Length.Threshold + 2 : lzData.Length.Max - lzData.Length.Threshold + 2))) : new(), distsSL = lz ? new(RedStarLinq.Fill(1, (int)lzData.UseSpiralLengths + 1)) : new();
		var firstIntervalDist = lz ? (lzData.Dist.R == 0 ? lzData.Dist.Max + 1 : lzData.Dist.R == 1 ? lzData.Dist.Threshold + 2 : lzData.Dist.Max - lzData.Dist.Threshold + 2) + lzData.UseSpiralLengths : 0;
		if (lz)
			set.Add((newBase - 1, 1));
		for (var i = startPos; i < input.Length; i++, Status[tn]++)
		{
			var item = input[i][0].Lower;
			var sum = set.GetLeftValuesSum(item, out var frequency);
			var bufferInterval = GetBufferInterval((uint)set.ValuesSum);
			var fullBase = (uint)(set.ValuesSum + bufferInterval);
			if (frequency == 0)
			{
				ar.WritePart((uint)set.ValuesSum, bufferInterval, fullBase);
				if (n != 2)
					ar.WriteEqual(item, newBase);
			}
			else
				ar.WritePart((uint)sum, (uint)frequency, fullBase);
			set.Increase(item);
			int lzLength = 0, lzSpiralLength = 0;
			var j = 1;
			if (lz && item == newBase - 1)
			{
				item = input[i][j].Lower;
				lzLength = (int)(item + (lzData.Length.R == 2 ? lzData.Length.Threshold : 0));
				sum = lengthsSL.GetLeftValuesSum((int)item, out frequency);
				ar.WritePart((uint)sum, (uint)frequency, (uint)lengthsSL.ValuesSum);
				lengthsSL.Increase((int)item);
				j++;
				if (lzData.Length.R != 0 && item == lengthsSL.Length - 1)
				{
					ar.WritePart(input[i][j].Lower, input[i][j].Length, input[i][j].Base);
					lzLength = (int)(lzData.Length.R == 2 ? input[i][j].Lower : input[i][j].Lower + lzData.Length.Threshold + 1);
					j++;
				}
				item = input[i][j].Lower;
				sum = distsSL.GetLeftValuesSum((int)item, out frequency);
				ar.WritePart((uint)sum, (uint)frequency, (uint)distsSL.ValuesSum);
				distsSL.Increase((int)item);
				j++;
				if (lzData.Dist.R != 0 && input[i][j - 1].Lower == input[i][j - 1].Base - 1)
				{
					ar.WritePart(input[i][j].Lower, input[i][j].Length, input[i][j].Base);
					j++;
				}
				lzSpiralLength = lzData.UseSpiralLengths != 0 && input[i][j - 1].Lower == input[i][j - 1].Base - 1 ? lzData.SpiralLength.R == 0 ? (int)input[i][^1].Lower : (int)(input[i][^1].Lower + (lzData.SpiralLength.R == 2 != (input[i][^2].Lower == input[i][^2].Base - 1) ? lzData.SpiralLength.Threshold + 2 - lzData.SpiralLength.R : 0)) : 0;
				if (lz && distsSL.Length < firstIntervalDist)
					new Chain(Min((int)firstIntervalDist - distsSL.Length, (lzLength + 2) * (lzSpiralLength + 1))).ForEach(x => distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1));
			}
			else if (lz && distsSL.Length < firstIntervalDist)
				distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1);
			for (; j < input[i].Length; j++)
				ar.WritePart(input[i][j].Lower, input[i][j].Length, input[i][j].Base);
		}
		return true;
	}

	private bool AdaptiveHuffmanBits(ArithmeticEncoder ar, List<ShortIntervalList> input, int startPos)
	{
		if (!(input.Length >= startPos + 2 && input.AsSpan(startPos).All(x => x.Length > 0 && x[0].Base == 2)))
			throw new EncoderFallbackException();
		Status[tn] = 0;
		StatusMaximum[tn] = 3;
		Current[tn] += ProgressBarStep;
		Status[tn]++;
		ar.WriteCount((uint)input.Length);
		for (var i = 0; i < startPos; i++)
			for (var j = 0; j < input[i].Length; j++)
			{
				var x = input[i][j];
				ar.WritePart(x.Lower, x.Length, x.Base);
			}
		Status[tn]++;
		var newBase = input[startPos][0].Base;
		ar.WriteCount(newBase);
		Status[tn] = 0;
		StatusMaximum[tn] = input.Length - startPos;
		Current[tn] += ProgressBarStep;
		var windowSize = 1 << 13;
		uint zeroFreq = 1, totalFreq = 2;
		for (var i = startPos; i < input.Length; i++, Status[tn]++)
		{
			var item = input[i][0].Lower == 1;
			var sum = item ? zeroFreq : 0;
			ar.WritePart(sum, item ? totalFreq - zeroFreq : zeroFreq, totalFreq);
			if (i < windowSize + startPos)
			{
				if (!item)
					zeroFreq++;
				totalFreq++;
			}
			if (i >= windowSize + startPos && input[i - windowSize][0].Lower == (item ? 0u : 1))
			{
				if (item)
					zeroFreq--;
				else
					zeroFreq++;
			}
			for (var j = 1; j < input[i].Length; j++)
				ar.WritePart(input[i][j].Lower, input[i][j].Length, input[i][j].Base);
		}
		return true;
	}

	private BitList ArchaicHuffman(List<ShortIntervalList> input)
	{
		if (!(input.Length >= (CreateVar(input[0].Length >= 1 && input[0][0] == LengthsApplied, out var lengths) ? 4 : 3) && input.AsSpan(CreateVar(lengths ? (int)input[0][1].Base + 1 : 1, out var startPos)).All(x => x.Length >= 1 && x.All(y => y.Length == 1)) && input.AsSpan(startPos + 1).All(x => x[0].Base == input[startPos][0].Base)))
			throw new EncoderFallbackException();
		var frequencyTable = input.AsSpan(startPos).FrequencyTable(x => x[0].Lower).NSort(x => ~(uint)x.Count);
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

	private byte[] PPM(List<ShortIntervalList> input)
	{
		if (input.Length < 4)
			throw new EncoderFallbackException();
		using ArithmeticEncoder ar = new();
		if (!PPMInternal(ar, input))
			throw new EncoderFallbackException();
		ar.WriteEqual(1234567890, 4294967295);
		return ar;
	}

	private byte[] PPM(List<List<ShortIntervalList>> input)
	{
		if (!(input.Length == 3 && input.All(x => x.Length >= 4)))
			throw new EncoderFallbackException();
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 2;
		using ArithmeticEncoder ar = new();
		for (var i = 0; i < input.Length; i++)
		{
			if (!PPMInternal(ar, input[i], i))
				throw new EncoderFallbackException();
			if (i != input.Length - 1)
				Current[tn] += ProgressBarStep;
		}
		ar.WriteEqual(1234567890, 4294967295);
		return ar;
	}

	private bool PPMInternal(ArithmeticEncoder ar, List<ShortIntervalList> input, int n = 1)
	{
		if (!(input.Length >= 4 && input[CreateVar(input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base + 1 : 1, out var startPos)].Length is 1 or 2 && input[startPos][0].Length == 1 && CreateVar(input[startPos][0].Base, out var inputBase) >= 2 && input[startPos][^1].Length == 1 && input.AsSpan(startPos + 1).All(x => x.Length == input[startPos].Length && x[0].Length == 1 && x[0].Base == inputBase && (x.Length == 1 || x[1].Length == 1 && x[1].Base == input[startPos][1].Base))))
			throw new EncoderFallbackException();
		Status[tn] = 0;
		StatusMaximum[tn] = input.Length - startPos;
		for (var i = 0; i < input[0].Length; i++)
			ar.WritePart(input[0][i].Lower, input[0][i].Length, input[0][i].Base);
		if (n == 0)
		{
			ar.WritePart(input[1][0].Lower, 1, 3);
			ar.WriteCount(inputBase);
			for (var i = 2; i < startPos; i++)
				for (var j = 0; j < input[i].Length; j++)
					ar.WritePart(input[i][j].Lower, input[i][j].Length, input[i][j].Base);
		}
		ar.WriteCount((uint)(input.Length - startPos));
		ar.WriteCount(LZDictionarySize);
		SumSet<uint> globalSet = new(), newItemsSet = n == 2 ? new() : new(new Chain((int)inputBase).Convert(x => ((uint)x, 1)));
		var maxDepth = inputBase == 2 ? 96 : 12;
		LimitedQueue<List<Interval>> buffer = new(maxDepth);
		var comparer = new NListEComparer<uint>();
		FastDelHashSet<NList<uint>> contextHS = new(comparer);
		HashList<NList<uint>> lzhl = new(comparer);
		List<SumSet<uint>> sumSets = new();
		uint lzCount = 1, notLZCount = 1, spaceCount = 1, notSpaceCount = 1;
		LimitedQueue<bool> spaceBuffer = new(maxDepth);
		LimitedQueue<uint> newItemsBuffer = new(maxDepth);
		var nextTarget = 0;
		for (var i = startPos; i < input.Length; i++, Status[tn]++)
		{
			var item = input[i][0].Lower;
			var context = input.AsSpan(Max(startPos, i - maxDepth)..i).NConvert(x => x[0].Lower).Reverse();
			var context2 = context.Copy();
			if (i < nextTarget)
				goto l1;
			var index = -1;
			SumSet<uint>? set = null, excludingSet = new();
			List<Interval> intervalsForBuffer = new();
			if (context.Length == maxDepth && i >= (maxDepth << 1) + startPos && ProcessLZ(context, item, i) && i < nextTarget)
				goto l1;
			for (; context.Length > 0 && !contextHS.TryGetIndexOf(context, out index); context.RemoveAt(^1)) ;
			long sum = 0;
			var frequency = 0;
			for (; context.Length > 0 && contextHS.TryGetIndexOf(context, out index) && (sum = (set = sumSets[index].Copy().ExceptWith(excludingSet)).GetLeftValuesSum(item, out frequency)) >= 0 && frequency == 0; context.RemoveAt(^1), excludingSet.UnionWith(set))
				if (set.Length != 0)
					intervalsForBuffer.Add(new((uint)set.ValuesSum, (uint)set.Length * 100, (uint)(set.ValuesSum + set.Length * 100)));
			if (set == null || context.Length == 0)
				set = globalSet.Copy().ExceptWith(excludingSet);
			if (frequency == 0)
				sum = set.GetLeftValuesSum(item, out frequency);
			if (frequency == 0)
			{
				if (set.Length != 0)
					intervalsForBuffer.Add(new((uint)set.ValuesSum, (uint)set.Length * 100, (uint)(set.ValuesSum + set.Length * 100)));
				if (n != 2)
				{
					intervalsForBuffer.Add(new((uint)newItemsSet.IndexOf(item), (uint)newItemsSet.Length));
					newItemsSet.RemoveValue(item);
					newItemsBuffer.Enqueue(item);
				}
			}
			else
			{
				intervalsForBuffer.Add(new(0, (uint)set.ValuesSum, (uint)(set.ValuesSum + set.Length * 100)));
				intervalsForBuffer.Add(new((uint)sum, (uint)frequency, (uint)set.ValuesSum));
				newItemsBuffer.Enqueue(uint.MaxValue);
			}
			var isSpace = false;
			if (n == 2)
			{
				isSpace = input[i][1].Lower != 0;
				uint bufferSpaces = (uint)spaceBuffer.Count(true), bufferNotSpaces = (uint)spaceBuffer.Count(false);
				intervalsForBuffer.Add(new(isSpace ? notSpaceCount + bufferNotSpaces : 0, isSpace ? spaceCount + bufferSpaces : notSpaceCount + bufferNotSpaces, notSpaceCount + spaceCount + (uint)spaceBuffer.Length));
			}
			else
				for (var j = 1; j < input[i].Length; j++)
					intervalsForBuffer.Add(new(input[i][j].Lower, input[i][j].Length, input[i][j].Base));
			if (buffer.IsFull)
				buffer.Dequeue().ForEach(x => ar.WritePart(x.Lower, x.Length, x.Base));
			buffer.Enqueue(intervalsForBuffer);
			if (n == 2 && spaceBuffer.IsFull)
			{
				var space2 = spaceBuffer.Dequeue();
				if (space2)
					spaceCount++;
				else
					notSpaceCount++;
			}
			spaceBuffer.Enqueue(isSpace);
		l1:
			if (context2.Length == maxDepth)
				lzhl.SetOrAdd((i - startPos - maxDepth) % LZDictionarySize, context2.Copy());
			Increase(context2, item);
			context.Dispose();
			context2.Dispose();
		}
		while (buffer.Length != 0)
			buffer.Dequeue().ForEach(x => ar.WritePart(x.Lower, x.Length, x.Base));
		bool ProcessLZ(NList<uint> context, uint item, int curPos)
		{
			if (!buffer.IsFull)
				return false;
			var bestDist = -1;
			var bestLength = -1;
			foreach (var pos in lzhl.IndexesOf(context))
			{
				var dist = (pos - (curPos - startPos - maxDepth)) % LZDictionarySize + curPos - startPos - maxDepth;
				int length;
				for (length = -maxDepth; length < input.Length - startPos - curPos && RedStarLinq.Equals(input[curPos + length], input[dist + maxDepth + startPos + length], (x, y) => x.Lower == y.Lower); length++) ;
				if (curPos - (dist + maxDepth + startPos) >= 2 && length > bestLength)
				{
					bestDist = dist;
					bestLength = length;
				}
			}
			if (bestDist == -1)
			{
				if (buffer.IsFull)
				{
					ar.WritePart(0, notLZCount, lzCount + notLZCount);
					notLZCount++;
				}
				return false;
			}
			ar.WritePart(notLZCount, lzCount, lzCount + notLZCount);
			lzCount++;
			ar.WriteEqual((uint)(curPos - (bestDist + maxDepth + startPos) - 2), (uint)Min(curPos - startPos - maxDepth, LZDictionarySize - 1));
			ar.WriteFibonacci((uint)bestLength + 1);
			buffer.Clear();
			spaceBuffer.Clear();
			if (n != 2)
				newItemsBuffer.Filter(x => x != uint.MaxValue).ForEach(x => newItemsSet.Add((x, 1)));
			newItemsBuffer.Clear();
			nextTarget = curPos + bestLength;
			return true;
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
		return true;
	}

	private byte[] PPMBits(BitList input)
	{
		Status[tn] = 0;
		StatusMaximum[tn] = input.Length;
		ArithmeticEncoder ar = new();
		ar.WriteCount((uint)input.Length);
		var dicsize = LZDictionarySize << 3;
		ar.WriteCount((uint)dicsize);
		(uint Zeros, uint Units) globalSet = (1, 1);
		var maxDepth = 96;
		LimitedQueue<List<Interval>> buffer = new(maxDepth);
		var comparer = new EComparer<BitList>((x, y) => x.Equals(y), x => x.Length == 0 ? 1234567890 : x.ToUIntList().Progression(0, (x, y) => x << 9 ^ (int)y));
		FastDelHashSet<BitList> contextHS = new(comparer);
		HashList<BitList> lzhl = new(comparer);
		List<(uint Zeros, uint Units)> sumSets = new();
		uint lzCount = 1, notLZCount = 1;
		var nextTarget = 0;
		for (var i = 0; i < input.Length; i++, Status[tn]++)
		{
			var item = input[i];
			var context = input.GetRange(Max(0, i - maxDepth)..i).Reverse();
			var context2 = context.Copy();
			if (i < nextTarget)
				goto l1;
			var index = -1;
			List<Interval> intervals = new();
			if (context.Length == maxDepth && i >= maxDepth << 1 && ProcessLZ(context, item, i) && i < nextTarget)
				goto l1;
			for (; context.Length > 0 && !contextHS.TryGetIndexOf(context, out index); context.RemoveAt(^1)) ;
			if (context.Length == 0)
				intervals.Add(new(item ? globalSet.Zeros : 0, item ? globalSet.Units : globalSet.Zeros, globalSet.Zeros + globalSet.Units));
			else
				intervals.Add(new(item ? sumSets[index].Zeros : 0, item ? sumSets[index].Units : sumSets[index].Zeros, sumSets[index].Zeros + sumSets[index].Units));
			if (buffer.IsFull)
				buffer.Dequeue().ForEach(x => ar.WritePart(x.Lower, x.Length, x.Base));
			buffer.Enqueue(intervals);
		l1:
			if (context2.Length == maxDepth)
				lzhl.SetOrAdd((i - maxDepth) % dicsize, context2.Copy());
			Increase(context2, item);
			context.Dispose();
			context2.Dispose();
		}
		while (buffer.Length != 0)
			buffer.Dequeue().ForEach(x => ar.WritePart(x.Lower, x.Length, x.Base));
		void Increase(BitList context, bool item)
		{
			for (; context.Length > 0 && !contextHS.TryGetIndexOf(context, out var index); context.RemoveAt(^1))
			{
				contextHS.TryAdd(context.Copy(), out index);
				sumSets.SetOrAdd(index, (item ? 1u : 2, item ? 2u : 1));
			}
			for (; context.Length > 0 && contextHS.TryGetIndexOf(context, out var index); sumSets[index] = (sumSets[index].Zeros + (item ? 0u : 1), sumSets[index].Units + (item ? 1u : 0)), context.RemoveAt(^1)) ;
			globalSet = (globalSet.Zeros + (item ? 0u : 1), globalSet.Units + (item ? 1u : 0));
		}
		bool ProcessLZ(BitList context, bool item, int i)
		{
			if (!buffer.IsFull)
				return false;
			var bestDist = -1;
			var bestLength = -1;
			foreach (var pos in lzhl.IndexesOf(context))
			{
				var dist = (pos - (i - maxDepth)) % dicsize + i - maxDepth;
				int length;
				for (length = -maxDepth; length < input.Length - i && input[i + length] == input[dist + maxDepth + length]; length++) ;
				if (i - (dist + maxDepth) >= 2 && length > bestLength)
				{
					bestDist = dist;
					bestLength = length;
				}
			}
			if (bestDist == -1)
			{
				if (buffer.IsFull)
				{
					ar.WritePart(0, notLZCount, lzCount + notLZCount);
					notLZCount++;
				}
				return false;
			}
			ar.WritePart(notLZCount, lzCount, lzCount + notLZCount);
			lzCount++;
			ar.WriteEqual((uint)(i - (bestDist + maxDepth) - 2), (uint)Min(i - maxDepth, dicsize - 1));
			ar.WriteFibonacci((uint)bestLength + 1);
			buffer.Clear();
			nextTarget = i + bestLength;
			return true;
		}
		return ar;
	}
}
