
using Mpir.NET;

namespace AresTLib;

internal class BWTT(List<ShortIntervalList> Input, List<ShortIntervalList> Result, int TN)
{
	public List<ShortIntervalList> Encode(bool words = false)
	{
		if (Input.Length == 0)
			throw new EncoderFallbackException();
		if (Input[0].Contains(HuffmanApplied) || Input[0].Contains(BWTApplied))
			return Input;
		Current[TN] = 0;
		CurrentMaximum[TN] = ProgressBarStep * 2;
		var lz = CreateVar(Input[0].IndexOf(LempelZivApplied), out var lzIndex) != -1;
		var startPos = (lz ? (Input[0].Length >= lzIndex + BWTBlockExtraSize && Input[0][lzIndex + 1] == LempelZivSubdivided ? 3 : 2) : 1) + (Input[0].Length >= 1 && Input[0][0] == LengthsApplied ? (int)Input[0][1].Base : 0);
		var wordsApplied = Input[startPos].Length > 1;
		if (!(Input.Length >= startPos + BWTBlockExtraSize))
			throw new EncoderFallbackException();
		Result.Replace(Input.GetSlice(0, startPos));
		if (!wordsApplied)
			Result[0] = new(Result[0]) { new((uint)BitsCount((uint)BWTBlockSize) - 14, 18) };
		Status[TN] = 0;
		StatusMaximum[TN] = 7;
		var byteInput = Input.GetSlice(startPos).ToNList(x => wordsApplied ? x[0].Lower * 2 + x[1].Lower : x[0].Lower);
		Status[TN]++;
		var uniqueElems = byteInput.ToHashSet();
		Status[TN]++;
		var uniqueElems2 = uniqueElems.ToNList().Sort();
		Status[TN]++;
		var inputPos = startPos;
		NList<uint> byteResult;
		Status[TN] = 0;
		StatusMaximum[TN] = byteInput.Length;
		Current[TN] += ProgressBarStep;
		byteResult = byteInput.Copy().AddRange(new uint[GetArrayLength(byteInput.Length, BWTBlockSize) * BWTBlockExtraSize]);
		BWTInternal();
		Status[TN] = 0;
		StatusMaximum[TN] = byteResult.Length;
		Current[TN] += ProgressBarStep;
		byteInput.Clear();
		for (var i = 0; i < byteResult.Length; i += BWTBlockSize)
			byteInput.AddRange(byteResult.GetRange(i..(i += BWTBlockExtraSize))).AddRange(ZLE(byteResult.Skip(i).Take(BWTBlockSize), byteInput.GetRange(^BWTBlockExtraSize..), uniqueElems2[0]));
		uniqueElems2 = byteResult.Filter((x, index) => index % (BWTBlockSize + BWTBlockExtraSize) >= BWTBlockExtraSize).ToHashSet().ToNList().Sort();
		Result.AddRange(byteInput.Convert((x, index) => new ShortIntervalList() { new(x, index >= BWTBlockExtraSize && wordsApplied ? Input[startPos][0].Base * 2 : ValuesInByte) }));
		Result[0].Add(BWTApplied);
		uniqueElems.ExceptWith(uniqueElems2);
#if DEBUG
		var input2 = Input.Skip(startPos);
		var decoded = new DecodingT().DecodeBWT(Result.GetRange(startPos), [.. uniqueElems], BWTBlockSize);
		for (var i = 0; i < input2.Length && i < decoded.Length; i++)
			for (var j = 0; j < input2[i].Length && j < decoded[i].Length; j++)
			{
				var x = input2[i][j];
				var y = decoded[i][j];
				if (!(x.Equals(y) || GetBaseWithBuffer(x.Base, words) == y.Base && x.Lower == y.Lower && x.Length == y.Length))
					throw new DecoderFallbackException();
			}
		if (input2.Length != decoded.Length)
			throw new DecoderFallbackException();
#endif
		var c = uniqueElems.PConvert(x => new Interval(x, wordsApplied ? Input[startPos][0].Base * 2 : ValuesInByte));
		c.Insert(0, GetCountList((uint)uniqueElems.Length));
		var cSplit = c.SplitIntoEqual(8);
		c.Dispose();
		var cLength = (uint)cSplit.Length;
		Result[0].Add(new(0, cLength, cLength));
		Result.Insert(startPos, cSplit.PConvert(x => new ShortIntervalList(x)));
		cSplit.Dispose();
		return Result;
		void BWTInternal()
		{
			var buffer = RedStarLinq.FillArray(Environment.ProcessorCount, index => byteInput.Length < BWTBlockSize * (index + 1) ? default! : GC.AllocateUninitializedArray<uint>(BWTBlockSize * 2 - 1));
			var currentBlock = RedStarLinq.FillArray(buffer.Length, index => byteInput.Length < BWTBlockSize * (index + 1) ? default! : GC.AllocateUninitializedArray<uint>(BWTBlockSize));
			var indexes = RedStarLinq.FillArray(buffer.Length, index => byteInput.Length < BWTBlockSize * (index + 1) ? default! : GC.AllocateUninitializedArray<int>(BWTBlockSize));
			var tasks = new Task[buffer.Length];
			var MTFMemory = RedStarLinq.FillArray<uint[]>(buffer.Length, _ => default!);
			for (var i = 0; i < GetArrayLength(byteInput.Length, BWTBlockSize); i++)
			{
				tasks[i % buffer.Length]?.Wait();
				int i2 = i * BWTBlockSize, length = Min(BWTBlockSize, byteInput.Length - i2);
				MTFMemory[i % buffer.Length] = [.. uniqueElems2];
				if (byteInput.Length - i2 < BWTBlockSize)
				{
					buffer[i % buffer.Length] = default!;
					currentBlock[i % buffer.Length] = default!;
					indexes[i % buffer.Length] = default!;
					GC.Collect();
					buffer[i % buffer.Length] = GC.AllocateUninitializedArray<uint>((byteInput.Length - i2) * 2 - 1);
					currentBlock[i % buffer.Length] = GC.AllocateUninitializedArray<uint>(byteInput.Length - i2);
					indexes[i % buffer.Length] = GC.AllocateUninitializedArray<int>(byteInput.Length - i2);
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
					byteResult[(BWTBlockSize + BWTBlockExtraSize) * blockIndex + i] = unchecked((byte)firstPermutation);
					firstPermutation >>= BitsPerByte;
				}
				WriteToMTF(blockIndex);
			}
			void GetBWT(uint[] source, uint[] buffer, int[] indexes, uint[] Result, ref int firstPermutation)
			{
				CopyMemory(source, 0, buffer, 0, source.Length);
				CopyMemory(source, 0, buffer, source.Length, source.Length - 1);
				for (var i = 0; i < indexes.Length; i++)
					indexes[i] = i;
				var indexesToSort = buffer.BWTCompare(source.Length);
				foreach (var index in indexesToSort)
				{
					indexes.NSort(x => buffer[index + x]);
					Status[TN] += (int)Floor((double)source.Length / indexesToSort.Length);
				}
				indexesToSort.Dispose();
#if DEBUG
				if (indexes.ToHashSet().Length != indexes.Length)
					throw new InvalidOperationException();
#endif
				firstPermutation = Array.IndexOf(indexes, 0);
				// Копирование результата
				for (var i = 0; i < source.Length; i++)
					Result[i] = buffer[indexes[i] + indexes.Length - 1];
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
					Status[TN]++;
				}
			}
		}
	}

	private Slice<uint> ZLE(Slice<uint> input, NList<uint> firstPermutationRange, uint zero)
	{
		var frequency = new int[input.Max() + 1];
		for (var i = 0; i < input.Length; i++)
			frequency[input[i]]++;
		var zeroB = (uint)Array.IndexOf(frequency, 0);
		if (zeroB == uint.MaxValue)
			zeroB = input.Max() + 1;
		var Result = new NList<uint>(input.Length + 2) { zero, zeroB };
		for (var i = 0; i < input.Length;)
		{
			while (i < input.Length && input[i] != zero)
			{
				Result.Add(input[i++]);
				Status[TN]++;
			}
			if (i >= input.Length)
				break;
			var j = i;
			while (i < input.Length && input[i] == zero)
			{
				i++;
				Status[TN]++;
			}
			if (i == j)
				throw new EncoderFallbackException();
			Result.AddRange(((MpzT)(i - j + 1)).ToString(2)?.Skip(1).ToArray(x => x == '1' ? zeroB : x == '0' ? zero : throw new EncoderFallbackException()));
		}
		if (Result.Length < input.Length * 0.936)
		{
			firstPermutationRange[0] |= ValuesInByte >> 1;
			return Result.GetSlice();
		}
		else
			return input;
	}
}
