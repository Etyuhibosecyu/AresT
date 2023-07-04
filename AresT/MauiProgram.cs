global using Corlib.NStar;
global using global::Microsoft.Extensions.DependencyInjection;
global using global::Microsoft.Maui;
global using global::Microsoft.Maui.Accessibility;
global using global::Microsoft.Maui.ApplicationModel;
global using global::Microsoft.Maui.ApplicationModel.Communication;
global using global::Microsoft.Maui.ApplicationModel.DataTransfer;
global using global::Microsoft.Maui.Authentication;
global using global::Microsoft.Maui.Controls;
global using global::Microsoft.Maui.Controls.Hosting;
global using global::Microsoft.Maui.Controls.Xaml;
global using global::Microsoft.Maui.Devices;
global using global::Microsoft.Maui.Devices.Sensors;
global using global::Microsoft.Maui.Dispatching;
global using global::Microsoft.Maui.Graphics;
global using global::Microsoft.Maui.Hosting;
global using global::Microsoft.Maui.Media;
global using global::Microsoft.Maui.Networking;
global using global::Microsoft.Maui.Storage;
global using global::System;
global using global::System.IO;
global using global::System.Net.Http;
global using global::System.Threading;
global using global::System.Threading.Tasks;
global using G = System.Collections.Generic;
global using static System.Math;
global using static UnsafeFunctions.Global;
using Microsoft.Extensions.Logging;

//[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshallingAttribute]
namespace AresT;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts => fonts
				.AddFont("OpenSans-Regular.ttf", "OpenSansRegular")
				.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold"));
#if DEBUG
		builder.Logging.AddDebug();
#endif
		return builder.Build();
	}
}
