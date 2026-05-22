using MelonLoader;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UIFramework;
using RumbleModdingAPI.RMAPI;
using static ChaoticFights.UISetup;

[assembly: MelonInfo(typeof(ChaoticFights.Core), "ChaoticFights", "1.0.0", "bord4life", null)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]
[assembly: MelonAdditionalDependencies("UIFramework")]

namespace ChaoticFights
{
    public class Core : MelonMod
    {
        public static string basePath = Path.Combine(Directory.GetCurrentDirectory(), "UserData", "ChaoticFights");

        public override void OnInitializeMelon()
        {
            InitPrefs();
            UI.Register((MelonBase)this, privateStuff);
        }
        public override void OnLateInitializeMelon()
        {
            Actions.onMatchStarted += () => Task.Run(() => OpenAndLoop("C:\\Program Files\\WindowsApps\\Microsoft.ZuneMusic_11.2604.9.0_x64__8wekyb3d8bbwe\\Microsoft.Media.Player.exe", file2.Value, "Microsoft.Media.Player"));
            Actions.onMatchEnded += () => Task.Run(() => OpenAndLoop("C:\\Program Files\\WindowsApps\\Microsoft.ZuneMusic_11.2604.9.0_x64__8wekyb3d8bbwe\\Microsoft.Media.Player.exe", file1.Value, "Microsoft.Media.Player"));
        }
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Gym" || sceneName == "Park")
            {
                Task.Run(() => OpenAndLoop("C:\\Program Files\\WindowsApps\\Microsoft.ZuneMusic_11.2604.9.0_x64__8wekyb3d8bbwe\\Microsoft.Media.Player.exe", file1.Value, "Microsoft.Media.Player"));
            }
        }
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        public void OpenAndLoop(string exePath, string filePath, string processName)
        {
            // Close if already open
            foreach (var p in Process.GetProcessesByName(processName))
            {
                p.Kill();
                p.WaitForExit();
            }

            IntPtr gameWindow = GetForegroundWindow();

            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(basePath, filePath),
                UseShellExecute = true
            });

            Process mediaProcess = null;
            for (int i = 0; i < 20; i++)
            {
                Thread.Sleep(500);
                var procs = Process.GetProcessesByName(processName);

                if (procs.Length > 0)
                {
                    mediaProcess = procs[0];
                    break;
                }
            }

            // Wait for the window to actually be ready
            Thread.Sleep(3000);

            if (mediaProcess == null)
            {
                return;
            }

            SetForegroundWindow(mediaProcess.MainWindowHandle);
            Thread.Sleep(200);

            keybd_event((byte)0x11, 0, 0, 0);
            keybd_event((byte)0x54, 0, 0, 0);        // T down
            keybd_event((byte)0x54, 0, 0x0002, 0);   // T up
            keybd_event((byte)0x11, 0, 0x0002, 0);

            Thread.Sleep(100);
            SetForegroundWindow(gameWindow);
        }
    }

    public class UISetup
    {
        public static MelonPreferences_Category privateStuff;

        public static MelonPreferences_Entry<string> file1;
        public static MelonPreferences_Entry<string> file2;

        private const string USER_DATA = "UserData/ChaoticFights/";
        private const string CONFIG_FILE = "config.cfg";

        public static void InitPrefs()
        {
            privateStuff = MelonPreferences.CreateCategory("ChaoticFights_privateStuff", "music");

            if (!Directory.Exists(USER_DATA)) Directory.CreateDirectory(USER_DATA);

            privateStuff.SetFilePath(Path.Combine(USER_DATA, CONFIG_FILE));

            file1 = (MelonPreferences_Entry<string>)privateStuff.CreateEntry("file1", "circus.mp3", "safeMusic", false);
            file2 = (MelonPreferences_Entry<string>)privateStuff.CreateEntry("file2", "THEWORLDREVOLVING.mp3", "dangerMusic", false);
        }
    }
}