// CtrlV 存图 (CtrlV Paste Image)
// Copyright (C) 2026 xqmake7
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

class App : Form
{
    const int WH_KEYBOARD_LL = 13;
    const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104;
    const int WM_CLIPBOARDUPDATE = 0x031D;
    const int VK_V = 0x56, VK_CONTROL = 0x11, VK_MENU = 0x12;
    const int SHCNE_CREATE = 0x2;
    const uint SHCNF_PATHW = 0x5, SHCNF_FLUSHNOWAIT = 0x3000;
    const uint GA_ROOT = 2;

    [StructLayout(LayoutKind.Sequential)]
    struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

    struct RECT { public int Left, Top, Right, Bottom; }

    struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int idHook, HookProc proc, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")]
    static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]
    static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetClassName(IntPtr hWnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")]
    static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);
    [DllImport("user32.dll")]
    static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);
    [DllImport("user32.dll")]
    static extern IntPtr GetParent(IntPtr hWnd);
    [DllImport("user32.dll")]
    static extern bool AddClipboardFormatListener(IntPtr hWnd);
    [DllImport("user32.dll")]
    static extern bool RemoveClipboardFormatListener(IntPtr hWnd);
    [DllImport("user32.dll")]
    static extern bool MessageBeep(uint type);
    [DllImport("kernel32.dll")]
    static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")]
    static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern void SHChangeNotify(int eventId, uint flags, string item, IntPtr item2);

    static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CtrlV存图");
    static readonly string ConfigFile = Path.Combine(ConfigDir, "settings.ini");

    string prefix = "截图";
    bool beep = true;
    double minInterval = 0.6;

    HookProc hookProc;
    IntPtr hook = IntPtr.Zero;
    bool vDown = false;
    DateTime lastSave = DateTime.MinValue;

    readonly object clipLock = new object();
    byte[] cachedPng = null;
    string cachedText = null;
    string cachedTextExt = "txt";

    System.Windows.Forms.Timer cacheTimer;
    NotifyIcon tray;
    ContextMenuStrip menu;
    ToolStripMenuItem autoItem;

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "CtrlV存图";

    [STAThread]
    static void Main()
    {
        bool created;
        using (Mutex mutex = new Mutex(true, "CtrlVPasteImage_CS_B1", out created))
        {
            if (!created) return;
            try
            {
                if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware();
            }
            catch
            {
                try { SetProcessDPIAware(); }
                catch { }
            }
            Application.EnableVisualStyles();
            Application.Run(new App());
        }
    }

    App()
    {
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        LoadConfig();
        cacheTimer = new System.Windows.Forms.Timer();
        cacheTimer.Interval = 150;
        cacheTimer.Tick += delegate { cacheTimer.Stop(); CacheClipboard(); };
        IntPtr force = Handle;
    }

    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(false);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        HealAutoStart();
        BuildTray();
        AddClipboardFormatListener(Handle);
        hookProc = new HookProc(HookCallback);
        hook = SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, GetModuleHandle(null), 0);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        cacheTimer.Stop();
        if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
        RemoveClipboardFormatListener(Handle);
        if (tray != null) tray.Visible = false;
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE)
        {
            cacheTimer.Stop();
            cacheTimer.Start();
        }
        base.WndProc(ref m);
    }

    void BuildTray()
    {
        Icon icon = null;
        try { icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { }
        if (icon == null) icon = SystemIcons.Application;
        autoItem = new ToolStripMenuItem("开机自启");
        autoItem.CheckOnClick = true;
        autoItem.Checked = IsAutoStart();
        autoItem.Click += delegate { SetAutoStart(autoItem.Checked); };
        menu = new ContextMenuStrip();
        menu.Items.Add(autoItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, delegate { Close(); });
        menu.Opening += delegate { autoItem.Checked = IsAutoStart(); };
        tray = new NotifyIcon();
        tray.Icon = icon;
        tray.Text = "CtrlV 存图";
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += delegate { OpenConfig(); };
        tray.Visible = true;
    }

    static bool IsAutoStart()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
            {
                return key != null && key.GetValue(RunName) != null;
            }
        }
        catch { return false; }
    }

    static void HealAutoStart()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key == null) return;
                object value = key.GetValue(RunName);
                if (value == null) return;
                string current = "\"" + Application.ExecutablePath + "\"";
                if (!string.Equals(value.ToString(), current, StringComparison.OrdinalIgnoreCase))
                    key.SetValue(RunName, current);
            }
        }
        catch { }
    }

    static void SetAutoStart(bool on)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key == null) return;
                if (on) key.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                else key.DeleteValue(RunName, false);
            }
        }
        catch { }
    }

    void OpenConfig()
    {
        try
        {
            if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
            Process.Start(ConfigDir);
        }
        catch { }
    }

    IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == 0)
        {
            KBDLLHOOKSTRUCT kb = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
            if (kb.vkCode == VK_V)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    if (!vDown)
                    {
                        vDown = true;
                        bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                        bool alt = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
                        if (ctrl && !alt) MaybeSave();
                    }
                }
                else if (msg == WM_KEYUP)
                {
                    vDown = false;
                }
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    void MaybeSave()
    {
        if ((DateTime.UtcNow - lastSave).TotalSeconds < minInterval) return;
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero || (!IsExplorer(hwnd) && !IsDesktop(hwnd))) return;
        if (IsEditingText(hwnd)) return;
        lastSave = DateTime.UtcNow;
        BeginInvoke((MethodInvoker)delegate { DoSave(hwnd); });
    }

    static string ClassName(IntPtr hwnd)
    {
        StringBuilder sb = new StringBuilder(64);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    static bool IsExplorer(IntPtr hwnd)
    {
        string cls = ClassName(hwnd);
        return cls == "CabinetWClass" || cls == "ExploreWClass";
    }

    static bool IsDesktop(IntPtr hwnd)
    {
        string cls = ClassName(GetAncestor(hwnd, GA_ROOT));
        return cls == "Progman" || cls == "WorkerW";
    }

    static bool IsEditingText(IntPtr fg)
    {
        try
        {
            uint tid = GetWindowThreadProcessId(fg, IntPtr.Zero);
            GUITHREADINFO gti = new GUITHREADINFO();
            gti.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
            if (!GetGUIThreadInfo(tid, ref gti)) return false;
            IntPtr focus = gti.hwndFocus;
            for (int i = 0; i < 6 && focus != IntPtr.Zero; i++)
            {
                string cls = ClassName(focus);
                if (cls == "Edit" || cls == "ComboBox" || cls == "msctls_edit") return true;
                if (cls.IndexOf("RichEdit", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                focus = GetParent(focus);
            }
        }
        catch { }
        return false;
    }

    static string DesktopFolder()
    {
        try
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            return Directory.Exists(path) ? path : null;
        }
        catch { return null; }
    }

    static string TargetFolder(IntPtr hwnd)
    {
        if (IsDesktop(hwnd)) return DesktopFolder();
        return ExplorerPath(hwnd);
    }

    void CacheClipboard()
    {
        try
        {
            lock (clipLock)
            {
                cachedPng = null;
                cachedText = null;
                cachedTextExt = "txt";
                if (Clipboard.ContainsFileDropList()) return;
                string raw = CaptureText();
                if (raw != null && !LooksLikeSvg(raw))
                {
                    cachedText = raw;
                    return;
                }
                cachedPng = CapturePngFormat();
                if (cachedPng != null) return;
                cachedPng = CaptureDib();
                if (cachedPng != null) return;
                cachedPng = CaptureVector();
                if (cachedPng != null) return;
                if (raw != null)
                {
                    cachedText = raw;
                    cachedTextExt = "svg";
                }
            }
        }
        catch { }
    }

    byte[] CapturePngFormat()
    {
        try
        {
            if (Clipboard.ContainsData("PNG"))
            {
                object data = Clipboard.GetData("PNG");
                Stream stream = data as Stream;
                if (stream != null)
                {
                    using (stream)
                    using (MemoryStream ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
                byte[] raw = data as byte[];
                if (raw != null) return raw;
            }
        }
        catch { }
        return null;
    }

    byte[] CaptureDib()
    {
        try
        {
            if (Clipboard.ContainsImage())
            {
                using (Image img = Clipboard.GetImage())
                {
                    if (img != null)
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            img.Save(ms, ImageFormat.Png);
                            return ms.ToArray();
                        }
                    }
                }
            }
        }
        catch { }
        return null;
    }

    byte[] CaptureVector()
    {
        try
        {
            IDataObject data = Clipboard.GetDataObject();
            if (data == null) return null;
            object meta = null;
            foreach (string fmt in data.GetFormats(false))
            {
                string f = fmt.ToLowerInvariant();
                if (f == "enhancedmetafile" || f == "metafilepict")
                {
                    meta = data.GetData(fmt, true);
                    break;
                }
            }
            if (meta == null) return null;
            Metafile mf = meta as Metafile;
            Stream stream = meta as Stream;
            if (mf == null && stream != null) mf = new Metafile(stream);
            if (mf == null) return null;
            using (mf)
            {
                Size sz = mf.Size;
                if (sz.Width <= 0 || sz.Height <= 0) return null;
                const int Max = 6000;
                double scale = Math.Min(1.0, Math.Min((double)Max / sz.Width, (double)Max / sz.Height));
                int w = Math.Max(1, (int)(sz.Width * scale));
                int h = Math.Max(1, (int)(sz.Height * scale));
                using (Bitmap bmp = new Bitmap(w, h))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.DrawImage(mf, 0, 0, w, h);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Png);
                        return ms.ToArray();
                    }
                }
            }
        }
        catch { return null; }
    }

    string CaptureText()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText();
                if (text != null && text.Trim().Length > 0) return text;
            }
        }
        catch { }
        return null;
    }

    static bool LooksLikeSvg(string text)
    {
        return text.ToLowerInvariant().IndexOf("<svg") >= 0;
    }

    void DoSave(IntPtr hwnd)
    {
        byte[] png;
        string text;
        string ext;
        lock (clipLock) { png = cachedPng; text = cachedText; ext = cachedTextExt; }
        if (png == null && text == null)
        {
            if (Clipboard.ContainsFileDropList()) return;
            string raw = CaptureText();
            if (raw != null && !LooksLikeSvg(raw))
            {
                text = raw;
                ext = "txt";
            }
            else
            {
                png = CapturePngFormat();
                if (png == null) png = CaptureDib();
                if (png == null) png = CaptureVector();
                if (png == null && raw != null)
                {
                    text = raw;
                    ext = "svg";
                }
            }
        }
        if (png == null && text == null) return;
        string folder = TargetFolder(hwnd);
        if (folder == null) return;
        string name;
        if (png != null) name = UniqueName(folder);
        else if (ext == "svg") name = SvgName(folder);
        else name = TextName(folder, text);
        string full = Path.Combine(folder, name);
        try
        {
            if (png != null) File.WriteAllBytes(full, png);
            else File.WriteAllText(full, text, Encoding.UTF8);
        }
        catch { return; }
        SHChangeNotify(SHCNE_CREATE, SHCNF_PATHW | SHCNF_FLUSHNOWAIT, full, IntPtr.Zero);
        if (beep) MessageBeep(0);
    }

    string UniqueName(string folder)
    {
        string baseName = prefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string name = baseName + ".png";
        int i = 1;
        while (File.Exists(Path.Combine(folder, name)))
        {
            name = baseName + "_" + i + ".png";
            i++;
        }
        return name;
    }

    string SvgName(string folder)
    {
        string baseName = prefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string name = baseName + ".svg";
        int i = 1;
        while (File.Exists(Path.Combine(folder, name)))
        {
            name = baseName + "_" + i + ".svg";
            i++;
        }
        return name;
    }

    string TextName(string folder, string text)
    {
        string baseName = Sanitize(text);
        if (baseName.Length == 0)
            baseName = prefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        if (baseName.Length > 80) baseName = baseName.Substring(0, 80).Trim();
        string name = baseName + ".txt";
        int i = 1;
        while (File.Exists(Path.Combine(folder, name)))
        {
            name = baseName + "_" + i + ".txt";
            i++;
        }
        return name;
    }

    static readonly string[] Reserved = {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    static string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        StringBuilder sb = new StringBuilder();
        foreach (char c in text)
        {
            if (c == '\r' || c == '\n' || c == '\t' || c == ' ')
            {
                if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
            }
            else if (c < 32)
            {
            }
            else if ("<>:\"/\\|?*".IndexOf(c) >= 0)
            {
                sb.Append('_');
            }
            else
            {
                sb.Append(c);
            }
            if (sb.Length >= 120) break;
        }
        string s = sb.ToString().Trim(' ', '.');
        string upper = s.ToUpperInvariant();
        for (int i = 0; i < Reserved.Length; i++)
        {
            if (upper == Reserved[i]) { s = "_" + s; break; }
        }
        return s;
    }

    static string ExplorerPath(IntPtr target)
    {
        object shell = null;
        object wins = null;
        try
        {
            Type shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return null;
            shell = Activator.CreateInstance(shellType);
            wins = shellType.InvokeMember("Windows", BindingFlags.InvokeMethod, null, shell, null);
            Type winsType = wins.GetType();
            int count = Convert.ToInt32(winsType.InvokeMember("Count", BindingFlags.GetProperty, null, wins, null));
            for (int i = 0; i < count; i++)
            {
                object win = null;
                try
                {
                    win = winsType.InvokeMember("Item", BindingFlags.InvokeMethod, null, wins, new object[] { i });
                    object hwnd = win.GetType().InvokeMember("HWND", BindingFlags.GetProperty, null, win, null);
                    if (Convert.ToInt32(hwnd) != target.ToInt32()) continue;
                    object doc = win.GetType().InvokeMember("Document", BindingFlags.GetProperty, null, win, null);
                    object folder = doc.GetType().InvokeMember("Folder", BindingFlags.GetProperty, null, doc, null);
                    object self = folder.GetType().InvokeMember("Self", BindingFlags.GetProperty, null, folder, null);
                    string path = (string)self.GetType().InvokeMember("Path", BindingFlags.GetProperty, null, self, null);
                    if (path != null && Directory.Exists(path)) return path;
                }
                catch { }
                finally { Release(win); }
            }
        }
        catch { }
        finally
        {
            Release(wins);
            Release(shell);
        }
        return null;
    }

    static void Release(object o)
    {
        try { if (o != null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); }
        catch { }
    }

    void LoadConfig()
    {
        try
        {
            if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
            if (!File.Exists(ConfigFile))
            {
                File.WriteAllText(ConfigFile, "prefix=截图\r\nbeep=1\r\nmin_interval=0.6\r\n", Encoding.UTF8);
                return;
            }
            foreach (string raw in File.ReadAllLines(ConfigFile, Encoding.UTF8))
            {
                int eq = raw.IndexOf('=');
                if (eq < 0) continue;
                string key = raw.Substring(0, eq).Trim();
                string val = raw.Substring(eq + 1).Trim();
                if (key == "prefix" && val.Length > 0) prefix = val;
                else if (key == "beep") beep = (val == "1" || val.ToLower() == "true");
                else if (key == "min_interval")
                {
                    double d;
                    if (double.TryParse(val, out d)) minInterval = d;
                }
            }
        }
        catch { }
    }
}
