
namespace AresTLib;

internal record class PPM(int TN)
{
	private const int LZDictionarySize = 8388607;
	private ArithmeticEncoder ar = default!;
	private readonly List<ShortIntervalList> input = new();
	private int startPos = 1, n = 1;
	private readonly SumSet<uint> globalSet = new(), newItemsSet = new();
	private const int maxDepth = 12;
	private readonly LimitedQueue<List<Interval>> buffer = new(maxDepth);
	private G.IEqualityComparer<NList<uint>> comparer = default!;
	private FastDelHashSet<NList<uint>> contextHS = default!;
	private HashList<NList<uint>> lzhl = default!;
	private readonly List<SumSet<uint>> sumSets = new();
	private readonly SumList lzLengthsSL = new();
	private uint lzCount, notLZCount, spaceCount, notSpaceCount;
	private readonly LimitedQueue<bool> spaceBuffer = new(maxDepth);
	private readonly LimitedQueue<uint> newItemsBuffer = new(maxDepth);
	private readonly NList<uint> context = new(maxDepth), context2 = new(maxDepth);
	private readonly SumSet<uint> set = new(), excludingSet = new();
	private readonly List<Interval> intervalsForBuffer = new();
	private int nextTarget = 0;

	public byte[] Encode(List<ShortIntervalList> input)
	{
		if (input.Length < 4)
			throw new EncoderFallbackException();
		ar = new();
		this.input.Replace(input);
		if (!PPMInternal())
			throw new EncoderFallbackException();
		ar.WriteEqual(1234567890, 4294967295);
		return ar;
	}

	public byte[] Encode(List<List<ShortIntervalList>> input)
	{
		if (!(input.Length == 3 && input.All(x => x.Length >= 4)))
			throw new EncoderFallbackException();
		Current[TN] = 0;
		CurrentMaximum[TN] = ProgressBarStep * 2;
		ar = new();
		for (var i = 0; i < input.Length; i++)
		{
			this.input.Replace(input[i]);
			n = i;
			if (!PPMInternal())
				throw new EncoderFallbackException();
			if (i != input.Length - 1)
				Current[TN] += ProgressBarStep;
		}
		ar.WriteEqual(1234567890, 4294967295);
		return ar;
	}

	private bool PPMInternal()
	{
		if (!(input.Length >= 4 && input[CreateVar(input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base + 1 : 1, out startPos)].Length is 1 or 2 && input[startPos][0].Length == 1 && CreateVar(input[startPos][0].Base, out var inputBase) >= 2 && input[startPos][^1].Length == 1 && input.GetSlice(startPos + 1).All(x => x.Length == input[startPos].Length && x[0].Length == 1 && x[0].Base == inputBase && (x.Length == 1 || x[1].Length == 1 && x[1].Base == input[startPos][1].Base))))
			throw new EncoderFallbackException();
		Status[TN] = 0;
		StatusMaximum[TN] = input.Length - startPos;
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
		ar.WriteCount((uint)Min(LZDictionarySize, FragmentLength));
		PrepareFields(inputBase);
		for (var i = startPos; i < input.Length; i++, Status[TN]++)
		{
			var item = input[i][0].Lower;
			input.GetSlice(Max(startPos, i - maxDepth)..i).ForEach((x, index) => context.SetOrAdd(index, x[0].Lower));
			context.Reverse();
			context2.Replace(context);
			if (i < nextTarget)
				goto l1;
			intervalsForBuffer.Clear();
			if (context.Length == maxDepth && i >= (maxDepth << 1) + startPos && ProcessLZ(context, i) && i < nextTarget)
				goto l1;
			set.Clear();
			excludingSet.Clear();
			Escape(item, out var sum, out var frequency);
			ProcessFrequency(item, ref sum, ref frequency);
			ProcessBuffers(i);
		l1:
			if (context2.Length == maxDepth)
				lzhl.SetOrAdd((i - startPos - maxDepth) % LZDictionarySize, context2.Copy());
			Increase(context2, context, item);
		}
		while (buffer.Length != 0)
			buffer.Dequeue().ForEach(x => ar.WritePart(x.Lower, x.Length, x.Base));
		return true;
	}

	private void PrepareFields(uint inputBase)
	{
		globalSet.Clear();
		if (n == 2)
			newItemsSet.Clear();
		else
			newItemsSet.Replace(new Chain((int)inputBase).Convert(x => ((uint)x, 1)));
		buffer.Clear();
		comparer = n == 2 ? new NListEComparer<uint>() : new EComparer<NList<uint>>((x, y) => x.Equals(y), x => (int)x.Progression((uint)x.Length, (x, y) => (x << 7 | x >> BitsPerInt - 7) ^ (uint)y.GetHashCode()));
		contextHS = new(comparer);
		lzhl = new(comparer);
		sumSets.Clear();
		lzLengthsSL.Replace(new[] { 1 });
		lzCount = notLZCount = spaceCount = notSpaceCount = 1;
		spaceBuffer.Clear();
		newItemsBuffer.Clear();
		context.Clear();
		context2.Clear();
		set.Clear();
		excludingSet.Clear();
		intervalsForBuffer.Clear();
		nextTarget = 0;
	}

	private void Escape(uint item, out long sum, out int frequency)
	{
		for (; context.Length > 0 && !contextHS.TryGetIndexOf(context, out _); context.RemoveAt(^1)) ;
		sum = 0;
		frequency = 0;
		for (; context.Length > 0 && contextHS.TryGetIndexOf(context, out var index) && (sum = set.Replace(sumSets[index]).ExceptWith(excludingSet).GetLeftValuesSum(item, out frequency)) >= 0 && frequency == 0; context.RemoveAt(^1), excludingSet.UnionWith(set))
			if (set.Length != 0)
				intervalsForBuffer.Add(new((uint)set.ValuesSum, (uint)set.Length * 100, (uint)(set.ValuesSum + set.Length * 100)));
		if (set.Length == 0 || context.Length == 0)
			set.Replace(globalSet).ExceptWith(excludingSet);
	}

	private void ProcessFrequency(uint item, ref long sum, ref int frequency)
	{
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
	}

	private void ProcessBuffers(int i)
	{
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
		buffer.Enqueue(intervalsForBuffer.Copy());
		if (n == 2 && spaceBuffer.IsFull)
		{
			var space2 = spaceBuffer.Dequeue();
			if (space2)
				spaceCount++;
			else
				notSpaceCount++;
		}
		spaceBuffer.Enqueue(isSpace);
	}

	bool ProcessLZ(NList<uint> context, int curPos)
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
		if (bestLength < lzLengthsSL.Length - 1)
		{
			ar.WritePart((uint)lzLengthsSL.GetLeftValuesSum(bestLength, out var frequency), (uint)frequency, (uint)lzLengthsSL.ValuesSum);
			lzLengthsSL.Increase(bestLength);
		}
		else
		{
			ar.WritePart((uint)(lzLengthsSL.ValuesSum - lzLengthsSL[^1]), (uint)lzLengthsSL[^1], (uint)lzLengthsSL.ValuesSum);
			lzLengthsSL.Increase(lzLengthsSL.Length - 1);
			ar.WriteFibonacci((uint)(bestLength - lzLengthsSL.Length + 2));
			new Chain(bestLength - lzLengthsSL.Length + 1).ForEach(x => lzLengthsSL.Insert(lzLengthsSL.Length - 1, 1));
		}
		buffer.Clear();
		spaceBuffer.Clear();
		if (n != 2)
			newItemsBuffer.Filter(x => x != uint.MaxValue).ForEach(x => newItemsSet.Add((x, 1)));
		newItemsBuffer.Clear();
		nextTarget = curPos + bestLength;
		return true;
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
}

internal record class PPMBits(int TN)
{
	private const int LZDictionarySize = 8388607;

	public byte[] Encode(BitList input)
	{
		Status[TN] = 0;
		StatusMaximum[TN] = input.Length;
		ArithmeticEncoder ar = new();
		ar.WriteCount((uint)input.Length);
		var dicsize = LZDictionarySize << 3;
		ar.WriteCount((uint)dicsize);
		(uint Zeros, uint Units) globalSet = (1, 1);
		var maxDepth = 96;
		LimitedQueue<List<Interval>> buffer = new(maxDepth);
		var comparer = new EComparer<BitList>((x, y) => x.Equals(y), x => x.Length == 0 ? 1234567890 : x.ToUIntList().Progression(0, (x, y) => x << 7 ^ (int)y));
		FastDelHashSet<BitList> contextHS = new(comparer);
		HashList<BitList> lzhl = new(comparer);
		List<(uint Zeros, uint Units)> sumSets = new();
		uint lzCount = 1, notLZCount = 1;
		var nextTarget = 0;
		for (var i = 0; i < input.Length; i++, Status[TN]++)
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
		return ar;
	}
}
