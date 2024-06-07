
namespace AresTLib;

public class PPMDec(AresTLib005.Decoding decoding, ArithmeticDecoder ar, uint inputBase, int n = -1) : AresTLib007.PPMDec(decoding, ar, inputBase, n)
{
	protected SumSet<uint> lzPositions = [(uint.MaxValue, 100)];

	protected override int ProcessLZDist()
	{
		var index = ar.ReadPart(lzPositions);
		var pos = lzPositions[index];
		lzPositions.Update(pos.Key, pos.Value + 100);
		if (index == lzPositions.Length - 1)
		{
			pos = ((uint)(result.Length - base.ProcessLZDist() - 2), 100);
			lzPositions.Add(pos);
		}
		return (int)pos.Key;
	}
}
