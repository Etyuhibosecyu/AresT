﻿using System.Net;
using System.Net.Sockets;

namespace AresTLib;

public static class MainClass
{
	private static TcpClient? client;
	private static NetworkStream? netStream;
	private static byte[] toSend = [];
	private static Thread thread = new(() => { });
	private static readonly int[] FibonacciSequence = [1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610, 987, 1597, 2584, 4181, 6765, 10946, 17711, 28657, 46368, 75025, 121393, 196418, 317811, 514229, 832040, 1346269, 2178309, 3524578, 5702887, 9227465, 14930352, 24157817, 39088169, 63245986, 102334155, 165580141, 267914296, 433494437, 701408733, 1134903170, 1836311903];
	private static FileStream rfs = default!;
	private static FileStream wfs = default!;
	private static int fragmentCount;
	private static bool continue_ = true;
	private static bool isWorking;
	private static readonly object lockObj = new();

	public static void Main(string[] args)
	{
#if !RELEASE
		args = ["11000"];
		Thread.Sleep(MillisecondsPerSecond * 2);
#endif
		if (!(args.Length != 0 && int.TryParse(args[0], out var port) && port >= 1024 && port <= 65535))
			return;
		if (args.Length >= 2 && int.TryParse(args[1], out var mainProcessId))
		{
			var mainProcess = Process.GetProcessById(mainProcessId);
			mainProcess.EnableRaisingEvents = true;
			mainProcess.Exited += (_, _) => Environment.Exit(0);
		}
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		Connect(port);
	}

	private static void Connect(int port)
	{
		IPEndPoint ipe = new(IPAddress.Loopback, port); //IP с номером порта
		client = new(); //подключение клиента
		try
		{
			client.Connect(ipe);
			netStream = client.GetStream();
			Thread receiveThread = new(ReceiveData) { Name = "Подключение" };//получение данных
			receiveThread.Start();//старт потока
		}
		catch
		{
			return;
		}
		while (true)
			SendMessage();
	}

	private static void SendMessage()
	{
		try
		{
			Thread.Sleep(MillisecondsPerSecond / 4);
			if (netStream == null)
				Disconnect();
			else if (toSend.Length != 0)
			{
				var toSendLen = BitConverter.GetBytes(toSend.Length);
				netStream.Write(toSendLen, 0, toSendLen.Length);
				netStream.Write(toSend, 0, toSend.Length);
				netStream.Flush(); //удаление данных из потока
				toSend = [];
			}
		}
		catch
		{
			Disconnect();
		}
	}

	private static void ReceiveData()
	{
		var receiveLen = new byte[4];
		byte[] receiveMessage;
		while (true)
		{
			try
			{
				if (netStream != null)
				{
					netStream.ReadExactly(receiveLen);//чтение сообщения
					receiveMessage = new byte[BitConverter.ToInt32(receiveLen)];
					netStream.ReadExactly(receiveMessage);
					WorkUpReceiveMessage(receiveMessage);
				}
				else
					Disconnect();
			}
			catch
			{
				Disconnect();
			}
		}
	}

	private static void WorkUpReceiveMessage(byte[] message)
	{
		try
		{
			if (message[0] == 0)
				PresentMethods = (UsedMethods)BitConverter.ToInt32(message.AsSpan(1..));
			else if (message[0] == 1)
			{
				FragmentLength = 1000000 << Min(BitConverter.ToInt32(message.AsSpan(1..)) & 0xF, 11);
			}
			else if (message[0] <= 4)
			{
				var filename = Encoding.UTF8.GetString(message[1..]);
				thread = new((message[0] - 2) switch
				{
					0 => () => MainThread(filename, (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\" + Path.GetFileNameWithoutExtension(filename), Decompress),
					1 => () => MainThread(filename, filename + ".ares-t", Compress),
					2 => () => MainThread(filename, Path.GetDirectoryName(filename) + @"\" + Path.GetFileNameWithoutExtension(filename), Decompress),
					3 => () => MainThread(filename, filename, Recompress),
					_ => throw new NotImplementedException(),
				})
				{ Name = "Основной процесс" };
				thread.Start();
				thread.IsBackground = true;
				Thread thread2 = new(TransferProgress) { Name = "Передача прогресса" };
				thread2.Start();
				thread2.IsBackground = true;
			}
		}
		catch
		{
			lock (lockObj)
			{
				isWorking = false;
				toSend = [2];
				SendMessage();
			}
		}
	}

	public static void MainThread(string filename, string filename2, Action<FileStream, FileStream> action, bool send = true)
	{
		var tempFilename = "";
		try
		{
			Supertotal = 0;
			isWorking = true;
			if (action == Compress)
				fragmentCount = (int)Max(Min((new FileInfo(filename).Length + FragmentLength - 1) / FragmentLength, int.MaxValue / 10), 1);
			rfs = File.OpenRead(filename);
			tempFilename = (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-" + Environment.ProcessId + ".tmp";
			wfs = File.Open(tempFilename, FileMode.Create);
			action(rfs, wfs);
			rfs.Close();
			wfs.Close();
			File.Move(tempFilename, filename2, true);
			lock (lockObj)
			{
				isWorking = false;
				toSend = [1];
				if (send)
					SendMessage();
			}
		}
		catch (DecoderFallbackException)
		{
			lock (lockObj)
			{
				isWorking = false;
				toSend = [(byte)(action == Compress ? 3 : 2)];
				if (send)
					SendMessage();
				else
					throw;
			}
		}
		catch
		{
			lock (lockObj)
			{
				isWorking = false;
				toSend = [2];
				if (send)
					SendMessage();
				else
					throw;
			}
		}
		finally
		{
			if (File.Exists(tempFilename))
			{
				rfs.Close();
				wfs.Close();
				File.Delete(tempFilename);
			}
		}
	}

	private static void TransferProgress()
	{
		Thread.Sleep(MillisecondsPerSecond);
		while (isWorking)
		{
			NList<byte> list =
			[
				0,
				.. BitConverter.GetBytes(Supertotal),
				.. BitConverter.GetBytes(SupertotalMaximum),
				.. BitConverter.GetBytes(Total),
				.. BitConverter.GetBytes(TotalMaximum),
			];
			for (var i = 0; i < ProgressBarGroups; i++)
			{
				list.AddRange(BitConverter.GetBytes(Subtotal[i]));
				list.AddRange(BitConverter.GetBytes(SubtotalMaximum[i]));
				list.AddRange(BitConverter.GetBytes(Current[i]));
				list.AddRange(BitConverter.GetBytes(CurrentMaximum[i]));
				list.AddRange(BitConverter.GetBytes(Status[i]));
				list.AddRange(BitConverter.GetBytes(StatusMaximum[i]));
			}
			lock (lockObj)
				toSend = [.. list];
			Thread.Sleep(MillisecondsPerSecond);
		}
	}

	public static void Disconnect()
	{
		client?.Close();//отключение клиента
		netStream?.Close();//отключение потока
		var tempFilename = (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-" + Environment.ProcessId + ".tmp";
		if (File.Exists(tempFilename))
		{
			rfs.Close();
			wfs.Close();
			File.Delete(tempFilename);
		}
		Environment.Exit(0); //завершение процесса
	}

	public static void Compress(FileStream rfs, FileStream wfs)
	{
		var bytes = Array.Empty<byte>();
		if (continue_)
		{
			Supertotal = 0;
			SupertotalMaximum = fragmentCount * 10;
			var fragmentCount2 = fragmentCount;
			BitList bits = default!;
			int i;
			for (i = FibonacciSequence.Length - 1; i >= 0; i--)
			{
				if (FibonacciSequence[i] <= fragmentCount2)
				{
					bits = new(i + 2, false) { [i] = true, [i + 1] = true };
					fragmentCount2 -= FibonacciSequence[i];
					break;
				}
			}
			for (i--; i >= 0;)
			{
				if (FibonacciSequence[i] <= fragmentCount2)
				{
					bits[i] = true;
					fragmentCount2 -= FibonacciSequence[i];
					i -= 2;
				}
				else
					i--;
			}
			bits.Insert(0, new BitList(6, ProgramVersion));
			bytes = new byte[(bits.Length + 7) / 8];
			bits.CopyTo(bytes, 0);
			wfs.Write(bytes, 0, bytes.Length);
		}
		if (fragmentCount != 1)
			bytes = new byte[FragmentLength];
		for (; fragmentCount > 0; fragmentCount--)
		{
			if (fragmentCount == 1)
			{
				var leftLength = (int)(rfs.Length % FragmentLength);
				if (leftLength != 0)
					bytes = new byte[leftLength];
			}
			rfs.ReadExactly(bytes);
			var s = new Executions(bytes).Encode();
			if (fragmentCount != 1)
				wfs.Write([(byte)(s.Length >> (BitsPerByte << 1)), (byte)(s.Length >> BitsPerByte), (byte)s.Length], 0, 3);
			wfs.Write(s, 0, s.Length);
			Supertotal += ProgressBarStep;
			GC.Collect();
		}
		if (wfs.Position > rfs.Length + 2)
		{
			wfs.Seek(0, SeekOrigin.Begin);
			wfs.Write([0], 0, 1);
			rfs.Seek(0, SeekOrigin.Begin);
			var bytes2 = rfs.Length < FragmentLength ? default! : new byte[FragmentLength];
			for (var i = 0; i < rfs.Length; i += FragmentLength)
			{
				var length = (int)Min(rfs.Length - i, FragmentLength);
				if (length < FragmentLength)
					bytes2 = new byte[length];
				rfs.ReadExactly(bytes2);
				wfs.Write(bytes2);
			}
			wfs.SetLength(wfs.Position);
		}
	}

	public static void Decompress(FileStream rfs, FileStream wfs)
	{
		PreservedFragmentLength = FragmentLength;
		FragmentLength = 2048000000;
		var readByte = (byte)rfs.ReadByte();
		var encodingVersion = (byte)(readByte & 63);
		if (encodingVersion == 0)
		{
			var bytes2 = rfs.Length < 2048000000 ? default! : new byte[2048000000];
			for (var i = 1; i < rfs.Length; i += 2048000000)
			{
				var length = (int)Min(rfs.Length - i, 2048000000);
				if (length < 2048000000)
					bytes2 = new byte[length];
				rfs.ReadExactly(bytes2);
				wfs.Write(bytes2);
			}
			wfs.SetLength(wfs.Position);
			return;
		}
		if (continue_)
		{
			fragmentCount = 0;
			DecodeFibonacci(rfs, readByte);
			SupertotalMaximum = fragmentCount * 10;
		}
		byte[] bytes;
		for (; fragmentCount > 0; fragmentCount--)
		{
			if (fragmentCount == 1)
			{
				bytes = new byte[(int)Min(rfs.Length - rfs.Position, 2048000002)];
				rfs.ReadExactly(bytes);
			}
			else
			{
				bytes = new byte[4];
				rfs.ReadExactly(bytes, 0, 3);
				var fragmentLength = Min(bytes[0] * ValuesIn2Bytes + bytes[1] * ValuesInByte + bytes[2], 2048000002);
				if (fragmentLength > 16000010)
				{
					rfs.ReadExactly(bytes);
					fragmentLength = Min(bytes[0] * ValuesIn3Bytes + bytes[1] * ValuesIn2Bytes + bytes[2] * ValuesInByte + bytes[3], 2048000002);
				}
				bytes = new byte[fragmentLength];
				rfs.ReadExactly(bytes);
			}
			var s = new Decoding().Decode(bytes, encodingVersion);
			wfs.Write(s, 0, s.Length);
			Supertotal += ProgressBarStep;
			GC.Collect();
		}
		FragmentLength = PreservedFragmentLength;
	}

	private static void Recompress(FileStream rfs, FileStream wfs)
	{
		PreservedFragmentLength = FragmentLength;
		FragmentLength = 2048000000;
		var readByte = (byte)rfs.ReadByte();
		var encodingVersion = (byte)(readByte & 63);
		if (encodingVersion >= ProgramVersion)
			wfs.WriteByte(readByte);
		if (encodingVersion is 0 or >= ProgramVersion)
		{
			var bytes2 = rfs.Length < 2048000000 ? default! : new byte[2048000000];
			for (var i = 1; i < rfs.Length; i += 2048000000)
			{
				var length = (int)Min(rfs.Length - i, 2048000000);
				if (length < 2048000000)
					bytes2 = new byte[length];
				rfs.ReadExactly(bytes2);
				wfs.Write(bytes2);
			}
			wfs.SetLength(wfs.Position);
			return;
		}
		if (continue_)
		{
			fragmentCount = 0;
			DecodeFibonacci(rfs, readByte);
			SupertotalMaximum = fragmentCount * 10;
		}
		byte[] bytes;
		for (; fragmentCount > 0; fragmentCount--)
		{
			if (fragmentCount == 1)
			{
				bytes = new byte[(int)Min(rfs.Length - rfs.Position, 2048000002)];
				rfs.ReadExactly(bytes);
			}
			else
			{
				bytes = new byte[4];
				rfs.ReadExactly(bytes, 0, 3);
				var fragmentLength = (int)Min((uint)bytes[0] * ValuesIn2Bytes + bytes[1] * ValuesInByte + bytes[2], 2048000002);
				if (fragmentLength == 0xFFFFFF)
				{
					rfs.ReadExactly(bytes);
					fragmentLength = Min(bytes[0] * ValuesIn3Bytes + bytes[1] * ValuesIn2Bytes + bytes[2] * ValuesInByte + bytes[3], 2048000002);
				}
				bytes = new byte[fragmentLength];
				rfs.ReadExactly(bytes);
			}
			var s = new Executions(new Decoding().Decode(bytes, encodingVersion)).Encode();
			if (fragmentCount != 1)
				wfs.Write([(byte)(s.Length >> (BitsPerByte << 1)), (byte)(s.Length >> BitsPerByte), (byte)s.Length], 0, 3);
			wfs.Write(s, 0, s.Length);
			Supertotal += ProgressBarStep;
			GC.Collect();
		}
		if (wfs.Position > rfs.Length)
		{
			wfs.Seek(0, SeekOrigin.Begin);
			rfs.Seek(0, SeekOrigin.Begin);
			var bytes2 = rfs.Length < 2048000000 ? default! : new byte[2048000000];
			for (var i = 0; i < rfs.Length; i += 2048000000)
			{
				var length = (int)Min(rfs.Length - i, 2048000000);
				if (length < 2048000000)
					bytes2 = new byte[length];
				rfs.ReadExactly(bytes2);
				wfs.Write(bytes2);
			}
			wfs.SetLength(wfs.Position);
		}
		FragmentLength = PreservedFragmentLength;
	}

	private static void DecodeFibonacci(FileStream rfs, byte readByte)
	{
		BitList bits;
		bool one = false, success = false;
		var sequencePos = 0;
		while (1 == 1)
		{
			if (sequencePos >= 2)
				readByte = (byte)rfs.ReadByte();
			bits = sequencePos < 2 ? new(2, (byte)(readByte >> 6)) : new(8, readByte);
			for (var i = 0; i < bits.Length; i++)
			{
				if (bits[i] && one || sequencePos == FibonacciSequence.Length)
				{
					success = true;
					break;
				}
				else
				{
					if (bits[i]) fragmentCount += FibonacciSequence[sequencePos];
					sequencePos++;
					one = bits[i];
				}
			}
			if (success)
				break;
		}
	}
}
