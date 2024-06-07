
namespace AresTLib;

public class AdaptiveHuffmanDec : AresTLib007.AdaptiveHuffmanDec
{
	private readonly Decoding decoding = default!;
	protected int lzLength;

	public AdaptiveHuffmanDec(Decoding decoding, ArithmeticDecoder ar, NList<byte> skipped, LZData lzData, int lz, int bwt, int n, int counter, bool hfw) : base(decoding, ar, skipped, lzData, lz, bwt, n, counter, hfw)
	{
		this.decoding = decoding;
		firstIntervalDist = lz != 0 ? (lzData.Dist.R == 1 ? lzData.Dist.Threshold + 2 : lzData.Dist.Max + 1) + lzData.UseSpiralLengths : 0;
	}

	protected override void DecodeIteration()
	{
		var readIndex = ReadFirst();
		if (!(lz != 0 && uniqueList[readIndex].Lower == fileBase - 1))
		{
			result.Add(n == 2 ? new() { uniqueList[readIndex], new(ar.ReadEqual(2), 2) } : new() { uniqueList[readIndex] });
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

	protected override void FirstUpdateSet() => set.Update(uint.MaxValue, Max(set.Length - 1, 1));

	protected override List<ShortIntervalList> Postrequisites() => result;
}
