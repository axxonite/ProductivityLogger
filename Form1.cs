using System;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace ProductivityLog
{
    // todo add heartbeat data to the log to indicate whether the process was recording
    // todo record to which window the keys are directed
    public partial class MainFrm : Form
    {
        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const string binaryLogFilename = "log.log";
        private const string textLogFilename = "log.txt";

        const int HeartbeatCode = 65534;
        const int WindowSwitchCode = 65534;

        private static HashSet<Keys> NonDelimiters = new HashSet<Keys>
        {
            Keys.A,
            Keys.B,
            Keys.C,
            Keys.D,
            Keys.E,
            Keys.F,
            Keys.G,
            Keys.H,
            Keys.I,
            Keys.J,
            Keys.K,
            Keys.L,
            Keys.M,
            Keys.N,
            Keys.O,
            Keys.P,
            Keys.Q,
            Keys.R,
            Keys.S,
            Keys.T,
            Keys.U,
            Keys.V,
            Keys.W,
            Keys.X,
            Keys.Y,
            Keys.Z,
            Keys.D0,
            Keys.D1,
            Keys.D2,
            Keys.D3,
            Keys.D4,
            Keys.D5,
            Keys.D6,
            Keys.D7,
            Keys.D8,
            Keys.D9,
            Keys.NumPad0,
            Keys.NumPad1,
            Keys.NumPad2,
            Keys.NumPad3,
            Keys.NumPad4,
            Keys.NumPad5,
            Keys.NumPad6,
            Keys.NumPad7,
            Keys.NumPad8,
            Keys.NumPad9,
        };

        public MainFrm()
        {
            mainFrm = this;
            InitializeComponent();
            heartbeatTimer.Elapsed += ( sender, e ) => Heartbeat();
            activeWindowTimer.Elapsed += (sender, e) => LogActiveWindow();
        }

        // -------------------------------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------------------------------

        private void MainFrm_Load(object sender, EventArgs e)
        {
            CountKeystrokesFromPreviousLog();
            binaryLog = new FileStream(binaryLogFilename, FileMode.Append);
            binaryLogWriter = new BinaryWriter(binaryLog);
            textLogWriter = File.AppendText(textLogFilename);
            textLogWriter.AutoFlush = true;
            hookProcDelegate = HookCallback;
            hookId = SetHook();
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e) => Close();

        private void MainFrm_FormClosed(object sender, FormClosedEventArgs e) => UnhookWindowsHookEx(hookId);

        private void CountKeystroke(Keys key)
        {
            keyStrokes++;
            bool isChar = IsCharacter(key);
            if (!isChar && wasCharacter)
                words++;
            wasCharacter = isChar;
        }

        void CountKeystrokesFromPreviousLog()
        {
            if (!File.Exists(binaryLogFilename))
                return;
            var prevLog = new FileStream(binaryLogFilename, FileMode.Open);
            try
            {
                using (var binaryLogReader = new BinaryReader(prevLog))
                {
                    while ( true )
                    {
                        var timestamp = binaryLogReader.ReadUInt32();
                        uint value = binaryLogReader.ReadUInt32();
                        if (!IsOpCode(value))
                        {
                            Keys key = (Keys)binaryLogReader.ReadUInt32();
                            if (IntToDateTime(timestamp).Date == DateTime.Today)
                                CountKeystroke(key);
                        }
                        else if (value == WindowSwitchCode)
                        {
                            binaryLogReader.ReadString(); // window title
                            binaryLogReader.ReadString(); // process name
                        }
                    }
                 }
            }
            catch (EndOfStreamException e)
            {
            }
            finally
            {
                prevLog.Close();
                UpdateIconText();
            }
        }


        private static bool IsCharacter(Keys key) => NonDelimiters.Contains(key);

        private static bool IsOpCode(UInt32 code) => code == HeartbeatCode || code == WindowSwitchCode;

        private static bool ShouldLokKeyToTextLog(Keys key) => IsCharacter(key) || key == Keys.Space || key == Keys.Enter;

        private static char ToCharacter(Keys key)
        {
            if (key >= Keys.A && key <= Keys.Z)
                return (char)('a' + (key - Keys.A));
            if (key >= Keys.D0 && key <= Keys.D9)
                return (char)('0' + (key - Keys.D0));
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
                return (char)('0' + (key - Keys.NumPad0));
            if (key == Keys.Space)
                return ' ';
            if (key == Keys.Enter)
                return '\n';
            return (char)0;
        }

        void UpdateIconText() => notifyIcon.Text = keyStrokes + " keystrokes, " + words + " words";

        // todo make this into an extension method.
        private static UInt32 DateTimeToInt(DateTime dateTime) =>  (uint)(dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        
        private static DateTime IntToDateTime(UInt32 value) => LogFileBaseTime.AddSeconds(value);
        
        private void KeyPressed(Keys key)
        {
            lock (binaryLogWriter)
            {
                binaryLogWriter.Write(DateTimeToInt(DateTime.Now));
                binaryLogWriter.Write((UInt32)key);
                binaryLogWriter.Flush();
            }
            CountKeystroke(key);
            if (ShouldLokKeyToTextLog(key))
                textLogWriter.Write(ToCharacter(key));
            UpdateIconText();
        }


        private static IntPtr SetHook() => SetWindowsHookEx(WH_KEYBOARD_LL, hookProcDelegate, GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
                mainFrm.KeyPressed((Keys)Marshal.ReadInt32(lParam));
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        void Heartbeat()
        {
            lock (binaryLogWriter)
            {
                binaryLogWriter.Write(DateTimeToInt(DateTime.Now));
                binaryLogWriter.Write(HeartbeatCode);
                binaryLogWriter.Flush();
            }
        }

        void LogActiveWindow()
        {
            lock (activeWindowTimer)
            {
                IntPtr handle = GetForegroundWindow();
                StringBuilder windowTitleBuilder = new StringBuilder(512);
                GetWindowText(handle, windowTitleBuilder, 512);
                string windowTitle = windowTitleBuilder.ToString();
                if (windowTitle != lastWindowTitle)
                {
                    var matchingProcess = processList.FirstOrDefault((Process process) => process.MainWindowHandle == handle);
                    if (matchingProcess == null)
                        processList = Process.GetProcesses();
                    matchingProcess = processList.FirstOrDefault((Process process) => process.MainWindowHandle == handle);
                    lastWindowTitle = windowTitle;
                    lock (binaryLogWriter)
                    {
                        binaryLogWriter.Write(DateTimeToInt(DateTime.Now));
                        binaryLogWriter.Write(WindowSwitchCode);
                        binaryLogWriter.Write(windowTitle);
                        binaryLogWriter.Write(matchingProcess?.ProcessName ?? "");
                        binaryLogWriter.Flush();
                    }
                }
            }
        }

        static DateTime LogFileBaseTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static HookProc hookProcDelegate;
        private static IntPtr hookId = IntPtr.Zero;
        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        private FileStream binaryLog;
        private StreamWriter textLogWriter;
        private BinaryWriter binaryLogWriter;
        private static MainFrm mainFrm;
        private int words;
        private int keyStrokes;
        private bool wasCharacter;
        private System.Timers.Timer heartbeatTimer = new System.Timers.Timer() { Interval = 60000, Enabled = true };
        private System.Timers.Timer activeWindowTimer = new System.Timers.Timer() { Interval = 1000, Enabled = true };
        private Process[] processList = new Process[0];
        private string lastWindowTitle;
    }
}
