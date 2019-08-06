using System;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
        private const string logFileName = "log.txt";

        public MainFrm()
        {
            mainFrm = this;
            InitializeComponent();
        }

        private void MainFrm_Load(object sender, EventArgs e)
        {
            logFile = File.AppendText(logFileName);
            logFile.AutoFlush = true;
            hookId = SetHook(HookCallback);
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private static IntPtr SetHook(HookProc hookProc)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);
        }

        private static bool IsCharacter(Keys key)
        {
            return key.ToString().Length == 1;
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                Keys key = (Keys)Marshal.ReadInt32(lParam);
                logFile.WriteLine(key);
                keyStrokes++;
                bool isChar = IsCharacter(key);
                if (!isChar && wasCharacter)
                    words++;
                wasCharacter = isChar;
                mainFrm.notifyIcon.Text = keyStrokes + " keystrokes, " + words + " words";
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private void MainFrm_FormClosed(object sender, FormClosedEventArgs e)
        {
            UnhookWindowsHookEx(hookId);
        }

        private static IntPtr hookId = IntPtr.Zero;
        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static StreamWriter logFile;

        private static int words;
        private static int keyStrokes;
        private static MainFrm mainFrm;
        private static bool wasCharacter;
    }
}
