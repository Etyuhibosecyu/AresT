
namespace AresTLib;

internal partial class Compression
{
	private byte[] AdaptiveHuffman(List<ShortIntervalList> input, LZData lzData)
	{
		if (input.Length < 2)
			throw new EncoderFallbackException();
		using ArithmeticEncoder ar = new();
		if (!AdaptiveHuffmanInternal(ar, input, lzData))
			throw new EncoderFallbackException();
		ar.WriteEqual(1234567890, 4294967295);
		return ar;
	}

	private byte[] AdaptiveHuffman(List<List<ShortIntervalList>> input, LZData[] lzData)
	{
		if (input.Any(x => x.Length < 2))
			throw new EncoderFallbackException();
		using ArithmeticEncoder ar = new();
		for (var i = 0; i < input.Length; i++)
			if (!AdaptiveHuffmanInternal(ar, input[i], lzData[i], i))
				throw new EncoderFallbackException();
		ar.WriteEqual(1234567890, 4294967295);
		return ar;
	}

	private bool AdaptiveHuffmanInternal(ArithmeticEncoder ar, List<ShortIntervalList> input, LZData lzData, int n = 1)
	{
		var bwtIndex = input[0].IndexOf(BWTApplied);
		if (CreateVar(input[0].IndexOf(HuffmanApplied), out var huffmanIndex) != -1 && !(bwtIndex != -1 && huffmanIndex == bwtIndex + 1))
			throw new EncoderFallbackException();
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 2;
		Status[tn] = 0;
		StatusMaximum[tn] = 3;
		var lz = CreateVar(input[0].IndexOf(LempelZivApplied), out var lzIndex) != -1 && (bwtIndex == -1 || lzIndex != bwtIndex + 1);
		var lzDummy = CreateVar(input[0].IndexOf(LempelZivDummyApplied), out var lzDummyIndex) != -1 && (bwtIndex == -1 || lzDummyIndex != bwtIndex + 1);
		var bwtLength = bwtIndex != -1 ? (int)input[0][bwtIndex + 1].Base : 0;
		var startPos = (lz || lzDummy ? (input[0].Length >= lzIndex + 2 && input[0][lzIndex + 1] == LempelZivSubdivided ? 3 : 2) : 1) + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base : 0) + bwtLength;
		Status[tn]++;
		var lzPos = bwtIndex != -1 ? 4 : 2;
		if (input.Length < startPos + lzPos + 1)
			throw new EncoderFallbackException();
		var originalBase = input[startPos + lzPos][0].Base;
		if (!input.GetSlice(startPos + lzPos + 1).All((x, index) => bwtIndex != -1 && (index + lzPos + 1) % (BWTBlockSize + 2) is 0 or 1 || x[0].Base == originalBase))
			throw new EncoderFallbackException();
		Status[tn]++;
		ar.WriteCount((uint)input.Length);
		for (var i = 0; i < startPos; i++)
			for (var j = 0; j < input[i].Length; j++)
			{
				var x = input[i][j];
				if (i == startPos - bwtLength && j == 2)
					ar.WriteCount(x.Base);
				ar.WritePart(x.Lower, x.Length, x.Base);
			}
		Status[tn]++;
		var newBase = input[startPos][0].Base + (lz ? 1u : 0);
		ar.WriteCount(newBase);
		Status[tn] = 0;
		StatusMaximum[tn] = input.Length - startPos;
		Current[tn] += ProgressBarStep;
		SumSet<uint> set = new();
		SumList lengthsSL = lz ? new(RedStarLinq.Fill(1, (int)(lzData.Length.R == 0 ? lzData.Length.Max + 1 : lzData.Length.R == 1 ? lzData.Length.Threshold + 2 : lzData.Length.Max - lzData.Length.Threshold + 2))) : new(), distsSL = lz ? new(RedStarLinq.Fill(1, (int)lzData.UseSpiralLengths + 1)) : new();
		var firstIntervalDist = lz ? (lzData.Dist.R == 1 ? lzData.Dist.Threshold + 2 : lzData.Dist.Max + 1) + lzData.UseSpiralLengths : 0;
		if (lz)
			set.Add((newBase - 1, 1));
		for (var i = startPos; i < input.Length; i++, Status[tn]++)
		{
			var item = input[i][0].Lower;
			var sum = set.GetLeftValuesSum(item, out var frequency);
			var bufferInterval = GetBufferInterval((uint)set.ValuesSum);
			var fullBase = (uint)(set.ValuesSum + bufferInterval);
			if (frequency == 0)
			{
				ar.WritePart((uint)set.ValuesSum, bufferInterval, fullBase);
				if (n != 2)
					ar.WriteEqual(item, newBase);
			}
			else
				ar.WritePart((uint)sum, (uint)frequency, fullBase);
			set.Increase(item);
			int lzLength = 0, lzDist = 0, lzSpiralLength = 0;
			var j = 1;
			if (lz && item == newBase - 1)
			{
				item = input[i][j].Lower;
				lzLength = (int)(item + (lzData.Length.R == 2 ? lzData.Length.Threshold : 0));
				sum = lengthsSL.GetLeftValuesSum((int)item, out frequency);
				ar.WritePart((uint)sum, (uint)frequency, (uint)lengthsSL.ValuesSum);
				lengthsSL.Increase((int)item);
				j++;
				if (lzData.Length.R != 0 && item == lengthsSL.Length - 1)
				{
					ar.WritePart(input[i][j].Lower, input[i][j].Length, input[i][j].Base);
					lzLength = (int)(lzData.Length.R == 2 ? input[i][j].Lower : input[i][j].Lower + lzData.Length.Threshold + 1);
					j++;
				}
				item = input[i][j].Lower;
				lzDist = (int)(item + (lzData.Dist.R == 2 && distsSL.Length - lzData.UseSpiralLengths - lzLength - startPos >= lzData.Dist.Threshold ? lzData.Dist.Threshold : 0));
				if (lzData.Dist.R == 2 && distsSL.Length - lzData.UseSpiralLengths - lzLength - startPos >= lzData.Dist.Threshold && lzDist == (distsSL.Length == firstIntervalDist ? lzData.Dist.Max : distsSL.Length - lzData.UseSpiralLengths - lzLength - startPos) + 1)
				{
					j++;
					if (input[i][j].Lower != lzData.Dist.Threshold) lzDist = (int)input[i][j].Lower;
				}
				sum = distsSL.GetLeftValuesSum(lzDist, out frequency);
				ar.WritePart((uint)sum, (uint)frequency, (uint)distsSL.ValuesSum);
				distsSL.Increase(lzDist);
				j++;
				if (lzData.Dist.R != 0 && input[i][j - 1].Base == firstIntervalDist - lzData.UseSpiralLengths && input[i][j - 1].Lower == input[i][j - 1].Base - 1)
				{
					ar.WritePart(input[i][j].Lower, input[i][j].Length, input[i][j].Base);
					j++;
				}
				lzSpiralLength = lzData.UseSpiralLengths != 0 && input[i][j - 1].Lower == input[i][j - 1].Base - 1 ? lzData.SpiralLength.R == 0 ? (int)input[i][^1].Lower : (int)(input[i][^1].Lower + (lzData.SpiralLength.R == 2 != (input[i][j].Lower == input[i][j].Base - 1) ? lzData.SpiralLength.Threshold + 2 - lzData.SpiralLength.R : 0)) : 0;
				if (lz && distsSL.Length < firstIntervalDist)
					new Chain(Min((int)firstIntervalDist - distsSL.Length, (lzLength + 2) * (lzSpiralLength + 1))).ForEach(x => distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1));
			}
			else if (lz && distsSL.Length < firstIntervalDist)
				distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1);
			for (; j < input[i].Length; j++)
				ar.WritePart(input[i][j].Lower, input[i][j].Length, input[i][j].Base);
		}
		return true;
	}

	private bool AdaptiveHuffmanBits(ArithmeticEncoder ar, List<ShortIntervalList> input, int startPos)
	{
		if (!(input.Length >= startPos + 2 && input.GetSlice(startPos).All(x => x.Length > 0 && x[0].Base == 2)))
			throw new EncoderFallbackException();
		Status[tn] = 0;
		StatusMaximum[tn] = 3;
		Current[tn] += ProgressBarStep;
		Status[tn]++;
		ar.WriteCount((uint)input.Length);
		for (var i = 0; i < startPos; i++)
			for (var j = 0; j < input[i].Length; j++)
			{
				var x = input[i][j];
				ar.WritePart(x.Lower, x.Length, x.Base);
			}
		Status[tn]++;
		var newBase = input[startPos][0].Base;
		ar.WriteCount(newBase);
		Status[tn] = 0;
		StatusMaximum[tn] = input.Length - startPos;
		Current[tn] += ProgressBarStep;
		var windowSize = 1 << 13;
		uint zeroFreq = 1, totalFreq = 2;
		for (var i = startPos; i < input.Length; i++, Status[tn]++)
		{
			var item = input[i][0].Lower == 1;
			var sum = item ? zeroFreq : 0;
			ar.WritePart(sum, item ? totalFreq - zeroFreq : zeroFreq, totalFreq);
			if (i < windowSize + startPos)
			{
				if (!item)
					zeroFreq++;
				totalFreq++;
			}
			if (i >= windowSize + startPos && input[i - windowSize][0].Lower == (item ? 0u : 1))
			{
				if (item)
					zeroFreq--;
				else
					zeroFreq++;
			}
			for (var j = 1; j < input[i].Length; j++)
				ar.WritePart(input[i][j].Lower, input[i][j].Length, input[i][j].Base);
		}
		return true;
	}
}
