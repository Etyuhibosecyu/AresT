namespace AresTLib;

public class Decoding2T : IDisposable
{
	protected DecodingT decoding = default!;
	protected GlobalDecoding globalDecoding = default!;
	protected ArithmeticDecoder ar = default!;
	protected ListHashSet<int> nulls = default!;
	protected int maxFrequency, frequencyCount, bwt, lz, counter, n, bwtBlockSize;
	protected uint encoding, maxLength, lzRDist, lzMaxDist, lzThresholdDist, lzRLength, lzMaxLength, lzThresholdLength, lzUseSpiralLengths, lzRSpiralLength, lzMaxSpiralLength, lzThresholdSpiralLength;
	protected MethodDataUnit lzDist = new(), lzLength = new(), lzSpiralLength = new();
	protected LZData lzData = default!;
	protected NList<uint> arithmeticMap = default!;
	protected NList<Interval> uniqueList = default!;
	protected NList<uint> skipped = default!;

	public Decoding2T(DecodingT decoding, GlobalDecoding globalDecoding, ArithmeticDecoder ar, ListHashSet<int> nulls, int bwt, int lz, int n, ref int bwtBlockSize)
	{
		this.decoding = decoding;
		this.globalDecoding = globalDecoding;
		this.ar = ar;
		this.nulls = nulls;
		this.bwt = bwt;
		this.lz = lz;
		this.n = n;
		counter = (int)ar.ReadNumber() - (n == 0 ? 2 : 1);
		if (bwt != 0 && n == 1)
		{
			bwtBlockSize = (int)ar.ReadEqual(18);
			if (bwtBlockSize < 5)
				bwtBlockSize = 12500 << bwtBlockSize;
			else
				bwtBlockSize = 500000 << (bwtBlockSize - 5);
		}
		this.bwtBlockSize = bwtBlockSize;
		lzDist = new();
		lzLength = new();
		lzSpiralLength = new();
		maxFrequency = 0;
		frequencyCount = 0;
		arithmeticMap = [];
		uniqueList = [];
		skipped = [];
	}

	public virtual void Dispose()
	{
		arithmeticMap?.Dispose();
		uniqueList?.Dispose();
		skipped?.Dispose();
		GC.SuppressFinalize(this);
	}

	public virtual NList<ShortIntervalList> Decode()
	{
		ProcessNulls();
		if (lz != 0)
		{
			var counter2 = 7;
			lzRDist = ar.ReadEqual(3);
			lzMaxDist = ar.ReadNumber();
			if (lzRDist != 0)
			{
				lzThresholdDist = ar.ReadEqual(lzMaxDist + 1);
				counter2++;
			}
			lzDist = new(lzRDist, lzMaxDist, lzThresholdDist);
			lzRLength = ar.ReadEqual(3);
			lzMaxLength = ar.ReadNumber(16);
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
				lzMaxSpiralLength = ar.ReadNumber(16);
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
		(encoding, maxLength, var nullsCount) = n == 0 ? (ar.ReadEqual(3), ar.ReadNumber(), decoding.GetNullsCount()) : (0, 0, 0);
		if (n == 0 && nulls != null)
		{
			var counter2 = 1;
			if (maxLength < 2 || maxLength > decoding.GetFragmentLength() || nullsCount > decoding.GetFragmentLength())
				throw new DecoderFallbackException();
			for (var i = 0; i < nullsCount; i++)
			{
				var value = ar.ReadNumber();
				if (value > decoding.GetFragmentLength())
					throw new DecoderFallbackException();
				nulls.Add((int)value + (nulls.Length == 0 ? 0 : nulls[^1] + 1));
				counter2++;
			}
			counter -= GetArrayLength(counter2, 4);
		}
	}

	protected virtual NList<ShortIntervalList> ProcessHuffman()
	{
		var compressedList = DecodeAdaptive();
		if (n == 0)
			decoding.SetEncoding(encoding);
		if (bwt != 0 && n != 0)
		{
			Current[0] += ProgressBarStep;
			compressedList = decoding.DecodeBWT(compressedList, skipped, bwtBlockSize);
		}
		if (n != 2)
			Current[0] += ProgressBarStep;
		return compressedList;
	}

	protected virtual NList<ShortIntervalList> DecodeAdaptive()
	{
		using var dec = new AdaptiveHuffmanDec(globalDecoding, ar, skipped, lzData, bwt == 0 || n == 2 ? lz : 0, bwt, n, bwtBlockSize, counter);
		return dec.Decode();
	}
}
