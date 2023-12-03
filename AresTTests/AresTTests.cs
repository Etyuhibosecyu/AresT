global using AresTLib;
global using Corlib.NStar;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using System;
global using System.IO;
global using System.Text;
global using UnsafeFunctions;
global using G = System.Collections.Generic;
global using static AresTLib.Global;
global using static Corlib.NStar.Extents;
global using static System.Math;
global using static UnsafeFunctions.Global;
using System.Text.RegularExpressions;

namespace AresTTests;

[TestClass]
public partial class DecompressionTests
{
	private readonly string[] files = Directory.GetFiles(ExcludeBinRegex().Replace(AppDomain.CurrentDomain.BaseDirectory, ""), "*.txt", SearchOption.TopDirectoryOnly);
	private readonly string[] programLogFiles = Directory.GetFiles(ExcludeBinRegex().Replace(AppDomain.CurrentDomain.BaseDirectory, ""), "Program log*.ares-t", SearchOption.TopDirectoryOnly);
	private readonly string[] wapFiles = Directory.GetFiles(ExcludeBinRegex().Replace(AppDomain.CurrentDomain.BaseDirectory, ""), "Война и мир*.ares-t", SearchOption.TopDirectoryOnly);

	[TestMethod]
	public void TestHF()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		PresentMethods = UsedMethods.CS1 | UsedMethods.HF1;
		foreach (var file in files)
			MainClass.MainThread(file, (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-" + Environment.ProcessId + ".tmp", MainClass.Compress, false);
	}

	[TestMethod]
	public void TestLZ()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		PresentMethods = UsedMethods.CS1 | UsedMethods.LZ1;
		foreach (var file in files)
			MainClass.MainThread(file, (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-" + Environment.ProcessId + ".tmp", MainClass.Compress, false);
	}

	[TestMethod]
	public void TestHF_LZ()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		PresentMethods = UsedMethods.CS1 | UsedMethods.LZ1 | UsedMethods.HF1;
		foreach (var file in files)
			MainClass.MainThread(file, (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-" + Environment.ProcessId + ".tmp", MainClass.Compress, false);
	}

	[TestMethod]
	public void TestHFW()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		PresentMethods = UsedMethods.CS2;
		foreach (var file in files)
			MainClass.MainThread(file, (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-" + Environment.ProcessId + ".tmp", MainClass.Compress, false);
	}

	[TestMethod]
	public void TestHFW_LZ()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		PresentMethods = UsedMethods.CS2 | UsedMethods.LZ2;
		foreach (var file in files)
			MainClass.MainThread(file, (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-" + Environment.ProcessId + ".tmp", MainClass.Compress, false);
	}

	[TestMethod]
	public void TestHFW_SHET()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		PresentMethods = UsedMethods.CS2 | UsedMethods.SHET2;
		foreach (var file in files)
			MainClass.MainThread(file, (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-" + Environment.ProcessId + ".tmp", MainClass.Compress, false);
	}

	[TestMethod]
	public void TestHFW_LZ_SHET()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		PresentMethods = UsedMethods.CS2 | UsedMethods.LZ2 | UsedMethods.SHET2;
		foreach (var file in files)
			MainClass.MainThread(file, (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-" + Environment.ProcessId + ".tmp", MainClass.Compress, false);
	}

	[TestMethod]
	public void TestHF_BWT()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		PresentMethods = UsedMethods.CS3;
		foreach (var file in files)
			MainClass.MainThread(file, (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-" + Environment.ProcessId + ".tmp", MainClass.Compress, false);
	}

	[TestMethod]
	public void TestAHF_BWT()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		PresentMethods = UsedMethods.CS3 | UsedMethods.AHF3;
		foreach (var file in files)
			MainClass.MainThread(file, (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-" + Environment.ProcessId + ".tmp", MainClass.Compress, false);
	}

	[TestMethod]
	public void TestHFW_BWT()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		PresentMethods = UsedMethods.CS4;
		foreach (var file in files)
			MainClass.MainThread(file, (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-" + Environment.ProcessId + ".tmp", MainClass.Compress, false);
	}

	[TestMethod]
	public void TestHFW_BWT_SHET()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		PresentMethods = UsedMethods.CS4 | UsedMethods.SHET4;
		foreach (var file in files)
			MainClass.MainThread(file, (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-" + Environment.ProcessId + ".tmp", MainClass.Compress, false);
	}

	[TestMethod]
	public void TestProgramLogDecompression()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		PresentMethods = UsedMethods.CS4 | UsedMethods.SHET4;
		foreach (var file in programLogFiles)
		{
			MainClass.MainThread(file, (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-decompressed.tmp", MainClass.Decompress, false);
			Assert.IsTrue(RedStarLinq.Equals(File.ReadAllBytes((Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-decompressed.tmp"), File.ReadAllBytes(ExcludeBinRegex().Replace(AppDomain.CurrentDomain.BaseDirectory, "") + @"Program log.txt")));
		}
		File.Delete((Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-decompressed.tmp");
	}

	[TestMethod]
	public void TestWAPDecompression()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		PresentMethods = UsedMethods.CS4 | UsedMethods.SHET4;
		foreach (var file in wapFiles)
		{
			MainClass.MainThread(file, (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-decompressed.tmp", MainClass.Decompress, false);
			Assert.IsTrue(RedStarLinq.Equals(File.ReadAllBytes((Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-decompressed.tmp"), File.ReadAllBytes(ExcludeBinRegex().Replace(AppDomain.CurrentDomain.BaseDirectory, "") + @"Война и мир.txt")));
		}
		File.Delete((Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-decompressed.tmp");
	}

	[GeneratedRegex(@"(?<=\\)bin\\.*")]
	private static partial Regex ExcludeBinRegex();
}
