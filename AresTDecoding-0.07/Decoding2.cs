
namespace AresTLib007;
public class Decoding2 : AresTLib005.Decoding2
{
	public Decoding2(AresTLib005.Decoding decoding, ArithmeticDecoder ar, ListHashSet<int> nulls, int hf, int bwt, int lz, int n, bool hfw, ref int repeatsCount) : base(decoding, ar, nulls, hf, bwt, lz, n, hfw)
	{
		if (n == 0)
		{
			var repeats = ar.ReadPart(new List<uint>(2, 224, 225));
			repeatsCount = repeats == 0 ? 1 : (int)ar.ReadCount() + 2;
			if (repeatsCount > decoding.GetFragmentLength() >> 1)
				throw new DecoderFallbackException();
		}
	}

	public override List<ShortIntervalList> Decode()
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
		LZData lzData = new(lzDist, lzLength, lzUseSpiralLengths, lzSpiralLength);
		return ProcessHuffman(lzData);
	}
}
