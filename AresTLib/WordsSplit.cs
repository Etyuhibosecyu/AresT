
namespace AresTLib;

internal partial class Compression
{
	private List<List<ShortIntervalList>> MakeWordsSplit(bool comb, bool fab)
	{
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 3;
		var s = AdaptEncoding(out var encoding, out var nulls, out var redundantByte);
		if (s == "")
			throw new EncoderFallbackException();
		var encoding2 = (encoding == 1) ? Encoding.Unicode : (encoding == 2) ? Encoding.UTF8 : Encoding.GetEncoding(1251);
		Current[tn] += ProgressBarStep;
		var words = DivideIntoWords(s);
		if (words.Length < 5)
			throw new EncoderFallbackException();
		Current[tn] += ProgressBarStep;
		if (comb)
		{
			var combined = words.GetSlice().Combine(words.GetSlice(1), words.GetSlice(2));
			var min = Max((int)Sqrt(Cbrt(words.Length)), 3);
			var grouped = combined.PGroup(tn).FilterInPlace(x => x.Group.Length >= min && x.Key.Item1.String != "" && x.Key.Item2.String != "" && x.Key.Item3.String != "");
			combined.Dispose();
			words.ReplaceInPlace(grouped.ToDictionary(x => x.Key, x => (G.IEnumerable<Word>)[new(x.Key.Item1.String + (x.Key.Item1.Space ? "," : x.Key.Item2.String is "," or "." ? "." : "") + x.Key.Item2.String + (x.Key.Item2.Space ? "," : x.Key.Item3.String is "," or "." ? "." : "") + x.Key.Item3.String, x.Key.Item3.Space)]));
			grouped.Dispose();
			var combined2 = words.GetSlice().Combine(words.GetSlice(1));
			min = Max((int)Sqrt(Sqrt(words.Length)), 5);
			var grouped2 = combined2.PGroup(tn).FilterInPlace(x => x.Group.Length >= min && x.Key.Item1.String != "" && x.Key.Item2.String != "" && IsSingleWord(x.Key.Item1.String) && IsSingleWord(x.Key.Item2.String));
			combined2.Dispose();
			words.ReplaceInPlace(grouped2.ToDictionary(x => x.Key, x => (G.IEnumerable<Word>)[new(x.Key.Item1.String + (x.Key.Item1.Space ? "," : x.Key.Item2.String is "," or "." ? "." : "") + x.Key.Item2.String, x.Key.Item2.Space)]));
			grouped2.Dispose();
		}
		Current[tn] += ProgressBarStep;
		Status[tn] = 0;
		StatusMaximum[tn] = 10;
		var wordsWithoutSpaces = words.PConvert(x => new Word(x.String, false));
		Status[tn]++;
		var uniqueWords = wordsWithoutSpaces.ToHashSet().ToArray();
		if (uniqueWords.Any(x => x.String.Contains(' ')))
			throw new EncoderFallbackException();
		if (words.Length < uniqueWords.Length * 3)
			throw new EncoderFallbackException();
		var uniqueWords2 = uniqueWords.PConvert(x => encoding2.GetBytes(x.String));
		var joinedWords = ProcessUnicode(uniqueWords2.JoinIntoSingle(), encoding, encoding2);
		if (fab)
			FAB(ref joinedWords, tn);
		Status[tn]++;
		var uniqueIntervals = RedStarLinq.Fill(uniqueWords.Length, index => new Interval((uint)index, (uint)uniqueWords.Length));
		var uniqueLists = uniqueIntervals.ToArray(x => new ShortIntervalList[] { [x, new Interval(0, 2)], [x, new(1, 2)] });
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
		List<List<ShortIntervalList>> result = [];
		List<Interval> c = [new(encoding, 3)];
		c.WriteCount(maxLength);
		result.Add(lengths.PConvert(x => new ShortIntervalList { new(x, maxLength + 1) }));
		Status[tn]++;
		result.Add(joinedWords.Convert(x => byteLists[x]));
		Status[tn]++;
		result.Add(indexCodes.PConvert((x, index) => uniqueLists[x][words[index].Space ? 1 : 0]));
		Status[tn]++;
		if (encoding == 2)
		{
			if (redundantByte == null)
				result.Add([[new(0, 2)]]);
			else
			{
				result.Add([[new(1, 2)]]);
				result[^1][^1].Add(new(redundantByte.Value, ValuesInByte));
			}
		}
		List<Interval> nullIntervals = [];
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
		try
		{
			var original = originalFile.Filter((x, index) => !nulls.Contains(index) && (encoding == 0 || !nulls.Contains(index - 1))).ToArray();
			var decoded = result.Wrap(tl =>
			{
				var a = 0;
				var joinedWords = new Decoding().DecodeUnicode(new Decoding().DecodeFAB(tl[1].GetSlice(1).NConvert(x => (byte)x[0].Lower), fab), encoding, encoding2).ToArray();
				var wordsList = tl[0].GetSlice(GetArrayLength(nullIntervals.Length, 8) + 2).Convert(l => encoding2.GetString(joinedWords[a..(a += (int)l[0].Lower)]));
				return encoding2.GetBytes(RedStarLinq.ToString(tl[2].GetSlice(1).ConvertAndJoin(l => wordsList[(int)l[0].Lower].Wrap(x => new Decoding().DecodeCOMB(l[1].Lower == 1 ? [.. x, ' '] : x.ToList(), comb))))).Wrap(bl => encoding == 2 && tl[3][0][0].Lower == 1 ? bl.ToNList().Add((byte)tl[3][0][1].Lower) : bl.ToNList());
			});
			for (var i = 0; i < original.Length; i++)
				if (original[i] != decoded[i])
					throw new DecoderFallbackException();
		}
		catch (Exception ex) when (ex is not DecoderFallbackException)
		{
			throw new DecoderFallbackException();
		}
#endif
		return result;
	}

	private static bool IsSingleWord(string s) => s.Length <= 2 || !s.Any(x => x is ',' or '.');

	private string AdaptEncoding(out uint encoding, out ListHashSet<int> nulls, out byte? redundantByte)
	{
		redundantByte = null;
		var threadsCount = Environment.ProcessorCount;
		int[] ansiLetters = new int[threadsCount], utf16Letters = new int[threadsCount], utf8Letters = new int[threadsCount];
		List<int>[] singleNulls = RedStarLinq.FillArray(threadsCount, _ => new List<int>()), doubleNulls = RedStarLinq.FillArray(threadsCount, _ => new List<int>());
		if (originalFile.Length == 0)
		{
			encoding = 0;
			nulls = [];
			return "";
		}
		Status[tn] = 0;
		StatusMaximum[tn] = originalFile.Length;
		if (originalFile[0] is >= 0xC0 and <= 0xFF)
			ansiLetters[0]++;
		var lockObj = RedStarLinq.FillArray(threadsCount, _ => new object());
		Status[tn]++;
		Parallel.For(1, originalFile.Length, (i, pls) =>
		{
			if (originalFile[i] == 0)
			{
				lock (lockObj[i % threadsCount]) singleNulls[i % threadsCount].Add(i);
				if (originalFile[i - 1] == 0)
					lock (lockObj[i % threadsCount]) doubleNulls[i % threadsCount].Add(i - 1);
			}
			if (originalFile[i] is >= 0xC0 and <= 0xFF)
				lock (lockObj[i % threadsCount])
					ansiLetters[i % threadsCount]++;
			else if (originalFile[i - 1] >= 0x10 && originalFile[i - 1] <= 0x4F && originalFile[i] == 0x04 || originalFile[i - 1] >= 0x20 && originalFile[i - 1] <= 0x7F && originalFile[i] == 0x00)
				lock (lockObj[i % threadsCount])
					utf16Letters[i % threadsCount]++;
			else if (originalFile[i - 1] == 0xD0 && originalFile[i] >= 0x90 && originalFile[i] <= 0xBF || originalFile[i - 1] == 0xD1 && originalFile[i] >= 0x80 && originalFile[i] <= 0x8F)
				lock (lockObj[i % threadsCount])
					utf8Letters[i % threadsCount]++;
			Status[tn]++;
		});
		int ansiLettersSum = ansiLetters.Sum(), utf16LettersSum = utf16Letters.Sum(), utf8LettersSum = utf8Letters.Sum();
		ListHashSet<int> singleNullsSum = [.. singleNulls.ConvertAndJoin(x => x)], doubleNullsSum = [.. doubleNulls.ConvertAndJoin(x => x)];
		var nullSequenceStart = -1;
		doubleNullsSum.FilterInPlace((x, index) => (index == 0 || doubleNullsSum[index - 1] != x - 1 || (index - nullSequenceStart) % 2 == 0) && (nullSequenceStart = index) >= 0);
		if (doubleNullsSum.Length * doubleNullsSum.Length * 400 >= originalFile.Length)
		{
			encoding = 0;
			nulls = [];
			return "";
		}
		if ((utf16LettersSum >= (originalFile.Length + 9) / 10 || utf16LettersSum > ansiLettersSum) && utf16LettersSum > utf8LettersSum)
		{
			encoding = 1;
			nulls = doubleNullsSum;
			return Encoding.Unicode.GetString(originalFile.Filter((x, index) => !doubleNullsSum.Contains(index) && !doubleNullsSum.Contains(index - 1)).ToArray());
		}
		else if (utf8LettersSum >= (originalFile.Length + 9) / 10 || utf8LettersSum > ansiLettersSum)
		{
			encoding = 2;
			nulls = doubleNullsSum;
			if (originalFile[^1] >= ValuesInByte >> 1)
			{
				redundantByte = originalFile[^1];
				return Encoding.UTF8.GetString(originalFile.GetSlice(..^1).Filter((x, index) => !doubleNullsSum.Contains(index) && !doubleNullsSum.Contains(index - 1)).ToArray());
			}
			return Encoding.UTF8.GetString(originalFile.Filter((x, index) => !doubleNullsSum.Contains(index) && !doubleNullsSum.Contains(index - 1)).ToArray());
		}
		else
		{
			encoding = 0;
			if (singleNulls.Length * singleNulls.Length * 400 >= originalFile.Length)
			{
				nulls = [];
				return "";
			}
			else
			{
				nulls = singleNullsSum;
				return Encoding.GetEncoding(1251).GetString(originalFile.Filter((x, index) => !singleNullsSum.Contains(index)).ToArray());
			}
		}
	}

	/// <summary>Разделяет текст на слова. Для этой цели можно было бы использовать регулярные выражения, но они потребляют слишком много ресурсов.</summary>
	private List<Word> DivideIntoWords(string text)
	{
		List<Word> outputWords = [];
		var wordStart = 0;
		var state = 0; //0 - начальное состояние, 1 - прописные буквы, 2 - строчные буквы, 3 - цифры, 4 - пробел, 5 - перевод строки #1, 6 - перевод строки #2, 7 - прочие символы.
		var space = false;
		Status[tn] = 0;
		StatusMaximum[tn] = text.Length;
		for (var i = 0; i < text.Length; i++, Status[tn]++)
		{
			if (text[i] == ' ')
			{
				if (state == 4)
				{
					outputWords.Add(new Word(text[wordStart..(i - 1)], true));
					wordStart = i;
				}
				space = true;
				state = 4;
			}
			else if (text[i] is >= 'A' and <= 'Z' or >= 'А' and <= 'Я')
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
			else
			{
				state = 7;
				outputWords.Add(new Word(text[wordStart..(i - (space ? 1 : 0))], space));
				wordStart = i;
			}
			if (text[i] != ' ')
				space = false;
		}
		outputWords.Add(new Word(text[wordStart..^(space ? 1 : 0)], space));
		return outputWords.Filter(x => x.String != "" || x.Space);
	}

	private static NList<byte> ProcessUnicode(G.IList<byte> input, uint encoding, Encoding encoding2)
	{
		if (encoding == 0 || input.Count < 2)
			return input.ToNList();
		NList<byte> result = new(input.Count);
		if (encoding == 1)
		{
			for (var i = 0; i < input.Count; i += 2)
			{
				if (i == input.Count - 1)
					result.Add(input[i]);
				if (input[i] < ValuesInByte >> 1 && input[i + 1] == 0)
					result.Add(input[i]);
				else if (UnicodeDic[0].TryGetValue((input[i], input[i + 1]), out var value))
					result.Add(value);
				else
					result.AddRange([0, input[i], input[i + 1]]);
			}
			return result;
		}
		if (encoding != 2)
			return input.ToNList();
		var prev = -1;
		for (var i = 0; i < input.Count; i++)
		{
			if (input[i] < ValuesInByte >> 1)
				result.Add(input[i]);
			else if (prev != -1)
			{
				if (UnicodeDic[1].TryGetValue(((byte)prev, input[i]), out var value))
					result.Add(value);
				else
					result.AddRange([0, (byte)prev, input[i]]);
				prev = -1;
			}
			else if (input[i] is >= 0b11000000 and <= 0b11011111)
				prev = input[i];
			else if (input[i] is >= 0b10000000 and <= 0b10111111)
				result.Add(input[i]);
			else
				result.AddRange([0, input[i]]);
		}
		return result;
	}

	private static void FAB(ref NList<byte> joinedWords, int tn)
	{
		using var extraBytes = new Chain(ValuesInByte).Convert(x => (byte)x).ToHashSet().ExceptWith(joinedWords);
		var combined = joinedWords.GetSlice().NCombine(joinedWords.GetSlice(1), joinedWords.GetSlice(2));
		var min = Max((int)Cbrt(joinedWords.Length), 10);
		var grouped = combined.PGroup(tn).FilterInPlace(x => x.Group.Length >= min).NSort(x => (uint)(0xffffffff - x.Group.Length));
		var groupedPart = grouped.Take(extraBytes.Length);
		combined.Dispose();
		int index = 0, index2 = 0;
		joinedWords = [(byte)groupedPart.Length, .. groupedPart.ConvertAndJoin(x => (byte[])[extraBytes[index++], x.Key.Item1, x.Key.Item2, x.Key.Item3]), .. joinedWords.Replace(groupedPart.ToDictionary(x => x.Key, x => (G.IEnumerable<byte>)[extraBytes[index2++]]))];
		groupedPart.Dispose();
		if (extraBytes.Length != groupedPart.Length)
		{
			if (joinedWords.Length < groupedPart.Length * 4 + 3)
				return;
			var first = extraBytes[0];
			extraBytes.Remove(0, groupedPart.Length);
			var combined2 = joinedWords.GetSlice(groupedPart.Length * 4 + 1).Combine(joinedWords.GetSlice(groupedPart.Length * 4 + 2));
			min = Max((int)Sqrt(joinedWords.Length), 30);
			var grouped2 = combined2.PGroup(tn).FilterInPlace(x => x.Group.Length >= min).Take(extraBytes.Length);
			combined2.Dispose();
			index = index2 = 0;
			joinedWords = [.. joinedWords.GetRange(0, groupedPart.Length * 4 + 1), ValuesInByte - 1, .. grouped2.ConvertAndJoin(x => (byte[])[extraBytes[index++], x.Key.Item1, x.Key.Item2]), first, .. joinedWords.GetRange(groupedPart.Length * 4 + 1).Replace(grouped2.ToDictionary(x => x.Key, x => (G.IEnumerable<byte>)[extraBytes[index2++]]))];
			grouped2.Dispose();
		}
		//else if (grouped.Length != groupedPart.Length)
		//{
		//	using var extraBytes2 = joinedWords.PGroup(tn).NConvert(x => (x.Key, x.Group.Length)).Sort(x => (uint)x.Length).FilterInPlace(x => !extraBytes.Contains(x.Key)).TakeWhile((x, index) => x.Length < grouped[extraBytes.Length + index].Group.Length);
		//	joinedWords = [.. joinedWords.GetRange(0, groupedPart.Length * 4 + 1), (byte)extraBytes2.Length, .. grouped.GetSlice(extraBytes.Length, extraBytes2.Length).ConvertAndJoin((x, index) => (byte[])[extraBytes2[index].Key, x.Key.Item1, x.Key.Item2, x.Key.Item3]), .. joinedWords.GetRange(groupedPart.Length * 4 + 1).Replace(extraBytes2.ToNList().Add((extraBytes[0], 0)).ToDictionary(x => x.Key, x => (G.IEnumerable<byte>)[extraBytes[0], x.Key])).Replace(extraBytes2.ToDictionary(x => grouped[index2++].Key, x => (G.IEnumerable<byte>)[x.Key]))];
		//}
		else
			joinedWords.Insert(groupedPart.Length * 4 + 1, [ValuesInByte - 1, extraBytes[0]]);
	}
}
