using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Keep Windows' standard message boxes, button behavior and accessibility.
// The temporary hook is confined to this UI thread and removed before activation;
// it observes only dialog activation, never keyboard input or another process.
internal sealed class ParentDialogPosition : IDisposable
{
    delegate IntPtr Hook(int code, IntPtr window, IntPtr data);
    readonly Hook callback;
    readonly IntPtr owner;
    readonly Rectangle workArea;
    IntPtr hook;
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left,Top,Right,Bottom; }
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int kind,Hook callback,IntPtr module,uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr window,IntPtr data);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window,uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,System.Text.StringBuilder text,int count);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);

    internal ParentDialogPosition(IWin32Window parent) {
        if(parent==null || parent.Handle==IntPtr.Zero)return;
        owner=GetAncestor(parent.Handle,2); // GA_ROOT: settings panels belong to the main form.
        if(owner==IntPtr.Zero)return;
        workArea=Screen.FromHandle(owner).WorkingArea;
        callback=OnActivate;
        hook=SetWindowsHookEx(5,callback,IntPtr.Zero,GetCurrentThreadId()); // WH_CBT, current thread only.
    }
    internal static Point Center(Rectangle parent,Size dialog,Rectangle work) {
        int x=parent.Left+(parent.Width-dialog.Width)/2, y=parent.Top+(parent.Height-dialog.Height)/2;
        return new Point(Math.Max(work.Left,Math.Min(x,work.Right-dialog.Width)),
                         Math.Max(work.Top,Math.Min(y,work.Bottom-dialog.Height)));
    }
    IntPtr OnActivate(int code,IntPtr window,IntPtr data) {
        if(code==5 && GetWindow(window,4)==owner) { // HCBT_ACTIVATE, GW_OWNER
            var name=new System.Text.StringBuilder(64);
            GetClassName(window,name,name.Capacity);
            if(name.ToString()=="#32770") {
                Rect parent,dialog;
                if(GetWindowRect(owner,out parent) && GetWindowRect(window,out dialog)) {
                    var point=Center(Rectangle.FromLTRB(parent.Left,parent.Top,parent.Right,parent.Bottom),
                        new Size(dialog.Right-dialog.Left,dialog.Bottom-dialog.Top),workArea);
                    SetWindowPos(window,IntPtr.Zero,point.X,point.Y,0,0,0x15); // no size, Z-order or activation changes
                }
                Dispose();
            }
        }
        return CallNextHookEx(IntPtr.Zero,code,window,data);
    }
    public void Dispose() {
        if(hook!=IntPtr.Zero) { UnhookWindowsHookEx(hook);hook=IntPtr.Zero; }
        GC.KeepAlive(callback);
    }
    internal static DialogResult Show(IWin32Window parent,string text,string caption,
        MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None) {
        var control=parent as Control;
        if(control!=null && control.FindForm()!=null)parent=control.FindForm();
        using(var position=new ParentDialogPosition(parent))
            return MessageBox.Show(parent,text,caption,buttons,icon);
    }
}

// Native WinForms controls, using the WSG Client palette. No web runtime needed.
internal static class WsgTheme
{
    // Parent-driven paints (notably disabled controls) can supply a translated and
    // clipped Graphics. GDI text must retain that state or it bleeds onto siblings.
    internal const TextFormatFlags PaintBounds=TextFormatFlags.PreserveGraphicsTranslateTransform|TextFormatFlags.PreserveGraphicsClipping;
    internal static bool Light;
    internal static bool HighContrast { get { return SystemInformation.HighContrast; } }
    internal static Color Background { get { return Pick(0x0d1418,0xedf3f7,SystemColors.Control); } }
    internal static Color Panel { get { return Pick(0x17242a,0xffffff,SystemColors.Control); } }
    internal static Color Button { get { return Pick(0x20323a,0xeef4f7,SystemColors.Control); } }
    internal static Color Line { get { return Pick(0x304a53,0xa9bdc8,SystemColors.ControlDark); } }
    internal static Color Text { get { return Pick(0xedf7f6,0x183142,SystemColors.ControlText); } }
    internal static Color Muted { get { return Pick(0xa8bcc4,0x586f7d,SystemColors.GrayText); } }
    internal static Color Mint { get { return Pick(0x78d8b7,0x147957,SystemColors.Highlight); } }
    internal static Color Warning { get { return Pick(0xffcc80,0x8b4300,SystemColors.ControlText); } }
    internal static Color Selected { get { return Pick(0x263852,0xd7e7ef,SystemColors.Highlight); } }
    internal static Color SelectedBorder { get { return Pick(0x9db7ff,0x3e63a8,SystemColors.Highlight); } }

    static Color Pick(int dark, int light, Color fallback) {
        int rgb=Light ? light : dark;
        return HighContrast ? fallback : Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
    }
    // Visual reading order, including nested scrolling panels. Labels aren't tab stops.
    internal static void OrderControls(Control root) {
        var controls=new System.Collections.Generic.List<Control>();
        foreach(Control control in root.Controls)controls.Add(control);
        controls.Sort((a,b)=>a.Top==b.Top ? a.Left.CompareTo(b.Left) : a.Top.CompareTo(b.Top));
        for(int i=0;i<controls.Count;i++) {
            controls[i].TabIndex=i;
            if(controls[i] is Panel)OrderControls(controls[i]);
        }
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
            if (page != null) page.BackColor = Equals(page.Tag,"surface") ? Background : Panel;
            var link = control as LinkLabel;
            if (link != null) { link.LinkColor=Mint; link.ActiveLinkColor=Text; link.VisitedLinkColor=Mint; }
            var text = control as TextBox;
            if (text != null) { text.BackColor=Background; text.BorderStyle=BorderStyle.FixedSingle; }
            var number = control as NumericUpDown;
            if (number != null) { number.BackColor=Background; number.ForeColor=Text; number.BorderStyle=BorderStyle.FixedSingle; }
            var combo = control as ComboBox;
            if (combo != null) { combo.BackColor=Panel; combo.ForeColor=Text; combo.Invalidate(); }
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

// Native combo-box navigation and accessibility, with matching popup/item colours.
internal sealed class WsgComboBox : ComboBox
{
    internal WsgComboBox() { DrawMode=DrawMode.OwnerDrawFixed; }
    protected override void OnFontChanged(EventArgs e) {
        base.OnFontChanged(e); ItemHeight=Font.Height+6;
    }
    protected override void OnDrawItem(DrawItemEventArgs e) {
        bool selected=(e.State & DrawItemState.Selected)!=0;
        Color background=selected ? (WsgTheme.HighContrast ? SystemColors.Highlight : WsgTheme.Selected) : WsgTheme.Panel;
        Color foreground=!Enabled ? WsgTheme.Muted : selected && WsgTheme.HighContrast ? SystemColors.HighlightText : WsgTheme.Text;
        using(var brush=new SolidBrush(background)) e.Graphics.FillRectangle(brush,e.Bounds);
        string text=e.Index>=0 && e.Index<Items.Count ? GetItemText(Items[e.Index]) : Text;
        var bounds=Rectangle.Inflate(e.Bounds,-3,0);
        TextRenderer.DrawText(e.Graphics,text,Font,bounds,foreground,WsgTheme.PaintBounds|TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        if((e.State & DrawItemState.Focus)!=0) ControlPaint.DrawFocusRectangle(e.Graphics,e.Bounds,foreground,background);
        base.OnDrawItem(e);
    }
}

// WinForms otherwise substitutes a hard-coded disabled colour on dark panels.
// Keep controls genuinely disabled; only their rendering changes.
internal sealed class WsgLabel : Label
{
    protected override void OnPaint(PaintEventArgs e) {
        if(Enabled || WsgTheme.HighContrast) { base.OnPaint(e); return; }
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,WsgTheme.Muted,
            WsgTheme.PaintBounds|TextFormatFlags.Left|TextFormatFlags.Top|TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl);
    }
}

// Scale the glyph and clickable label together; retain native CheckBox input/accessibility.
internal sealed class WsgCheckBox : CheckBox
{
    internal int GlyphSize { get { return Math.Max(16,Font.Height); } }
    int Gap { get { return Math.Max(6,Font.Height/3); } }
    public override Size GetPreferredSize(Size proposedSize) {
        if(WsgTheme.HighContrast)return base.GetPreferredSize(proposedSize);
        Size text=TextRenderer.MeasureText(Text,Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
        return new Size(GlyphSize+Gap+text.Width+Padding.Horizontal+4,Math.Max(GlyphSize,text.Height)+Padding.Vertical+8);
    }
    protected override void OnPaint(PaintEventArgs e) {
        if(WsgTheme.HighContrast) { base.OnPaint(e);return; }
        // ButtonBase may reuse a buffer containing a previously painted sibling.
        // Our custom renderer replaces its native paint adapter, including background fill.
        using(var background=new SolidBrush(BackColor))e.Graphics.FillRectangle(background,ClientRectangle);
        int size=GlyphSize;
        var box=new Rectangle(Padding.Left+2,Padding.Top+(Height-Padding.Vertical-size)/2,size-1,size-1);
        Color foreground=Enabled?WsgTheme.Text:WsgTheme.Muted;
        using(var brush=new SolidBrush(Checked&&Enabled?WsgTheme.Selected:WsgTheme.Background))e.Graphics.FillRectangle(brush,box);
        using(var pen=new Pen(foreground,Math.Max(1f,size/17f))) {
            e.Graphics.DrawRectangle(pen,box);
            if(CheckState==CheckState.Indeterminate) {
                using(var brush=new SolidBrush(foreground))e.Graphics.FillRectangle(brush,Rectangle.Inflate(box,-size/4,-size/4));
            } else if(Checked) {
                pen.Width=Math.Max(2f,size/8f);
                e.Graphics.DrawLines(pen,new[] { new Point(box.Left+size/5,box.Top+size/2),new Point(box.Left+size*2/5,box.Top+size*3/4),new Point(box.Left+size*4/5,box.Top+size/4) });
            }
        }
        var text=new Rectangle(box.Right+Gap,Padding.Top,Math.Max(0,Width-box.Right-Gap-Padding.Right),Height-Padding.Vertical);
        var flags=WsgTheme.PaintBounds|TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding;
        if(!UseMnemonic)flags|=TextFormatFlags.NoPrefix;else if(!ShowKeyboardCues)flags|=TextFormatFlags.HidePrefix;
        TextRenderer.DrawText(e.Graphics,Text,Font,text,foreground,flags);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-1,-1),foreground,BackColor);
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
            WsgTheme.PaintBounds|TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4),WsgTheme.SelectedBorder,fill);
    }
}
