
namespace AresTLib007;

public class PPMDec(AresTLib005.Decoding decoding, ArithmeticDecoder ar, uint inputBase, int n = -1) : AresTLib005.PPMDec(decoding, ar, inputBase, n)
{
	protected SumList lzLengthsSL = default!;

	protected override void Initialize()
	{
		if (n == -1)
			(decoding as Decoding ?? throw new InvalidOperationException()).GetRepeatsCount();
		base.Initialize();
		lzLengthsSL = [1];
	}

	protected override uint ProcessLZLength()
	{
		var readIndex = ar.ReadPart(lzLengthsSL);
		uint length;
		if (readIndex < lzLengthsSL.Length - 1)
		{
			length = (uint)readIndex + 1;
			lzLengthsSL.Increase(readIndex);
		}
		else if (ar.ReadFibonacci(out length) && length + maxDepth - 1 <= counter)
		{
			length += (uint)lzLengthsSL.Length - 1;
			lzLengthsSL.Increase(lzLengthsSL.Length - 1);
			new Chain((int)length - lzLengthsSL.Length).ForEach(x => lzLengthsSL.Insert(lzLengthsSL.Length - 1, 1));
		}
		else
			throw new DecoderFallbackException();
		return length;
	}
}
