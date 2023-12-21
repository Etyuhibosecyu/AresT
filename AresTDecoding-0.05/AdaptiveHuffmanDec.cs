
namespace AresTLib005;

public class AdaptiveHuffmanDec
{
	private readonly Decoding decoding = default!;
	protected ArithmeticDecoder ar = default!;
	protected List<ShortIntervalList> result = default!;
	protected List<byte> skipped = default!;
	protected SumSet<uint> set = default!;
	protected List<Interval> uniqueList = default!;
	protected LZData lzData = default!;
	protected uint fileBase, nextWordLink;
	protected int lz, bwt, n, fullLength, counter;
	protected bool hfw;

	protected AdaptiveHuffmanDec() { }

	public AdaptiveHuffmanDec(Decoding decoding, ArithmeticDecoder ar, List<byte> skipped, LZData lzData, int lz, int bwt, int n, int counter, bool hfw)
	{
		this.decoding = decoding;
		this.ar = ar;
		this.skipped = skipped;
		this.lzData = lzData;
		this.lz = lz;
		this.bwt = bwt;
		this.n = n;
		this.counter = counter;
		this.hfw = hfw;
		Prerequisites();
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
		if (bwt != 0 && !(hfw && n != 1))
		{
			var skippedCount = (int)ar.ReadCount();
			var @base = skippedCount == 0 ? 1 : ar.ReadCount();
			if (skippedCount > @base || @base > decoding.GetFragmentLength())
				throw new DecoderFallbackException();
			for (var i = 0; i < skippedCount; i++)
				skipped.Add((byte)ar.ReadEqual(@base));
			counter -= skippedCount == 0 ? 1 : (skippedCount + 9) / 8;
		}
		fileBase = ar.ReadCount();
		if (counter < 0 || counter > decoding.GetFragmentLength() + (bwt == 0 ? 0 : decoding.GetFragmentLength() >> 8))
			throw new DecoderFallbackException();
		Status[0] = 0;
		StatusMaximum[0] = counter;
		set = [(uint.MaxValue, 1)];
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
			result.Add(n == 2 ? new() { uniqueList[readIndex], new(ar.ReadEqual(2), 2) } : new() { uniqueList[readIndex] });
			fullLength++;
			return;
		}
		result.Add([uniqueList[^1]]);
		decoding.ProcessLZLength(lzData, out var length);
		result[^1].Add(new(length, lzData.Length.Max + 1));
		if (length > result.Length - 2)
			throw new DecoderFallbackException();
		decoding.ProcessLZDist(lzData, fullLength, out var dist, length, out var maxDist);
		ProcessDist(dist, length, out _, maxDist);
	}

	protected virtual int ReadFirst()
	{
		var readIndex = ar.ReadPart(set);
		if (readIndex == set.Length - 1)
		{
			var actualIndex = n == 2 ? nextWordLink++ : ar.ReadEqual(fileBase);
			if (!set.TryAdd((actualIndex, 1), out readIndex))
				throw new DecoderFallbackException();
			uniqueList.Insert(readIndex, new Interval(actualIndex, fileBase));
		}
		else
			set.Increase(uniqueList[readIndex].Lower);
		FirstUpdateSet();
		return readIndex;
	}

	protected virtual void FirstUpdateSet() => set.Update(uint.MaxValue, (int)GetBufferInterval((uint)set.GetLeftValuesSum(uint.MaxValue, out _)));

	protected virtual void ProcessDist(uint dist, uint length, out uint spiralLength, uint maxDist)
	{
		result[^1].Add(new(dist, maxDist + lzData.UseSpiralLengths + 1));
		if (decoding.ProcessLZSpiralLength(lzData, ref dist, out spiralLength, maxDist))
			result[^1].Add(new(spiralLength, lzData.SpiralLength.Max + 1));
		fullLength += (int)((length + 2) * (spiralLength + 1));
	}

	protected virtual List<ShortIntervalList> Postrequisites() => new LempelZivDec(result, lz != 0, new() { UseSpiralLengths = lzData.UseSpiralLengths }, 0).Decode();
}
