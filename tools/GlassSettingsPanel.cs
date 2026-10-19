using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// Only glass settings are written; no FG settings,
// key handling and diagnostics stay in the unchanged official DLL.
internal sealed class GlassSettingsPanel : UserControl
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern uint GetPrivateProfileInt(string section, string key, int fallback, string path);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern uint GetPrivateProfileString(string section, string key, string fallback, StringBuilder value, int size, string path);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool WritePrivateProfileString(string section, string key, string value, string path);
    readonly System.Collections.Generic.List<Panel> pages = new System.Collections.Generic.List<Panel>();
    readonly System.Collections.Generic.List<Button> tabs = new System.Collections.Generic.List<Button>();
    int selectedIndex;
    internal int SelectedIndex {
        get { return selectedIndex; }
        set {
            selectedIndex=value;
            for(int i=0;i<pages.Count;i++) { pages[i].Visible=i==value; WsgTheme.SelectButton(tabs[i],i==value); }
        }
    }
    readonly Func<string> ini;
    readonly Dictionary<string, NumericUpDown> values = new Dictionary<string, NumericUpDown>();
    readonly Dictionary<string, CheckBox> switches = new Dictionary<string, CheckBox>();
    readonly CheckBox extra = new WsgCheckBox { Text = "Extra (up to 100)", AutoSize = true };
    readonly CheckBox dark = new WsgCheckBox { Text = "Dark screen fix", AutoSize = true };
    readonly CheckBox seam = new WsgCheckBox { Text = "UV seam fix", AutoSize = true };
    readonly CheckBox flip = new WsgCheckBox { Text = "Flip jitter direction", AutoSize = true };
    readonly ToolTip tips = new ToolTip { AutoPopDelay = 20000 };
    readonly Button windowShortcut = new WsgButton { AccessibleName="Change settings window shortcut" };
    bool refreshing;
    readonly Button resetGlass, resetAdvanced, resetExperimental;

    internal GlassSettingsPanel(Func<string> settingsPath)
    {
        BackColor=WsgTheme.Background; Font=new Font("Segoe UI",10);
        ini = settingsPath;
        var basic = Page("Glass settings"); var advanced = Page("Advanced"); var experimentalPage = Page("Experimental");
        var experimental=new Panel { Left=0,Top=0,Width=624,Height=177,AutoScroll=true,TabIndex=0,TabStop=false,BackColor=WsgTheme.Panel };
        experimentalPage.Controls.Add(experimental);
        Number(basic, "Line smoothing", "line_smoothing", 50, 35, 16,
            "0 is sharpest, 35 is the author's default, 50 is DLSS-like. Extra allows softer smoothing up to 100.");
        extra.SetBounds(438, 18, 160, 28); basic.Controls.Add(extra);
        tips.SetToolTip(extra, "Above 50 softens the whole display beyond normal DLSS. Default off.");
        extra.CheckedChanged += delegate {
            if (refreshing) return;
            if (!extra.Checked && values["line_smoothing"].Value > 50) values["line_smoothing"].Value = 50;
            values["line_smoothing"].Maximum = extra.Checked ? 100 : 50;
            Save("extra_smoothing", extra.Checked ? 1 : 0);
        };
        basic.Controls.Add(new WsgLabel { Text="Settings window shortcut", Left=16,Top=64,Width=212,Height=26 });
        windowShortcut.SetBounds(235,58,373,32); basic.Controls.Add(windowShortcut);
        tips.SetToolTip(windowShortcut,"Click, press your shortcut, then save. Use it in MSFS to open the original settings window and its green-tint button.");
        windowShortcut.Click += delegate {
            using(var recorder=new ShortcutRecorder(ReadWindowShortcut())) {
                if(recorder.ShowDialog(this)!=DialogResult.OK)return;
                try { SaveWindowShortcut(recorder.Shortcut); }
                catch(Exception e) { ParentDialogPosition.Show(this,e.Message,"Settings window shortcut",MessageBoxButtons.OK,MessageBoxIcon.Information); }
            }
        };
        Label(basic, "Click the shortcut to change it.", 104, 26);
        resetGlass = Reset(basic, "Reset glass settings", 185, delegate { extra.Checked = false; values["line_smoothing"].Value = 35; });
        flip.SetBounds(16, 19, 400, 28); advanced.Controls.Add(flip);
        tips.SetToolTip(flip, "Leave off unless troubleshooting jitter alignment with the mod author.");
        Number(advanced, "Glass layer strength (%)", "glass_strength", 100, 100, 60,
            "100 preserves the simulator's full glare, dirt and reflections. Lower values reduce this layer.");
        Label(advanced, "Troubleshooting settings — leave at defaults unless needed.", 108, 44);
        resetAdvanced = Reset(advanced, "Reset advanced", 185, delegate { flip.Checked = false; values["glass_strength"].Value = 100; });
        dark.SetBounds(16, 6, 220, 26); seam.SetBounds(286, 6, 220, 26); experimental.Controls.Add(dark); experimental.Controls.Add(seam);
        tips.SetToolTip(dark, "Helps mostly black screens in strong sun, but can interfere with other displays. Default off. Official v1.0.1 has a known brief glare error when this is enabled.");
        tips.SetToolTip(seam, "Removes lines at texture seams within a display, such as E-Jet bezel bands. Default on.");
        dark.CheckedChanged += delegate { if (!refreshing) Save("dark_screen_fix", dark.Checked ? 1 : 0); };
        seam.CheckedChanged += delegate { if (!refreshing) Save("seam_fix", seam.Checked ? 1 : 0); };
        Number(experimental, "Detail clamp (%)", "detail_clamp", 100, 0, 38,
            "Limits detail to nearby simulator brightness. May hide dashed bezel lines, dull small text or flicker.");
        Number(experimental, "Edge fade (100 = 1 pixel)", "edge_fade", 250, 0, 72,
            "Blends edges into the simulator image. Higher values can flicker; measured in render pixels.");
        Number(experimental, "Angle softening (%)", "angle_softening", 100, 0, 106,
            "Softens screens viewed at steep angles; straight-on screens are unchanged.");
        Number(experimental, "Edge stretcher (100 = 1 pixel)", "edge_stretch", 400, 0, 140,
            "Extends the screen image towards its bezel. Measured in render pixels.");
        experimental.AutoScroll = true;
        Switch(experimental, "Temporal anti-aliasing", "temporal_aa", false, 178,
            "Smooths moving screen details using previous frames. Uses additional VRAM at output resolution; default off. Cannot combine with soft smoothing.");
        Switch(experimental, "Soft-edged line smoothing", "soft_smoothing", false, 210,
            "Uses a softer Gaussian filter. Default off; cannot combine with temporal anti-aliasing.");
        Switch(experimental, "Stable screen edges", "stable_edges", true, 242,
            "Stabilizes the screen boundaries. Author default on.");
        Switch(experimental, "Full-size texture sampling", "full_size_sampling", true, 274,
            "Samples the full-resolution display texture. Off uses the game's mip levels. Author default on.");
        Switch(experimental, "Calm glass at symbols", "calm_glass", false, 306,
            "Reduces glass detail from the upscaler beside symbols. Author default off.");
        Switch(experimental, "Occlusion check", "occlusion_check", true, 338,
            "Uses depth to leave objects in front of a screen to the upscaler. Author default on.");
        Number(experimental, "Edge trim (100 = 1 pixel)", "edge_trim", 400, 0, 374,
            "Leaves the outer screen edge to the upscaler. Reverse of edge stretcher; default 0.");
        resetExperimental = Reset(experimentalPage, "Reset experimental", 185, delegate {
            dark.Checked = false; seam.Checked = true;
            foreach (string key in new[] { "detail_clamp", "edge_fade", "angle_softening", "edge_stretch", "edge_trim" }) values[key].Value = 0;
            foreach (var item in switches) item.Value.Checked = (bool)item.Value.Tag;
        });
        flip.CheckedChanged += delegate { if (!refreshing) Save("jitter_positive", flip.Checked ? 0 : 1); };
        foreach(var page in pages) WsgTheme.OrderControls(page);
        foreach(var item in values) item.Value.AccessibleDescription=tips.GetToolTip(item.Value);
    }

    Panel Page(string name) {
        int index=pages.Count;
        var button=new WsgButton { Text=name, Left=index*210, Top=0, Width=204, Height=34, TabIndex=index };
        WsgTheme.StyleButton(button,false);
        button.Click += delegate { SelectedIndex=index; };
        var page=new Panel { Left=0,Top=44,Width=624,Height=226,BackColor=WsgTheme.Panel,TabIndex=3+index,Visible=index==0 };
        pages.Add(page);tabs.Add(button);Controls.Add(button);Controls.Add(page);
        WsgTheme.SelectButton(button,index==0);
        return page;
    }
    internal void ApplyTheme() {
        BackColor=WsgTheme.Background;
        foreach(var page in pages) WsgTheme.Apply(page);
        SelectedIndex=selectedIndex;
    }
    static string Section(string key) { return key == "extra_smoothing" || key == "line_smoothing" || key == "glass_strength" || key == "jitter_positive" ? (key == "glass_strength" ? "advanced" : "overlay") : "experimental"; }
    static void Label(Control page, string text, int y, int height) { page.Controls.Add(new WsgLabel { Text = text, Left = 16, Top = y, Width = 565, Height = height }); }
    Button Reset(Control page, string text, int y, Action action) {
        var button = new WsgButton { Text = text, Left = 16, Top = y, Width = 195, Height = 32 };
        page.Controls.Add(button); button.Click += delegate { action(); }; return button;
    }
    void Number(Control page, string text, string key, int maximum, int fallback, int y, string help) {
        var label = new WsgLabel { Text = text, Left = 16, Top = y + 4, Width = 310, Height = 26 };
        var number = new NumericUpDown { Left = 338, Top = y, Width = 82, Height = 28, Minimum = 0, Maximum = maximum, Value = fallback, Tag = fallback, AccessibleName=text };
        page.Controls.Add(label); page.Controls.Add(number); values.Add(key, number);
        tips.SetToolTip(label, help); tips.SetToolTip(number, help);
        number.ValueChanged += delegate { if (!refreshing) Save(key, (int)number.Value); };
    }
    void Switch(Control page, string text, string key, bool fallback, int y, string help) {
        var box = new WsgCheckBox { Text=text, Left=16, Top=y, Width=560, Height=28, Checked=fallback, Tag=fallback };
        switches.Add(key, box); page.Controls.Add(box); tips.SetToolTip(box, help);
        box.CheckedChanged += delegate {
            if (refreshing) return;
            if (box.Checked && (key == "temporal_aa" || key == "soft_smoothing"))
                switches[key == "temporal_aa" ? "soft_smoothing" : "temporal_aa"].Checked = false;
            Save(key, box.Checked ? 1 : 0);
        };
    }
    void Save(string key, int value) {
        if (!Enabled || !File.Exists(ini()) || !WritePrivateProfileString(Section(key), key, value.ToString(), ini())) {
            RefreshValues(Enabled);
            ParentDialogPosition.Show(this, "Could not save the glass setting. Check settings file write access.", "Glass settings");
        }
    }
    string ReadWindowShortcut() {
        var value=new StringBuilder(256);
        GetPrivateProfileString("hotkeys","settings_window","Ctrl+Shift+F6",value,value.Capacity,ini());
        return value.ToString();
    }
    void RefreshShortcut() { windowShortcut.Text=ReadWindowShortcut(); }
    void SaveWindowShortcut(string shortcut) {
        if(!Enabled)return;
        string key=SettingsWindowShortcut.Normalize(shortcut);
        if(!File.Exists(ini()) || !WritePrivateProfileString("hotkeys","settings_window",key,ini()))
            throw new IOException("Could not save the shortcut. Check settings file write access.");
        RefreshShortcut();
    }
    internal void RefreshValues(bool available) {
        refreshing = true;
        Enabled = available;
        RefreshShortcut();
        SelectedIndex=selectedIndex;
        extra.Checked = available && GetPrivateProfileInt("overlay", "extra_smoothing", 0, ini()) != 0;
        values["line_smoothing"].Maximum = extra.Checked ? 100 : 50;
        dark.Checked = available && GetPrivateProfileInt("experimental", "dark_screen_fix", 0, ini()) != 0;
        seam.Checked = !available || GetPrivateProfileInt("experimental", "seam_fix", 1, ini()) != 0;
        foreach (var item in switches)
            item.Value.Checked = available ? GetPrivateProfileInt("experimental", item.Key, (bool)item.Value.Tag ? 1 : 0, ini()) != 0 : (bool)item.Value.Tag;
        if (switches["soft_smoothing"].Checked) switches["temporal_aa"].Checked = false;
        foreach (var item in values) {
            // Do not interrupt a user typing a number with the one-second UI refresh.
            if (item.Value.ContainsFocus) continue;
            int v = available ? unchecked((int)GetPrivateProfileInt(Section(item.Key), item.Key, (int)item.Value.Tag, ini())) : (int)item.Value.Tag;
            item.Value.Value = Math.Max(0, Math.Min((int)item.Value.Maximum, v));
        }
        flip.Checked = available && GetPrivateProfileInt("overlay", "jitter_positive", 1, ini()) == 0;
        refreshing = false;
    }
    protected override void Dispose(bool disposing) {
        if (disposing) tips.Dispose();
        base.Dispose(disposing);
    }
    internal void ReviewLayout(string preview) {
        foreach(var number in values.Values)if(String.IsNullOrEmpty(number.AccessibleName))throw new Exception("Numeric field needs an accessible name");
        SelectedIndex=2;
        var scroll=(Panel)values["edge_trim"].Parent;
        if(resetExperimental.Parent==scroll)throw new Exception("Experimental reset must stay outside scrolling content");
        scroll.ScrollControlIntoView(values["edge_trim"]);Application.DoEvents();
        if(!resetExperimental.Parent.ClientRectangle.Contains(resetExperimental.Bounds))throw new Exception("Experimental reset is clipped");
        var form=FindForm();
        using(var b=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(preview+".experimental-bottom.png"); }
        scroll.AutoScrollPosition=Point.Empty;SelectedIndex=0;
    }
    internal void SelfTest(string preview) {
        byte[] original = File.ReadAllBytes(ini());
        try {
            foreach(string sample in new[]{"Ctrl+5","Pause","ScrollLock","F13","F24","Ctrl+Shift+F6","Alt+Num0","Shift+PageDown","Ctrl+Shift+Alt+F11"})
                if(SettingsWindowShortcut.Normalize(sample)!=sample)throw new Exception("Supported shortcut rejected: "+sample);
            if(SettingsWindowShortcut.Normalize(" control + shift + f06 ")!="Ctrl+Shift+F6")throw new Exception("Shortcut normalization failed");
            foreach(string sample in new[]{"","A","5","F6","F12","Ctrl+Shift+F7","Ctrl+Shift+F12","Alt+F4","Ctrl+F25","Win+5","Ctrl+","Ctrl+Escape","Ctrl+5\n[bad]","Ctrl+F99999999999999999999"}) {
                bool rejected=false;
                try { SettingsWindowShortcut.Normalize(sample); } catch(ArgumentException) { rejected=true; }
                if(!rejected)throw new Exception("Invalid shortcut accepted: "+sample);
            }
            RefreshValues(true);
            if(Convert.ToBase64String(original)!=Convert.ToBase64String(File.ReadAllBytes(ini())))throw new Exception("Opening settings changed the shortcut/INI");
            WritePrivateProfileString("hotkeys","settings_window",null,ini());
            RefreshValues(true);
            if(windowShortcut.Text!="Ctrl+Shift+F6")throw new Exception("Missing shortcut must display official default");
            WritePrivateProfileString("hotkeys","settings_window","Ctrl+5",ini());
            WritePrivateProfileString("hotkeys","future_key","KeepMe",ini());
            RefreshValues(true);
            if(windowShortcut.Text!="Ctrl+5")throw new Exception("External shortcut not refreshed");
            using(var recorder=new ShortcutRecorder(ReadWindowShortcut())) {
                recorder.RecordKey(Keys.ControlKey|Keys.Control);
                if(recorder.Shortcut!=null)throw new Exception("Modifier alone must not be captured");
                recorder.RecordKey(Keys.Control|Keys.D5);
                if(recorder.Shortcut!="Ctrl+5")throw new Exception("Combination recording failed");
                recorder.RecordKey(Keys.ShiftKey|Keys.Shift);
                if(recorder.Shortcut!="Ctrl+5")throw new Exception("Shift+Tab navigation must not erase captured shortcut");
                recorder.RecordKey(Keys.Control|Keys.Shift|Keys.F11);
                if(recorder.Shortcut!=null)throw new Exception("Reserved shortcut was recordable");
                recorder.RecordKey(Keys.Pause);
                if(recorder.Shortcut!="Pause")throw new Exception("Single key recording failed");
                RefreshValues(true);
                if(ReadWindowShortcut()!="Ctrl+5")throw new Exception("Recording saved without confirmation");
                recorder.RecordKey(Keys.Escape);
                if(recorder.DialogResult!=DialogResult.Cancel || ReadWindowShortcut()!="Ctrl+5")throw new Exception("Cancel modified shortcut");
            }
            RefreshValues(false); SaveWindowShortcut("Pause");
            if(ReadWindowShortcut()!="Ctrl+5")throw new Exception("Disabled controls wrote shortcut");
            RefreshValues(true); SaveWindowShortcut("Pause");
            if(ReadWindowShortcut()!="Pause")throw new Exception("Explicit shortcut save failed");
            var future=new StringBuilder(64);GetPrivateProfileString("hotkeys","future_key","",future,64,ini());
            if(future.ToString()!="KeepMe")throw new Exception("Shortcut save changed unrelated key");
            try { SaveWindowShortcut("Alt+F4");throw new Exception("Unsafe shortcut saved"); } catch(ArgumentException) { }
            if(ReadWindowShortcut()!="Pause")throw new Exception("Rejected shortcut changed INI");
            RefreshShortcut();
            foreach (var item in switches) {
                item.Value.Checked = !(bool)item.Value.Tag;
                if (GetPrivateProfileInt("experimental", item.Key, 999, ini()) != (item.Value.Checked ? 1u : 0u)) throw new Exception("Switch write failed: " + item.Key);
            }
            switches["temporal_aa"].Checked = true;
            if (switches["soft_smoothing"].Checked || GetPrivateProfileInt("experimental", "soft_smoothing", 999, ini()) != 0) throw new Exception("Exclusive smoothing failed");
            switches["soft_smoothing"].Checked = true;
            if (switches["temporal_aa"].Checked || GetPrivateProfileInt("experimental", "temporal_aa", 999, ini()) != 0) throw new Exception("Exclusive temporal failed");
            extra.Checked = true; dark.Checked = true; seam.Checked = false;
            foreach (var item in values) {
                item.Value.Value = item.Value.Maximum;
                if (GetPrivateProfileInt(Section(item.Key), item.Key, 999, ini()) != (uint)item.Value.Maximum) throw new Exception("Glass option write failed: " + item.Key);
            }
            if (GetPrivateProfileInt("overlay", "extra_smoothing", 0, ini()) != 1 || GetPrivateProfileInt("experimental", "dark_screen_fix", 0, ini()) != 1 || GetPrivateProfileInt("experimental", "seam_fix", 1, ini()) != 0) throw new Exception("New glass switches failed");
            extra.Checked = false;
            if (values["line_smoothing"].Value != 50 || GetPrivateProfileInt("overlay", "line_smoothing", 0, ini()) != 50) throw new Exception("Extra smoothing clamp failed");
            flip.Checked = true;
            if (GetPrivateProfileInt("overlay", "jitter_positive", 1, ini()) != 0) throw new Exception("Jitter flip failed");

            // PerformClick requires the button's tab to be visible.
            SelectedIndex = 0; resetGlass.PerformClick();
            SelectedIndex = 1; resetAdvanced.PerformClick();
            SelectedIndex = 2; resetExperimental.PerformClick();
            foreach (var item in switches) if (item.Value.Checked != (bool)item.Value.Tag || GetPrivateProfileInt("experimental", item.Key, 999, ini()) != ((bool)item.Value.Tag ? 1u : 0u)) throw new Exception("Reset switch failed: " + item.Key);
            foreach (var item in values) if (GetPrivateProfileInt(Section(item.Key), item.Key, 999, ini()) != (uint)(int)item.Value.Tag) throw new Exception("Reset failed: " + item.Key);
            if (extra.Checked || dark.Checked || !seam.Checked || flip.Checked) throw new Exception("Reset toggles failed");
            if(ReadWindowShortcut()!="Pause")throw new Exception("Glass reset changed the shortcut");
            for (int i = 0; i < 3; ++i) {
                SelectedIndex = i; Application.DoEvents(); var form = FindForm();
                using (var b = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(b, new Rectangle(0, 0, form.Width, form.Height)); b.Save(preview + ".glass-" + i + ".png"); }
            }
        } finally { File.WriteAllBytes(ini(), original); SelectedIndex = 0; RefreshValues(true); }
    }
}

// Focus-local recording: no global keyboard hooks, registration or simulated input.
internal sealed class ShortcutRecorder : Form
{
    [DllImport("user32.dll")]
    static extern short GetKeyState(int key);
    readonly Label recorded=new WsgLabel { Text="Press your shortcut", TextAlign=ContentAlignment.MiddleCenter };
    readonly Label hint=new WsgLabel();
    readonly Button save=new WsgButton { Text="Save", Enabled=false, DialogResult=DialogResult.OK };
    internal string Shortcut { get; private set; }
    internal ShortcutRecorder(string current) {
        Text="Settings window shortcut"; Font=new Font("Segoe UI",10);
        AutoScaleDimensions=new SizeF(96,96); AutoScaleMode=AutoScaleMode.Dpi;
        ClientSize=new Size(450,216); FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false; MinimizeBox=false; ShowInTaskbar=false; StartPosition=FormStartPosition.CenterParent;
        BackColor=WsgTheme.Background; ForeColor=WsgTheme.Text;
        var existing=new WsgLabel { Text="Current: "+current, Left=20,Top=16,Width=410,Height=26 };
        recorded.SetBounds(20,50,410,48); recorded.Font=new Font("Segoe UI",15,FontStyle.Bold);recorded.BackColor=WsgTheme.Panel;
        hint.SetBounds(20,108,410,48);hint.Text="Press the keys together. Esc cancels.";
        save.SetBounds(220,170,100,32);
        var cancel=new WsgButton { Text="Cancel", DialogResult=DialogResult.Cancel, Left=330,Top=170,Width=100,Height=32 };
        Controls.AddRange(new Control[]{existing,recorded,hint,save,cancel}); CancelButton=cancel;
        WsgTheme.Apply(this); HandleCreated+=delegate { WsgTheme.TitleBar(this); };
        Deactivate+=delegate { if(DialogResult==DialogResult.None) { DialogResult=DialogResult.Cancel; Close(); } };
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData) {
        // Tab stays available for keyboard navigation to Save/Cancel.
        if((keyData & Keys.KeyCode)==Keys.Tab)return base.ProcessCmdKey(ref msg,keyData);
        if((GetKeyState(0x5B)&0x8000)!=0 || (GetKeyState(0x5C)&0x8000)!=0) { RecordKey(Keys.LWin);return true; }
        if(keyData==Keys.Enter && ((Control)CancelButton).ContainsFocus) { CancelButton.PerformClick();return true; }
        if(keyData==Keys.Space && (save.ContainsFocus || ((Control)CancelButton).ContainsFocus))return base.ProcessCmdKey(ref msg,keyData);
        RecordKey(keyData);return true;
    }
    internal void RecordKey(Keys keys) {
        Keys key=keys & Keys.KeyCode;
        if(key==Keys.Escape) { DialogResult=DialogResult.Cancel;Close();return; }
        if(keys==Keys.Enter && Shortcut!=null) { DialogResult=DialogResult.OK;Close();return; }
        if(key==Keys.ControlKey || key==Keys.ShiftKey || key==Keys.Menu) {
            if(Shortcut==null)recorded.Text="Press the next key…";return;
        }
        Shortcut=null;save.Enabled=false;
        string name=null;
        if(key>=Keys.F1 && key<=Keys.F24)name="F"+((int)key-(int)Keys.F1+1);
        else if(key>=Keys.A && key<=Keys.Z)name=key.ToString();
        else if(key>=Keys.D0 && key<=Keys.D9)name=((int)key-(int)Keys.D0).ToString();
        else if(key>=Keys.NumPad0 && key<=Keys.NumPad9)name="Num"+((int)key-(int)Keys.NumPad0);
        else switch(key) {
            case Keys.Insert:name="Insert";break;case Keys.Delete:name="Delete";break;
            case Keys.Home:name="Home";break;case Keys.End:name="End";break;
            case Keys.PageUp:name="PageUp";break;case Keys.PageDown:name="PageDown";break;
            case Keys.Pause:name="Pause";break;case Keys.Scroll:name="ScrollLock";break;case Keys.Space:name="Space";break;
        }
        try {
            if(name==null)throw new ArgumentException("Try Ctrl + a key, Pause, ScrollLock or F13–F24.");
            Shortcut=SettingsWindowShortcut.Normalize(((keys&Keys.Control)!=0?"Ctrl+":"")+((keys&Keys.Shift)!=0?"Shift+":"")+((keys&Keys.Alt)!=0?"Alt+":"")+name);
            recorded.Text=Shortcut;hint.Text="Save to use this shortcut. Esc cancels.";save.Enabled=true;save.Select();
        } catch(ArgumentException e) { recorded.Text="Try another shortcut";hint.Text=e.Message; }
    }
}

// INI syntax accepted by the official v1.0.1 DLL. No key hooks or simulated input.
internal static class SettingsWindowShortcut
{
    internal static string Normalize(string input) {
        if(input==null || input.Length>63)throw new ArgumentException("Enter a shortcut such as Ctrl+5, Pause or F13.");
        string[] parts=input.Split('+');
        bool ctrl=false,shift=false,alt=false;
        for(int i=0;i<parts.Length-1;i++) {
            switch(parts[i].Trim().ToUpperInvariant()) {
                case "CTRL": case "CONTROL": ctrl=true;break;
                case "SHIFT": shift=true;break;
                case "ALT": alt=true;break;
                default: throw new ArgumentException("Use Ctrl, Shift or Alt before the key, for example Ctrl+5.");
            }
        }
        string key=parts[parts.Length-1].Trim().ToUpperInvariant(),name=null;
        int function=0;
        if(key.Length==1 && ((key[0]>='A' && key[0]<='Z') || (key[0]>='0' && key[0]<='9')))name=key;
        else if(key.Length>=2 && key[0]=='F' && System.Text.RegularExpressions.Regex.IsMatch(key.Substring(1),"^[0-9]+$") && int.TryParse(key.Substring(1),out function) && function>=1 && function<=24)name="F"+function;
        else if(key.Length==4 && key.StartsWith("NUM") && key[3]>='0' && key[3]<='9')name="Num"+key[3];
        else foreach(string named in new[]{"Insert","Delete","Home","End","PageUp","PageDown","Pause","ScrollLock","Space"})
            if(string.Equals(key,named,StringComparison.OrdinalIgnoreCase)) { name=named;break; }
        if(name==null)throw new ArgumentException("Unsupported key. Try Ctrl+5, Pause, ScrollLock or F13-F24.");
        if(ctrl && shift && !alt && function>=7 && function<=12)
            throw new ArgumentException("Ctrl+Shift+F7 through F12 are reserved for the fix's own controls.");
        if(alt && !ctrl && !shift && function==4)throw new ArgumentException("Alt+F4 closes MSFS. Choose another shortcut.");
        if(!ctrl && !shift && !alt && !(function>=13 && function<=24) && name!="Pause" && name!="ScrollLock")
            throw new ArgumentException("This key needs Ctrl, Shift or Alt. Single-key options are Pause, ScrollLock and F13-F24.");
        return (ctrl ? "Ctrl+":"")+(shift ? "Shift+":"")+(alt ? "Alt+":"")+name;
    }
}
