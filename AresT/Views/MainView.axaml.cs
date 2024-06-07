using AresT.ViewModels;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MsBox.Avalonia;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Tmds.Utils;

namespace AresT.Views;

public partial class MainView : UserControl
{
	private bool emptyFileName;
	private enum OperationType
	{
		Opening,
		Compression,
		Unpacking,
		Recompression,
	};

	private Thread thread = new(() => { });
	private string filename = "";
	private OperationType operation_type;
	private bool continue_;
	private bool is_working;
//#if DEBUG
	private static UsedMethods usedMethods;
	private static int usedSizes;
//#else
//	private static UsedMethods usedMethods = UsedMethods.CS1 | UsedMethods.LZ1 | UsedMethods.HF1 | UsedMethods.CS2 | UsedMethods.LZ2;
//	private static int usedSizes = 4;
//#endif
#if !DEBUG
	private DateTime compressionStart;
#endif

	private readonly string[] args;
	private readonly TcpListener tcpListener; //монитор подключений TCP клиентов
	private readonly Thread listenThread; //создание потока

	private readonly List<TcpClient> clients = []; //список клиентских подключений
	private readonly List<NetworkStream> netStream = []; //список потока данных
	private readonly int port = 11000;
	private Process executor;
	private int executorId;
#if RELEASE
	private readonly Random random = new(1234567890);
#endif

	public MainView()
	{
		ThreadsLayout = new Grid[ProgressBarGroups];
		TextBlockSubtotal = new TextBlock[ProgressBarGroups];
		TextBlockCurrent = new TextBlock[ProgressBarGroups];
		TextBlockStatus = new TextBlock[ProgressBarGroups];
		ContentViewSubtotal = new ContentControl[ProgressBarGroups];
		ContentViewCurrent = new ContentControl[ProgressBarGroups];
		ContentViewStatus = new ContentControl[ProgressBarGroups];
		ProgressBarSubtotal = new ProgressBar[ProgressBarGroups];
		ProgressBarCurrent = new ProgressBar[ProgressBarGroups];
		ProgressBarStatus = new ProgressBar[ProgressBarGroups];
		tcpListener = default!;
		listenThread = default!;
		executor = default!;
		args = Environment.GetCommandLineArgs();
		if (args.Length >= 1)
			args = args[1..];
		if (ExecFunction.IsExecFunctionCommand(args))
		{
			ExecFunction.Program.Main(args);
			Environment.Exit(0);
		}
		InitializeComponent();
		for (var i = 0; i < ProgressBarGroups; i++)
		{
			ThreadsLayout[i] = new();
			GridThreadsProgressBars.Children.Add(ThreadsLayout[i]);
			Grid.SetColumn(ThreadsLayout[i], i / ProgressBarVGroups);
			Grid.SetRow(ThreadsLayout[i], i % ProgressBarVGroups);
			ThreadsLayout[i].ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Auto)];
			ThreadsLayout[i].RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto)];
			TextBlockSubtotal[i] = new();
			ThreadsLayout[i].Children.Add(TextBlockSubtotal[i]);
			Grid.SetColumn(TextBlockSubtotal[i], 0);
			Grid.SetRow(TextBlockSubtotal[i], 0);
			TextBlockSubtotal[i].FontSize = 12;
			TextBlockSubtotal[i].Foreground = new SolidColorBrush(new Color(255, 0, 0, 0));
			TextBlockSubtotal[i].Text = "Subtotal" + (i + 1).ToString();
			ContentViewSubtotal[i] = new();
			ThreadsLayout[i].Children.Add(ContentViewSubtotal[i]);
			Grid.SetColumn(ContentViewSubtotal[i], 1);
			Grid.SetRow(ContentViewSubtotal[i], 0);
			ContentViewSubtotal[i].MinHeight = 16;
			ProgressBarSubtotal[i] = new();
			ContentViewSubtotal[i].Content = ProgressBarSubtotal[i];
			ProgressBarSubtotal[i].Background = new SolidColorBrush(new Color(255, 255, 191, 223));
			ProgressBarSubtotal[i].Maximum = 1;
			ProgressBarSubtotal[i].MinHeight = 16;
			ProgressBarSubtotal[i].MinWidth = 180;
			ProgressBarSubtotal[i].Value = 0.25;
			ProgressBarSubtotal[i].Foreground = new SolidColorBrush(new Color(255, 191, 128, 128));
			TextBlockCurrent[i] = new();
			ThreadsLayout[i].Children.Add(TextBlockCurrent[i]);
			Grid.SetColumn(TextBlockCurrent[i], 0);
			Grid.SetRow(TextBlockCurrent[i], 1);
			TextBlockCurrent[i].FontSize = 12;
			TextBlockCurrent[i].Foreground = new SolidColorBrush(new Color(255, 0, 0, 0));
			TextBlockCurrent[i].Text = "Current" + (i + 1).ToString();
			ContentViewCurrent[i] = new();
			ThreadsLayout[i].Children.Add(ContentViewCurrent[i]);
			Grid.SetColumn(ContentViewCurrent[i], 1);
			Grid.SetRow(ContentViewCurrent[i], 1);
			ContentViewCurrent[i].MinHeight = 16;
			ProgressBarCurrent[i] = new();
			ContentViewCurrent[i].Content = ProgressBarCurrent[i];
			ProgressBarCurrent[i].Background = new SolidColorBrush(new Color(255, 128, 255, 191));
			ProgressBarCurrent[i].Maximum = 1;
			ProgressBarCurrent[i].MinHeight = 16;
			ProgressBarCurrent[i].MinWidth = 180;
			ProgressBarCurrent[i].Value = 0.5;
			ProgressBarCurrent[i].Foreground = new SolidColorBrush(new Color(255, 64, 128, 64));
			TextBlockStatus[i] = new();
			ThreadsLayout[i].Children.Add(TextBlockStatus[i]);
			Grid.SetColumn(TextBlockStatus[i], 0);
			Grid.SetRow(TextBlockStatus[i], 2);
			TextBlockStatus[i].FontSize = 12;
			TextBlockStatus[i].Foreground = new SolidColorBrush(new Color(255, 0, 0, 0));
			TextBlockStatus[i].Text = "Status" + (i + 1).ToString();
			ContentViewStatus[i] = new();
			ThreadsLayout[i].Children.Add(ContentViewStatus[i]);
			Grid.SetColumn(ContentViewStatus[i], 1);
			Grid.SetRow(ContentViewStatus[i], 2);
			ContentViewStatus[i].Background = new SolidColorBrush(new Color(255, 191, 191, 255));
			ContentViewStatus[i].MinHeight = 16;
			ProgressBarStatus[i] = new();
			ContentViewStatus[i].Content = ProgressBarStatus[i];
			ProgressBarStatus[i].Background = new SolidColorBrush(new Color(255, 191, 191, 255));
			ProgressBarStatus[i].Maximum = 1;
			ProgressBarStatus[i].MinHeight = 16;
			ProgressBarStatus[i].MinWidth = 180;
			ProgressBarStatus[i].Value = 0.75;
			ProgressBarStatus[i].Foreground = new SolidColorBrush(new Color(255, 128, 128, 191));
		}
		//TabView.CurrentItem = TabItemText;
		ComboQuickSetup.SelectedIndex = 1;
		filename = args.Length == 0 ? "" : args[0];
#if RELEASE
		port = random.Next(1024, 65536);
		StartExecutor();
#endif
		System.Threading.Thread.CurrentThread.Priority = ThreadPriority.Normal;
		try
		{
			tcpListener = new(IPAddress.Loopback, port);
			listenThread = new(new ThreadStart(ListenThread)) { Name = "Ожидание подключения клиентов", IsBackground = true };
			listenThread.Start(); //старт потока
			if ((args.Length == 0 ? "" : args[0]) != "")
			{
				System.Threading.Thread.Sleep(MillisecondsPerSecond / 4);
				operation_type = OperationType.Opening;
				thread = new Thread(() => Thread(true)) { Name = "Основной фоновый поток", IsBackground = true };
				thread.Start();
			}
		}
		catch
		{
			Disconnect();
		}
	}

	private void ClientReceive(object? ID)
	{
		var thisID = (int?)ID ?? 0;
		byte[] receive;
		var receiveLen = new byte[4];
		while (true)
		{
			try
			{
				netStream[thisID].ReadExactly(receiveLen);
				receive = new byte[BitConverter.ToInt32(receiveLen)];
				netStream[thisID].ReadExactly(receive);
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

	private void Disconnect()
	{
		tcpListener.Stop(); //остановка чтения
		for (var i = 0; i < clients.Length; i++)
		{
			clients[i].Close(); //отключение клиента
			netStream[i].Close(); //отключение потока
		}
		Environment.Exit(0); //завершение процесса
	}

	private async void ExecutorExited(object? sender, EventArgs? e)
	{
		var tempFilename = (Environment.GetEnvironmentVariable("temp") ?? throw new IOException()) + @"\AresT-" + executorId + ".tmp";
		try
		{
			if (File.Exists(tempFilename))
				File.Delete(tempFilename);
		}
		catch
		{
		}
		var button = await Dispatcher.UIThread.InvokeAsync(async () =>
			await MessageBoxManager.GetMessageBoxStandard("", "Произошла серьезная ошибка в рабочем модуле Ares и он аварийно завершился. Нажмите ОК, чтобы перезапустить его, или Отмена, чтобы выйти из приложения.", MsBox.Avalonia.Enums.ButtonEnum.OkCancel)
			.ShowAsync() == MsBox.Avalonia.Enums.ButtonResult.Ok);
		if (!button)
			Environment.Exit(0);
		SetProgressBarsFull();
		StartExecutor();
	}

	private void StartExecutor()
	{
		executor = ExecFunction.Start(MainClass.Main, [port.ToString(), Environment.ProcessId.ToString()]);
		executorId = executor.Id;
		executor.EnableRaisingEvents = true;
		executor.Exited += ExecutorExited;
	}

	private void ListenThread()
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

	private void SendMessageToClient(int index, byte[] toSend)
	{
		var toSendLen = BitConverter.GetBytes(toSend.Length);
		netStream[index].Write(toSendLen, 0, toSendLen.Length);
		netStream[index].Write(toSend, 0, toSend.Length);
		netStream[index].Flush(); //удаление данных из потока
	}

	private static async void SetValue(ProgressBar pb, double new_value) => await Dispatcher.UIThread.InvokeAsync(() => SetValueInternal(pb, new_value));

	private static void SetValueInternal(ProgressBar pb, double new_value)
	{
		if (new_value < 0)
			pb.Value = 1;
		else
			pb.Value = new_value;
	}

	private async void WorkUpReceiveMessage(byte[] message)
	{
		try
		{
			if (message.Length == 0)
				return;
			if (message[0] == 0 && message.Length == ProgressBarGroups * 24 + 17 && is_working)
				UpdateProgressBars(message);
			else if (message[0] == 1)
				await OpenFile();
			else if (message[0] == 2)
				await Dispatcher.UIThread.InvokeAsync(async () =>
					await MessageBoxManager.GetMessageBoxStandard("", "Ошибка! Не удалось " + (operation_type is OperationType.Compression or OperationType.Recompression ? "сжать" : "распаковать") + " файл.", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowAsync());
			else if (message[0] == 3)
				await Dispatcher.UIThread.InvokeAsync(async () =>
					await MessageBoxManager.GetMessageBoxStandard("", "Ошибка! Файл сжат, но распаковка не удалась.", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowAsync());
			if (message[0] is not 0)
			{
				SetProgressBarsFull();
			}
		}
		catch
		{
			if (message.Length != 0 && message[0] is not 0)
				await Dispatcher.UIThread.InvokeAsync(async () =>
					await MessageBoxManager.GetMessageBoxStandard("", "Произошла серьезная ошибка при попытке выполнить действие. Повторите попытку позже. Если проблема не исчезает, обратитесь к разработчикам приложения.", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowAsync());
		}
	}

	private void UpdateProgressBars(byte[] message)
	{
		SetValue(ProgressBarSupertotal, (double)BitConverter.ToInt32(message.AsSpan(1, 4)) / BitConverter.ToInt32(message.AsSpan(5, 4)));
		SetValue(ProgressBarTotal, (double)BitConverter.ToInt32(message.AsSpan(9, 4)) / BitConverter.ToInt32(message.AsSpan(13, 4)));
		for (var i = 0; i < ProgressBarGroups; i++)
		{
			SetValue(ProgressBarSubtotal[i], (double)BitConverter.ToInt32(message.AsSpan(i * 24 + 17, 4)) / BitConverter.ToInt32(message.AsSpan(i * 24 + 21, 4)));
			SetValue(ProgressBarCurrent[i], (double)BitConverter.ToInt32(message.AsSpan(i * 24 + 25, 4)) / BitConverter.ToInt32(message.AsSpan(i * 24 + 29, 4)));
			SetValue(ProgressBarStatus[i], (double)BitConverter.ToInt32(message.AsSpan(i * 24 + 33, 4)) / BitConverter.ToInt32(message.AsSpan(i * 24 + 37, 4)));
		}
	}

	private async Task OpenFile()
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
			System.Threading.Thread.Sleep(MillisecondsPerSecond);
			await Dispatcher.UIThread.InvokeAsync(async () =>
				await MessageBoxManager.GetMessageBoxStandard("", "Файл успешно распакован" + timeString + "!", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowAsync());
			process.WaitForExit();
			File.Delete(path);
		}
		else
			await Dispatcher.UIThread.InvokeAsync(async () =>
				await MessageBoxManager.GetMessageBoxStandard("", "Файл успешно " + (operation_type is OperationType.Compression or OperationType.Recompression ? "сжат" : "распакован") + timeString + "!", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowAsync());
	}

	private void Thread() => Thread(false);

	private async void Thread(bool startImmediate)
	{
		switch (operation_type)
		{
			case OperationType.Opening:
			if (!startImmediate && ProcessStartup(".ares-t").Result is bool)
				return;
			try
			{
				if (startImmediate)
					InitProgressBars();
			}
			catch
			{
				await Dispatcher.UIThread.InvokeAsync(async () =>
					await MessageBoxManager.GetMessageBoxStandard("", "Ошибка! Не удалось распаковать файл.", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowAsync());
				break;
			}
			await StartProcess(true);
			break;
			case OperationType.Compression:
			if (ProcessStartup("").Result is bool)
				return;
			await StartProcess(false);
			break;
			case OperationType.Unpacking:
			if (ProcessStartup(".ares-t").Result is bool)
				return;
			await StartProcess(true);
			break;
			case OperationType.Recompression:
			if (ProcessStartup(".ares-t").Result is bool)
				return;
			await StartProcess(false);
			break;
		}
	}

	private async Task<bool?> ProcessStartup(string filter)
	{
		if (!continue_)
		{
			await SetOFDPars(filter);
			if (emptyFileName == true)
				return true;
			InitProgressBars();
		}
		return null;
	}

	private void InitProgressBars()
	{
		SetButtonsEnabled(false);
		SetValue(ProgressBarSupertotal, 0);
		SetValue(ProgressBarTotal, 0);
		for (var i = 0; i < ProgressBarGroups; i++)
		{
			SetValue(ProgressBarSubtotal[i], 0);
			SetValue(ProgressBarCurrent[i], 0);
			SetValue(ProgressBarStatus[i], 0);
		}
	}

	private void SetProgressBarsFull()
	{
		SetValue(ProgressBarSupertotal, -1);
		SetValue(ProgressBarTotal, -1);
		for (var i = 0; i < ProgressBarGroups; i++)
		{
			SetValue(ProgressBarSubtotal[i], -1);
			SetValue(ProgressBarCurrent[i], -1);
			SetValue(ProgressBarStatus[i], -1);
		}
		SetButtonsEnabled(true);
		continue_ = false;
		is_working = false;
	}

	private async Task SetOFDPars(string Filter)
	{
		emptyFileName = false;
		await SetOFDParsInternal(Filter);
	}

	private async Task<int> SetOFDParsInternal(string Filter)
	{
		var fileResult = await Dispatcher.UIThread.InvokeAsync(async () =>
			await TopLevel.GetTopLevel(this)?.StorageProvider.OpenFilePickerAsync(new() { Title = $"Select the *{Filter} file", FileTypeFilter = [MainViewModel.GetFilesType(Filter == "")] })!);
		filename = fileResult?.Count == 0 ? "" : fileResult?[0]?.TryGetLocalPath() ?? "";
		if (filename == "" || !filename.EndsWith(Filter))
			emptyFileName = true;
		return 0;
	}

	private async void SetButtonsEnabled(bool Enabled)
	{
		emptyFileName = false;
		await Dispatcher.UIThread.InvokeAsync(() =>
				SetButtonsEnabledInternal(Enabled));
	}

	private int SetButtonsEnabledInternal(bool isEnabled)
	{
		ButtonOpen.IsEnabled = isEnabled;
		ButtonOpenForCompression.IsEnabled = isEnabled;
		ButtonOpenForUnpacking.IsEnabled = isEnabled;
		ButtonOpenForRecompression.IsEnabled = isEnabled;
		ComboQuickSetup.IsEnabled = isEnabled;
		GridSettings.IsEnabled = isEnabled;
		return 0;
	}

	private async Task StartProcess(bool unpack)
	{
		try
		{
			is_working = true;
			SendMessageToClient(0, [(byte)(operation_type + 2), .. Encoding.UTF8.GetBytes(filename)]);
#if !DEBUG
				compressionStart = DateTime.Now;
#endif
		}
		catch (OperationCanceledException)
		{
		}
		catch
		{
			await Dispatcher.UIThread.InvokeAsync(async () =>
				await MessageBoxManager.GetMessageBoxStandard("", "Ошибка! Не удалось " + (unpack ? "распаковать" : "сжать") + " файл.", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowAsync());
		}
	}

	private void ButtonOpen_Click(object? sender, RoutedEventArgs e)
	{
		operation_type = OperationType.Opening;
		thread = new Thread(new ThreadStart(Thread)) { Name = "Поток открытия" };
		thread.Start();
		thread.IsBackground = true;
	}

	private void ButtonOpenForCompression_Click(object? sender, RoutedEventArgs e)
	{
		operation_type = OperationType.Compression;
		thread = new Thread(new ThreadStart(Thread)) { Name = "Поток сжатия" };
		thread.Start();
		thread.IsBackground = true;
	}

	private void ButtonOpenForUnpacking_Click(object? sender, RoutedEventArgs e)
	{
		operation_type = OperationType.Unpacking;
		thread = new Thread(new ThreadStart(Thread)) { Name = "Поток распаковки" };
		thread.Start();
		thread.IsBackground = true;
	}

	private void ButtonOpenForRecompression_Click(object? sender, RoutedEventArgs e)
	{
		operation_type = OperationType.Recompression;
		thread = new Thread(new ThreadStart(Thread)) { Name = "Поток пересжатия" };
		thread.Start();
		thread.IsBackground = true;
	}

	private void ButtonStop_Click(object? sender, RoutedEventArgs e)
	{
		executor?.Kill();
		SetValue(ProgressBarSupertotal, -1);
		SetValue(ProgressBarTotal, -1);
		for (var i = 0; i < ProgressBarGroups; i++)
		{
			SetValue(ProgressBarSubtotal[i], -1);
			SetValue(ProgressBarCurrent[i], -1);
			SetValue(ProgressBarStatus[i], -1);
		}
		SetButtonsEnabled(true);
		continue_ = false;
	}

	private void ComboQuickSetup_SelectedIndexChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (ComboQuickSetup == null)
			return;
		var selectedIndex = ComboQuickSetup.SelectedIndex;
		CheckBoxCS1.IsChecked = selectedIndex >= 0;
		CheckBoxLZ1.IsChecked = selectedIndex >= 0;
		CheckBoxHF1.IsChecked = selectedIndex >= 0;
		CheckBoxCS2.IsChecked = selectedIndex >= 1;
		CheckBoxLZ2.IsChecked = selectedIndex >= 1;
		CheckBoxCOMB2.IsChecked = false;
		CheckBoxFAB2.IsChecked = false;
		CheckBoxCS3.IsChecked = selectedIndex >= 2;
		CheckBoxCS4.IsChecked = selectedIndex >= 2;
		CheckBoxCOMB4.IsChecked = false;
		CheckBoxFAB4.IsChecked = false;
		CheckBoxCS5.IsChecked = false;
		CheckBoxCS6.IsChecked = selectedIndex >= 3;
		CheckBoxCS7.IsChecked = selectedIndex >= 4;
		CheckBoxCOMB7.IsChecked = false;
		CheckBoxFAB7.IsChecked = false;
		SendUsedMethods();
	}

	private void CheckBoxCS1_CheckedChanged(object? sender, RoutedEventArgs e)
	{
		usedMethods ^= UsedMethods.CS1;
		SendUsedMethods();
	}

	private void CheckBoxCS2_CheckedChanged(object? sender, RoutedEventArgs e)
	{
		usedMethods ^= UsedMethods.CS2;
		SendUsedMethods();
	}

	private void CheckBoxCS3_CheckedChanged(object? sender, RoutedEventArgs e)
	{
		usedMethods ^= UsedMethods.CS3;
		SendUsedMethods();
	}

	private void CheckBoxCS4_CheckedChanged(object? sender, RoutedEventArgs e)
	{
		usedMethods ^= UsedMethods.CS4;
		SendUsedMethods();
	}

	private void CheckBoxCS5_CheckedChanged(object? sender, RoutedEventArgs e)
	{
#if RELEASE
		if (CheckBoxCS5 != null && (CheckBoxCS5.IsChecked ?? false))
		{
			CheckBoxCS5.IsChecked = false;
			Dispatcher.UIThread.InvokeAsync(async () =>
				await MessageBoxManager.GetMessageBoxStandard("", "Ошибка! Этот метод находится в разработке.", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowAsync());
		}
#else
		usedMethods ^= UsedMethods.CS5;
		SendUsedMethods();
#endif
	}

	private void CheckBoxCS6_CheckedChanged(object? sender, RoutedEventArgs e)
	{
		usedMethods ^= UsedMethods.CS6;
		SendUsedMethods();
	}

	private void CheckBoxCS7_CheckedChanged(object? sender, RoutedEventArgs e)
	{
		usedMethods ^= UsedMethods.CS7;
		SendUsedMethods();
	}

	private void CheckBoxCS8_CheckedChanged(object? sender, RoutedEventArgs e)
	{
#if RELEASE
		if (CheckBoxCS8 != null && (CheckBoxCS8.IsChecked ?? false))
		{
			CheckBoxCS8.IsChecked = false;
			Dispatcher.UIThread.InvokeAsync(async () =>
				await MessageBoxManager.GetMessageBoxStandard("", "Ошибка! Этот метод находится в разработке.", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowAsync());
		}
#else
		usedMethods ^= UsedMethods.CS8;
		SendUsedMethods();
#endif
	}

	private void CheckBoxLZ1_CheckedChanged(object? sender, RoutedEventArgs e)
	{
		usedMethods ^= UsedMethods.LZ1;
		if (CheckBoxHF1 != null && !(CheckBoxHF1.IsChecked ?? false) && CheckBoxLZ1 != null && !(CheckBoxLZ1.IsChecked ?? false))
			CheckBoxHF1.IsChecked = true;
		SendUsedMethods();
	}

	private void CheckBoxHF1_CheckedChanged(object? sender, RoutedEventArgs e)
	{
		usedMethods ^= UsedMethods.HF1;
		if (CheckBoxHF1 != null && !(CheckBoxHF1.IsChecked ?? false) && CheckBoxLZ1 != null && !(CheckBoxLZ1.IsChecked ?? false))
			CheckBoxLZ1.IsChecked = true;
		SendUsedMethods();
	}

	private void CheckBoxLZ2_CheckedChanged(object? sender, RoutedEventArgs e)
	{
		usedMethods ^= UsedMethods.LZ2;
		SendUsedMethods();
	}

	private void CheckBoxCOMB2_CheckedChanged(object? sender, RoutedEventArgs e)
	{
		usedMethods ^= UsedMethods.COMB2;
		SendUsedMethods();
	}

	private void CheckBoxFAB2_CheckedChanged(object? sender, RoutedEventArgs e)
	{
		usedMethods ^= UsedMethods.FAB2;
		SendUsedMethods();
	}

	private void CheckBoxAHF3_CheckedChanged(object? sender, RoutedEventArgs e)
	{
		usedMethods ^= UsedMethods.AHF3;
		SendUsedMethods();
	}

	private void CheckBoxCOMB4_CheckedChanged(object? sender, RoutedEventArgs e)
	{
		usedMethods ^= UsedMethods.COMB4;
		SendUsedMethods();
	}

	private void CheckBoxFAB4_CheckedChanged(object? sender, RoutedEventArgs e)
	{
		usedMethods ^= UsedMethods.FAB4;
		SendUsedMethods();
	}

	private void CheckBoxCOMB7_CheckedChanged(object? sender, RoutedEventArgs e)
	{
		usedMethods ^= UsedMethods.COMB7;
		SendUsedMethods();
	}

	private void CheckBoxFAB7_CheckedChanged(object? sender, RoutedEventArgs e)
	{
		usedMethods ^= UsedMethods.FAB7;
		SendUsedMethods();
	}

	private void SendUsedMethods()
	{
		if (netStream.Length != 0)
			SendMessageToClient(0, [0, .. BitConverter.GetBytes((int)usedMethods)]);
	}

	private void ComboFragmentLength_SelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (ComboFragmentLength == null)
			return;
		usedSizes = (usedSizes & ~0xF) | ComboFragmentLength.SelectedIndex;
		SendUsedSizes();
	}

	private void SendUsedSizes()
	{
		if (netStream.Length != 0)
			SendMessageToClient(0, [1, .. BitConverter.GetBytes(usedSizes)]);
	}

	private readonly Grid[] ThreadsLayout;
	private readonly TextBlock[] TextBlockSubtotal;
	private readonly TextBlock[] TextBlockCurrent;
	private readonly TextBlock[] TextBlockStatus;
	private readonly ContentControl[] ContentViewSubtotal;
	private readonly ContentControl[] ContentViewCurrent;
	private readonly ContentControl[] ContentViewStatus;
	private readonly ProgressBar[] ProgressBarSubtotal;
	private readonly ProgressBar[] ProgressBarCurrent;
	private readonly ProgressBar[] ProgressBarStatus;
}

public class OpacityConverter : IValueConverter
{
	public static readonly OpacityConverter Instance = new();

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not bool b)
			b = false;
		return b ? 1 : 0.5;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => new();
}
