global using AresTLib;
global using Corlib.NStar;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Maui;
global using Microsoft.Maui.Accessibility;
global using Microsoft.Maui.ApplicationModel;
global using Microsoft.Maui.ApplicationModel.Communication;
global using Microsoft.Maui.ApplicationModel.DataTransfer;
global using Microsoft.Maui.Authentication;
global using Microsoft.Maui.Controls;
global using Microsoft.Maui.Controls.Hosting;
global using Microsoft.Maui.Controls.Xaml;
global using Microsoft.Maui.Devices;
global using Microsoft.Maui.Devices.Sensors;
global using Microsoft.Maui.Dispatching;
global using Microsoft.Maui.Graphics;
global using Microsoft.Maui.Hosting;
global using Microsoft.Maui.Media;
global using Microsoft.Maui.Networking;
global using Microsoft.Maui.Storage;
global using System;
global using System.IO;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
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
