using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Tmds.Utils;

namespace AresT;

public partial class MainPage : ContentPage
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
#if DEBUG
	private static UsedMethods usedMethods;
#else
	private static UsedMethods usedMethods = UsedMethods.CS1 | UsedMethods.LZ1 | UsedMethods.HF1 | UsedMethods.CS2 | UsedMethods.LZ2;
#endif
#if !DEBUG
	private DateTime compressionStart;
#endif

	private readonly TcpListener tcpListener; //монитор подключений TCP клиентов
	private readonly Thread listenThread; //создание потока

	private readonly List<TcpClient> clients = new(); //список клиентских подключений
	private readonly List<NetworkStream> netStream = new(); //список потока данных
	private readonly int port = 11000;
	private Process executor;
	private int executorId;
#if RELEASE
	private readonly Random random = new(1234567890);
#endif

	public MainPage()
	{
		ThreadsLayout = new Grid[ProgressBarGroups];
		LabelSubtotal = new Label[ProgressBarGroups];
		LabelCurrent = new Label[ProgressBarGroups];
		LabelStatus = new Label[ProgressBarGroups];
		ContentViewSubtotal = new ContentView[ProgressBarGroups];
		ContentViewCurrent = new ContentView[ProgressBarGroups];
		ContentViewStatus = new ContentView[ProgressBarGroups];
		ProgressBarSubtotal = new ProgressBar[ProgressBarGroups];
		ProgressBarCurrent = new ProgressBar[ProgressBarGroups];
		ProgressBarStatus = new ProgressBar[ProgressBarGroups];
		tcpListener = default!;
		listenThread = default!;
		executor = default!;
		var args = Environment.GetCommandLineArgs();
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
			GridThreadsProgressBars.Add(ThreadsLayout[i], i / ProgressBarVGroups, i % ProgressBarVGroups);
			ThreadsLayout[i].ColumnDefinitions = new() { new(GridLength.Auto), new(GridLength.Auto) };
			ThreadsLayout[i].RowDefinitions = new() { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto) };
			LabelSubtotal[i] = new();
			ThreadsLayout[i].Add(LabelSubtotal[i], 0, 0);
			LabelSubtotal[i].FontSize = 12;
			LabelSubtotal[i].TextColor = new(0, 0, 0);
			LabelSubtotal[i].Text = "Subtotal" + (i + 1).ToString();
			ContentViewSubtotal[i] = new();
			ThreadsLayout[i].Add(ContentViewSubtotal[i], 1, 0);
			ContentViewSubtotal[i].BackgroundColor = new(255, 191, 223);
			ContentViewSubtotal[i].MinimumHeightRequest = 16;
			ProgressBarSubtotal[i] = new();
			ContentViewSubtotal[i].Content = ProgressBarSubtotal[i];
			ProgressBarSubtotal[i].MinimumHeightRequest = 16;
			ProgressBarSubtotal[i].MinimumWidthRequest = 180;
			ProgressBarSubtotal[i].Progress = 0.25;
			ProgressBarSubtotal[i].ProgressColor = new(191, 128, 128);
			LabelCurrent[i] = new();
			ThreadsLayout[i].Add(LabelCurrent[i], 0, 1);
			LabelCurrent[i].FontSize = 12;
			LabelCurrent[i].TextColor = new(0, 0, 0);
			LabelCurrent[i].Text = "Current" + (i + 1).ToString();
			ContentViewCurrent[i] = new();
			ThreadsLayout[i].Add(ContentViewCurrent[i], 1, 1);
			ContentViewCurrent[i].BackgroundColor = new(128, 255, 191);
			ContentViewCurrent[i].MinimumHeightRequest = 16;
			ProgressBarCurrent[i] = new();
			ContentViewCurrent[i].Content = ProgressBarCurrent[i];
			ProgressBarCurrent[i].MinimumHeightRequest = 16;
			ProgressBarCurrent[i].MinimumWidthRequest = 180;
			ProgressBarCurrent[i].Progress = 0.5;
			ProgressBarCurrent[i].ProgressColor = new(64, 128, 64);
			LabelStatus[i] = new();
			ThreadsLayout[i].Add(LabelStatus[i], 0, 2);
			LabelStatus[i].FontSize = 12;
			LabelStatus[i].TextColor = new(0, 0, 0);
			LabelStatus[i].Text = "Status" + (i + 1).ToString();
			ContentViewStatus[i] = new();
			ThreadsLayout[i].Add(ContentViewStatus[i], 1, 2);
			ContentViewStatus[i].BackgroundColor = new(191, 191, 255);
			ContentViewStatus[i].MinimumHeightRequest = 16;
			ProgressBarStatus[i] = new();
			ContentViewStatus[i].Content = ProgressBarStatus[i];
			ProgressBarStatus[i].MinimumHeightRequest = 16;
			ProgressBarStatus[i].MinimumWidthRequest = 180;
			ProgressBarStatus[i].Progress = 0.75;
			ProgressBarStatus[i].ProgressColor = new(128, 128, 191);
		}
		//TabView.CurrentItem = TabItemText;
		PickerQuickSetup.SelectedIndex = 1;
		filename = args.Length == 0 ? "" : args[0];
#if RELEASE
		port = random.Next(1024, 65536);
		StartExecutor();
#endif
		System.Threading.Thread.CurrentThread.Priority = ThreadPriority.Normal;
		try
		{
			tcpListener = new(IPAddress.Loopback, port);
			listenThread = new(new ThreadStart(ListenThread)) { Name = "Ожидание подключения клиентов" };
			listenThread.Start(); //старт потока
			listenThread.IsBackground = true;
			if ((args.Length == 0 ? "" : args[0]) != "")
			{
				System.Threading.Thread.Sleep(MillisecondsPerSecond / 4);
				operation_type = OperationType.Opening;
				thread = new Thread(new ThreadStart(() => Thread(true)));
				thread.Start();
				thread.IsBackground = true;
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
		var button = await MainThread.InvokeOnMainThreadAsync(async () => await DisplayAlert("", "Произошла серьезная ошибка в рабочем модуле Ares и он аварийно завершился. Нажмите ОК, чтобы перезапустить его, или Отмена, чтобы выйти из приложения.", "ОК", "Отмена"));
		if (!button)
			Environment.Exit(0);
		SetProgressBarsFull();
		StartExecutor();
	}

	private void StartExecutor()
	{
		executor = ExecFunction.Start(MainClass.Main, new[] { port.ToString(), Environment.ProcessId.ToString() });
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

	private static async void SetValue(ProgressBar pb, double new_value) => await MainThread.InvokeOnMainThreadAsync(() => SetValueInternal(pb, new_value));

	private static void SetValueInternal(ProgressBar pb, double new_value)
	{
		if (new_value < 0)
			pb.ProgressTo(1, 500, Easing.Linear);
		else
			pb.ProgressTo(new_value, 500, Easing.Linear);
	}

	private async void WorkUpReceiveMessage(byte[] message)
	{
		try
		{
			if (message.Length == 0)
				return;
			if (message[0] == 0 && message.Length == ProgressBarGroups * 24 + 17 && is_working)
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
					System.Threading.Thread.Sleep(MillisecondsPerSecond);
					await MainThread.InvokeOnMainThreadAsync(async () => await DisplayAlert("", "Файл успешно распакован" + timeString + "!", "ОК"));
					process.WaitForExit();
					File.Delete(path);
				}
				else
					await MainThread.InvokeOnMainThreadAsync(async () => await DisplayAlert("", "Файл успешно " + (operation_type is OperationType.Compression or OperationType.Recompression ? "сжат" : "распакован") + timeString + "!", "ОК"));
			}
			else if (message[0] == 2)
				await MainThread.InvokeOnMainThreadAsync(async () => await DisplayAlert("", "Ошибка! Не удалось " + (operation_type is OperationType.Compression or OperationType.Recompression ? "сжать" : "распаковать") + " файл.", "ОК"));
			else if (message[0] == 3)
				await MainThread.InvokeOnMainThreadAsync(async () => await DisplayAlert("", "Ошибка! Файл сжат, но распаковка не удалась.", "ОК"));
			if (message[0] is not 0)
			{
				SetProgressBarsFull();
			}
		}
		catch
		{
			if (message.Length != 0 && message[0] is not 0)
				await MainThread.InvokeOnMainThreadAsync(async () => await DisplayAlert("", "Произошла серьезная ошибка при попытке выполнить действие. Повторите попытку позже. Если проблема не исчезает, обратитесь к разработчикам приложения.", "ОК"));
		}
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
				await MainThread.InvokeOnMainThreadAsync(async () => await DisplayAlert("", "Ошибка! Не удалось распаковать файл.", "ОК"));
			}
			break;
			case OperationType.Compression:
			if (ProcessStartup("").Result is bool)
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
				await MainThread.InvokeOnMainThreadAsync(async () => await DisplayAlert("", "Ошибка! Не удалось сжать файл.", "ОК"));
			}
			break;
			case OperationType.Unpacking:
			if (ProcessStartup(".ares-t").Result is bool)
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
				await MainThread.InvokeOnMainThreadAsync(async () => await DisplayAlert("", "Ошибка! Не удалось распаковать файл.", "ОК"));
			}
			break;
			case OperationType.Recompression:
			if (ProcessStartup(".ares-t").Result is bool)
				return;
			try
			{
				is_working = true;
				SendMessageToClient(0, Encoding.UTF8.GetBytes(filename).Prepend((byte)4).ToArray());
#if !DEBUG
				compressionStart = DateTime.Now;
#endif
			}
			catch (OperationCanceledException)
			{
			}
			catch
			{
				await MainThread.InvokeOnMainThreadAsync(async () => await DisplayAlert("", "Ошибка! Не удалось сжать файл.", "ОК"));
			}
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
		var fileResult = await MainThread.InvokeOnMainThreadAsync(async () => await FilePicker.Default.PickAsync(new() { PickerTitle = $"Select the *{Filter} file", FileTypes = new(new Dictionary<DevicePlatform, G.IEnumerable<string>>() { { DevicePlatform.Android, new[] { "multipart/mixed" } }, { DevicePlatform.MacCatalyst, new[] { "UTType.Item" } }, { DevicePlatform.WinUI, new[] { Filter } } }) }));
		filename = fileResult?.FullPath ?? "";
		if (filename == "" || !filename.EndsWith(Filter))
			emptyFileName = true;
		return 0;
	}

	private async void SetButtonsEnabled(bool Enabled)
	{
		emptyFileName = false;
		await MainThread.InvokeOnMainThreadAsync(() => SetButtonsEnabledInternal(Enabled));
	}

	private int SetButtonsEnabledInternal(bool isEnabled)
	{
		ButtonOpen.IsEnabled = isEnabled;
		ButtonOpenForCompression.IsEnabled = isEnabled;
		ButtonOpenForUnpacking.IsEnabled = isEnabled;
		ButtonOpenForRecompression.IsEnabled = isEnabled;
		PickerQuickSetup.IsEnabled = isEnabled;
		GridSettings.IsEnabled = isEnabled;
		return 0;
	}

	private void ButtonOpen_Click(object? sender, EventArgs e)
	{
		operation_type = OperationType.Opening;
		thread = new Thread(new ThreadStart(Thread)) { Name = "Поток открытия" };
		thread.Start();
		thread.IsBackground = true;
	}

	private void ButtonOpenForCompression_Click(object? sender, EventArgs e)
	{
		operation_type = OperationType.Compression;
		thread = new Thread(new ThreadStart(Thread)) { Name = "Поток сжатия" };
		thread.Start();
		thread.IsBackground = true;
	}

	private void ButtonOpenForUnpacking_Click(object? sender, EventArgs e)
	{
		operation_type = OperationType.Unpacking;
		thread = new Thread(new ThreadStart(Thread)) { Name = "Поток распаковки" };
		thread.Start();
		thread.IsBackground = true;
	}

	private void ButtonOpenForRecompression_Click(object? sender, EventArgs e)
	{
		operation_type = OperationType.Recompression;
		thread = new Thread(new ThreadStart(Thread)) { Name = "Поток пересжатия" };
		thread.Start();
		thread.IsBackground = true;
	}

	private void ButtonStop_Click(object? sender, EventArgs e)
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

	private void PickerQuickSetup_SelectedIndexChanged(object? sender, EventArgs e)
	{
		if (PickerQuickSetup == null)
			return;
		var selectedIndex = PickerQuickSetup.SelectedIndex;
		CheckBoxCS1.IsChecked = selectedIndex >= 0;
		CheckBoxLZ1.IsChecked = selectedIndex >= 0;
		CheckBoxHF1.IsChecked = selectedIndex >= 0;
		CheckBoxCS2.IsChecked = selectedIndex >= 1;
		CheckBoxLZ2.IsChecked = selectedIndex >= 1;
		CheckBoxSHET2.IsChecked = false;
		CheckBoxCS3.IsChecked = selectedIndex >= 2;
		CheckBoxCS4.IsChecked = selectedIndex >= 2;
		CheckBoxSHET4.IsChecked = false;
		CheckBoxCS5.IsChecked = false;
		CheckBoxCS6.IsChecked = selectedIndex >= 4;
		CheckBoxCS7.IsChecked = selectedIndex >= 4;
		CheckBoxSHET7.IsChecked = false;
		(selectedIndex >= 3 ? RadioButtonAHF : RadioButtonSHF).IsChecked = true;
		SendUsedMethods();
	}

	private void CheckBoxCS1_CheckedChanged(object? sender, EventArgs e)
	{
		if (PanelCS1 != null)
			PanelCS1.IsEnabled = !PanelCS1.IsEnabled;
		usedMethods ^= UsedMethods.CS1;
		SendUsedMethods();
	}

	private void CheckBoxCS2_CheckedChanged(object? sender, EventArgs e)
	{
		if (PanelCS2 != null)
			PanelCS2.IsEnabled = !PanelCS2.IsEnabled;
		usedMethods ^= UsedMethods.CS2;
		SendUsedMethods();
	}

	private void CheckBoxCS3_CheckedChanged(object? sender, EventArgs e)
	{
		if (PanelCS3 != null)
			PanelCS3.IsEnabled = !PanelCS3.IsEnabled;
		usedMethods ^= UsedMethods.CS3;
		SendUsedMethods();
	}

	private void CheckBoxCS4_CheckedChanged(object? sender, EventArgs e)
	{
		if (PanelCS4 != null)
			PanelCS4.IsEnabled = !PanelCS4.IsEnabled;
		usedMethods ^= UsedMethods.CS4;
		SendUsedMethods();
	}

	private void CheckBoxCS5_CheckedChanged(object? sender, EventArgs e)
	{
		if (PanelCS5 != null)
			PanelCS5.IsEnabled = !PanelCS5.IsEnabled;
		usedMethods ^= UsedMethods.CS5;
		SendUsedMethods();
	}

	private void CheckBoxCS6_CheckedChanged(object? sender, EventArgs e)
	{
		if (PanelCS5 != null)
			PanelCS5.IsEnabled = !PanelCS6.IsEnabled;
		usedMethods ^= UsedMethods.CS6;
		SendUsedMethods();
	}

	private void CheckBoxCS7_CheckedChanged(object? sender, EventArgs e)
	{
		if (PanelCS5 != null)
			PanelCS5.IsEnabled = !PanelCS7.IsEnabled;
		usedMethods ^= UsedMethods.CS7;
		SendUsedMethods();
	}

	private void CheckBoxCS8_CheckedChanged(object? sender, EventArgs e)
	{
		if (PanelCS5 != null)
			PanelCS5.IsEnabled = !PanelCS8.IsEnabled;
		usedMethods ^= UsedMethods.CS8;
		SendUsedMethods();
	}

	private void CheckBoxLZ1_CheckedChanged(object? sender, EventArgs e)
	{
		usedMethods ^= UsedMethods.LZ1;
		if ((usedMethods & UsedMethods.LZ1) != 0 && (usedMethods & UsedMethods.PSLZ1) != 0)
			CheckBoxPSLZ1.IsChecked = false;
		SendUsedMethods();
	}

	private void CheckBoxHF1_CheckedChanged(object? sender, EventArgs e)
	{
		usedMethods ^= UsedMethods.HF1;
		SendUsedMethods();
	}

	private void CheckBoxPSLZ1_CheckedChanged(object sender, EventArgs e)
	{
		usedMethods ^= UsedMethods.PSLZ1;
		if ((usedMethods & UsedMethods.LZ1) != 0 && (usedMethods & UsedMethods.PSLZ1) != 0)
			CheckBoxLZ1.IsChecked = false;
		SendUsedMethods();
	}

	private void CheckBoxLZ2_CheckedChanged(object? sender, EventArgs e)
	{
		usedMethods ^= UsedMethods.LZ2;
		SendUsedMethods();
	}

	private void CheckBoxSHET2_CheckedChanged(object? sender, EventArgs e)
	{
		usedMethods ^= UsedMethods.SHET2;
		SendUsedMethods();
	}

	private void CheckBoxSHET4_CheckedChanged(object? sender, EventArgs e)
	{
		usedMethods ^= UsedMethods.SHET4;
		SendUsedMethods();
	}

	private void CheckBoxSHET7_CheckedChanged(object? sender, EventArgs e)
	{
		usedMethods ^= UsedMethods.SHET7;
		SendUsedMethods();
	}

	private void RadioButtonAHF_CheckedChanged(object? sender, EventArgs e)
	{
		if ((RadioButtonAHF?.IsChecked ?? false) ^ (usedMethods & UsedMethods.AHF) != 0)
			usedMethods ^= UsedMethods.AHF;
		SendUsedMethods();
	}

	private void SendUsedMethods()
	{
		if (netStream.Length != 0)
			SendMessageToClient(0, BitConverter.GetBytes((int)usedMethods).Prepend((byte)0).ToArray());
	}

	private readonly Grid[] ThreadsLayout;
	private readonly Label[] LabelSubtotal;
	private readonly Label[] LabelCurrent;
	private readonly Label[] LabelStatus;
	private readonly ContentView[] ContentViewSubtotal;
	private readonly ContentView[] ContentViewCurrent;
	private readonly ContentView[] ContentViewStatus;
	private readonly ProgressBar[] ProgressBarSubtotal;
	private readonly ProgressBar[] ProgressBarCurrent;
	private readonly ProgressBar[] ProgressBarStatus;
}

public class OpacityConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is not bool b)
			b = false;
		return b ? 1 : 0.5;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => new();
}
