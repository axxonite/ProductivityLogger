using System;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace ProductivityLog
{
    public partial class MainFrm : Form
    {
        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const string binaryLogFilename = "log.log";
        private const string textLogFilename = "log.txt";

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
        }

        // todo save context so that the app can be restarted without losing the running count for the day.
        private void MainFrm_Load(object sender, EventArgs e)
        {
            CountCharactersFromPreviousLog();
            binaryLog = new FileStream(binaryLogFilename, FileMode.Append);
            binaryLogWriter = new BinaryWriter(binaryLog);
            textLogWriter = File.AppendText(textLogFilename);
            textLogWriter.AutoFlush = true;
            hookId = SetHook(HookCallback);
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void CountCharacter(Keys key)
        {
            keyStrokes++;
            bool isChar = IsCharacter(key);
            if (!isChar && wasCharacter)
                words++;
            wasCharacter = isChar;
        }

        void CountCharactersFromPreviousLog()
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
                        Keys key = (Keys)binaryLogReader.ReadUInt32();
                        if (IntToDateTime(timestamp).Date == DateTime.Today)
                            CountCharacter(key);
                    }
                 }
            }
            catch (EndOfStreamException e)
            {
            }
            finally
            {
                prevLog.Close();
                UpdateUI();
            }
        }


        private static bool IsCharacter(Keys key)
        {
            return NonDelimiters.Contains(key);
        }

        private static bool IsLogToTextLog(Keys key)
        {
            return IsCharacter(key) || key == Keys.Space || key == Keys.Enter;
        }

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

        void UpdateUI()
        {
            notifyIcon.Text = keyStrokes + " keystrokes, " + words + " words";
        }

        // todo make this into an extension method.
        private static UInt32 DateTimeToInt(DateTime dateTime)
        {
            return (UInt32)(dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
        
        private static DateTime IntToDateTime(UInt32 value)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(value);
        }
        
        private void KeyPressed(Keys key)
        {
            binaryLogWriter.Write(DateTimeToInt(DateTime.Now));
            binaryLogWriter.Write((UInt32)key);
            binaryLogWriter.Flush();
            CountCharacter(key);
            if (IsLogToTextLog(key))
                textLogWriter.Write(ToCharacter(key));
            UpdateUI();
        }

        private void MainFrm_FormClosed(object sender, FormClosedEventArgs e)
        {
            UnhookWindowsHookEx(hookId);
        }

        private static IntPtr SetHook(HookProc hookProc)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
                mainFrm.KeyPressed((Keys)Marshal.ReadInt32(lParam));
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static IntPtr hookId = IntPtr.Zero;

        private FileStream binaryLog;
        private StreamWriter textLogWriter;
        private BinaryWriter binaryLogWriter;
        private static MainFrm mainFrm;
        private int words;
        private int keyStrokes;
        private bool wasCharacter;
    }

}
