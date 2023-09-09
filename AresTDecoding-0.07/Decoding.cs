global using AresGlobalMethods;
global using Corlib.NStar;
global using System;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using UnsafeFunctions;
global using G = System.Collections.Generic;
global using static AresGlobalMethods.Decoding;
global using static AresTLib.Global;
global using static Corlib.NStar.Extents;
global using static System.Math;
global using static UnsafeFunctions.Global;

namespace AresTLib;

public enum UsedMethods
{
	None = 0,
	CS1 = 1,
	LZ1 = 1 << 1,
	HF1 = 1 << 2,
	//Dev1 = 1 << 3,
	PSLZ1 = 1 << 4,
	CS2 = 1 << 5,
	//Dev2 = 1 << 6,
	LZ2 = 1 << 7,
	SHET2 = 1 << 8,
	CS3 = 1 << 9,
	//Dev3 = 1 << 10,
	//Dev3_2 = 1 << 11,
	CS4 = 1 << 12,
	//Dev4 = 1 << 13,
	SHET4 = 1 << 14,
	CS5 = 1 << 15,
	SHET5 = 1 << 16,
	CS6 = 1 << 17,
	//Dev6 = 1 << 18,
	CS7 = 1 << 19,
	//Dev7 = 1 << 20,
	SHET7 = 1 << 21,
	CS8 = 1 << 22,
	AHF = 1 << 31,
}

public static class Global
{
	public const byte ProgramVersion = 2;
	public const int FragmentLength = 8000000;
	public const int BWTBlockSize = 50000;
	public static UsedMethods PresentMethods { get; set; } = UsedMethods.CS1 | UsedMethods.HF1 | UsedMethods.LZ1 | UsedMethods.CS2 | UsedMethods.LZ2;
	public static string[][] SHETEndinds { get; } = new[] { new[] { "а", "я", "ы", "и", "е", "у", "ю", "ой", "ей", "ам", "ям", "ами", "ями", "ах", "ях", "о", "ь", "ом", "ем", "ём", "ов", "ью", "ени", "енем", "ен", "ён", "ян", "енам", "енами", "енах" }, new[] { "ый", "ий", "ого", "его", "ому", "ему", "ым", "им", "ая", "яя", "ую", "юю", "ою", "ею", "ое", "ее", "ые", "ие", "ых", "их", "ной", "ный", "ний", "ного", "него", "ному", "нему", "ным", "ним", "ном", "нем", "ная", "няя", "ней", "ную", "нюю", "ною", "нею", "ное", "нее", "ные", "ние", "ных", "них", "он", "на", "ня", "но", "нё", "ны", "ни" }, new[] { "ть", "ать", "еть", "ить", "оть", "уть", "ыть", "ять", "овать", "евать", "л", "ал", "ел", "ил", "ол", "ул", "ыл", "ял", "овал", "евал", "ла", "ала", "ела", "ила", "ола", "ула", "ыла", "яла", "овала", "евала", "ло", "ало", "ело", "ило", "оло", "уло", "ыло", "яло", "овало", "евало", "ли", "али", "ели", "или", "оли", "ули", "ыли", "яли", "овали", "евали", "ишь", "ит", "ите", "ешь", "ёшь", "ет", "ёт", "ете", "ёте", "аешь", "аёшь", "ает", "аёт", "аем", "аём", "аете", "аёте", "еешь", "еет", "еем", "еете", "иешь", "иёшь", "иет", "иёт", "ием", "иём", "иете", "иёте", "оешь", "оет", "оем", "оете", "уешь", "уёшь", "ует", "уёт", "уем", "уём", "уете", "уёте", "ьешь", "ьёшь", "ьет", "ьёт", "ьем", "ьём", "ьете", "ьёте", "юешь", "юёшь", "юет", "юёт", "юем", "юём", "юете", "юёте", "яешь", "яет", "яем", "яете", "ут", "ют", "ают", "еют", "иют", "оют", "уют", "ьют", "юют", "яют", "ат", "ят", "ться", "аться", "еться", "иться", "оться", "уться", "ыться", "яться", "оваться", "еваться", "лся", "ался", "елся", "ился", "олся", "улся", "ылся", "ялся", "овался", "евался", "лась", "алась", "елась", "илась", "олась", "улась", "ылась", "ялась", "овалась", "евалась", "лось", "алось", "елось", "илось", "олось", "улось", "ылось", "ялось", "овалось", "евалось", "лись", "ались", "елись", "ились", "олись", "улись", "ылись", "ялись", "овались", "евались", "усь", "юсь", "ишься", "ится", "имся", "итесь", "ешься", "ёшься", "ется", "ётся", "емся", "ёмся", "етесь", "ётесь", "аешься", "аёшься", "ается", "аётся", "аемся", "аёмся", "аетесь", "аётесь", "еешься", "еется", "еемся", "еетесь", "иется", "иётся", "иемся", "иёмся", "иетесь", "иётесь", "оешься", "оется", "оемся", "оетесь", "уешься", "уёшься", "уется", "уётся", "уемся", "уёмся", "уетесь", "уётесь", "ьешься", "ьёшься", "ьется", "ьётся", "ьемся", "ьёмся", "ьетесь", "ьётесь", "юешься", "юёшься", "юется", "юётся", "юемся", "юёмся", "юетесь", "юётесь", "яешься", "яется", "яемся", "яетесь", "утся", "ются", "аются", "еются", "иются", "оются", "уются", "ьются", "юются", "яются", "атся", "ятся", "й", "ай", "ей", "ой", "уй", "ый", "яй", "йся", "айся", "ейся", "ойся", "уйся", "ыйся", "яйся", "ись", "ься", "ущий", "ющий", "ающий", "еющий", "иющий", "оющий", "ующий", "ьющий", "юющий", "яющий", "ащий", "ящий", "ущего", "ющего", "ающего", "еющего", "иющего", "оющего", "ующего", "ьющего", "юющего", "яющего", "ащего", "ящего", "ущему", "ющему", "ающему", "еющему", "иющему", "оющему", "ующему", "ьющему", "юющему", "яющему", "ащему", "ящему", "ущим", "ющим", "ающим", "еющим", "иющим", "оющим", "ующим", "ьющим", "юющим", "яющим", "ащим", "ящим", "ущем", "ющем", "ающем", "еющем", "иющем", "оющем", "ующем", "ьющем", "юющем", "яющем", "ащем", "ящем", "ущая", "ющая", "ающая", "еющая", "иющая", "оющая", "ующая", "ьющая", "юющая", "яющая", "ащая", "ящая", "ущей", "ющей", "ающей", "еющей", "иющей", "оющей", "ующей", "ьющей", "юющей", "яющей", "ащей", "ящей", "ущую", "ющую", "ающую", "еющую", "иющую", "оющую", "ующую", "ьющую", "юющую", "яющую", "ащую", "ящую", "ущее", "ющее", "ающее", "еющее", "иющее", "оющее", "ующее", "ьющее", "юющее", "яющее", "ащее", "ящее", "ущие", "ющие", "ающие", "еющие", "иющие", "оющие", "ующие", "ьющие", "юющие", "яющие", "ащие", "ящие", "ущих", "ющих", "ающих", "еющих", "иющих", "оющих", "ующих", "ьющих", "юющих", "яющих", "ащих", "ящих", "ущими", "ющими", "ающими", "еющими", "иющими", "оющими", "ующими", "ьющими", "юющими", "яющими", "ащими", "ящими", "ущийся", "ющийся", "ающийся", "еющийся", "иющийся", "оющийся", "ующийся", "ьющийся", "юющийся", "яющийся", "ащийся", "ящийся", "ущегося", "ющегося", "ающегося", "еющегося", "иющегося", "оющегося", "ующегося", "ьющегося", "юющегося", "яющегося", "ащегося", "ящегося", "ущемуся", "ющемуся", "ающемуся", "еющемуся", "иющемуся", "оющемуся", "ующемуся", "ьющемуся", "юющемуся", "яющемуся", "ащемуся", "ящемуся", "ущимся", "ющимся", "ающимся", "еющимся", "иющимся", "оющимся", "ующимся", "ьющимся", "юющимся", "яющимся", "ащимся", "ящимся", "ущемся", "ющемся", "ающемся", "еющемся", "иющемся", "оющемся", "ующемся", "ьющемся", "юющемся", "яющемся", "ащемся", "ящемся", "ущаяся", "ющаяся", "ающаяся", "еющаяся", "иющаяся", "оющаяся", "ующаяся", "ьющаяся", "юющаяся", "яющаяся", "ащаяся", "ящаяся", "ущейся", "ющейся", "ающейся", "еющейся", "иющейся", "оющейся", "ующейся", "ьющейся", "юющейся", "яющейся", "ащейся", "ящейся", "ущуюся", "ющуюся", "ающуюся", "еющуюся", "иющуюся", "оющуюся", "ующуюся", "ьющуюся", "юющуюся", "яющуюся", "ащуюся", "ящуюся", "ущееся", "ющееся", "ающееся", "еющееся", "иющееся", "оющееся", "ующееся", "ьющееся", "юющееся", "яющееся", "ащееся", "ящееся", "ущиеся", "ющиеся", "ающиеся", "еющиеся", "иющиеся", "оющиеся", "ующиеся", "ьющиеся", "юющиеся", "яющиеся", "ащиеся", "ящиеся", "ущихся", "ющихся", "ающихся", "еющихся", "иющихся", "оющихся", "ующихся", "ьющихся", "юющихся", "яющихся", "ащихся", "ящихся", "ущимися", "ющимися", "ающимися", "еющимися", "иющимися", "оющимися", "ующимися", "ьющимися", "юющимися", "яющимися", "ащимися", "ящимися", "вший", "авший", "евший", "ивший", "овший", "увший", "ывший", "явший", "овавший", "евавший", "вшего", "авшего", "евшего", "ившего", "овшего", "увшего", "ывшего", "явшего", "овавшего", "евавшего", "вшему", "авшему", "евшему", "ившему", "овшему", "увшему", "ывшему", "явшему", "овавшему", "евавшему", "вшим", "авшим", "евшим", "ившим", "овшим", "увшим", "ывшим", "явшим", "овавшим", "евавшим", "вшем", "авшем", "евшем", "ившем", "овшем", "увшем", "ывшем", "явшем", "овавшем", "евавшем", "вшая", "авшая", "евшая", "ившая", "овшая", "увшая", "ывшая", "явшая", "овавшая", "евавшая", "вшей", "авшей", "евшей", "ившей", "овшей", "увшей", "ывшей", "явшей", "овавшей", "евавшей", "вшую", "авшую", "евшую", "ившую", "овшую", "увшую", "ывшую", "явшую", "овавшую", "евавшую", "вшее", "авшее", "евшее", "ившее", "овшее", "увшее", "ывшее", "явшее", "овавшее", "евавшее", "вшие", "авшие", "евшие", "ившие", "овшие", "увшие", "ывшие", "явшие", "овавшие", "евавшие", "вших", "авших", "евших", "ивших", "овших", "увших", "ывших", "явших", "овавших", "евавших", "вшими", "авшими", "евшими", "ившими", "овшими", "увшими", "ывшими", "явшими", "овавшими", "евавшими", "вшийся", "авшийся", "евшийся", "ившийся", "овшийся", "увшийся", "ывшийся", "явшийся", "овавшийся", "евавшийся", "вшегося", "авшегося", "евшегося", "ившегося", "овшегося", "увшегося", "ывшегося", "явшегося", "овавшегося", "евавшегося", "вшемуся", "авшемуся", "евшемуся", "ившемуся", "овшемуся", "увшемуся", "ывшемуся", "явшемуся", "овавшемуся", "евавшемуся", "вшимся", "авшимся", "евшимся", "ившимся", "овшимся", "увшимся", "ывшимся", "явшимся", "овавшимся", "евавшимся", "вшемся", "авшемся", "евшемся", "ившемся", "овшемся", "увшемся", "ывшемся", "явшемся", "овавшемся", "евавшемся", "вшаяся", "авшаяся", "евшаяся", "ившаяся", "овшаяся", "увшаяся", "ывшаяся", "явшаяся", "овавшаяся", "евавшаяся", "вшейся", "авшейся", "евшейся", "ившейся", "овшейся", "увшейся", "ывшейся", "явшейся", "овавшейся", "евавшейся", "вшуюся", "авшуюся", "евшуюся", "ившуюся", "овшуюся", "увшуюся", "ывшуюся", "явшуюся", "овавшуюся", "евавшуюся", "вшееся", "авшееся", "евшееся", "ившееся", "овшееся", "увшееся", "ывшееся", "явшееся", "овавшееся", "евавшееся", "вшиеся", "авшиеся", "евшиеся", "ившиеся", "овшиеся", "увшиеся", "ывшиеся", "явшиеся", "овавшиеся", "евавшиеся", "вшихся", "авшихся", "евшихся", "ившихся", "овшихся", "увшихся", "ывшихся", "явшихся", "овавшихся", "евавшихся", "вшимися", "авшимися", "евшимися", "ившимися", "овшимися", "увшимися", "ывшимися", "явшимися", "овавшимися", "евавшимися",
			"имый", "емый", "аемый", "еемый", "оемый", "уемый", "юемый", "яемый", "омый", "имого", "емого", "аемого", "еемого", "оемого", "уемого", "юемого", "яемого", "омого", "имому", "емому", "аемому", "еемому", "оемому", "уемому", "юемому", "яемому", "омому", "имым", "емым", "аемым", "еемым", "оемым", "уемым", "юемым", "яемым", "омым", "имом", "емом", "аемом", "еемом", "оемом", "уемом", "юемом", "яемом", "омом", "имая", "емая", "аемая", "еемая", "оемая", "уемая", "юемая", "яемая", "омая", "имой", "емой", "аемой", "еемой", "оемой", "уемой", "юемой", "яемой", "омой", "имую", "емую", "аемую", "еемую", "оемую", "уемую", "юемую", "яемую", "омую", "имое", "емое", "аемое", "еемое", "оемое", "уемое", "юемое", "яемое", "омое", "имые", "емые", "аемые", "еемые", "оемые", "уемые", "юемые", "яемые", "омые", "имых", "емых", "аемых", "еемых", "оемых", "уемых", "юемых", "яемых", "омых", "имыми", "емыми", "аемыми", "еемыми", "оемыми", "уемыми", "юемыми", "яемыми", "омыми", "аный", "еный", "ёный", "яный", "ованый", "еваный", "ёваный", "анный", "енный", "ённый", "янный", "ованный", "еванный", "ёванный", "аного", "еного", "ёного", "яного", "ованого", "еваного", "ёваного", "анного", "енного", "ённого", "янного", "ованного", "еванного", "ёванного", "аному", "еному", "ёному", "яному", "ованому", "еваному", "ёваному", "анному", "енному", "ённому", "янному", "ованному", "еванному", "ёванному", "аным", "еным", "ёным", "яным", "ованым", "еваным", "ёваным", "анным", "енным", "ённым", "янным", "ованным", "еванным", "ёванным", "аном", "еном", "ёном", "яном", "ованом", "еваном", "ёваном", "анном", "енном", "ённом", "янном", "ованном", "еванном", "ёванном", "аная", "еная", "ёная", "яная", "ованая", "еваная", "ёваная", "анная", "енная", "ённая", "янная", "ованная", "еванная", "ёванная", "аной", "еной", "ёной", "яной", "ованой", "еваной", "ёваной", "анной", "енной", "ённой", "янной", "ованной", "еванной", "ёванной", "аную", "еную", "ёную", "яную", "ованую", "еваную", "ёваную", "анную", "енную", "ённую", "янную", "ованную", "еванную", "ёванную", "аное", "еное", "ёное", "яное", "ованое", "еваное", "ёваное", "анное", "енное", "ённое", "янное", "ованное", "еванное", "ёванное", "аные", "еные", "ёные", "яные", "ованые", "еваные", "ёваные", "анные", "енные", "ённые", "янные", "ованные", "еванные", "ёванные", "аных", "еных", "ёных", "яных", "ованых", "еваных", "ёваных", "анных", "енных", "ённых", "янных", "ованных", "еванных", "ёванных", "аными", "еными", "ёными", "яными", "оваными", "еваными", "ёваными", "анными", "енными", "ёнными", "янными", "ованными", "еванными", "ёванными", "тый", "атый", "етый", "итый", "отый", "утый", "ытый", "ятый", "того", "атого", "етого", "итого", "отого", "утого", "ытого", "ятого", "тому", "атому", "етому", "итому", "отому", "утому", "ытому", "ятому", "тым", "атым", "етым", "итым", "отым", "утым", "ытым", "ятым", "том", "атом", "етом", "итом", "отом", "утом", "ытом", "ятом", "тая", "атая", "етая", "итая", "отая", "утая", "ытая", "ятая", "той", "атой", "етой", "итой", "отой", "утой", "ытой", "ятой", "тую", "атую", "етую", "итую", "отую", "утую", "ытую", "ятую", "тое", "атое", "етое", "итое", "отое", "утое", "ытое", "ятое", "тые", "атые", "етые", "итые", "отые", "утые", "ытые", "ятые", "тых", "атых", "етых", "итых", "отых", "утых", "ытых", "ятых", "тыми", "атыми", "етыми", "итыми", "отыми", "утыми", "ытыми", "ятыми", "учи", "ючи", "аючи", "еючи", "иючи", "оючи", "уючи", "ьючи", "юючи", "яючи", "в", "ав", "ев", "ив", "ув", "ыв", "яв", "овав", "евав", "вши", "авши", "евши", "ивши", "овши", "увши", "ывши", "явши", "овавши", "евавши", "ши", "ась", "ясь", "учись", "ючись", "аючись", "еючись", "иючись", "оючись", "уючись", "ьючись", "юючись", "яючись", "вшись", "авшись", "евшись", "ившись", "овшись", "увшись", "ывшись", "явшись", "овавшись", "евавшись", "шись" }, new[] { "без", "безо", "близ", "в", "вблизи", "ввиду", "вглубь", "вдогон", "вдоль", "взамен", "включая", "вкруг", "вместо", "вне", "внизу", "внутри", "внутрь", "во", "вовнутрь", "возле", "вокруг", "вопреки", "впереди", "вроде", "вслед", "вследствие", "встречу", "выключая", "для", "до", "за", "заместо", "из", "изнутри", "изо", "исключая", "к", "касаемо", "касательно", "ко", "кончая", "кроме", "кругом", "меж", "между", "мимо", "на", "наверху", "навстречу", "над", "надо", "назад", "назло", "накануне", "наперекор", "наперерез", "наподобие", "напротив", "насчёт", "ниже", "о", "об", "обо", "обок", "около", "от", "относительно", "ото", "перед", "передо", "по", "поверх", "под", "под видом", "подле", "подо", "подобно", "позади", "помимо", "поперёд", "поперёк", "порядка", "посередине", "после", "посреди", "посредине", "посредством", "пред", "предо", "прежде", "при", "про", "против", "путём", "ради", "с", "сверх", "сверху", "свыше", "сзади", "сквозь", "снизу", "со", "согласно", "спустя", "среди", "средь", "сродни", "супротив", "у", "через", "черезо", "чрез" } };
	public static Dictionary<string, int> SHETDic1 { get; } = SHETEndinds.GetSlice(0, 3).JoinIntoSingle().Filter(x => x.Length > 2).Wrap(x => (x, new Chain(x.Length)).ToDictionary());
	public static int SHETThreshold1 { get; } = ValuesInByte - (SHETDic1.Length == ValuesInByte ? 0 : Max(SHETDic1.Length, 0) >> BitsPerByte);
	public static Dictionary<string, int> SHETDic2 { get; } = SHETEndinds[3].Filter(x => x.Length > 2).Wrap(x => (x, new Chain(x.Length)).ToDictionary());
	public static int SHETThreshold2 { get; } = ValuesInByte - (SHETDic2.Length == ValuesInByte ? 0 : Max(SHETDic2.Length, 0) >> BitsPerByte);
}

public static class Decoding
{
	public static byte[] Decode(byte[] compressedFile, byte encodingVersion)
	{
		if (compressedFile.Length <= 2)
			return Array.Empty<byte>();
		if (encodingVersion == 0)
			return compressedFile;
		else if (encodingVersion < ProgramVersion)
		{
			return encodingVersion switch
			{
				1 => AresTLib005.Decoding.Decode(compressedFile, encodingVersion),
				_ => throw new DecoderFallbackException(),
			};
		}
		int method = compressedFile[0];
		if (method == 0)
			return compressedFile[1..];
		else if (compressedFile.Length <= 2)
			throw new DecoderFallbackException();
		int misc = method >= 64 ? method % 64 % 7 : -1, hf = method % 64 % 7, rle = method % 64 % 21 / 7 * 7, lz = method % 64 % 42 / 21 * 21, bwt = method % 64 / 42 * 42;
		var hfw = hf is 2 or 3 or 5 or 6;
		if (method != 0 && compressedFile.Length <= 5)
			throw new DecoderFallbackException();
		NList<byte> byteList;
		var repeatsCount = 1;
		if (misc == 2)
		{
			using ArithmeticDecoder ar = compressedFile[1..];
			var repeats = ar.ReadPart(new List<uint>(2, 224, 225));
			repeatsCount = repeats == 0 ? 1 : (int)ar.ReadCount() + 2;
			if (repeatsCount > FragmentLength >> 1)
				throw new DecoderFallbackException();
			var (encoding, maxLength, nullCount) = (ar.ReadEqual(3), ar.ReadCount(), ar.ReadCount((uint)BitsCount(FragmentLength)));
			if (maxLength is < 2 or > FragmentLength || nullCount > FragmentLength)
				throw new DecoderFallbackException();
			ListHashSet<int> nulls = new();
			for (var i = 0; i < nullCount; i++)
				nulls.Add((int)ar.ReadCount((uint)BitsCount(FragmentLength)) + (nulls.Length == 0 ? 0 : nulls[^1] + 1));
			Current[0] = 0;
			CurrentMaximum[0] = ProgressBarStep * 5;
			List<List<ShortIntervalList>> list = DecodePPM(ar, maxLength, ref repeatsCount, 0);
			list[0].Add(new() { new(encoding, 3) });
			Current[0] += ProgressBarStep;
			list.Add(DecodePPM(ar, ValuesInByte, ref repeatsCount, 1));
			Current[0] += ProgressBarStep;
			list.Add(DecodePPM(ar, (uint)list[0].Length - 1, ref repeatsCount, 2));
			Current[0] += ProgressBarStep;
			byteList = list.JoinWords(nulls);
		}
		else if (misc == 1)
			byteList = DecodePPM(compressedFile[1..], ValuesInByte, ref repeatsCount).PNConvert(x => (byte)x[0].Lower);
		else
		{
			Current[0] = 0;
			CurrentMaximum[0] = ProgressBarStep * (bwt != 0 ? (hfw ? 8 : 4) : (hfw ? 7 : 3));
			using ArithmeticDecoder ar = compressedFile[1..];
			ListHashSet<int> nulls = new();
			byteList = hfw ? RedStarLinq.Fill(3, i => Decode2(ar, hf, hfw, bwt, lz, ref repeatsCount, nulls, i)).JoinWords(nulls) : Decode2(ar, hf, hfw, bwt, lz, ref repeatsCount).PNConvert(x => (byte)x[0].Lower);
		}
		Current[0] += ProgressBarStep;
		if (rle == 14)
			byteList = DecodeRLE3(byteList);
		Current[0] += ProgressBarStep;
		if (rle == 7)
			byteList = DecodeRLE(byteList);
		return byteList.Repeat(repeatsCount).ToArray();
	}

	private static NList<byte> JoinWords(this List<List<ShortIntervalList>> input, ListHashSet<int> nulls) => input.Wrap(tl =>
	{
		var encoding = tl[0][^1][0].Lower;
		var encoding2 = (encoding == 1) ? Encoding.Unicode : (encoding == 2) ? Encoding.UTF8 : Encoding.GetEncoding(1251);
		var a = 0;
		var wordsList = tl[0].GetSlice(..^1).Convert(l => encoding2.GetString(tl[1][a..(a += (int)l[0].Lower)].ToArray(x => (byte)x[0].Lower)));
		var result = encoding2.GetBytes(tl[2].ConvertAndJoin(l => wordsList[(int)l[0].Lower].Wrap(x => l[1].Lower == 1 ? new List<char>(x).Add(' ') : x)).ToArray()).ToNList();
		foreach (var x in nulls)
			if (encoding == 0)
				result.Insert(x, 0);
			else
				result.Insert(x, new byte[] { 0, 0 });
		return result;
	});

	private static List<ShortIntervalList> Decode2(ArithmeticDecoder ar, int hf, bool hfw, int bwt, int lz, ref int repeatsCount, ListHashSet<int> nulls = default!, int n = 0)
	{
		var counter = (int)ar.ReadCount() - (hfw && n == 0 ? 2 : 1);
		uint lzRDist, lzMaxDist, lzThresholdDist = 0, lzRLength, lzMaxLength, lzThresholdLength = 0, lzUseSpiralLengths = 0, lzRSpiralLength, lzMaxSpiralLength, lzThresholdSpiralLength = 0;
		MethodDataUnit lzDist = new(), lzLength = new(), lzSpiralLength = new();
		int maxFrequency = 0, frequencyCount = 0;
		List<uint> arithmeticMap = new();
		List<Interval> uniqueList = new();
		List<int> skipped = new();
		if (n == 0)
		{
			var repeats = ar.ReadPart(new List<uint>(2, 224, 225));
			repeatsCount = repeats == 0 ? 1 : (int)ar.ReadCount() + 2;
			if (repeatsCount > FragmentLength >> 1)
				throw new DecoderFallbackException();
		}
		var (encoding, maxLength, nullsCount) = hfw && n == 0 ? (ar.ReadEqual(3), ar.ReadCount(), ar.ReadCount((uint)BitsCount(FragmentLength))) : (0, 0, 0);
		if (hfw && n == 0 && nulls != null)
		{
			var counter2 = 1;
			if (maxLength is < 2 or > FragmentLength || nullsCount > FragmentLength)
				throw new DecoderFallbackException();
			for (var i = 0; i < nullsCount; i++)
			{
				var value = ar.ReadCount((uint)BitsCount(FragmentLength));
				if (value > FragmentLength)
					throw new DecoderFallbackException();
				nulls.Add((int)value + (nulls.Length == 0 ? 0 : nulls[^1] + 1));
				counter2++;
			}
			counter -= GetArrayLength(counter2, 4);
		}
		if (lz != 0)
		{
			var counter2 = 7;
			lzRDist = ar.ReadEqual(3);
			lzMaxDist = ar.ReadCount();
			if (lzRDist != 0)
			{
				lzThresholdDist = ar.ReadEqual(lzMaxDist + 1);
				counter2++;
			}
			lzDist = (lzRDist, lzMaxDist, lzThresholdDist);
			lzRLength = ar.ReadEqual(3);
			lzMaxLength = ar.ReadCount(16);
			if (lzRLength != 0)
			{
				lzThresholdLength = ar.ReadEqual(lzMaxLength + 1);
				counter2++;
			}
			lzLength = (lzRLength, lzMaxLength, lzThresholdLength);
			if (lzMaxDist == 0 && lzMaxLength == 0 && ar.ReadEqual(2) == 0)
			{
				lz = 0;
				goto l0;
			}
			lzUseSpiralLengths = ar.ReadEqual(2);
			if (lzUseSpiralLengths == 1)
			{
				lzRSpiralLength = ar.ReadEqual(3);
				lzMaxSpiralLength = ar.ReadCount(16);
				counter2 += 3;
				if (lzRSpiralLength != 0)
				{
					lzThresholdSpiralLength = ar.ReadEqual(lzMaxSpiralLength + 1);
					counter2++;
				}
				lzSpiralLength = (lzRSpiralLength, lzMaxSpiralLength, lzThresholdSpiralLength);
			}
			l0:
			counter -= GetArrayLength(counter2, 8);
		}
		LZData lzData = (lzDist, lzLength, lzUseSpiralLengths, lzSpiralLength);
		List<ShortIntervalList> compressedList;
		if (hf >= 4)
		{
			compressedList = ar.DecodeAdaptive(hfw, bwt, skipped, lzData, lz, counter, n);
			goto l1;
		}
		if (hf != 0)
		{
			var counter2 = 4;
			maxFrequency = (int)ar.ReadCount() + 1;
			frequencyCount = (int)ar.ReadCount() + 1;
			if (maxFrequency > FragmentLength || frequencyCount > FragmentLength)
				throw new DecoderFallbackException();
			Status[0] = 0;
			StatusMaximum[0] = frequencyCount;
			var @base = hfw && n == 0 ? maxLength + 1 : hfw && n == 2 ? (uint)frequencyCount : 256;
			if (maxFrequency > frequencyCount * 2 || frequencyCount <= 256)
			{
				arithmeticMap.Add((uint)maxFrequency);
				var prev = (uint)maxFrequency;
				for (var i = 0; i < frequencyCount; i++, Status[0]++)
				{
					counter2++;
					uniqueList.Add(new(ar.ReadEqual(@base), @base));
					if (i == 0) continue;
					prev = ar.ReadEqual(prev) + 1;
					counter2++;
					arithmeticMap.Add(arithmeticMap[^1] + prev);
				}
			}
			else
				for (var i = 0; i < frequencyCount; i++, Status[0]++)
				{
					uniqueList.Add(new((uint)i, hfw && n == 0 ? maxLength + 1 : (uint)frequencyCount));
					counter2++;
					arithmeticMap.Add((arithmeticMap.Length == 0 ? 0 : arithmeticMap[^1]) + ar.ReadEqual((uint)maxFrequency) + 1);
				}
			if (lz != 0)
				arithmeticMap.Add(GetBaseWithBuffer(arithmeticMap[^1]));
			counter -= GetArrayLength(counter2, 8);
			if (bwt != 0 && !(hfw && n != 1))
			{
				var skippedCount = (int)ar.ReadCount();
				for (var i = 0; i < skippedCount; i++)
					skipped.Add((int)ar.ReadEqual(@base));
				counter -= (skippedCount + 9) / 8;
			}
		}
		else
		{
			uniqueList.AddRange(RedStarLinq.Fill(256, index => new Interval((uint)index, 256)));
			arithmeticMap.AddRange(RedStarLinq.Fill(256, index => (uint)(index + 1)));
			if (lz != 0)
				arithmeticMap.Add(269);
		}
		if (counter is < 0 or > FragmentLength)
			throw new DecoderFallbackException();
		HuffmanData huffmanData = (maxFrequency, frequencyCount, arithmeticMap, uniqueList);
		Current[0] += ProgressBarStep;
		compressedList = ar.ReadCompressedList(huffmanData, bwt, lzData, lz, counter, n == 2);
	l1:
		if (hfw && n == 0)
			compressedList.Add(new() { new(encoding, 3) });
		if (bwt != 0 && !(hfw && n != 1))
		{
			Current[0]++;
			compressedList = compressedList.DecodeBWT(skipped);
		}
		if (hfw && n != 2)
			Current[0]++;
		return compressedList;
	}

	private static List<ShortIntervalList> DecodeAdaptive(this ArithmeticDecoder ar, bool hfw, int bwt, List<int> skipped, LZData lzData, int lz, int counter, int n)
	{
		if (bwt != 0 && !(hfw && n != 1))
		{
			var skippedCount = (int)ar.ReadCount();
			var @base = skippedCount == 0 ? 1 : ar.ReadCount();
			if (skippedCount > @base || @base > FragmentLength)
				throw new DecoderFallbackException();
			for (var i = 0; i < skippedCount; i++)
				skipped.Add((int)ar.ReadEqual(@base));
			counter -= skippedCount == 0 ? 1 : (skippedCount + 11) / 8;
		}
		var fileBase = ar.ReadCount();
		if (counter is < 0 or > FragmentLength)
			throw new DecoderFallbackException();
		Status[0] = 0;
		StatusMaximum[0] = counter;
		SumSet<uint> set = new() { (uint.MaxValue, 1) };
		SumList lengthsSL = lz != 0 ? new(RedStarLinq.Fill(1, (int)(lzData.Length.R == 0 ? lzData.Length.Max + 1 : lzData.Length.R == 1 ? lzData.Length.Threshold + 2 : lzData.Length.Max - lzData.Length.Threshold + 2))) : new(), distsSL = lz != 0 ? new(RedStarLinq.Fill(1, (int)lzData.UseSpiralLengths + 1)) : new();
		var firstIntervalDist = lz != 0 ? (lzData.Dist.R == 1 ? lzData.Dist.Threshold + 2 : lzData.Dist.Max + 1) + lzData.UseSpiralLengths : 0;
		List<Interval> uniqueList = new();
		if (lz != 0)
		{
			set.Add((fileBase - 1, 1));
			uniqueList.Add(new(fileBase - 1, fileBase));
		}
		List<ShortIntervalList> result = new();
		var fullLength = 0;
		uint nextWordLink = 0;
		for (; counter > 0; counter--, Status[0]++)
		{
			var readIndex = ar.ReadPart(set);
			if (readIndex == set.Length - 1)
			{
				var actualIndex = n == 2 ? nextWordLink++ : ar.ReadEqual(fileBase);
				if (!set.TryAdd((actualIndex, 1), out readIndex))
					throw new DecoderFallbackException();
				uniqueList.Insert(readIndex, new Interval(actualIndex, fileBase));
			}
			else
				set.Increase(uniqueList[readIndex].Lower);
			set.Update(uint.MaxValue, (int)GetBufferInterval((uint)set.GetLeftValuesSum(uint.MaxValue, out _)));
			if (!(lz != 0 && uniqueList[readIndex].Lower == fileBase - 1))
			{
				result.Add(n == 2 ? new() { uniqueList[readIndex], new(ar.ReadEqual(2), 2) } : new() { uniqueList[readIndex] });
				fullLength++;
				if (lz != 0 && distsSL.Length < firstIntervalDist)
					distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1);
				continue;
			}
			result.Add(new() { uniqueList[^1] });
			uint dist, length, spiralLength = 0;
			readIndex = ar.ReadPart(lengthsSL);
			lengthsSL.Increase(readIndex);
			if (lzData.Length.R == 0)
				length = (uint)readIndex;
			else if (lzData.Length.R == 1)
			{
				length = (uint)readIndex;
				if (length == lzData.Length.Threshold + 1)
					length += ar.ReadEqual(lzData.Length.Max - lzData.Length.Threshold);
			}
			else
			{
				length = (uint)readIndex + lzData.Length.Threshold;
				if (length == lzData.Length.Max + 1)
					length = ar.ReadEqual(lzData.Length.Threshold);
			}
			result[^1].Add(new(length, lzData.Length.Max + 1));
			var maxDist = Min(lzData.Dist.Max, (uint)(fullLength - length - 2));
			readIndex = ar.ReadPart(distsSL);
			distsSL.Increase(readIndex);
			if (lzData.Dist.R == 0 || maxDist < lzData.Dist.Threshold)
				dist = (uint)readIndex;
			else if (lzData.Dist.R == 1)
			{
				dist = (uint)readIndex;
				if (dist == lzData.Dist.Threshold + 1)
					dist += ar.ReadEqual(maxDist - lzData.Dist.Threshold + lzData.UseSpiralLengths);
			}
			else
			{
				dist = (uint)readIndex/* + lzData.Dist.Threshold*/;
				//if (dist == maxDist + 1)
				//{
				//	dist = ar.ReadEqual(lzData.Dist.Threshold + lzData.UseSpiralLengths);
				//	if (dist == lzData.Dist.Threshold)
				//		dist = maxDist + 1;
				//}
			}
			result[^1].Add(new(dist, maxDist + lzData.UseSpiralLengths + 1));
			if (dist == maxDist + 1)
			{
				if (lzData.SpiralLength.R == 0)
					spiralLength = ar.ReadEqual(lzData.SpiralLength.Max + 1);
				else if (lzData.SpiralLength.R == 1)
				{
					spiralLength = ar.ReadEqual(lzData.SpiralLength.Threshold + 2);
					if (spiralLength == lzData.SpiralLength.Threshold + 1)
						spiralLength += ar.ReadEqual(lzData.SpiralLength.Max - lzData.SpiralLength.Threshold);
				}
				else
				{
					spiralLength = ar.ReadEqual(lzData.SpiralLength.Max - lzData.SpiralLength.Threshold + 2) + lzData.SpiralLength.Threshold;
					if (spiralLength == lzData.SpiralLength.Max + 1)
						spiralLength = ar.ReadEqual(lzData.SpiralLength.Threshold);
				}
				result[^1].Add(new(spiralLength, lzData.SpiralLength.Max + 1));
			}
			fullLength += (int)((length + 2) * (spiralLength + 1));
			if (lz != 0 && distsSL.Length < firstIntervalDist)
				new Chain((int)Min(firstIntervalDist - distsSL.Length, (length + 2) * (spiralLength + 1))).ForEach(x => distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1));
		}
		return DecodeLempelZiv(result, lz != 0, 0, 0, 0, 0, lzData.UseSpiralLengths, 0, 0, 0);
	}

	private static List<ShortIntervalList> ReadCompressedList(this ArithmeticDecoder ar, HuffmanData huffmanData, int bwt, LZData lzData, int lz, int counter, bool spaceCodes)
	{
		Status[0] = 0;
		StatusMaximum[0] = counter;
		List<ShortIntervalList> result = new();
		var startingArithmeticMap = lz == 0 ? huffmanData.ArithmeticMap : huffmanData.ArithmeticMap[..^1];
		var uniqueLists = spaceCodes ? RedStarLinq.Fill(2, i => huffmanData.UniqueList.PConvert(x => new ShortIntervalList { x, new((uint)i, 2) })) : huffmanData.UniqueList.Convert(x => new ShortIntervalList() { x });
		for (; counter > 0; counter--, Status[0]++)
		{
			var readIndex = ar.ReadPart(result.Length < 2 || bwt != 0 && (result.Length < 4 || (result.Length + 0) % (BWTBlockSize + 2) is 0 or 1) ? startingArithmeticMap : huffmanData.ArithmeticMap);
			if (!(lz != 0 && readIndex == huffmanData.ArithmeticMap.Length - 1))
			{
				result.Add(uniqueLists[spaceCodes ? (int)ar.ReadEqual(2) * 1 : 0][readIndex]);
				continue;
			}
			uint dist, length, spiralLength = 0;
			if (lzData.Length.R == 0)
				length = ar.ReadEqual(lzData.Length.Max + 1);
			else if (lzData.Length.R == 1)
			{
				length = ar.ReadEqual(lzData.Length.Threshold + 2);
				if (length == lzData.Length.Threshold + 1)
					length += ar.ReadEqual(lzData.Length.Max - lzData.Length.Threshold);
			}
			else
			{
				length = ar.ReadEqual(lzData.Length.Max - lzData.Length.Threshold + 2) + lzData.Length.Threshold;
				if (length == lzData.Length.Max + 1)
					length = ar.ReadEqual(lzData.Length.Threshold);
			}
			if (length > result.Length - 2)
				throw new DecoderFallbackException();
			var maxDist = Min(lzData.Dist.Max, (uint)(result.Length - length - 2));
			if (lzData.Dist.R == 0 || maxDist < lzData.Dist.Threshold)
				dist = ar.ReadEqual(maxDist + lzData.UseSpiralLengths + 1);
			else if (lzData.Dist.R == 1)
			{
				dist = ar.ReadEqual(lzData.Dist.Threshold + 2);
				if (dist == lzData.Dist.Threshold + 1)
					dist += ar.ReadEqual(maxDist - lzData.Dist.Threshold + lzData.UseSpiralLengths);
			}
			else
			{
				dist = ar.ReadEqual(maxDist - lzData.Dist.Threshold + 2) + lzData.Dist.Threshold;
				if (dist == maxDist + 1)
				{
					dist = ar.ReadEqual(lzData.Dist.Threshold + lzData.UseSpiralLengths);
					if (dist == lzData.Dist.Threshold)
						dist = maxDist + 1;
				}
			}
			if (dist == maxDist + 1)
			{
				if (lzData.SpiralLength.R == 0)
					spiralLength = ar.ReadEqual(lzData.SpiralLength.Max + 1);
				else if (lzData.SpiralLength.R == 1)
				{
					spiralLength = ar.ReadEqual(lzData.SpiralLength.Threshold + 2);
					if (spiralLength == lzData.SpiralLength.Threshold + 1)
						spiralLength += ar.ReadEqual(lzData.SpiralLength.Max - lzData.SpiralLength.Threshold);
				}
				else
				{
					spiralLength = ar.ReadEqual(lzData.SpiralLength.Max - lzData.SpiralLength.Threshold + 2) + lzData.SpiralLength.Threshold;
					if (spiralLength == lzData.SpiralLength.Max + 1)
						spiralLength = ar.ReadEqual(lzData.SpiralLength.Threshold);
				}
				dist = 0;
			}
			var start = (int)(result.Length - dist - length - 2);
			if (start < 0)
				throw new DecoderFallbackException();
			var fullLength = (int)((length + 2) * (spiralLength + 1));
			for (var i = fullLength; i > 0; i -= (int)length + 2)
			{
				var length2 = (int)Min(length + 2, i);
				result.AddRange(result.GetRange(start, length2));
			}
		}
		return result;
	}

	private static List<ShortIntervalList> DecodePPM(this ArithmeticDecoder ar, uint inputBase, ref int repeatsCount, int n = -1)
	{
		if (n == -1)
		{
			var repeats = ar.ReadPart(new List<uint>(2, 224, 225));
			repeatsCount = repeats == 0 ? 1 : (int)ar.ReadCount() + 2;
			if (repeatsCount > FragmentLength >> 1)
				throw new DecoderFallbackException();
		}
		uint counter = ar.ReadCount(), dicsize = ar.ReadCount();
		if (counter > FragmentLength || dicsize > FragmentLength)
			throw new DecoderFallbackException();
		Status[0] = 0;
		StatusMaximum[0] = (int)counter;
		List<ShortIntervalList> result = new();
		SumSet<uint> globalSet = new(), newItemsSet = n == 2 ? new() : new(new Chain((int)inputBase).Convert(x => ((uint)x, 1)));
		var maxDepth = 12;
		var comparer = n == 2 ? (G.IEqualityComparer<NList<uint>>)new NListEComparer<uint>() : new EComparer<NList<uint>>((x, y) => x.Equals(y), x => (int)x.Progression((uint)x.Length, (x, y) => (x << 7 | x >> BitsPerInt - 7) ^ (uint)y.GetHashCode()));
		FastDelHashSet<NList<uint>> contextHS = new(comparer);
		List<SumSet<uint>> sumSets = new();
		SumList lzLengthsSL = new() { 1 };
		List<uint> preLZMap = new(2, 1, 2), spacesMap = new(2, 1, 2);
		uint nextWordLink = 0;
		for (; (int)counter > 0; counter--, Status[0]++)
		{
			var context = result.GetSlice(Max(result.Length - maxDepth, 0)..).NConvert(x => x[0].Lower).Reverse();
			var context2 = context.Copy();
			var index = -1;
			SumSet<uint>? set = null, excludingSet = new();
			uint item;
			if (context.Length == maxDepth && counter > maxDepth)
			{
				if (ar.ReadPart(preLZMap) == 1)
				{
					ProcessLZ(result.Length);
					continue;
				}
				else
				{
					preLZMap[0]++;
					preLZMap[1]++;
				}
			}
			for (; context.Length > 0 && !contextHS.TryGetIndexOf(context, out index); context.RemoveAt(^1)) ;
			var arithmeticIndex = -1;
			for (; context.Length > 0 && contextHS.TryGetIndexOf(context, out index) && (arithmeticIndex = (set = sumSets[index].Copy().ExceptWith(excludingSet)).Length == 0 ? 1 : ar.ReadPart(new List<uint>(2, (uint)set.ValuesSum, (uint)(set.ValuesSum + set.Length * 100)))) == 1; context.RemoveAt(^1), excludingSet.UnionWith(set)) ;
			if (set == null || context.Length == 0)
			{
				set = globalSet.Copy().ExceptWith(excludingSet);
				if (set.Length != 0 && (arithmeticIndex = ar.ReadPart(new List<uint>(2, (uint)set.ValuesSum, (uint)(set.ValuesSum + set.Length * 100)))) != 1)
				{
					if (set.Length != 0) arithmeticIndex = ar.ReadPart(set);
					item = set[arithmeticIndex].Key;
				}
				else if (n == 2)
					item = nextWordLink++;
				else
				{
					item = newItemsSet[ar.ReadPart(newItemsSet)].Key;
					newItemsSet.RemoveValue(item);
				}
			}
			else
			{
				if (set.Length != 0) arithmeticIndex = ar.ReadPart(set);
				item = set[arithmeticIndex].Key;
			}
			result.Add(new() { new(item, inputBase) });
			if (n == 2)
			{
				var space = (uint)ar.ReadPart(spacesMap);
				result[^1].Add(new(space, 2));
				spacesMap[0] += 1 - space;
				spacesMap[1]++;
			}
			Increase(context2, item);
			context.Dispose();
			context2.Dispose();
		}
		void ProcessLZ(int curPos)
		{
			var dist = ar.ReadEqual(Min((uint)result.Length, dicsize - 1));
			var oldPos = (int)(result.Length - dist - 2);
			var readIndex = ar.ReadPart(lzLengthsSL);
			uint length;
			if (readIndex < lzLengthsSL.Length - 1)
			{
				length = (uint)readIndex + 1;
				lzLengthsSL.Increase(readIndex);
			}
			else if (ar.ReadFibonacci(out length) && length + maxDepth - 1 <= counter)
			{
				length += (uint)lzLengthsSL.Length - 1;
				lzLengthsSL.Increase(lzLengthsSL.Length - 1);
				new Chain((int)length - lzLengthsSL.Length).ForEach(x => lzLengthsSL.Insert(lzLengthsSL.Length - 1, 1));
			}
			else
				throw new DecoderFallbackException();
			for (var i = 0; i < length + maxDepth - 1; i++)
			{
				result.Add(result[oldPos + i]);
				Increase(result.GetSlice(result.Length - maxDepth - 1, maxDepth).NConvert(x => x[0].Lower).Reverse(), result[^1][0].Lower);
			}
			preLZMap[1]++;
			var decrease = length + maxDepth - 2;
			counter -= (uint)decrease;
			Status[0] += (int)decrease;
		}
		void Increase(NList<uint> context, uint item)
		{
			for (; context.Length > 0 && !contextHS.TryGetIndexOf(context, out var index); context.RemoveAt(^1))
			{
				contextHS.TryAdd(context.Copy(), out index);
				sumSets.SetOrAdd(index, new() { (item, 100) });
			}
			var successLength = context.Length;
			for (; context.Length > 0 && contextHS.TryGetIndexOf(context, out var index); context.RemoveAt(^1))
			{
				if (!sumSets[index].TryGetValue(item, out var itemValue))
				{
					sumSets[index].Add(item, 100);
					continue;
				}
				else if (context.Length == 1 || itemValue > 100)
				{
					sumSets[index].Update(item, itemValue + (int)Max(Round((double)100 / (successLength - context.Length + 1)), 1));
					continue;
				}
				var successContext = context.Copy().RemoveAt(^1);
				var successIndex = contextHS.IndexOf(successContext);
				if (!sumSets[successIndex].TryGetValue(item, out var successValue))
					successValue = 100;
				var step = (double)(sumSets[index].ValuesSum + sumSets[index].Length * 100) * successValue / (sumSets[index].ValuesSum + sumSets[successIndex].ValuesSum + sumSets[successIndex].Length * 100 - successValue);
				sumSets[index].Update(item, (int)(Max(Round(step), 1) + itemValue));
			}
			if (globalSet.TryGetValue(item, out var globalValue))
				globalSet.Update(item, globalValue + (int)Max(Round((double)100 / (successLength + 1)), 1));
			else
				globalSet.Add(item, 100);
		}
		return result;
	}

	private static List<ShortIntervalList> DecodeBWT(this List<ShortIntervalList> input, List<int> skipped)
	{
		Status[0] = 0;
		StatusMaximum[0] = GetArrayLength(input.Length, BWTBlockSize + 2);
		var hs = input.Convert(x => (int)x[0].Lower).FilterInPlace((x, index) => index % (BWTBlockSize + 2) is not (0 or 1)).ToHashSet().Concat(skipped).Sort().ToHashSet();
		List<ShortIntervalList> result = new(input.Length);
		for (var i = 0; i < input.Length; i += BWTBlockSize + 2, Status[0]++)
		{
			if (input.Length - i < 3)
				throw new DecoderFallbackException();
			var length = Min(BWTBlockSize, input.Length - i - 2);
			var firstPermutation = (int)(input[i][0].Lower * input[i + 1][0].Base + input[i + 1][0].Lower);
			result.AddRange(input.GetSlice(i + 2, length).DecodeBWT2(hs, firstPermutation));
		}
		return result;
	}

	private static List<ShortIntervalList> DecodeBWT2(this Slice<ShortIntervalList> input, ListHashSet<int> hs, int firstPermutation)
	{
		var indexCodes = input.Convert(x => (int)x[0].Lower);
		var mtfMemory = hs.ToArray();
		for (var i = 0; i < indexCodes.Length; i++)
		{
			var index = hs.IndexOf(indexCodes[i]);
			indexCodes[i] = mtfMemory[index];
			Array.Copy(mtfMemory, 0, mtfMemory, 1, index);
			mtfMemory[0] = indexCodes[i];
		}
		var sorted = indexCodes.ToArray((elem, index) => (elem, index)).NSort(x => (uint)x.elem);
		var convert = sorted.ToArray(x => x.index);
		var result = RedStarLinq.EmptyList<ShortIntervalList>(indexCodes.Length);
		var it = firstPermutation;
		for (var i = 0; i < indexCodes.Length; i++)
		{
			it = convert[it];
			result[i] = new() { new((uint)indexCodes[it], input[i][0].Base) };
			input[i].GetSlice(1).ForEach(x => result[i].Add(x));
		}
		return result;
	}

	private static uint ReadCount(this ArithmeticDecoder ar, uint maxT = 31)
	{
		var temp = (int)ar.ReadEqual(maxT);
		var read = ar.ReadEqual((uint)1 << Max(temp, 1));
		return read + ((temp == 0) ? 0 : (uint)1 << Max(temp, 1));
	}
}
