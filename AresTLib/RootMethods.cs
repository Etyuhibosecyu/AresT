
namespace AresTLib;

internal partial class Compression(NList<byte> originalFile, List<ShortIntervalList> input, int tn)
{
	private readonly List<ShortIntervalList> result = [];

	internal List<ShortIntervalList> PreEncode(ref int rle, out NList<byte> originalFile2)
	{
		List<ShortIntervalList> cdl;
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
		cdl = new ShortIntervalList[originalFile2.Length + 1];
		cdl[0] = [RepeatsNotApplied];
		var originalFile2_ = originalFile2;
		Parallel.For(0, originalFile2.Length, i => cdl[i + 1] = ByteIntervals[originalFile2_[i]]);
		Subtotal[tn] += ProgressBarStep;
		return cdl;
	}

	internal void Encode1(ref byte[] cs, ref int hf, ref int lz)
	{
		byte[] s;
		List<ShortIntervalList> dl1, cdl = input;
		LZData lzData = new();
		if ((PresentMethods & UsedMethods.LZ1) != 0)
		{
			dl1 = new(new LempelZiv(cdl, result, tn).Encode(out lzData));
			Subtotal[tn] += ProgressBarStep;
			s = WorkUpDoubleList(dl1, tn);
		}
		else
		{
			Subtotal[tn] += ProgressBarStep;
			dl1 = cdl;
			s = cs;
		}
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < cs.Length && s.Length > 0)
		{
			lz = 21;
			cdl = dl1;
			cs = s;
		}
		if ((PresentMethods & UsedMethods.HF1) != 0)
			s = new AdaptiveHuffman(tn).Encode(cdl, lzData);
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < cs.Length && s.Length > 0)
		{
			hf = 4;
			cs = s;
		}
	}

	internal void Encode2(ref byte[] cs, ref int hf, ref int lz)
	{
		byte[] s, cs2 = cs;
		List<List<ShortIntervalList>> tl1, ctl;
		LZData[] lzData = [new(), new(), new()];
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 9;
		ctl = MakeWordsSplit((PresentMethods & UsedMethods.COMB2) != 0, (PresentMethods & UsedMethods.FAB2) != 0);
		if (ctl.Length == 0)
			throw new EncoderFallbackException();
		Subtotal[tn] += ProgressBarStep;
		tl1 = RedStarLinq.Fill(ctl.Length, _ => new List<ShortIntervalList>());
		if ((PresentMethods & UsedMethods.LZ2) != 0)
		{
			tl1 = RedStarLinq.Fill(ctl.Length, _ => new List<ShortIntervalList>());
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
			lz = 21;
			ctl = tl1;
			cs2 = s;
		}
		s = new AdaptiveHuffman(tn).Encode(ctl, lzData);
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < cs2.Length && s.Length > 0)
		{
			hf = 4 + ((PresentMethods & UsedMethods.FAB2) != 0 ? 2 : 1);
			cs = s;
		}
	}

	internal void Encode3(ref byte[] cs, ref int hf)
	{
		byte[] s;
		List<ShortIntervalList> dl1, cdl = input;
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 4;
		Subtotal[tn] += ProgressBarStep;
		dl1 = new(BWT(cdl));
		Subtotal[tn] += ProgressBarStep;
		if ((PresentMethods & UsedMethods.AHF3) != 0)
		{
			s = new AdaptiveHuffman(tn).Encode(dl1, new());
			Subtotal[tn] += ProgressBarStep;
		}
		else
		{
			dl1 = new(new Huffman(dl1, result, tn).Encode());
			Subtotal[tn] += ProgressBarStep;
			s = WorkUpDoubleList(dl1, tn);
		}
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < cs.Length && s.Length > 0)
		{
			hf = (PresentMethods & UsedMethods.AHF3) != 0 ? 4 : 1;
			cs = s;
		}
	}

	internal void Encode4(ref byte[] cs, ref int hf)
	{
		byte[] s;
		List<List<ShortIntervalList>> ctl;
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 7;
		ctl = MakeWordsSplit((PresentMethods & UsedMethods.COMB4) != 0, (PresentMethods & UsedMethods.FAB4) != 0);
		if (ctl.Length is not (3 or 4))
			throw new EncoderFallbackException();
		Subtotal[tn] += ProgressBarStep;
		ctl[1] = new(BWT(ctl[1], true));
		Subtotal[tn] += ProgressBarStep;
		s = new AdaptiveHuffman(tn).Encode(ctl, new LZData[ctl.Length]);
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < cs.Length && s.Length > 0)
		{
			hf = 4 + ((PresentMethods & UsedMethods.FAB4) != 0 ? 2 : 1);
			cs = s;
		}
	}

	internal void Encode5(ref byte[] cs, ref int indicator)
	{
		byte[] s;
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 4;
		ArchaicHuffman(input);
		s = new LZMA(tn).Encode(input);
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < originalFile.Length && s.Length > 0)
		{
			indicator = 64;
			cs = s;
		}
	}

	internal void Encode6(ref byte[] cs, ref int indicator)
	{
		byte[] s;
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 2;
		var ppm = new PPM(tn);
		s = ppm.Encode(input);
		ppm.Dispose();
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < originalFile.Length && s.Length > 0)
		{
			indicator = 65;
			cs = s;
		}
	}

	internal void Encode7(ref byte[] cs, ref int indicator)
	{
		byte[] s;
		Subtotal[tn] = 0;
		SubtotalMaximum[tn] = ProgressBarStep * 2;
		var ctl = MakeWordsSplit((PresentMethods & UsedMethods.COMB7) != 0, (PresentMethods & UsedMethods.FAB7) != 0);
		if (ctl.Length == 0)
			throw new EncoderFallbackException();
		Subtotal[tn] += ProgressBarStep;
		var ppm = new PPM(tn);
		s = ppm.Encode(ctl);
		ppm.Dispose();
		Subtotal[tn] += ProgressBarStep;
		if (s.Length < originalFile.Length && s.Length > 0)
		{
			indicator = (PresentMethods & UsedMethods.FAB7) != 0 ? 67 : 66;
			cs = s;
		}
	}
}

public record class Executions(byte[] OriginalFile)
{
	private readonly byte[][] s = RedStarLinq.FillArray(ProgressBarGroups, _ => OriginalFile);
	private byte[] cs = OriginalFile;
	private int hf = 0, bwt = 0, rle = 0, lz = 0, misc = 0, hfP1 = 0, lzP1 = 0, hfP2 = 0, lzP2 = 0, hfP3 = 0, hfP4 = 0, miscP5 = 0, miscP6 = 0, miscP7 = 0;

	public byte[] Encode()
	{
		Total = 0;
		TotalMaximum = ProgressBarStep * 6;
		var mainInput = new Compression(OriginalFile.ToNList(), [], 0).PreEncode(ref rle, out var originalFile2);
		Total += ProgressBarStep;
		InitThreads(mainInput, originalFile2);
		ProcessThreads();
		if ((PresentMethods & UsedMethods.CS7) != 0 && s[6].Length < cs.Length && s[6].Length > 0 && s.GetSlice(0, 6).All(x => s[6].Length < x.Length))
		{
			misc = miscP7;
			cs = s[6];
		}
		else if ((PresentMethods & UsedMethods.CS6) != 0 && s[5].Length < cs.Length && s[5].Length > 0 && s.GetSlice(0, 5).All(x => s[5].Length < x.Length))
		{
			misc = miscP6;
			cs = s[5];
		}
		else if ((PresentMethods & UsedMethods.CS5) != 0 && s[4].Length < cs.Length && s[4].Length > 0 && s.GetSlice(0, 4).All(x => s[4].Length < x.Length))
		{
			misc = miscP5;
			cs = s[4];
		}
		else if ((PresentMethods & UsedMethods.CS4) != 0 && s[3].Length < cs.Length && s[3].Length > 0 && s.GetSlice(0, 3).All(x => s[3].Length < x.Length))
		{
			hf = hfP4;
			bwt = 42;
			cs = s[3];
		}
		else if ((PresentMethods & UsedMethods.CS3) != 0 && s[2].Length < cs.Length && s[2].Length > 0 && s[2].Length < s[1].Length && s[2].Length < s[0].Length)
		{
			hf = hfP3;
			bwt = 42;
			cs = s[2];
		}
		else if ((PresentMethods & UsedMethods.CS2) != 0 && s[1].Length < cs.Length && s[1].Length > 0 && s[1].Length < s[0].Length)
		{
			hf = hfP2;
			bwt = 0;
			lz = lzP2;
			cs = s[1];
		}
		else if ((PresentMethods & UsedMethods.CS1) != 0 && s[0].Length < cs.Length && s[0].Length > 0)
		{
			hf = hfP1;
			bwt = 0;
			lz = lzP1;
			cs = s[0];
		}
		else
			return [(byte)rle, .. originalFile2];
		var compressedFile = new[] { (byte)(misc + lz + bwt + rle + hf) }.Concat(cs).ToArray();
#if DEBUG
		Validate(compressedFile);
#endif
		return compressedFile;
	}

	private void InitThreads(List<ShortIntervalList> mainInput, NList<byte> originalFile2)
	{
		Threads[0] = new Thread(() =>
		{
			try
			{
				if ((PresentMethods & UsedMethods.CS1) != 0)
					new Compression(originalFile2, mainInput, 0).Encode1(ref s[0], ref hfP1, ref lzP1);
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
				if ((PresentMethods & UsedMethods.CS2) != 0 && rle == 0)
					new Compression(originalFile2, mainInput, 1).Encode2(ref s[1], ref hfP2, ref lzP2);
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
				if ((PresentMethods & UsedMethods.CS3) != 0)
					new Compression(originalFile2, mainInput, 2).Encode3(ref s[2], ref hfP3);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[3] = new Thread(() =>
		{
			try
			{
				if ((PresentMethods & UsedMethods.CS4) != 0 && rle == 0)
					new Compression(originalFile2, mainInput, 3).Encode4(ref s[3], ref hfP4);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[4] = new Thread(() =>
		{
			try
			{
				if ((PresentMethods & UsedMethods.CS5) != 0)
					new Compression(originalFile2, mainInput, 4).Encode5(ref s[4], ref miscP5);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[5] = new Thread(() =>
		{
			try
			{
				if ((PresentMethods & UsedMethods.CS6) != 0)
					new Compression(originalFile2, mainInput, 5).Encode6(ref s[5], ref miscP6);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
		Threads[6] = new Thread(() =>
		{
			try
			{
				if ((PresentMethods & UsedMethods.CS7) != 0)
					new Compression(originalFile2, mainInput, 6).Encode7(ref s[6], ref miscP7);
			}
			catch
			{
			}
			Total += ProgressBarStep;
		});
	}

	private static void ProcessThreads()
	{
		Threads[0].Name = "Процесс классического сжатия";
		Threads[1].Name = "Процесс сжатия для слов";
		Threads[2].Name = "Процесс сжатия с BWT";
		Threads[3].Name = "Процесс сжатия для слов с BWT";
		Threads[4].Name = "Процесс сжатия LZMA";
		Threads[5].Name = "Процесс сжатия PPM";
		Threads[6].Name = "Процесс сжатия PPM для слов";
		Threads[0].Priority = ThreadPriority.Normal;
		Threads[1].Priority = ThreadPriority.Normal;
		Threads[2].Priority = ThreadPriority.AboveNormal;
		Threads[3].Priority = ThreadPriority.Highest;
		Threads[4].Priority = ThreadPriority.Normal;
		Threads[5].Priority = ThreadPriority.Normal;
		Threads[6].Priority = ThreadPriority.Normal;
		Threads.ForEach(x => _ = x == null || (x.IsBackground = true));
		Thread.CurrentThread.Priority = ThreadPriority.Lowest;
		Threads.ForEach(x => x?.Start());
		Threads.ForEach(x => x?.Join());
	}
#if DEBUG

	private void Validate(byte[] compressedFile)
	{
		try
		{
			var decoded = new Decoding().Decode(compressedFile, ProgramVersion);
			for (var i = 0; i < OriginalFile.Length; i++)
				if (OriginalFile[i] != decoded[i])
					throw new DecoderFallbackException();
		}
		catch (Exception ex) when (ex is not DecoderFallbackException)
		{
			throw new DecoderFallbackException();
		}
	}
#endif
}
