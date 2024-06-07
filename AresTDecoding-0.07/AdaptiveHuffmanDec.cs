
namespace AresTLib007;

public class AdaptiveHuffmanDec(Decoding decoding, ArithmeticDecoder ar, NList<byte> skipped, LZData lzData, int lz, int bwt, int n, int counter, bool hfw) : AresTLib005.AdaptiveHuffmanDec(decoding, ar, skipped, lzData, lz, bwt, n, counter, hfw)
{
	protected SumList lengthsSL = lz != 0 ? new(RedStarLinq.Fill(1, (int)(lzData.Length.R == 0 ? lzData.Length.Max + 1 : lzData.Length.R == 1 ? lzData.Length.Threshold + 2 : lzData.Length.Max - lzData.Length.Threshold + 2))) : new(), distsSL = lz != 0 ? new(RedStarLinq.Fill(1, (int)lzData.UseSpiralLengths + 1)) : new();
	protected uint firstIntervalDist = lz != 0 ? (lzData.Dist.R == 1 ? lzData.Dist.Threshold + 2 : lzData.Dist.Max + 1) + lzData.UseSpiralLengths : 0;

	protected override void DecodeIteration()
	{
		var readIndex = ReadFirst();
		if (!(lz != 0 && uniqueList[readIndex].Lower == fileBase - 1))
		{
			result.Add(n == 2 ? new() { uniqueList[readIndex], new(ar.ReadEqual(2), 2) } : new() { uniqueList[readIndex] });
			fullLength++;
			if (lz != 0 && distsSL.Length < firstIntervalDist)
				distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1);
			return;
		}
		result.Add([uniqueList[^1]]);
		decoding.ProcessLZLength(lzData, lengthsSL, out readIndex, out var length);
		result[^1].Add(new(length, lzData.Length.Max + 1));
		decoding.ProcessLZDist(lzData, distsSL, fullLength, out readIndex, out var dist, length, out var maxDist);
		ProcessDist(dist, length, out var spiralLength, maxDist);
		if (lz != 0 && distsSL.Length < firstIntervalDist)
			new Chain((int)Min(firstIntervalDist - distsSL.Length, (length + 2) * (spiralLength + 1))).ForEach(x => distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1));
	}
}
