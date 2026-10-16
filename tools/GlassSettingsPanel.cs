using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Only glass settings are written; no FG settings,
// hotkeys, log viewers or report generation belong to this control.
internal sealed class GlassSettingsPanel : UserControl
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern uint GetPrivateProfileInt(string section, string key, int fallback, string path);
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
    readonly CheckBox extra = new CheckBox { Text = "Extra (up to 100)", AutoSize = true };
    readonly CheckBox dark = new CheckBox { Text = "Dark screen fix", AutoSize = true };
    readonly CheckBox seam = new CheckBox { Text = "UV seam fix", AutoSize = true };
    readonly CheckBox flip = new CheckBox { Text = "Flip jitter direction", AutoSize = true };
    readonly CheckBox tint = new CheckBox { Text = "Green tint: show corrected screens (while window is open)", AutoSize = true };
    readonly ToolTip tips = new ToolTip { AutoPopDelay = 20000 };
    readonly string eventName;
    EventWaitHandle tintEvent;
    bool refreshing;
    readonly Button resetGlass, resetAdvanced, resetExperimental;

    internal GlassSettingsPanel(Func<string> settingsPath, string tintEventName)
    {
        BackColor=WsgTheme.Background; Font=new Font("Segoe UI",10);
        ini = settingsPath;
        eventName = tintEventName + (Array.IndexOf(Environment.GetCommandLineArgs(), "--self-test") >= 0
            ? ".test." + System.Diagnostics.Process.GetCurrentProcess().Id : "");
        var basic = Page("Glass settings"); var advanced = Page("Advanced"); var experimental = Page("Experimental");
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
        tint.SetBounds(16, 62, 560, 28); basic.Controls.Add(tint);
        Label(basic, "Settings apply during upscaling; these show your requested values.\nNo need to leave VR. Green tint is only a coverage check.", 104, 64);
        resetGlass = Reset(basic, "Reset glass settings", 185, delegate { extra.Checked = false; values["line_smoothing"].Value = 35; tint.Checked = false; });
        flip.SetBounds(16, 19, 400, 28); advanced.Controls.Add(flip);
        tips.SetToolTip(flip, "Leave off unless troubleshooting jitter alignment with the mod author.");
        Number(advanced, "Glass layer strength (%)", "glass_strength", 100, 100, 60,
            "100 preserves the simulator's full glare, dirt and reflections. Lower values reduce this layer.");
        Label(advanced, "Only change these when troubleshooting a particular display.\nDefaults: normal jitter direction and full glass layer (100%).", 108, 64);
        resetAdvanced = Reset(advanced, "Reset advanced", 185, delegate { flip.Checked = false; values["glass_strength"].Value = 100; });
        dark.SetBounds(16, 6, 220, 26); seam.SetBounds(286, 6, 220, 26); experimental.Controls.Add(dark); experimental.Controls.Add(seam);
        tips.SetToolTip(dark, "Helps mostly black screens in strong sun, but can interfere with other displays. Default off.");
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

        resetExperimental = Reset(experimental, "Reset experimental", 185, delegate {
            dark.Checked = false; seam.Checked = true;
            foreach (string key in new[] { "detail_clamp", "edge_fade", "angle_softening", "edge_stretch" }) values[key].Value = 0;
        });
        flip.CheckedChanged += delegate { if (!refreshing) Save("jitter_positive", flip.Checked ? 0 : 1); };
        tint.CheckedChanged += delegate {
            if (refreshing) return;
            try {
                if (tintEvent == null) tintEvent = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);
                if (tint.Checked) tintEvent.Set(); else tintEvent.Reset();
            } catch (Exception e) { MessageBox.Show(this, e.Message, "Green tint"); }
        };
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
    static string Section(string key) { return key == "extra_smoothing" || key == "line_smoothing" || key == "glass_strength" || key == "jitter_positive" ? "cockpit" : "experimental"; }
    static void Label(Control page, string text, int y, int height) { page.Controls.Add(new Label { Text = text, Left = 16, Top = y, Width = 565, Height = height }); }
    Button Reset(Control page, string text, int y, Action action) {
        var button = new WsgButton { Text = text, Left = 16, Top = y, Width = 195, Height = 32 };
        page.Controls.Add(button); button.Click += delegate { action(); }; return button;
    }
    void Number(Control page, string text, string key, int maximum, int fallback, int y, string help) {
        var label = new Label { Text = text, Left = 16, Top = y + 4, Width = 310, Height = 26 };
        var number = new NumericUpDown { Left = 338, Top = y, Width = 82, Height = 28, Minimum = 0, Maximum = maximum, Value = fallback, Tag = fallback };
        page.Controls.Add(label); page.Controls.Add(number); values.Add(key, number);
        tips.SetToolTip(label, help); tips.SetToolTip(number, help);
        number.ValueChanged += delegate { if (!refreshing) Save(key, (int)number.Value); };
    }
    void Save(string key, int value) {
        if (!Enabled || !File.Exists(ini()) || !WritePrivateProfileString(Section(key), key, value.ToString(), ini())) {
            RefreshValues(Enabled);
            MessageBox.Show(this, "Could not save the glass setting. Check settings file write access.", "Glass settings");
        }
    }
    internal void RefreshValues(bool available) {
        refreshing = true;
        Enabled = available;
        SelectedIndex=selectedIndex;
        extra.Checked = available && GetPrivateProfileInt("cockpit", "extra_smoothing", 0, ini()) != 0;
        values["line_smoothing"].Maximum = extra.Checked ? 100 : 50;
        dark.Checked = available && GetPrivateProfileInt("experimental", "dark_screen_fix", 0, ini()) != 0;
        seam.Checked = !available || GetPrivateProfileInt("experimental", "seam_fix", 1, ini()) != 0;
        foreach (var item in values) {
            // Do not interrupt a user typing a number with the one-second UI refresh.
            if (item.Value.ContainsFocus) continue;
            int v = available ? unchecked((int)GetPrivateProfileInt(Section(item.Key), item.Key, (int)item.Value.Tag, ini())) : (int)item.Value.Tag;
            item.Value.Value = Math.Max(0, Math.Min((int)item.Value.Maximum, v));
        }
        flip.Checked = available && GetPrivateProfileInt("cockpit", "jitter_positive", 1, ini()) == 0;
        if (!available && tintEvent != null) { tintEvent.Reset(); tint.Checked = false; }
        refreshing = false;
    }
    protected override void Dispose(bool disposing) {
        if (disposing) { if (tintEvent != null) { tintEvent.Reset(); tintEvent.Dispose(); } tips.Dispose(); }
        base.Dispose(disposing);
    }
    internal void SelfTest(string preview) {
        byte[] original = File.ReadAllBytes(ini());
        try {
            extra.Checked = true; dark.Checked = true; seam.Checked = false;
            foreach (var item in values) {
                item.Value.Value = item.Value.Maximum;
                if (GetPrivateProfileInt(Section(item.Key), item.Key, 999, ini()) != (uint)item.Value.Maximum) throw new Exception("Glass option write failed: " + item.Key);
            }
            if (GetPrivateProfileInt("cockpit", "extra_smoothing", 0, ini()) != 1 || GetPrivateProfileInt("experimental", "dark_screen_fix", 0, ini()) != 1 || GetPrivateProfileInt("experimental", "seam_fix", 1, ini()) != 0) throw new Exception("New glass switches failed");
            extra.Checked = false;
            if (values["line_smoothing"].Value != 50 || GetPrivateProfileInt("cockpit", "line_smoothing", 0, ini()) != 50) throw new Exception("Extra smoothing clamp failed");
            flip.Checked = true;
            if (GetPrivateProfileInt("cockpit", "jitter_positive", 1, ini()) != 0) throw new Exception("Jitter flip failed");
            tint.Checked = true;
            using (var handle = EventWaitHandle.OpenExisting(eventName)) if (!handle.WaitOne(0)) throw new Exception("Transient tint failed");
            // PerformClick requires the button's tab to be visible.
            SelectedIndex = 0; resetGlass.PerformClick();
            SelectedIndex = 1; resetAdvanced.PerformClick();
            SelectedIndex = 2; resetExperimental.PerformClick();
            foreach (var item in values) if (GetPrivateProfileInt(Section(item.Key), item.Key, 999, ini()) != (uint)(int)item.Value.Tag) throw new Exception("Reset failed: " + item.Key);
            if (extra.Checked || dark.Checked || !seam.Checked || flip.Checked || tint.Checked || tintEvent.WaitOne(0)) throw new Exception("Reset toggles failed");
            for (int i = 0; i < 3; ++i) {
                SelectedIndex = i; Application.DoEvents(); var form = FindForm();
                using (var b = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(b, new Rectangle(0, 0, form.Width, form.Height)); b.Save(preview + ".glass-" + i + ".png"); }
            }
        } finally { File.WriteAllBytes(ini(), original); SelectedIndex = 0; RefreshValues(true); }
    }
}
