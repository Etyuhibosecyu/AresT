using Avalonia.Platform.Storage;

namespace AresT.ViewModels;

public class MainViewModel : ViewModelBase
{
	public static FilePickerFileType AresTFilesType { get; } = new("Ares T Files") { Patterns = ["*.ares-t"], AppleUniformTypeIdentifiers = ["UTType.Item"], MimeTypes = ["multipart/mixed"] };

	public static FilePickerFileType GetFilesType(bool compression) => compression ? FilePickerFileTypes.All : AresTFilesType;
}
