
namespace AresTLib;

public class AdaptiveHuffmanDecT
{
	private readonly DecodingT decoding = default!;
	protected ArithmeticDecoder ar = default!;
	protected List<ShortIntervalList> result = default!;
	protected NList<uint> skipped = default!;
	protected SumSet<uint> set = default!, newItems = default!;
	protected NList<Interval> uniqueList = default!;
	protected LZData lzData = default!;
	protected uint fileBase, nextWordLink;
	protected int lz, bwt, blockIndex, fullLength, bwtBlockSize, bwtBlockExtraSize, counter;
	protected SumList lengthsSL, distsSL;
	protected int lzLength;
	protected uint firstIntervalDist;

	public AdaptiveHuffmanDecT(DecodingT decoding, ArithmeticDecoder ar, NList<uint> skipped, LZData lzData, int lz, int bwt, int blockIndex, int bwtBlockSize, int counter)
	{
		this.decoding = decoding;
		this.ar = ar;
		this.skipped = skipped;
		this.lzData = lzData;
		this.lz = lz;
		this.bwt = bwt;
		this.blockIndex = blockIndex;
		this.bwtBlockSize = bwtBlockSize;
		bwtBlockExtraSize = bwtBlockSize <= 0x4000 ? 2 : bwtBlockSize <= 0x400000 ? 3 : bwtBlockSize <= 0x40000000 ? 4 : 5;
		this.counter = counter;
		Prerequisites();
		lengthsSL = lz != 0 ? new(RedStarLinq.Fill(1, (int)(lzData.Length.R == 0 ? lzData.Length.Max + 1 : lzData.Length.R == 1 ? lzData.Length.Threshold + 2 : lzData.Length.Max - lzData.Length.Threshold + 2))) : new();
		distsSL = lz != 0 ? new(RedStarLinq.Fill(1, (int)lzData.UseSpiralLengths + 1)) : new();
		firstIntervalDist = lz != 0 ? (lzData.Dist.R == 1 ? lzData.Dist.Threshold + 2 : lzData.Dist.Max + 1) + lzData.UseSpiralLengths : 0;
	}

	public virtual List<ShortIntervalList> Decode()
	{
		Prerequisites2();
		for (; counter > 0; counter--, Status[0]++)
			DecodeIteration();
		Current[0] += ProgressBarStep;
		return Postrequisites();
	}

	protected virtual void Prerequisites()
	{
		if (bwt != 0 && blockIndex != 0)
			DecodeSkipped();
		fileBase = ar.ReadCount();
		if (counter < 0 || counter > decoding.GetFragmentLength() + (bwt == 0 ? 0 : decoding.GetFragmentLength() >> 8))
			throw new DecoderFallbackException();
		Status[0] = 0;
		StatusMaximum[0] = counter;
		set = [(uint.MaxValue, 1)];
	}

	private void DecodeSkipped()
	{
		var skippedCount = (int)ar.ReadCount();
		var @base = skippedCount == 0 ? 1 : ar.ReadCount();
		newItems = new(RedStarLinq.NFill((int)@base, index => ((uint)index, 1)));
		if (skippedCount > @base || @base > decoding.GetFragmentLength())
			throw new DecoderFallbackException();
		for (var i = 0; i < skippedCount; i++)
		{
			skipped.Add(newItems[ar.ReadPart(newItems)].Key);
			newItems.RemoveValue(skipped[^1]);
		}
		counter -= skippedCount == 0 ? 1 : (skippedCount + 9) / 8;
	}

	protected virtual void Prerequisites2()
	{
		uniqueList = [];
		if (lz != 0)
		{
			set.Add((fileBase - 1, 1));
			uniqueList.Add(new(fileBase - 1, fileBase));
		}
		result = [];
	}

	protected virtual void DecodeIteration()
	{
		var readIndex = ReadFirst();
		if (!(lz != 0 && uniqueList[readIndex].Lower == fileBase - 1))
		{
			result.Add(bwt == 0 && blockIndex == 2 ? new() { uniqueList[readIndex], new(ar.ReadEqual(2), 2) } : bwt != 0 && blockIndex != 0 && result.Length < bwtBlockExtraSize ? [new((uint)readIndex, ValuesInByte)] : new() { uniqueList[readIndex] });
			lzLength++;
			if (lz != 0 && distsSL.Length < firstIntervalDist)
				distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1);
			return;
		}
		decoding.ProcessLZLength(lzData, lengthsSL, out readIndex, out var length);
		decoding.ProcessLZDist(lzData, distsSL, result.Length, out readIndex, out var dist, length, out var maxDist);
		if (decoding.ProcessLZSpiralLength(lzData, ref dist, out var spiralLength, maxDist))
			dist = 0;
		var start = (int)(result.Length - dist - length - 2);
		if (start < 0)
			throw new DecoderFallbackException();
		for (var k = (int)((length + 2) * (spiralLength + 1)); k > 0; k -= (int)length + 2)
			result.AddRange(result.GetSlice(start, (int)Min(length + 2, k)));
		lzLength++;
		if (lz != 0 && distsSL.Length < firstIntervalDist)
			new Chain((int)Min(firstIntervalDist - distsSL.Length, (length + 2) * (spiralLength + 1))).ForEach(x => distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1));
	}

	protected virtual int ReadFirst()
	{
		if (bwt != 0 && blockIndex != 0 && result.Length < bwtBlockExtraSize)
			return (int)ar.ReadEqual(ValuesInByte);
		var readIndex = ar.ReadPart(set);
		if (readIndex == set.Length - 1)
			readIndex = ReadNewItem();
		else
			set.Increase(uniqueList[readIndex].Lower);
		FirstUpdateSet();
		return readIndex;
	}

	private int ReadNewItem()
	{
		uint actualIndex;
		if (bwt != 0 && blockIndex == 2)
		{
			actualIndex = newItems[ar.ReadPart(newItems)].Key;
			newItems.RemoveValue(actualIndex);
		}
		else
			actualIndex = blockIndex != 2 ? ar.ReadEqual(fileBase) : nextWordLink++;
		if (!set.TryAdd((actualIndex, 1), out var readIndex))
			throw new DecoderFallbackException();
		uniqueList.Insert(readIndex, new Interval(actualIndex, fileBase));
		return readIndex;
	}

	protected virtual void FirstUpdateSet() => set.Update(uint.MaxValue, Max(set.Length - 1, 1));

	protected virtual List<ShortIntervalList> Postrequisites() => bwt != 0 && blockIndex == 2 ? result.ToList((x, index) => index < bwtBlockExtraSize ? x : new ShortIntervalList() { new(x[0].Lower / 2, fileBase / 2), new(x[0].Lower % 2, 2) }) : result;
}
