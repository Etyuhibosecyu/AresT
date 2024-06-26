
namespace AresTLib;

public class Decoding2T
{
	protected DecodingT decoding = default!;
	protected ArithmeticDecoder ar = default!;
	protected ListHashSet<int> nulls = default!;
	protected int maxFrequency, frequencyCount, bwt, lz, counter, n;
	protected uint encoding, maxLength, lzRDist, lzMaxDist, lzThresholdDist, lzRLength, lzMaxLength, lzThresholdLength, lzUseSpiralLengths, lzRSpiralLength, lzMaxSpiralLength, lzThresholdSpiralLength;
	protected MethodDataUnit lzDist = new(), lzLength = new(), lzSpiralLength = new();
	protected LZData lzData = default!;
	protected NList<uint> arithmeticMap = default!;
	protected NList<Interval> uniqueList = default!;
	protected NList<byte> skipped = default!;

	public Decoding2T(DecodingT decoding, ArithmeticDecoder ar, ListHashSet<int> nulls, int bwt, int lz, int n)
	{
		this.decoding = decoding;
		this.ar = ar;
		this.nulls = nulls;
		this.bwt = bwt;
		this.lz = lz;
		this.n = n;
		counter = (int)ar.ReadCount() - (n == 0 ? 2 : 1);
		lzDist = new();
		lzLength = new();
		lzSpiralLength = new();
		maxFrequency = 0;
		frequencyCount = 0;
		arithmeticMap = [];
		uniqueList = [];
		skipped = [];
		if (n == 0)
			decoding.GetRepeatsCount();
	}

	public virtual List<ShortIntervalList> Decode()
	{
		ProcessNulls();
		if (lz != 0)
		{
			var counter2 = 7;
			lzRDist = ar.ReadEqual(3);
			lzMaxDist = ar.ReadCount();
			if (lzRDist != 0)
			{
				lzThresholdDist = ar.ReadEqual(lzMaxDist + 1);
				counter2++;
			}
			lzDist = new(lzRDist, lzMaxDist, lzThresholdDist);
			lzRLength = ar.ReadEqual(3);
			lzMaxLength = ar.ReadCount(16);
			if (lzRLength != 0)
			{
				lzThresholdLength = ar.ReadEqual(lzMaxLength + 1);
				counter2++;
			}
			lzLength = new(lzRLength, lzMaxLength, lzThresholdLength);
			if (lzMaxDist == 0 && lzMaxLength == 0 && ar.ReadEqual(2) == 0)
			{
				lz = 0;
				goto l0;
			}
			lzUseSpiralLengths = ar.ReadEqual(2);
			if (lzUseSpiralLengths == 1)
			{
				lzRSpiralLength = ar.ReadEqual(3);
				lzMaxSpiralLength = ar.ReadCount(16);
				counter2 += 3;
				if (lzRSpiralLength != 0)
				{
					lzThresholdSpiralLength = ar.ReadEqual(lzMaxSpiralLength + 1);
					counter2++;
				}
				lzSpiralLength = new(lzRSpiralLength, lzMaxSpiralLength, lzThresholdSpiralLength);
			}
		l0:
			counter -= GetArrayLength(counter2, 8);
		}
		lzData = new(lzDist, lzLength, lzUseSpiralLengths, lzSpiralLength);
		return ProcessHuffman();
	}

	protected virtual void ProcessNulls()
	{
		(encoding, maxLength, var nullsCount) = n == 0 ? (ar.ReadEqual(3), ar.ReadCount(), decoding.GetNullsCount()) : (0, 0, 0);
		if (n == 0 && nulls != null)
		{
			var counter2 = 1;
			if (maxLength < 2 || maxLength > decoding.GetFragmentLength() || nullsCount > decoding.GetFragmentLength())
				throw new DecoderFallbackException();
			for (var i = 0; i < nullsCount; i++)
			{
				var value = ar.ReadCount();
				if (value > decoding.GetFragmentLength())
					throw new DecoderFallbackException();
				nulls.Add((int)value + (nulls.Length == 0 ? 0 : nulls[^1] + 1));
				counter2++;
			}
			counter -= GetArrayLength(counter2, 4);
		}
	}

	protected virtual List<ShortIntervalList> ProcessHuffman()
	{
		var compressedList = DecodeAdaptive();
		if (n == 0)
			compressedList.Add([new(encoding, 3)]);
		if (bwt != 0 && n == 1)
		{
			Current[0] += ProgressBarStep;
			compressedList = decoding.DecodeBWT(compressedList, skipped);
		}
		if (n != 2)
			Current[0] += ProgressBarStep;
		return compressedList;
	}

	protected virtual List<ShortIntervalList> DecodeAdaptive() => new AdaptiveHuffmanDecT(decoding, ar, skipped, lzData, bwt == 0 || n == 2 ? lz : 0, bwt, n, counter).Decode();

	protected virtual uint GetHuffmanBase(uint oldBase) => GetBaseWithBuffer(oldBase, true);
}
