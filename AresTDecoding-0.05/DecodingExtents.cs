
namespace AresTLib005;

public static class DecodingExtents
{
	public static uint ReadCount(this ArithmeticDecoder ar, uint maxT = 31)
	{
		var temp = (int)ar.ReadEqual(maxT);
		var read = ar.ReadEqual((uint)1 << Max(temp, 1));
		return read + ((temp == 0) ? 0 : (uint)1 << Max(temp, 1));
	}

	public static uint GetBaseWithBuffer(uint oldBase) => oldBase + GetBufferInterval(oldBase);
	public static uint GetBufferInterval(uint oldBase) => Max((oldBase + 10) / 20, 1);
}
