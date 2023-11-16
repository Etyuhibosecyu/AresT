
namespace AresTLib;

public class Decoding2 : AresTLib007.Decoding2
{
	public Decoding2(AresTLib005.Decoding decoding, ArithmeticDecoder ar, ListHashSet<int> nulls, int hf, int bwt, int lz, int n, bool hfw, ref int repeatsCount) : base(decoding, ar, nulls, hf, bwt, lz, n, hfw, ref repeatsCount)
	{
	}

	protected override List<ShortIntervalList> DecodeAdaptive() => new AdaptiveHuffmanDec(decoding as Decoding ?? throw new InvalidOperationException(), ar, skipped, lzData, lz, bwt, n, counter, hfw).Decode();

	protected override uint GetHuffmanBase(uint oldBase) => GetBaseWithBuffer(oldBase, hfw);
}
