﻿
namespace AresTLib005;

public class PPM
{
	protected ArithmeticDecoder ar = default!;
	protected uint inputBase, dicsize;
	protected uint counter, nextWordLink;
	protected int n;
	protected List<ShortIntervalList> result = default!;
	protected SumSet<uint> globalSet = default!, newItemsSet = default!;
	protected int maxDepth = default!;
	protected FastDelHashSet<NList<uint>> contextHS = default!;
	protected List<SumSet<uint>> sumSets = default!;
	protected List<uint> preLZMap = default!, spacesMap = default!;
	protected Decoding decoding = default!;

	protected PPM() { }

	public PPM(Decoding decoding, ArithmeticDecoder ar, uint inputBase, int n = -1)
	{
		this.decoding = decoding;
		this.ar = ar;
		this.inputBase = inputBase;
		this.n = n;
		counter = ar.ReadCount();
		dicsize = ar.ReadCount();
		if (counter < 0 || counter > decoding.GetFragmentLength() || dicsize < 0 || dicsize > decoding.GetFragmentLength())
			throw new DecoderFallbackException();
		Status[0] = 0;
		StatusMaximum[0] = (int)counter;
		result = new();
		globalSet = new();
		newItemsSet = n == 2 ? new() : new(new Chain((int)inputBase).Convert(x => ((uint)x, 1)));
		maxDepth = 12;
		var comparer = n == 2 ? (G.IEqualityComparer<NList<uint>>)new NListEComparer<uint>() : new EComparer<NList<uint>>((x, y) => x.Equals(y), x => (int)x.Progression((uint)x.Length, (x, y) => (x << 7 | x >> BitsPerInt - 7) ^ (uint)y.GetHashCode()));
		contextHS = new(comparer);
		sumSets = new();
		preLZMap = new(2, 1, 2);
		spacesMap = new(2, 1, 2);
		nextWordLink = 0;
	}

	public virtual List<ShortIntervalList> Decode()
	{
		NList<uint> context = new(maxDepth);
		NList<uint> context2 = new(maxDepth);
		SumSet<uint> set = new();
		SumSet<uint> excludingSet = new();
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
				excludingSet.IntersectWith(globalSet).ForEach(x => excludingSet.Update(x.Key, globalSet.TryGetValue(x.Key, out var newValue) ? newValue : throw new EncoderFallbackException()));
				var set2 = globalSet.ExceptWith(excludingSet);
				if (set2.Length != 0 && (arithmeticIndex = ar.ReadPart(new List<uint>(2, (uint)set2.ValuesSum, (uint)(set2.ValuesSum + set2.Length * 100)))) != 1)
				{
					if (set2.Length != 0) arithmeticIndex = ar.ReadPart(set2);
					item = set2[arithmeticIndex].Key;
				}
				else if (n == 2)
					item = nextWordLink++;
				else
				{
					item = newItemsSet[ar.ReadPart(newItemsSet)].Key;
					newItemsSet.RemoveValue(item);
				}
				globalSet.UnionWith(excludingSet);
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
			var length = PPMLZProcessLength();
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

	protected virtual uint PPMLZProcessLength()
	{
		if (!ar.ReadFibonacci(out var length) || length + maxDepth - 1 > counter)
			throw new DecoderFallbackException();
		return length;
	}
}
