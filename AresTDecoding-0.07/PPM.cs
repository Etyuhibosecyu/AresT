
namespace AresTLib007;

public class PPM : AresTLib005.PPM
{
	protected SumList lzLengthsSL;

	public PPM(AresTLib005.Decoding decoding, ArithmeticDecoder ar, uint inputBase, ref int repeatsCount, int n = -1)
	{
		this.decoding = decoding;
		this.ar = ar;
		this.inputBase = inputBase;
		this.n = n;
		if (n == -1)
		{
			var repeats = ar.ReadPart(new List<uint>(2, 224, 225));
			repeatsCount = repeats == 0 ? 1 : (int)ar.ReadCount() + 2;
			if (repeatsCount > decoding.GetFragmentLength() >> 1)
				throw new DecoderFallbackException();
		}
		counter = ar.ReadCount();
		dicsize = ar.ReadCount();
		if (counter > decoding.GetFragmentLength() || dicsize > decoding.GetFragmentLength())
			throw new DecoderFallbackException();
		Status[0] = 0;
		StatusMaximum[0] = (int)counter;
		result = new();
		globalSet = new();
		newItemsSet = n == 2 ? new() : new(new Chain((int)inputBase).Convert(x => ((uint)x, 1)));
		maxDepth = 12;
		var comparer = n == 2 ? (G.IEqualityComparer<NList<uint>>)new NListEComparer<uint>() : new EComparer<NList<uint>>((x, y) => x.Equals(y), x => (int)x.Progression((uint)x.Length, (x, y) => (x << 7 | x >> BitsPerInt - 7) ^ (uint)y.GetHashCode()));
		contextHS = new(comparer);
		sumSets = new();
		preLZMap = new(2, 1, 2);
		spacesMap = new(2, 1, 2);
		nextWordLink = 0;
		lzLengthsSL = new() { 1 };
	}

	protected override uint PPMLZProcessLength()
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
