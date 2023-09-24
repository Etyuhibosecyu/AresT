
namespace AresTLib;

internal partial class Compression
{
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
		if (!(input.Length >= 4 && input[CreateVar(input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base + 1 : 1, out var startPos)].Length is 1 or 2 && input[startPos][0].Length == 1 && CreateVar(input[startPos][0].Base, out var inputBase) >= 2 && input[startPos][^1].Length == 1 && input.GetSlice(startPos + 1).All(x => x.Length == input[startPos].Length && x[0].Length == 1 && x[0].Base == inputBase && (x.Length == 1 || x[1].Length == 1 && x[1].Base == input[startPos][1].Base))))
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
		ar.WriteCount((uint)Min(LZDictionarySize, FragmentLength));
		SumSet<uint> globalSet = new(), newItemsSet = n == 2 ? new() : new(new Chain((int)inputBase).Convert(x => ((uint)x, 1)));
		var maxDepth = inputBase == 2 ? 96 : 12;
		LimitedQueue<List<Interval>> buffer = new(maxDepth);
		var comparer = n == 2 ? (G.IEqualityComparer<NList<uint>>)new NListEComparer<uint>() : new EComparer<NList<uint>>((x, y) => x.Equals(y), x => (int)x.Progression((uint)x.Length, (x, y) => (x << 7 | x >> BitsPerInt - 7) ^ (uint)y.GetHashCode()));
		FastDelHashSet<NList<uint>> contextHS = new(comparer);
		HashList<NList<uint>> lzhl = new(comparer);
		List<SumSet<uint>> sumSets = new();
		SumList lzLengthsSL = new() { 1 };
		uint lzCount = 1, notLZCount = 1, spaceCount = 1, notSpaceCount = 1;
		LimitedQueue<bool> spaceBuffer = new(maxDepth);
		LimitedQueue<uint> newItemsBuffer = new(maxDepth);
		NList<uint> context = new(maxDepth), context2 = new(maxDepth);
		SumSet<uint>? set = new(), excludingSet = new();
		List<Interval> intervalsForBuffer = new();
		var nextTarget = 0;
		for (var i = startPos; i < input.Length; i++, Status[tn]++)
		{
			var item = input[i][0].Lower;
			input.GetSlice(Max(startPos, i - maxDepth)..i).ForEach((x, index) => context.SetOrAdd(index, x[0].Lower));
			context.Reverse();
			context2.Replace(context);
			if (i < nextTarget)
				goto l1;
			var index = -1;
			intervalsForBuffer.Clear();
			if (context.Length == maxDepth && i >= (maxDepth << 1) + startPos && ProcessLZ(context, item, i) && i < nextTarget)
				goto l1;
			set.Clear();
			excludingSet.Clear();
			for (; context.Length > 0 && !contextHS.TryGetIndexOf(context, out index); context.RemoveAt(^1)) ;
			long sum = 0;
			var frequency = 0;
			for (; context.Length > 0 && contextHS.TryGetIndexOf(context, out index) && (sum = set.Replace(sumSets[index]).ExceptWith(excludingSet).GetLeftValuesSum(item, out frequency)) >= 0 && frequency == 0; context.RemoveAt(^1), excludingSet.UnionWith(set))
				if (set.Length != 0)
					intervalsForBuffer.Add(new((uint)set.ValuesSum, (uint)set.Length * 100, (uint)(set.ValuesSum + set.Length * 100)));
			if (set.Length == 0 || context.Length == 0)
				set.Replace(globalSet).ExceptWith(excludingSet);
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
		l1:
			if (context2.Length == maxDepth)
				lzhl.SetOrAdd((i - startPos - maxDepth) % LZDictionarySize, context2.Copy());
			Increase(context2, context, item);
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
		var comparer = new EComparer<BitList>((x, y) => x.Equals(y), x => x.Length == 0 ? 1234567890 : x.ToUIntList().Progression(0, (x, y) => x << 7 ^ (int)y));
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
