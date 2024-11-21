
namespace AresTLib;

internal partial class Compression(NList<byte> originalFile, NList<ShortIntervalList> input, int tn)
{
	private readonly NList<ShortIntervalList> result = [];

	internal NList<ShortIntervalList> PreEncode(ref int rle, out NList<byte> originalFile2)
	{
		NList<ShortIntervalList> cdl;
		NList<byte> string1, string2, cstring;
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 11;
		cstring = originalFile;
		Subtotal[tn] += ProgressBarStep;
		string1 = new RLE(cstring, tn).Encode();
		Subtotal[tn] += ProgressBarStep;
		string2 = new RLE(cstring, tn).RLE3();
		Subtotal[tn] += ProgressBarStep;
		Current[tn] = 0;
		Status[tn] = 0;
		if (string1.Length < cstring.Length * 0.5 && string1.Length < string2.Length)
		{
			rle = 7;
			cstring = string1;
		}
		else if (string2.Length < cstring.Length * 0.5)
		{
			rle = 14;
			cstring = string2;
		}
		originalFile2 = cstring;
		cdl = RedStarLinq.NEmptyList<ShortIntervalList>(originalFile2.Length + 1);
		cdl[0] = [];
		var originalFile2_ = originalFile2;
		Parallel.For(0, originalFile2.Length, i => cdl[i + 1] = ByteIntervals[originalFile2_[i]]);
		Subtotal[tn] += ProgressBarStep;
		return cdl;
	}

	internal void Encode1(ref NList<byte> cs, ref int lz)
	{
		NList<byte> s, cs2 = cs;
		List<NList<ShortIntervalList>> tl1, ctl;
		LZData[] lzData = [new(), new(), new()];
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 9;
		ctl = MakeWordsSplit((PresentMethodsT & UsedMethodsT.COMB1) != 0);
		if (ctl.Length == 0)
			throw new EncoderFallbackException();
		Subtotal[tn] += ProgressBarStep;
		tl1 = RedStarLinq.Fill(ctl.Length, _ => new NList<ShortIntervalList>());
		if ((PresentMethodsT & UsedMethodsT.LZ1) != 0)
		{
			tl1 = RedStarLinq.Fill(ctl.Length, _ => new NList<ShortIntervalList>());
			for (var i = 0; i < WordsListActualParts; i++)
			{
				tl1[i] = new(new LempelZiv(ctl[i], result, tn).Encode(out lzData[i]));
				Subtotal[tn] += ProgressBarStep;
			}
			for (var i = WordsListActualParts; i < ctl.Length; i++)
				for (var j = 0; j < ctl[i].Length; j++)
					tl1[i].Add(new(ctl[i][j]));
			s = WorkUpTripleList(tl1, tn);
		}
		else
		{
			tl1 = ctl;
			Subtotal[tn] += ProgressBarStep * 3;
			s = cs2;
		}
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < cs2.Length && s.Length > 0)
		{
			lz = 1;
			ctl = tl1;
			cs2 = s;
		}
		s = new AdaptiveHuffman(tn).Encode(ctl, lzData);
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < cs2.Length && s.Length > 0)
			cs = s;
	}

	internal void Encode2(ref NList<byte> cs)
	{
		NList<byte> s;
		List<NList<ShortIntervalList>> ctl;
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 7;
		ctl = MakeWordsSplit((PresentMethodsT & UsedMethodsT.COMB2) != 0);
		if (ctl.Length is not (3 or 4))
			throw new EncoderFallbackException();
		Subtotal[tn] += ProgressBarStep;
		var bwtEncoder = new BWTT(result, tn);
		ctl[2] = new(bwtEncoder.Encode(ctl[2]));
		Subtotal[tn] += ProgressBarStep;
		ctl[1] = new(bwtEncoder.Encode(ctl[1]));
		Subtotal[tn] += ProgressBarStep;
		var lzData = new LZData[ctl.Length];
		Subtotal[tn] += ProgressBarStep;
		s = new AdaptiveHuffman(tn).Encode(ctl, lzData);
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < cs.Length && s.Length > 0)
			cs = s;
	}

	internal void Encode3(ref NList<byte> cs, ref int indicator)
	{
		NList<byte> s;
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 2;
		var ctl = MakeWordsSplit(false);
		if (ctl.Length == 0)
			throw new EncoderFallbackException();
		Subtotal[tn] += ProgressBarStep;
		var ppm = new PPM(tn);
		s = ppm.Encode(ctl);
		ppm.Dispose();
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < originalFile.Length && s.Length > 0)
		{
			indicator = 4;
			cs = s;
		}
	}
}

public record class ExecutionsT(NList<byte> OriginalFile)
{
	private readonly NList<byte>[] s = RedStarLinq.FillArray(ProgressBarGroups, _ => OriginalFile);
	private NList<byte> cs = OriginalFile;
	private int bwt = 0, rle = 0, lz = 0, misc = 0, lzP1 = 0, miscP3 = 0;

	public NList<byte> Encode()
	{
		Total = 0;
		TotalMaximum = ProgressBarStep * 6;
		var mainInput = new Compression(OriginalFile.ToNList(), [], 0).PreEncode(ref rle, out var originalFile2);
		if (rle != 0)
			throw new EncoderFallbackException();
		Total += ProgressBarStep;
		InitThreads(mainInput, originalFile2);
		ProcessThreads();
		if ((PresentMethodsT & UsedMethodsT.CS3) != 0 && s[2].Length < cs.Length && s[2].Length > 0 && s[2].Length < s[1].Length && s[2].Length < s[0].Length)
		{
			misc = miscP3;
			cs = s[2];
		}
		else if ((PresentMethodsT & UsedMethodsT.CS2) != 0 && s[1].Length < cs.Length && s[1].Length > 0 && s[1].Length < s[0].Length)
		{
			bwt = 2;
			cs = s[1];
		}
		else if ((PresentMethodsT & UsedMethodsT.CS1) != 0 && s[0].Length < cs.Length && s[0].Length > 0)
		{
			bwt = 0;
			lz = lzP1;
			cs = s[0];
		}
		else
			throw new EncoderFallbackException();
		var compressedFile = new[] { (byte)(misc + lz + bwt + 1) }.Concat(cs).ToNList();
#if DEBUG
		Validate(compressedFile);
#endif
		return compressedFile;
	}

	private void InitThreads(NList<ShortIntervalList> mainInput, NList<byte> originalFile2)
	{
		Threads[0] = new Thread(() =>
		{
			try
			{
				if ((PresentMethodsT & UsedMethodsT.CS1) != 0 && rle == 0)
					new Compression(originalFile2, mainInput, 0).Encode1(ref s[0], ref lzP1);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[1] = new Thread(() =>
		{
			try
			{
				if ((PresentMethodsT & UsedMethodsT.CS2) != 0 && rle == 0)
					new Compression(originalFile2, mainInput, 1).Encode2(ref s[1]);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[2] = new Thread(() =>
		{
			try
			{
				if ((PresentMethodsT & UsedMethodsT.CS3) != 0)
					new Compression(originalFile2, mainInput, 2).Encode3(ref s[2], ref miscP3);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		for (var i = 0; i < ProgressBarGroups; i++)
			if (Threads[i] != null && Threads[i].ThreadState is not System.Threading.ThreadState.Unstarted or System.Threading.ThreadState.Running)
				Threads[i] = default!;
	}

	private static void ProcessThreads()
	{
		Threads[0].Name = "Процесс сжатия для слов";
		Threads[1].Name = "Процесс сжатия для слов с BWT";
		Threads[2].Name = "Процесс сжатия PPM для слов";
		Threads.ForEach(x => _ = x == null || (x.IsBackground = true));
		Thread.CurrentThread.Priority = ThreadPriority.Lowest;
		Threads.ForEach(x => x?.Start());
		Threads.ForEach(x => x?.Join());
	}
#if DEBUG

	private void Validate(NList<byte> compressedFile)
	{
		try
		{
			using var dec = new DecodingT();
			using var decoded = dec.Decode(compressedFile, ProgramVersion);
			for (var i = 0; i < OriginalFile.Length; i++)
				if (OriginalFile[i] != decoded[i])
					throw new DecoderFallbackException();
			if (OriginalFile.Length != decoded.Length)
				throw new DecoderFallbackException();
		}
		catch (Exception ex) when (ex is not DecoderFallbackException)
		{
			throw new DecoderFallbackException();
		}
	}
#endif
}
