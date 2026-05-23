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
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class Core : MelonMod
    {
        public static string basePath = Path.Combine(Directory.GetCurrentDirectory(), "UserData", "ChaoticFights");

        private CancellationTokenSource _discordPollCts;
        private bool _isDucked = false;

        public override void OnInitializeMelon()
        {
            InitPrefs();
            UI.Register((MelonBase)this, privateStuff);
        }
        public override void OnLateInitializeMelon()
        {
            Actions.onMatchStarted += () => Task.Run(() => OpenAndLoop(file2.Value, "Microsoft.Media.Player"));
            Actions.onMatchEnded += () => Task.Run(() => OpenAndLoop(file1.Value, "Microsoft.Media.Player"));

            _discordPollCts = new CancellationTokenSource();
            Task.Run(() => DiscordPollLoop(_discordPollCts.Token));
        }
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Gym" || sceneName == "Park")
            {
                Task.Run(() => OpenAndLoop(file1.Value, "Microsoft.Media.Player"));
            }
        }

        public override void OnApplicationQuit()
        {
            _discordPollCts?.Cancel();
        }

        private async Task DiscordPollLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                bool inCall = IsDiscordInCall();

                if (inCall && !_isDucked)
                {
                    SetMediaPlayerVolume(duckVolume.Value);
                    _isDucked = true;
                }
                else if (!inCall && _isDucked)
                {
                    SetMediaPlayerVolume(normalVolume.Value);
                    _isDucked = false;
                }

                await Task.Delay(5000, ct).ContinueWith(_ => { }, ct);
            }
        }

        private static bool IsDiscordInCall()
        {
            return DiscordRPC.IsInVoiceCall();
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static void SetMediaPlayerVolume(float percent)
        {
            var procs = Process.GetProcessesByName("Microsoft.Media.Player");
            if (procs.Length == 0) return;

            var pids = new HashSet<int>(procs.Select(p => p.Id));
            foreach (int pid in pids)
                VolumeMixer.SetApplicationVolume(pid, percent);
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        public void OpenAndLoop(string filePath, string processName)
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

            SetMediaPlayerVolume(_isDucked ? duckVolume.Value : normalVolume.Value);
        }
    }
    public static class DiscordRPC
    {
        private const string ClientId = "1058498348266553394";

        public static bool IsInVoiceCall()
        {
            for (int port = 6463; port <= 6472; port++)
            {
                try
                {
                    using var client = new System.Net.WebSockets.ClientWebSocket();
                    var uri = new Uri($"ws://127.0.0.1:{port}/?v=1&client_id={ClientId}");
                    client.ConnectAsync(uri, CancellationToken.None).Wait(2000);

                    if (client.State != System.Net.WebSockets.WebSocketState.Open)
                        continue;

                    // Read the READY event Discord sends on connect
                    var buffer = new byte[4096];
                    var result = client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (!result.Wait(2000)) continue;

                    string json = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Result.Count);

                    client.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait(1000);

                    // If voice_state exists in the READY payload, we're in a call
                    return json.Contains("\"voice_state\"") && !json.Contains("\"voice_state\":null");
                }
                catch { continue; }
            }
            return false;
        }
    }

    public static class VolumeMixer
    {
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public static void SetApplicationVolume(int pid, float level)
        {
            IMMDeviceEnumerator deviceEnumerator = null;
            try
            {
                deviceEnumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                IMMDevice speakers = deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia);
                IAudioSessionManager2 mgr = (IAudioSessionManager2)speakers.Activate(typeof(IAudioSessionManager2).GUID, 0, IntPtr.Zero);

                IAudioSessionEnumerator sessionEnumerator = mgr.GetSessionEnumerator();
                int count = sessionEnumerator.GetCount();

                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl2 session = (IAudioSessionControl2)sessionEnumerator.GetSession(i);
                    if (session.GetProcessId() == pid)
                    {
                        ISimpleAudioVolume volumeControl = session as ISimpleAudioVolume;
                        volumeControl?.SetMasterVolume(level / 100f, Guid.Empty);
                    }
                    Marshal.ReleaseComObject(session);
                }

                Marshal.ReleaseComObject(sessionEnumerator);
                Marshal.ReleaseComObject(mgr);
                Marshal.ReleaseComObject(speakers);
            }
            finally
            {
                if (deviceEnumerator != null) Marshal.ReleaseComObject(deviceEnumerator);
            }
        }

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"), ClassInterface(ClassInterfaceType.None)]
        private class MMDeviceEnumerator { }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int NotImpl1();
            IMMDevice GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [return: MarshalAs(UnmanagedType.IUnknown)]
            object Activate([MarshalAs(UnmanagedType.LPStruct)] Guid iid, int dwClsCtx, IntPtr pActivationParams);
        }

        [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionManager2
        {
            int NotImpl1();
            int NotImpl2();
            IAudioSessionEnumerator GetSessionEnumerator();
        }

        [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionEnumerator
        {
            int GetCount();
            IAudioSessionControl2 GetSession(int SessionCount);
        }

        [Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl2
        {
            int NotImpl1();
            int NotImpl2();
            int NotImpl3();
            int NotImpl4();
            int NotImpl5();
            int NotImpl6();
            int NotImpl7();
            int NotImpl8();
            int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
            int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
            int GetProcessId();
        }

        [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISimpleAudioVolume
        {
            int SetMasterVolume(float fLevel, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);
            int GetMasterVolume(out float pfLevel);
            int SetMute(bool bMute, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);
            int GetMute(out bool pbMute);
        }

        private enum EDataFlow { eRender, eCapture, eAll }
        private enum ERole { eConsole, eMultimedia, eCommunications }
    }

    public class UISetup
    {
        public static MelonPreferences_Category privateStuff;

        public static MelonPreferences_Entry<string> file1;
        public static MelonPreferences_Entry<string> file2;
        public static MelonPreferences_Entry<float> normalVolume;
        public static MelonPreferences_Entry<float> duckVolume;

        private const string USER_DATA = "UserData/ChaoticFights/";
        private const string CONFIG_FILE = "config.cfg";

        public static void InitPrefs()
        {
            privateStuff = MelonPreferences.CreateCategory("ChaoticFights_privateStuff", "music");

            if (!Directory.Exists(USER_DATA)) Directory.CreateDirectory(USER_DATA);

            privateStuff.SetFilePath(Path.Combine(USER_DATA, CONFIG_FILE));

            file1 = (MelonPreferences_Entry<string>)privateStuff.CreateEntry("file1", "circus.mp3", "safeMusic", false);
            file2 = (MelonPreferences_Entry<string>)privateStuff.CreateEntry("file2", "THEWORLDREVOLVING.mp3", "dangerMusic", false);
            normalVolume = (MelonPreferences_Entry<float>)privateStuff.CreateEntry("normalVolume", 35f, "normalVolume", false);
            duckVolume = (MelonPreferences_Entry<float>)privateStuff.CreateEntry("duckVolume", 10f, "duckVolume", false);
        }
    }
}