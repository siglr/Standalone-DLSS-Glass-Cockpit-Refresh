using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Native WinForms controls, using the WSG Client palette. No web runtime needed.
internal static class WsgTheme
{
    internal static bool Light;
    internal static bool HighContrast { get { return SystemInformation.HighContrast; } }
    internal static Color Background { get { return Pick(0x0d1418,0xedf3f7,SystemColors.Control); } }
    internal static Color Panel { get { return Pick(0x17242a,0xffffff,SystemColors.Control); } }
    internal static Color Button { get { return Pick(0x20323a,0xeef4f7,SystemColors.Control); } }
    internal static Color Line { get { return Pick(0x304a53,0xa9bdc8,SystemColors.ControlDark); } }
    internal static Color Text { get { return Pick(0xedf7f6,0x183142,SystemColors.ControlText); } }
    internal static Color Muted { get { return Pick(0xa8bcc4,0x586f7d,SystemColors.GrayText); } }
    internal static Color Mint { get { return Pick(0x78d8b7,0x168060,SystemColors.Highlight); } }
    internal static Color Selected { get { return Pick(0x263852,0xd7e7ef,SystemColors.Highlight); } }
    internal static Color SelectedBorder { get { return Pick(0x9db7ff,0x3e63a8,SystemColors.Highlight); } }

    static Color Pick(int dark, int light, Color fallback) {
        int rgb=Light ? light : dark;
        return HighContrast ? fallback : Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
    }
    internal static Icon LoadIcon() {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WeSimGlide.ico")) {
            if (stream == null) throw new InvalidOperationException("The embedded WeSimGlide icon is missing.");
            using (var icon = new Icon(stream, new Size(48,48))) return (Icon)icon.Clone();
        }
    }
    internal static Bitmap LoadLogo() {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WeSimGlide.png")) {
            if(stream==null) throw new InvalidOperationException("The embedded WeSimGlide logo is missing.");
            using(var image=Image.FromStream(stream)) return new Bitmap(image);
        }
    }
    internal static void Apply(Control root) {
        foreach (Control control in root.Controls) {
            control.ForeColor = Text;
            var page = control as Panel;
            if (page != null) page.BackColor = Panel;
            var link = control as LinkLabel;
            if (link != null) { link.LinkColor=Mint; link.ActiveLinkColor=Text; link.VisitedLinkColor=Mint; }
            var text = control as TextBox;
            if (text != null) { text.BackColor=Background; text.BorderStyle=BorderStyle.FixedSingle; }
            var number = control as NumericUpDown;
            if (number != null) { number.BackColor=Background; number.ForeColor=Text; number.BorderStyle=BorderStyle.FixedSingle; }
            var check = control as CheckBox;
            if (check != null) { check.FlatStyle=FlatStyle.Flat; check.UseVisualStyleBackColor=false; }
            var button = control as Button;
            if (button != null) StyleButton(button, Equals(button.Tag,"primary"));
            if (number == null) Apply(control);
        }
    }
    internal static void StyleButton(Button button, bool primary) {
        button.FlatStyle=FlatStyle.Flat; button.UseVisualStyleBackColor=false;
        button.FlatAppearance.BorderSize=1;
        button.FlatAppearance.MouseOverBackColor=primary ? Pick(0x91e5c9,0x126b50,SystemColors.Highlight) : Pick(0x2a424c,0xdce7ed,SystemColors.Control);
        button.FlatAppearance.MouseDownBackColor=primary ? Mint : Selected;
        if(button.Font.Name!="Segoe UI" || !button.Font.Bold || button.Font.Size!=10)
            button.Font=new Font("Segoe UI",10,FontStyle.Bold);
        button.EnabledChanged -= ButtonEnabledChanged;
        button.EnabledChanged += ButtonEnabledChanged;
        PaintButton(button,primary);
    }
    static void ButtonEnabledChanged(object sender,EventArgs args) {
        var button=(Button)sender; PaintButton(button,Equals(button.Tag,"primary"));
    }
    static void PaintButton(Button button,bool primary) {
        button.BackColor=primary && button.Enabled ? Mint : Button;
        button.ForeColor=button.Enabled ? (primary ? (HighContrast ? SystemColors.HighlightText : Light ? Color.White : Background) : Text) : Muted;
        button.FlatAppearance.BorderColor=primary && button.Enabled ? Mint : Line;
    }
    internal static void SelectButton(Button button, bool selected) {
        button.BackColor=selected ? Selected : Button;
        button.ForeColor=selected && HighContrast ? SystemColors.HighlightText : Text;
        button.FlatAppearance.BorderColor=selected ? SelectedBorder : Line;
        button.AccessibleDescription=selected ? "Selected" : "Not selected";
    }
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    internal static void TitleBar(Form form) {
        if (HighContrast) return;
        try {
            int dark=Light ? 0 : 1, background=ColorTranslator.ToWin32(Background), text=ColorTranslator.ToWin32(Text);
            DwmSetWindowAttribute(form.Handle,20,ref dark,4);
            DwmSetWindowAttribute(form.Handle,35,ref background,4);
            DwmSetWindowAttribute(form.Handle,36,ref text,4);
        } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
    }
}

// Keep standard Button keyboard/accessibility behavior, but paint readable disabled text.
internal sealed class WsgButton : Button
{
    bool hover, pressed;
    protected override void OnMouseEnter(EventArgs e) { hover=true; base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { hover=false; pressed=false; base.OnMouseLeave(e); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed=true; base.OnMouseDown(e); Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed=false; base.OnMouseUp(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e) {
        if(WsgTheme.HighContrast) { base.OnPaint(e); return; }
        Color fill=Enabled ? (pressed ? FlatAppearance.MouseDownBackColor : hover ? FlatAppearance.MouseOverBackColor : BackColor) : WsgTheme.Button;
        using(var brush=new SolidBrush(fill)) e.Graphics.FillRectangle(brush,ClientRectangle);
        using(var pen=new Pen(FlatAppearance.BorderColor)) e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled ? ForeColor : WsgTheme.Muted,
            TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4),WsgTheme.SelectedBorder,fill);
    }
}
