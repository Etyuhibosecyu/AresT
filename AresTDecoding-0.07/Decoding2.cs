﻿
namespace AresTLib007;

public class Decoding2 : AresTLib005.Decoding2
{
	public Decoding2(AresTLib005.Decoding decoding, ArithmeticDecoder ar, ListHashSet<int> nulls, int hf, int bwt, int lz, int n, bool hfw) : base(decoding, ar, nulls, hf, bwt, lz, n, hfw)
	{
		if (n == 0)
			(decoding as Decoding ?? throw new InvalidOperationException()).GetRepeatsCount();
	}

	public override List<ShortIntervalList> Decode()
	{
		ProcessNulls();
		if (lz != 0)
		{
			var counter2 = 7;
			lzRDist = (int)ar.ReadEqual(3);
			lzMaxDist = ar.ReadCount();
			if (lzRDist != 0)
			{
				lzThresholdDist = ar.ReadEqual(lzMaxDist + 1);
				counter2++;
			}
			lzDist = new(lzRDist, lzMaxDist, lzThresholdDist);
			lzRLength = (int)ar.ReadEqual(3);
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
				lzRSpiralLength = (int)ar.ReadEqual(3);
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

	protected override List<ShortIntervalList> DecodeAdaptive() => new AdaptiveHuffmanDec(decoding as Decoding ?? throw new InvalidOperationException(), ar, skipped, lzData, lz, bwt, n, counter, hfw).Decode();
}
