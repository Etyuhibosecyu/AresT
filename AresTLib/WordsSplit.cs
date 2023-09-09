
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
			var wordsList = tl[0].GetSlice(GetArrayLength(nullIntervals.Length, 8) + 2).Convert(l => encoding2.GetString(tl[1].GetSlice(1)[a..(a += (int)l[0].Lower)].ToArray(x => (byte)x[0].Lower)));
			return encoding2.GetBytes(tl[2].GetSlice(1).ConvertAndJoin(l => wordsList[(int)l[0].Lower].Wrap(x => l[1].Lower == 1 ? new List<char>(x).Add(' ') : x.ToList())).ToArray());
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
}
