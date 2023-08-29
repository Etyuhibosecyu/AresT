using AresTLib;
using Corlib.NStar;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tmds.Utils;
using static System.Console;
using static System.Math;
using static UnsafeFunctions.Global;

if (ExecFunction.IsExecFunctionCommand(args))
{
	ExecFunction.Program.Main(args);
	Environment.Exit(0);
}
var emptyFileName = false;
Thread thread = new(() => { });
var filename = "";
OperationType operation_type = default;
var continue_ = false;
var is_working = false;
var usedMethods = UsedMethods.CS1 | UsedMethods.HF1 | UsedMethods.LZ1 | UsedMethods.CS2 | UsedMethods.LZ2;
#if !DEBUG
DateTime compressionStart = default;
#endif

TcpListener tcpListener = default!; //монитор подключений TCP клиентов
Thread listenThread = default!; //создание потока

List<TcpClient> clients = new(); //список клиентских подключений
List<NetworkStream> netStream = new(); //список потока данных
var port = 11000;
Process executor = default!;
#if RELEASE
Random random = new(1234567890);
#endif

double Supertotal = 0;
double Total = 0;
var Subtotal = new double[ProgressBarGroups];
var Current = new double[ProgressBarGroups];
var Status = new double[ProgressBarGroups];
var StatusSeconds = new int[ProgressBarGroups];

void ClientReceive(object? ID)
{
	var thisID = (int?)ID ?? 0;
	byte[] receive;
	var receiveLen = new byte[4];
	while (true)
	{
		try
		{
			netStream[thisID].Read(receiveLen, 0, 4);
			receive = new byte[BitConverter.ToInt32(receiveLen)];
			netStream[thisID].Read(receive, 0, receive.Length);
			WorkUpReceiveMessage(receive);
		}
		catch
		{
			clients.RemoveAt(thisID);
			netStream.RemoveAt(thisID);
			break;
		}
	}
}

void Disconnect()
{
	tcpListener.Stop(); //остановка чтения
	for (var i = 0; i < clients.Length; i++)
	{
		clients[i].Close(); //отключение клиента
		netStream[i].Close(); //отключение потока
	}
	Environment.Exit(0); //завершение процесса
}

void ExecutorExited(object? sender, EventArgs? e)
{
	WriteLine("Произошла серьезная ошибка в рабочем модуле Ares и он аварийно завершился. Введите 'y', чтобы перезапустить его, или 'n', чтобы выйти из приложения.");
	var refuse = ReadLine()?.ToLower() is "n" or "no";
	if (refuse)
		Environment.Exit(0);
	SetProgressBarsFull();
	StartExecutor();
}

void StartExecutor()
{
	executor = ExecFunction.Start(AresTLib.MainClass.Main, new[] { port.ToString() });
	executor.EnableRaisingEvents = true;
	executor.Exited += ExecutorExited;
}

void ListenThread()
{
	tcpListener.Start();
	while (true)
	{
		clients.Add(tcpListener.AcceptTcpClient()); //подключение пользователя
		netStream.Add(clients[^1].GetStream()); //обьект для получения данных
		Thread clientThread = new(new ParameterizedThreadStart(ClientReceive)) { Name = "Соединение с клиентом #" + clients.Length.ToString() };
		clientThread.Start(clients.Length - 1);
		clientThread.IsBackground = true;
	}
}

void SendMessageToClient(int index, byte[] toSend)
{
	var toSendLen = BitConverter.GetBytes(toSend.Length);
	netStream[index].Write(toSendLen, 0, toSendLen.Length);
	netStream[index].Write(toSend, 0, toSend.Length);
	netStream[index].Flush(); //удаление данных из потока
}

void WorkUpReceiveMessage(byte[] message)
{
	try
	{
		if (message.Length == 0 || Subtotal == null || Current == null || Status == null || StatusSeconds == null)
			return;
		if (message[0] == 0 && message.Length == ProgressBarGroups * 24 + 17 && is_working)
		{
			var newSupertotal = (double)BitConverter.ToInt32(message.AsSpan(1, 4)) / (BitConverter.ToInt32(message.AsSpan(5, 4)) + 1);
			if (Abs(newSupertotal - Supertotal) >= 0.0001)
			{
				WriteLine("Supertotal: {0:F2}%", newSupertotal * 100);
				Supertotal = newSupertotal;
				StatusSeconds.SetAll(0);
			}
			var newTotal = (double)BitConverter.ToInt32(message.AsSpan(9, 4)) / (BitConverter.ToInt32(message.AsSpan(13, 4)) + 1);
			if (Abs(newTotal - Total) >= 0.0001)
			{
				WriteLine("Total: {0:F2}%", newTotal * 100);
				Total = newTotal;
				StatusSeconds.SetAll(0);
			}
			var newSubtotal = new double[ProgressBarGroups];
			var newCurrent = new double[ProgressBarGroups];
			var newStatus = new double[ProgressBarGroups];
			for (var i = 0; i < ProgressBarGroups; i++)
			{
				newSubtotal[i] = (double)BitConverter.ToInt32(message.AsSpan(i * 24 + 17, 4)) / (BitConverter.ToInt32(message.AsSpan(i * 24 + 21, 4)) + 1);
				if (Abs(newSubtotal[i] - Subtotal[i]) >= 0.0001)
				{
					WriteLine("Subtotal[{0}]: {1:F2}%", i, newSubtotal[i] * 100);
					Subtotal[i] = newSubtotal[i];
					StatusSeconds[i] = 0;
				}
				newCurrent[i] = (double)BitConverter.ToInt32(message.AsSpan(i * 24 + 25, 4)) / (BitConverter.ToInt32(message.AsSpan(i * 24 + 29, 4)) + 1);
				if (Abs(newCurrent[i] - Current[i]) >= 0.0001)
				{
					WriteLine("Current[{0}]: {1:F2}%", i, newCurrent[i] * 100);
					Current[i] = newCurrent[i];
					StatusSeconds[i] = 0;
				}
				newStatus[i] = (double)BitConverter.ToInt32(message.AsSpan(i * 24 + 33, 4)) / (BitConverter.ToInt32(message.AsSpan(i * 24 + 37, 4)) + 1);
				if (++StatusSeconds[i] >= 10 && Status[i] != 0)
				{
					WriteLine("Status[{0}]: {1:F2}%", i, newStatus[i] * 100);
					Status[i] = newStatus[i];
				}
			}
		}
		else if (message[0] == 1)
		{
			var timeString = "";
#if !DEBUG
			var elapsed = DateTime.Now - compressionStart;
			timeString += " (" + (elapsed.Days == 0 ? "" : $"{elapsed.Days:D}:") + (elapsed.Days == 0 && elapsed.Hours == 0 ? "" : $"{elapsed.Hours:D2}:") + $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}.{elapsed.Milliseconds:D3})";
#endif
			if (operation_type == OperationType.Opening)
			{
				var path = (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\" + Path.GetFileNameWithoutExtension(filename);
				using Process process = new();
				process.StartInfo.FileName = "explorer";
				process.StartInfo.Arguments = "\"" + path + "\"";
				process.Start();
				System.Threading.Thread.Sleep(1000);
				File.Delete(path);
			}
			WriteLine("Файл успешно " + (operation_type == OperationType.Compression ? "сжат" : "распакован") + timeString + "!");
		}
		else if (message[0] == 2)
			WriteLine("Ошибка! Не удалось " + (operation_type == OperationType.Compression ? "сжать" : "распаковать") + " файл.");
		else if (message[0] == 3)
			WriteLine("Ошибка! Файл сжат, но распаковка не удалась.");
		if (message[0] is not 0)
		{
			SetProgressBarsFull();
		}
	}
	catch
	{
		if (message.Length != 0 && message[0] is not 0)
			WriteLine("Произошла серьезная ошибка при попытке выполнить действие. Повторите попытку позже. Если проблема не исчезает, обратитесь к разработчикам приложения.");
	}
}

void Thread() => ThreadBool(false);

void ThreadBool(bool startImmediate)
{
	if (operation_type == OperationType.Opening)
	{
		if (!startImmediate && ProcessStartup(".ares-t") is bool || filename == null)
			return;
		try
		{
			is_working = true;
			SendMessageToClient(0, Encoding.UTF8.GetBytes(filename).Prepend((byte)1).ToArray());
#if !DEBUG
			compressionStart = DateTime.Now;
#endif
		}
		catch (OperationCanceledException)
		{
		}
		catch
		{
			WriteLine("Ошибка! Не удалось распаковать файл.");
		}
	}
	else if (operation_type == OperationType.Compression)
	{
		if (ProcessStartup("") is bool || filename == null)
			return;
		try
		{
			is_working = true;
			SendMessageToClient(0, Encoding.UTF8.GetBytes(filename).Prepend((byte)2).ToArray());
#if !DEBUG
			compressionStart = DateTime.Now;
#endif
		}
		catch (OperationCanceledException)
		{
		}
		catch
		{
			WriteLine("Ошибка! Не удалось сжать файл.");
		}
	}
	else if (operation_type == OperationType.Unpacking)
	{
		if (ProcessStartup(".ares-t") is bool || filename == null)
			return;
		try
		{
			is_working = true;
			SendMessageToClient(0, Encoding.UTF8.GetBytes(filename).Prepend((byte)3).ToArray());
#if !DEBUG
			compressionStart = DateTime.Now;
#endif
		}
		catch (OperationCanceledException)
		{
		}
		catch
		{
			WriteLine("Ошибка! Не удалось распаковать файл.");
		}
	}
}

bool? ProcessStartup(string filter)
{
	StatusSeconds?.SetAll(0);
	if (!continue_)
	{
		if (emptyFileName == true)
			return true;
	}
	return null;
}

void SetProgressBarsFull()
{
	continue_ = false;
	is_working = false;
}

void Open()
{
	operation_type = OperationType.Opening;
	thread = new Thread(new ThreadStart(Thread)) { Name = "Поток открытия" };
	thread.Start();
	thread.IsBackground = true;
}

void Compress()
{
	operation_type = OperationType.Compression;
	thread = new Thread(new ThreadStart(Thread)) { Name = "Поток сжатия" };
	thread.Start();
	thread.IsBackground = true;
}

void Unpack()
{
	operation_type = OperationType.Unpacking;
	thread = new Thread(new ThreadStart(Thread)) { Name = "Поток распаковки" };
	thread.Start();
	thread.IsBackground = true;
}

void Stop(object? sender, ConsoleCancelEventArgs e)
{
	executor?.Kill();
	continue_ = false;
}

void SendUsedMethods()
{
	if (netStream.Length != 0)
		SendMessageToClient(0, BitConverter.GetBytes((int)usedMethods).Prepend((byte)0).ToArray());
}

if (ExecFunction.IsExecFunctionCommand(args))
{
	ExecFunction.Program.Main(args);
	Environment.Exit(0);
}
filename = args.Length == 0 ? "" : args[0];
#if RELEASE
	port = random.Next(1024, 65536);
	StartExecutor();
#endif
	System.Threading.Thread.CurrentThread.Priority = ThreadPriority.Normal;
	try
	{
		tcpListener = new (IPAddress.Loopback, port);
		listenThread = new (new ThreadStart(ListenThread)) { Name = "Ожидание подключения клиентов" };
listenThread.Start(); //старт потока
listenThread.IsBackground = true;
if ((args.Length == 0 ? "" : args[0]) != "")
{
	System.Threading.Thread.Sleep(MillisecondsPerSecond / 4);
	operation_type = OperationType.Opening;
	thread = new Thread(new ThreadStart(() => ThreadBool(true)));
	thread.Start();
	thread.IsBackground = true;
}
	}
	catch
	{
	Disconnect();
}
CancelKeyPress += Stop;
const string help = @"Использование:
Open <filename> - распаковать и открыть файл (только .ares-t)
Compress <filename> - сжать файл
Unpack <filename> - распаковать файл в ту же папку, поверх оригинала (только .ares-t)
Start, Optimus, Pro, Pro+, Unlim - установить один из оптимальных режимов
+<method>, -<method> - включить или отключить метод (можно указать несколько на строку)
Возможные методы:
CS1, LZ1, HF1, PSLZ1
CS2, LZ2, SHET2
CS3
CS4, SHET4
AHF
Help - помощь (этот текст)
Exit - выход";
WriteLine(help);
while (true)
{
	var s = (ReadLine()?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>()).AsSpan();
	if (s.Length == 0)
		continue;
	if (is_working)
	{
		WriteLine("Подождите! Ну или нажмите Ctrl+C.");
		continue;
	}
	switch (s[0])
	{
		case "Open":
			if (s.Length > 1 && ((s[1] = string.Join(' ', s[1..].ToArray())).Length >= 2 && s[1][0] == '\"' && s[1][^1] == '\"' && (s[1] = s[1][1..^1]).Length > 0 || false) && Path.GetExtension(s[1]) == ".ares-t" && File.Exists(s[1]))
			{
				filename = s[1];
				Open();
			}
			else
			{
				WriteLine("Некорректная команда! Введите Help, чтобы получить помощь.");
			}
			goto l1;
		case "Compress":
			if (s.Length > 1 && ((s[1] = string.Join(' ', s[1..].ToArray())).Length >= 2 && s[1][0] == '\"' && s[1][^1] == '\"' && (s[1] = s[1][1..^1]).Length > 0 || false) && File.Exists(s[1]))
			{
				filename = s[1];
				Compress();
			}
			else
			{
				WriteLine("Некорректная команда! Введите Help, чтобы получить помощь.");
			}
			goto l1;
		case "Unpack":
			if (s.Length > 1 && ((s[1] = string.Join(' ', s[1..].ToArray())).Length >= 2 && s[1][0] == '\"' && s[1][^1] == '\"' && (s[1] = s[1][1..^1]).Length > 0 || false) && Path.GetExtension(s[1]) == ".ares-t" && File.Exists(s[1]))
			{
				filename = s[1];
				Unpack();
			}
			else
			{
				WriteLine("Некорректная команда! Введите Help, чтобы получить помощь.");
			}
			goto l1;
		case "Start":
			usedMethods = UsedMethods.CS1 | UsedMethods.LZ1 | UsedMethods.HF1;
			goto l0;
		case "Optimus":
			usedMethods = UsedMethods.CS1 | UsedMethods.LZ1 | UsedMethods.HF1 | UsedMethods.CS2 | UsedMethods.LZ2;
			goto l0;
		case "Pro":
			usedMethods = UsedMethods.CS1 | UsedMethods.LZ1 | UsedMethods.HF1 | UsedMethods.CS2 | UsedMethods.LZ2 | UsedMethods.CS3 | UsedMethods.CS4;
			goto l0;
		case "Pro+":
			usedMethods = UsedMethods.CS1 | UsedMethods.LZ1 | UsedMethods.HF1 | UsedMethods.CS2 | UsedMethods.LZ2 | UsedMethods.CS3 | UsedMethods.CS4 | UsedMethods.AHF;
			goto l0;
		case "Unlim":
			usedMethods = UsedMethods.CS1 | UsedMethods.LZ1 | UsedMethods.HF1 | UsedMethods.CS2 | UsedMethods.LZ2 | UsedMethods.CS3 | UsedMethods.CS4 | UsedMethods.AHF;
			goto l0;
		case "Help":
			WriteLine(help);
			goto l1;
		case "Exit":
			Environment.Exit(0);
			goto l1;
	}
	for (var i = 0; i < s.Length; i++)
	{
		if (s[i].Length <= 1)
		{
			WriteLine("Некорректная команда! Введите Help, чтобы получить помощь.");
			goto l1;
		}
		var changingMethods = s[i][1..] switch
		{
			"CS1" => UsedMethods.CS1,
			"LZ1" => UsedMethods.LZ1,
			"HF1" => UsedMethods.HF1,
			"PSLZ1" => UsedMethods.PSLZ1,
			"CS2" => UsedMethods.CS2,
			"LZ2" => UsedMethods.LZ2,
			"SHET2" => UsedMethods.SHET2,
			"CS3" => UsedMethods.CS3,
			"CS4" => UsedMethods.CS4,
			"SHET4" => UsedMethods.SHET4,
			"AHF" => UsedMethods.AHF,
			_ => UsedMethods.None,
		};
		if (s[i][0] == '+')
			usedMethods |= changingMethods;
		else if (s[i][0] == '-')
			usedMethods &= ~changingMethods;
		else
		{
			WriteLine("Некорректная команда! Введите Help, чтобы получить помощь.");
			goto l1;
		}
	}
l0:
	WriteLine("Выполнено успешно!");
	SendUsedMethods();
l1:
	continue;
}

internal enum OperationType
{
	Opening,
	Compression,
	Unpacking,
};
