
namespace AresTLib005;

public class Decoding2
{
	protected Decoding decoding = default!;
	protected ArithmeticDecoder ar = default!;
	protected ListHashSet<int> nulls = default!;
	protected int maxFrequency, frequencyCount, hf, bwt, lz, lzRDist, lzRLength, lzRSpiralLength, counter, n;
	protected uint encoding, maxLength, lzMaxDist, lzThresholdDist, lzMaxLength, lzThresholdLength, lzUseSpiralLengths, lzMaxSpiralLength, lzThresholdSpiralLength;
	protected MethodDataUnit lzDist = new(), lzLength = new(), lzSpiralLength = new();
	protected LZData lzData = default!;
	protected NList<uint> arithmeticMap = default!;
	protected NList<Interval> uniqueList = default!;
	protected NList<byte> skipped = default!;
	protected bool hfw;

	protected Decoding2() { }

	public Decoding2(Decoding decoding, ArithmeticDecoder ar, ListHashSet<int> nulls, int hf, int bwt, int lz, int n, bool hfw)
	{
		this.decoding = decoding;
		this.ar = ar;
		this.nulls = nulls;
		this.hf = hf;
		this.bwt = bwt;
		this.lz = lz;
		this.n = n;
		this.hfw = hfw;
		counter = (int)ar.ReadCount() - (hfw && n == 0 ? 2 : 1);
		lzDist = new();
		lzLength = new();
		lzSpiralLength = new();
		maxFrequency = 0;
		frequencyCount = 0;
		arithmeticMap = [];
		uniqueList = [];
		skipped = [];
	}

	public virtual List<ShortIntervalList> Decode()
	{
		ProcessNulls();
		if (lz != 0)
		{
			var counter2 = 7;
			lzRDist = (int)ar.ReadEqual(3);
			lzMaxDist = ar.ReadCount(16);
			if (lzRDist != 0)
			{
				lzThresholdDist = ar.ReadEqual(lzMaxDist + 1);
				counter2++;
			}
			lzDist = (lzRDist, lzMaxDist, lzThresholdDist);
			lzRLength = (int)ar.ReadEqual(3);
			lzMaxLength = ar.ReadCount(16);
			if (lzRLength != 0)
			{
				lzThresholdLength = ar.ReadEqual(lzMaxLength + 1);
				counter2++;
			}
			lzLength = (lzRLength, lzMaxLength, lzThresholdLength);
			if (lzMaxDist == 0 && lzMaxLength == 0 && ar.ReadEqual(2) == 0)
			{
				lz = 0;
				goto l0;
			}
			lzUseSpiralLengths = ar.ReadEqual(2);
			if (lzUseSpiralLengths == 1)
			{
				lzRSpiralLength = (int)ar.ReadEqual(3);
				lzMaxSpiralLength = ar.ReadCount(16);
				counter2 += 3;
				if (lzRSpiralLength != 0)
				{
					lzThresholdSpiralLength = ar.ReadEqual(lzMaxSpiralLength + 1);
					counter2++;
				}
				lzSpiralLength = (lzRSpiralLength, lzMaxSpiralLength, lzThresholdSpiralLength);
			}
		l0:
			counter -= GetArrayLength(counter2, 8);
		}
		lzData = (lzDist, lzLength, lzUseSpiralLengths, lzSpiralLength);
		return ProcessHuffman();
	}

	protected virtual void ProcessNulls()
	{
		(encoding, maxLength, var nullsCount) = hfw && n == 0 ? (ar.ReadEqual(3), ar.ReadCount(), GetNullsCount()) : (0, 0, 0);
		if (hfw && n == 0 && nulls != null)
		{
			var counter2 = 1;
			if (maxLength < 2 || maxLength > decoding.GetFragmentLength() || nullsCount > decoding.GetFragmentLength())
				throw new DecoderFallbackException();
			for (var i = 0; i < nullsCount; i++)
			{
				var value = ar.ReadCount((uint)BitsCount(decoding.GetFragmentLength()));
				if (value > decoding.GetFragmentLength())
					throw new DecoderFallbackException();
				nulls.Add((int)value + (nulls.Length == 0 ? 0 : nulls[^1] + 1));
				counter2++;
			}
			counter -= GetArrayLength(counter2, 4);
		}
	}

	protected virtual uint GetNullsCount() => ar.ReadCount((uint)BitsCount(decoding.GetFragmentLength()));

	protected virtual List<ShortIntervalList> ProcessHuffman()
	{
		List<ShortIntervalList> compressedList;
		if (hf >= 4)
		{
			compressedList = DecodeAdaptive();
			goto l1;
		}
		if (hf != 0)
		{
			var counter2 = 4;
			maxFrequency = (int)ar.ReadCount() + 1;
			frequencyCount = (int)ar.ReadCount() + 1;
			if (maxFrequency > decoding.GetFragmentLength() || frequencyCount > decoding.GetFragmentLength())
				throw new DecoderFallbackException();
			Status[0] = 0;
			StatusMaximum[0] = frequencyCount;
			var @base = hfw && n == 0 ? maxLength + 1 : hfw && n == 2 ? (uint)frequencyCount : ValuesInByte;
			if (maxFrequency > frequencyCount * 2 || frequencyCount <= ValuesInByte)
			{
				arithmeticMap.Add((uint)maxFrequency);
				var prev = (uint)maxFrequency;
				for (var i = 0; i < frequencyCount; i++, Status[0]++)
				{
					counter2++;
					uniqueList.Add(new(ar.ReadEqual(@base), @base));
					if (i == 0) continue;
					prev = ar.ReadEqual(prev) + 1;
					counter2++;
					arithmeticMap.Add(arithmeticMap[^1] + prev);
				}
			}
			else
				for (var i = 0; i < frequencyCount; i++, Status[0]++)
				{
					uniqueList.Add(new((uint)i, hfw && n == 0 ? maxLength + 1 : (uint)frequencyCount));
					counter2++;
					arithmeticMap.Add((arithmeticMap.Length == 0 ? 0 : arithmeticMap[^1]) + ar.ReadEqual((uint)maxFrequency) + 1);
				}
			if (lz != 0)
				arithmeticMap.Add(GetHuffmanBase(arithmeticMap[^1]));
			counter -= GetArrayLength(counter2, 8);
			if (bwt != 0 && !(hfw && n != 1))
			{
				var skippedCount = (int)ar.ReadCount();
				for (var i = 0; i < skippedCount; i++)
					skipped.Add((byte)ar.ReadEqual(@base));
				counter -= (skippedCount + 9) / 8;
			}
		}
		else
		{
			uniqueList.AddRange(RedStarLinq.Fill(ValuesInByte, index => new Interval((uint)index, ValuesInByte)));
			arithmeticMap.AddRange(RedStarLinq.Fill(ValuesInByte, index => (uint)(index + 1)));
			if (lz != 0)
				arithmeticMap.Add(GetHuffmanBase(ValuesInByte));
		}
		if (counter is < 0 || counter > decoding.GetFragmentLength() + decoding.GetFragmentLength() / 1000)
			throw new DecoderFallbackException();
		HuffmanData huffmanData = new(maxFrequency, frequencyCount, arithmeticMap, uniqueList);
		Current[0] += ProgressBarStep;
		compressedList = decoding.ReadCompressedList(huffmanData, bwt, lzData, lz, counter, n == 2);
	l1:
		if (hfw && n == 0)
			compressedList.Add([new(encoding, 3)]);
		if (bwt != 0 && !(hfw && n != 1))
		{
			Current[0] += ProgressBarStep;
			compressedList = decoding.DecodeBWT(compressedList, skipped);
		}
		if (hfw && n != 2)
			Current[0] += ProgressBarStep;
		return compressedList;
	}

	protected virtual List<ShortIntervalList> DecodeAdaptive() => new AdaptiveHuffmanDec(decoding, ar, skipped, lzData, lz, bwt, n, counter, hfw).Decode();

	protected virtual uint GetHuffmanBase(uint oldBase) => GetBaseWithBuffer(oldBase);
}
