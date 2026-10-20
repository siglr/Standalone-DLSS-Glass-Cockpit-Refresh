using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class GlassWindow : Form
{
    [DllImport("user32.dll")]
    static extern bool IsWindowEnabled(IntPtr window);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
    static extern uint GetPrivateProfileInt(string section, string key, int fallback, string path);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool WritePrivateProfileString(string section, string key, string value, string path);
    static readonly string Package = AppDomain.CurrentDomain.BaseDirectory;
    readonly TextBox folder = new TextBox { ReadOnly=true };
    readonly Label status = new WsgLabel();
    readonly Label appUpdate = new WsgLabel();
    readonly Label note = new WsgLabel();
    readonly LinkLabel upstream = new LinkLabel();
    readonly LinkLabel credit = new LinkLabel();
    readonly ComboBox engineVersions=new WsgComboBox { DropDownStyle=ComboBoxStyle.DropDownList };
    readonly ComboBox installMode=new WsgComboBox { DropDownStyle=ComboBoxStyle.DropDownList };
    readonly Label modeHint=new WsgLabel { Font=new Font("Segoe UI",9),Tag="muted" };
    bool upstreamStarted;
    int upstreamRequest;
    bool AddonMode { get { return installMode.SelectedIndex==1; } }
    static bool SimulatorRunning() {
        var processes=Process.GetProcessesByName("FlightSimulator2024");
        bool running=processes.Length>0;foreach(var process in processes)process.Dispose();return running;
    }
    string BinaryName { get { return AddonMode?UpstreamRelease.AddonName:"dxgi.dll"; } }
    string BundledHash { get { return AddonMode?UpstreamRelease.AddonHash:UpstreamRelease.ZipHash; } }
    UpstreamRelease.Choice latestEngine;
    readonly System.Collections.Generic.HashSet<string> announcedEngineUpdates =
        new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
    UpstreamRelease.Choice[] pendingEngineChoices;
    string upstreamUrl=UpstreamRelease.Repository+"/releases/tag/v"+UpstreamRelease.Version;
    readonly Button install = new WsgButton { Text="Install", Tag="primary" };
    readonly Button uninstall = new WsgButton { Text="Uninstall" };
    readonly Button browse = new WsgButton { Text="Browse..." };
    readonly Button darkTheme=new WsgButton { Text="Dark", AccessibleName="Dark theme" };
    readonly Button lightTheme=new WsgButton { Text="Light", AccessibleName="Light theme" };
    readonly CheckBox enabled = new WsgCheckBox { Text="Enable glass-cockpit refresh fix" };
    readonly Timer timer = new Timer { Interval=1000 };
    readonly ToolTip hints = new ToolTip { AutoPopDelay=20000 };
    readonly GlassSettingsPanel glass;
    readonly Icon brandIcon;
    readonly PictureBox brand;
    readonly System.Collections.Generic.Dictionary<string,Tuple<string,string>> fileHashes =
        new System.Collections.Generic.Dictionary<string,Tuple<string,string>>(StringComparer.OrdinalIgnoreCase);
    readonly Panel setupPage=new Panel { Dock=DockStyle.Fill,Tag="surface" };
    readonly Panel setupScroll=new Panel { Dock=DockStyle.Fill,AutoScroll=true,Tag="surface" };
    readonly Panel settingsPage=new Panel { Dock=DockStyle.Fill,AutoScroll=true,Tag="surface" };
    readonly TableLayoutPanel setupContent=Column();
    readonly TableLayoutPanel settingsContent=Column();
    readonly Button setupTab=new WsgButton { Text="Setup" };
    readonly Button settingsTab=new WsgButton { Text="Settings" };
    readonly Label installedSummary=new WsgLabel { AutoSize=true };
    readonly Label compatibility=new WsgLabel { AutoSize=true };
    readonly Label compatibilityHint=new WsgLabel { AutoSize=true };
    readonly Panel emptySettings=new Panel { Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Tag="surface" };
    string currentPage="Setup",compatibilityStamp;

    bool FollowLatest { get { var choice=engineVersions.SelectedItem as UpstreamRelease.Choice;return choice==null||choice.Id==-1; } }
    bool busy, refreshing;
    string Target { get { return folder.Text; } }
    string Ini { get { return Path.Combine(Target, "glass-cockpit-dlss-fix.ini"); } }
    string Receipt { get { return Path.Combine(Target, ".StandaloneGlass-install.json"); } }
    static readonly string[] Payload = NativeInstaller.Payload;
    GlassWindow()
    {
        try { WsgTheme.Light=File.ReadAllText(Path.Combine(Package,"Appearance.txt")).Trim()=="light"; }
        catch(IOException) { WsgTheme.Light=false; } catch(UnauthorizedAccessException) { WsgTheme.Light=false; }
        Text="Standalone-DLSS-Glass-Cockpit-Refresh v"+global::ProductVersion.Value;
#if GLASS_NO_AUTO_UPDATE
        Text+=" (source build)";
#endif
        Font=new Font("Segoe UI",10);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        ClientSize=new Size(740,660);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
        brandIcon=WsgTheme.LoadIcon();Icon=brandIcon;
        brand=new PictureBox { Image=WsgTheme.LoadLogo(),SizeMode=PictureBoxSizeMode.Zoom,TabStop=false,AccessibleName="WeSimGlide logo" };
        var title=new WsgLabel { Text="Glass Cockpit Refresh" };
        var subtitle=new WsgLabel { Text="MSFS 2024  /  Desktop + VR",Tag="muted" };
        var folderLabel=Caption("MSFS 2024 GAME FOLDER");
        var help=new LinkLabel { Text="Help",TextAlign=ContentAlignment.MiddleCenter };
        var website=new LinkLabel { Text="About",TextAlign=ContentAlignment.MiddleCenter };
        help.LinkClicked+=delegate { ShowHelp(); };
        website.LinkClicked+=delegate { OpenSupport("https://wesimglide.org/otherdata/tools/Standalone-DLSS-Glass-Cockpit-Refresh.html"); };
        darkTheme.Click+=delegate { SelectAppearance(false); };lightTheme.Click+=delegate { SelectAppearance(true); };
        installMode.Items.AddRange(new object[]{"Standalone DLL","ReShade add-on"});installMode.SelectedIndex=0;
        installMode.AccessibleName="Installation mode";
        engineVersions.AccessibleName="Official fix version";
        engineVersions.AccessibleDescription="Latest is recommended. Select a specific version for rollback or offline installation. Selection alone installs nothing.";
        hints.SetToolTip(engineVersions,engineVersions.AccessibleDescription);
        engineVersions.Items.Add(new UpstreamRelease.Choice { Id=-1,Tag="latest" });engineVersions.SelectedIndex=0;
        folder.AccessibleName="MSFS 2024 game folder";
        credit.Text="Installer: siglr / WSG · Glass Cockpit DLSS Fix by IFreemz";
        credit.Font=new Font("Segoe UI",8.5f);credit.LinkClicked+=delegate { OpenSupport(UpstreamRelease.Repository); };
        appUpdate.Font=new Font("Segoe UI",8.5f);appUpdate.Tag="muted";appUpdate.AutoEllipsis=true;
        status.AutoEllipsis=true;upstream.AutoEllipsis=true;
        status.TextChanged+=delegate { hints.SetToolTip(status,status.Text);PaintStatus(); };
        appUpdate.TextChanged+=delegate { hints.SetToolTip(appUpdate,appUpdate.Text); };
        upstream.LinkClicked+=delegate { OpenSupport(upstreamUrl); };
        glass=new GlassSettingsPanel(()=>Ini);
        BuildPages(title,subtitle,website,help,folderLabel);
        installMode.SelectedIndexChanged+=delegate {
            latestEngine=null;pendingEngineChoices=null;engineVersions.Items.Clear();PopulateEngineChoices(new UpstreamRelease.Choice[0]);
            if(upstreamStarted)CheckUpstream();
        };
        engineVersions.SelectedIndexChanged+=delegate { RefreshState(); };
        engineVersions.DropDownClosed+=delegate {
            BeginInvoke(new Action(delegate {
                if(IsDisposed||engineVersions.DroppedDown)return;
                if(pendingEngineChoices!=null) { var choices=pendingEngineChoices;pendingEngineChoices=null;PopulateEngineChoices(choices); }
                RefreshState();
            }));
        };
        folder.TextChanged+=delegate {
            hints.SetToolTip(folder,folder.Text);var receipt=CurrentReceipt();
            installMode.SelectedIndex=(receipt!=null?NativeInstaller.UsesAddon(receipt):NativeInstaller.HasRootAddon(Target))?1:0;
            RefreshState();
        };
        browse.Click+=delegate {
            using(var dialog=new FolderBrowserDialog { SelectedPath=Target,Description="Choose the folder containing FlightSimulator2024.exe (Xbox Content or Steam game folder)." })
            using(var position=new ParentDialogPosition(this)) {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                if(!File.Exists(Path.Combine(dialog.SelectedPath,"FlightSimulator2024.exe"))) { ParentDialogPosition.Show(this,"Choose the folder containing FlightSimulator2024.exe.",Text);return; }
                folder.Text=dialog.SelectedPath;
                try { File.WriteAllText(Path.Combine(Package,"SimulatorDirectory.txt"),Target); } catch(Exception e) { ParentDialogPosition.Show(this,e.Message,Text); }
                SelectPage("Setup",true);
            }
        };
        enabled.CheckedChanged+=delegate {
            if(refreshing||!enabled.Enabled)return;
            if(!File.Exists(Receipt)||!File.Exists(Ini)||!WritePrivateProfileString("overlay","enabled",enabled.Checked?"1":"0",Ini))
                ParentDialogPosition.Show(this,"Could not save the setting. Check settings file write access.",Text);
            RefreshState();
        };
        install.Click+=async delegate { if(FollowLatest)await SetupLatestEngine();else await SetupSelectedEngine(); };
        uninstall.Click+=async delegate { await Setup(true); };
        timer.Tick+=delegate { RefreshState(); };
        FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(busy)e.Cancel=true; };
        FormClosed+=delegate { timer.Dispose();hints.Dispose();brand.Image.Dispose();brandIcon.Dispose(); };
        HandleCreated+=delegate { WsgTheme.TitleBar(this); };
        PopulateEngineChoices(new UpstreamRelease.Choice[0]);RefreshAppearance();RefreshState();
    }
    static TableLayoutPanel Column() {
        var table=new TableLayoutPanel { ColumnCount=1,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Height=1,Dock=DockStyle.Top,Margin=Padding.Empty,Tag="surface" };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));return table;
    }
    static void Row(TableLayoutPanel table,Control control,int height=0) {
        int row=table.RowCount++;table.RowStyles.Add(height==0?new RowStyle(SizeType.AutoSize):new RowStyle(SizeType.Absolute,height));
        control.Dock=DockStyle.Fill;control.Margin=new Padding(0,0,0,6);control.TabIndex=row;
        table.Controls.Add(control,0,row);
    }
    static TableLayoutPanel Pair(Control left,Control right,int rightWidth,int height) {
        var pair=new TableLayoutPanel { ColumnCount=2,Height=height,Margin=Padding.Empty,Dock=DockStyle.Top,Tag="surface" };
        pair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));pair.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,rightWidth));
        left.Dock=right.Dock=DockStyle.Fill;left.Margin=new Padding(0,0,10,0);right.Margin=Padding.Empty;
        pair.Controls.Add(left,0,0);pair.Controls.Add(right,1,0);return pair;
    }
    static Label Caption(string text) { return new WsgLabel { Text=text,AutoSize=true,Font=new Font("Segoe UI",9,FontStyle.Bold),Margin=new Padding(0,8,0,4) }; }
    static void FitDropdownRow(TableLayoutPanel table,ComboBox dropdown) {
        // Owner-drawn ComboBox's cached preferred size can omit part of its native border.
        // Track the actual closed height, including margins, after font/DPI changes.
        Action fit=delegate {
            RowStyle row=table.RowStyles[table.GetRow(dropdown)];
            int height=dropdown.Height+dropdown.Margin.Vertical;
            if(row.SizeType!=SizeType.Absolute)row.SizeType=SizeType.Absolute;
            if(row.Height!=height)row.Height=height;
        };
        dropdown.SizeChanged+=delegate { fit(); };dropdown.MarginChanged+=delegate { fit(); };
        table.Layout+=delegate { fit(); };fit();
    }
    void BuildPages(Label title,Label subtitle,LinkLabel website,LinkLabel help,Label folderLabel) {
        SuspendLayout();Controls.Clear();
        var shell=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(18,14,18,10),Tag="surface" };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));shell.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
        var heading=new TableLayoutPanel { ColumnCount=3,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Fill,MinimumSize=new Size(0,70),Margin=Padding.Empty,Tag="surface" };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,58));heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,146));
        brand.Dock=DockStyle.Top;brand.Size=new Size(48,48);brand.Margin=Padding.Empty;
        var titles=Column();title.AutoSize=true;title.Font=new Font("Segoe UI",17,FontStyle.Bold);subtitle.AutoSize=true;
        Row(titles,title);Row(titles,subtitle);heading.Controls.Add(brand,0,0);heading.Controls.Add(titles,1,0);
        var links=new TableLayoutPanel { ColumnCount=2,RowCount=2,Height=1,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,Margin=Padding.Empty,Tag="surface" };
        links.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));links.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        links.RowStyles.Add(new RowStyle(SizeType.AutoSize));links.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        foreach(var link in new[]{website,help}) { link.AutoSize=true;link.MinimumSize=new Size(0,24);link.Dock=DockStyle.Fill;link.Margin=new Padding(0,0,0,6); }
        foreach(var button in new[]{darkTheme,lightTheme}) { button.AutoSize=true;button.MinimumSize=new Size(0,34);button.Padding=new Padding(6,4,6,4);button.Dock=DockStyle.Fill; }
        darkTheme.Margin=new Padding(0,0,4,6);lightTheme.Margin=new Padding(4,0,0,6);
        links.Controls.Add(website,0,0);links.Controls.Add(help,1,0);links.Controls.Add(darkTheme,0,1);links.Controls.Add(lightTheme,1,1);
        heading.Controls.Add(links,2,0);
        shell.Controls.Add(heading,0,0);
        var navigation=new TableLayoutPanel { ColumnCount=2,Dock=DockStyle.Fill,Margin=new Padding(0,0,0,8),Tag="surface" };
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        setupTab.Dock=settingsTab.Dock=DockStyle.Fill;setupTab.Margin=new Padding(0,0,4,0);settingsTab.Margin=new Padding(4,0,0,0);
        navigation.Controls.Add(setupTab,0,0);navigation.Controls.Add(settingsTab,1,0);shell.Controls.Add(navigation,0,1);
        setupTab.Click+=delegate { SelectPage("Setup",true); };settingsTab.Click+=delegate { SelectPage("Settings",true); };
        var content=new Panel { Dock=DockStyle.Fill,Margin=Padding.Empty,Tag="surface" };content.Controls.Add(setupPage);content.Controls.Add(settingsPage);shell.Controls.Add(content,0,2);
        var setupLayout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty,Tag="surface" };
        setupLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        setupLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));setupLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        setupPage.Controls.Add(setupLayout);setupLayout.Controls.Add(setupScroll,0,0);setupScroll.Margin=Padding.Empty;
        setupContent.Padding=new Padding(0,4,14,8);setupScroll.Controls.Add(setupContent);
        folderLabel.AutoSize=true;Row(setupContent,folderLabel);Row(setupContent,Pair(folder,browse,96,32),40);
        var modeRow=new TableLayoutPanel { ColumnCount=2,RowCount=1,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Fill,Tag="surface" };
        modeRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        var modeLabel=Caption("Install as");modeLabel.Dock=DockStyle.Fill;installMode.Dock=DockStyle.Fill;modeRow.Controls.Add(modeLabel,0,0);modeRow.Controls.Add(installMode,1,0);
        // Native combo height follows its font/item metrics; fixed rows can crop its lower border.
        Row(setupContent,modeRow);modeHint.AutoSize=true;Row(setupContent,modeHint);
        Row(setupContent,installedSummary);Row(setupContent,Caption("Install version"));Row(setupContent,engineVersions);
        FitDropdownRow(modeRow,installMode);FitDropdownRow(setupContent,engineVersions);
        upstream.AutoSize=false;Row(setupContent,upstream,28);status.AutoSize=false;Row(setupContent,status,30);
        var actions=new FlowLayoutPanel { AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Dock=DockStyle.Fill,Margin=new Padding(0,8,0,0),Tag="surface" };
        install.Size=new Size(220,36);uninstall.Size=new Size(130,36);actions.Controls.Add(install);actions.Controls.Add(uninstall);setupLayout.Controls.Add(actions,0,1);
        Row(setupContent,Caption("Compatibility"));Row(setupContent,compatibility);Row(setupContent,compatibilityHint);
        settingsContent.Padding=new Padding(0,4,14,8);settingsPage.Controls.Add(settingsContent);
        Row(settingsContent,emptySettings);
        var emptyLayout=Column();emptySettings.Controls.Add(emptyLayout);Row(emptyLayout,new WsgLabel { Text="Install the fix to configure it.",AutoSize=true });
        var go=new WsgButton { Text="Go to Setup",Width=150,Height=34 };Row(emptyLayout,go,42);go.Click+=delegate { SelectPage("Setup",true); };
        enabled.AutoSize=true;Row(settingsContent,enabled);note.AutoSize=true;Row(settingsContent,note);glass.Dock=DockStyle.Top;Row(settingsContent,glass);
        var footer=Column();footer.Padding=new Padding(0,10,0,0);credit.AutoSize=false;credit.AutoEllipsis=true;credit.Dock=DockStyle.Fill;Row(footer,credit,22);
        appUpdate.AutoSize=false;Row(footer,appUpdate,22);shell.Controls.Add(footer,0,3);
        bool sizingFooter=false;
        Action sizeFooter=delegate {
            if(sizingFooter)return;sizingFooter=true;
            try {
                int total=footer.Padding.Vertical;
                int index=0;foreach(Control line in new Control[]{credit,appUpdate}) {
                    int textHeight=TextRenderer.MeasureText("Agyp",line.Font).Height;
                    int height=textHeight+Math.Max(4,textHeight/4)+line.Margin.Vertical;
                    if(footer.RowStyles[index].Height!=height)footer.RowStyles[index].Height=height;
                    total+=height;index++;
                }
                if(shell.RowStyles[3].Height!=total)shell.RowStyles[3].Height=total;
            } finally { sizingFooter=false; }
        };
        credit.FontChanged+=delegate { sizeFooter(); };appUpdate.FontChanged+=delegate { sizeFooter(); };
        footer.Layout+=delegate { sizeFooter(); };sizeFooter();
        hints.SetToolTip(credit,credit.Text);
        Controls.Add(shell);SelectPage("Setup",false);ResumeLayout(true);
    }
    void LayoutFooter() { /* Reserved footer rows keep status updates from moving controls. */ }
    void SelectPage(string page,bool remember) {
        currentPage=page=="Settings"?"Settings":"Setup";
        setupPage.Visible=currentPage=="Setup";settingsPage.Visible=!setupPage.Visible;
        WsgTheme.SelectButton(setupTab,setupPage.Visible);WsgTheme.SelectButton(settingsTab,settingsPage.Visible);
        if(remember)try { File.WriteAllText(Path.Combine(Package,"Navigation.txt"),currentPage); } catch(IOException) {} catch(UnauthorizedAccessException) {}
    }
    bool RecoveryPending() { return Directory.Exists(Path.Combine(Target,NativeInstaller.TransactionName))||File.Exists(Path.Combine(Target,".StandaloneGlass-backup",".update.json")); }
    bool CompleteInstallation() {
        var receipt=CurrentReceipt();return receipt!=null&&receipt.files!=null&&!RecoveryPending()&&Array.TrueForAll(NativeInstaller.UsesAddon(receipt)?NativeInstaller.AddonPayload:Payload,n=>File.Exists(Path.Combine(Target,n)));
    }
    void RestorePage() {
        string page="Settings";try { page=File.ReadAllText(Path.Combine(Package,"Navigation.txt")).Trim(); } catch(IOException) {} catch(UnauthorizedAccessException) {}
        SelectPage(CompleteInstallation()?page:"Setup",false);
    }
    void FitWorkingArea(Rectangle work) {
        Size=new Size(Math.Min(Width,work.Width),Math.Min(Height,work.Height));
        Location=WindowPlacement.KeepVisible(new Rectangle(Location,Size),work,Size).Location;
    }
    void RefreshCompatibility() {
        try {
            string stamp=Target+"|"+AddonMode;
            foreach(string name in new[]{"dxgi.dll","version.dll","winmm.dll","OptiScaler.dll"}) {
                var file=new FileInfo(Path.Combine(Target,name));stamp+="|"+(file.Exists?file.Length+":"+file.LastWriteTimeUtc.Ticks:"missing");
            }
            if(stamp==compatibilityStamp)return;compatibilityStamp=stamp;
            if(Target.Length==0||!Directory.Exists(Target)) { compatibility.Text="Choose the game folder to check graphics DLLs.";compatibilityHint.Text="";return; }
            var result=NativeInstaller.DescribeGraphicsFiles(Target);compatibility.Text=result.Item1;
            compatibilityHint.Text=result.Item2?NativeInstaller.CompatibilityAdvice(Target,AddonMode):"Only known filenames are checked; this is not a full compatibility test.";
        } catch(Exception) { compatibility.Text="Could not inspect graphics DLLs in this folder.";compatibilityHint.Text="Use Help if MSFS fails to start."; }
    }
    void RememberWindow() {

        string path=Path.Combine(Package,"WindowPlacement.json");
        Load+=delegate {
            Rectangle saved;
            if(WindowPlacement.TryRead(path,out saved)) {
                StartPosition=FormStartPosition.Manual;
                // Keep the designed size, including when an earlier build saved resized bounds.
                Location=saved.Location;FitWorkingArea(Screen.FromRectangle(saved).WorkingArea);
            } else FitWorkingArea(Screen.FromControl(this).WorkingArea);
        };
        FormClosing+=delegate(object sender,FormClosingEventArgs e) {
            if(e.Cancel)return;
            Rectangle normal=WindowState==FormWindowState.Normal ? Bounds : RestoreBounds;
            WindowPlacement.Save(path,normal);
        };
    }
    void OpenSupport(string url) {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute=true }); }
        catch(Exception e) { ParentDialogPosition.Show(this,e.Message,Text); }
    }
    async void CheckUpstream() {
        upstreamStarted=true;int request=++upstreamRequest;bool addon=AddonMode;
        try {
            var releases=await Task.Run(()=>Tuple.Create(UpstreamRelease.Latest(addon),UpstreamRelease.List(addon)));
            if(!IsDisposed&&request==upstreamRequest&&addon==AddonMode) {
                latestEngine=releases.Item1;
                PopulateEngineChoices(releases.Item2);
                upstreamUrl=UpstreamRelease.Repository+"/releases";
            }
        } catch { if(!IsDisposed&&request==upstreamRequest) { latestEngine=null;upstream.Text="Could not verify the latest release — retry later"; } }
    }
    void PopulateEngineChoices(UpstreamRelease.Choice[] choices) {
        if(engineVersions.DroppedDown) { pendingEngineChoices=choices;return; }
        var previous=engineVersions.SelectedItem as UpstreamRelease.Choice;
        engineVersions.BeginUpdate();
        engineVersions.Items.Clear();
        engineVersions.Items.Add(new UpstreamRelease.Choice { Id=-1,Addon=AddonMode,Tag="latest" });
        engineVersions.Items.Add(new UpstreamRelease.Choice { Id=0,Tag="v"+UpstreamRelease.Version,Addon=AddonMode });
        foreach(var choice in choices) {
            if(choice.Addon!=AddonMode||choice.Tag=="v"+UpstreamRelease.Version&&choice.Sha256==BundledHash)continue;
            choice.IsLatest=latestEngine!=null&&choice.Id==latestEngine.Id;engineVersions.Items.Add(choice);
        }
        int selected=0;
        if(previous!=null&&previous.Addon==AddonMode)for(int i=0;i<engineVersions.Items.Count;i++) {
            var item=(UpstreamRelease.Choice)engineVersions.Items[i];if(item.Id==previous.Id&&item.Tag==previous.Tag) { selected=i;break; }
        }
        engineVersions.SelectedIndex=selected;engineVersions.EndUpdate();
        upstream.Text=latestEngine==null?"Latest release not verified. Bundled / offline versions remain available.":"Latest official fix: "+latestEngine.Tag+" — verified release checksums";
        RefreshState();
    }
    void ShowHelp() {
        using(var dialog=new Form { Text="Glass Cockpit Refresh — Help",Font=Font,ClientSize=new Size(620,450),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,StartPosition=FormStartPosition.CenterParent }) {
            var layout=Column();layout.Padding=new Padding(18);var scroll=new Panel { Dock=DockStyle.Fill,AutoScroll=true,Tag="surface" };scroll.Controls.Add(layout);dialog.Controls.Add(scroll);
            Row(layout,Caption("Using the fix"));
            Row(layout,new WsgLabel { AutoSize=true,MaximumSize=new Size(560,0),Text=@"Standalone settings apply during upscaling. Use the recorded shortcut while MSFS has focus to open the original settings window, including green tint and diagnostics.

For the ReShade add-on, edit here with MSFS closed. During a flight, use the Glass Cockpit DLSS Fix panel in ReShade." });
            Row(layout,Caption("MSFS will not start"));
            Row(layout,new WsgLabel { AutoSize=true,MaximumSize=new Size(560,0),Text="Uninstall through this app, then test the same official version and mode installed manually, following IFreemz's instructions." });
            Row(layout,new WsgLabel { AutoSize=true,MaximumSize=new Size(560,0),Text="Manual installation works? Contact siglr / WSG for installer, update and app issues. Manual installation also fails? Contact IFreemz for graphics/fix issues with glass-cockpit-dlss-fix.log from the game folder." });
            var support=new FlowLayoutPanel { AutoSize=true,WrapContents=true };
            foreach(var item in new[]{Tuple.Create("Installer support","https://github.com/siglr/Standalone-DLSS-Glass-Cockpit-Refresh/issues"),Tuple.Create("Graphics / fix support",UpstreamRelease.Repository+"/issues")}) {
                var link=new LinkLabel { Text=item.Item1,AutoSize=true,Margin=new Padding(0,6,20,6) };string url=item.Item2;link.LinkClicked+=delegate { OpenSupport(url); };support.Controls.Add(link);
            }
            Row(layout,support);
            Row(layout,Caption("Other graphics tools"));
            Row(layout,new WsgLabel { AutoSize=true,MaximumSize=new Size(560,0),Text="Use one copy of the glass fix. ReShade with add-on support must be installed separately for add-on mode. Custom ReShade add-on folders require manual installation in this test version. Other graphics helpers may interact even when their DLL filenames differ; this is a possibility, not proof of a conflict. Nothing is removed or renamed automatically." });
            Row(layout,new WsgLabel { AutoSize=true,MaximumSize=new Size(560,0),Text="A user reported a startup failure with DLSS Enabler 4.9.0 as version.dll and the standalone glass fix. IFreemz reports an alternative working: MFG Unlocker renamed to winmm.dll with his dxgi.dll, without ReShade or OptiScaler. With OptiScaler, use the unlocker as an OptiScaler plugin, as its README describes. His test crashed when the winmm.dll method was used alongside OptiScaler. These reports do not guarantee results on every setup." });
            var mfgLink=new LinkLabel { Text="MFG Unlocker installation instructions",AutoSize=true };mfgLink.LinkClicked+=delegate { OpenSupport("https://github.com/dashdogy/MFG-Unlocker#install"); };Row(layout,mfgLink);

            var close=new WsgButton { Text="Close",Height=34,DialogResult=DialogResult.OK };Row(layout,close,42);dialog.CancelButton=close;
            dialog.BackColor=WsgTheme.Background;WsgTheme.Apply(dialog);dialog.HandleCreated+=delegate { WsgTheme.TitleBar(dialog); };
            dialog.ShowDialog(this);
        }
    }
    void RefreshAppearance() {
        BackColor=WsgTheme.Background; ForeColor=WsgTheme.Text;
        WsgTheme.Apply(this); glass.ApplyTheme();
        foreach(Control control in Controls) if(Equals(control.Tag,"muted"))control.ForeColor=WsgTheme.Muted;
        PaintStatus();
        WsgTheme.SelectButton(darkTheme,!WsgTheme.Light); WsgTheme.SelectButton(lightTheme,WsgTheme.Light);
        if(IsHandleCreated)WsgTheme.TitleBar(this);
        SelectPage(currentPage,false);Invalidate(true);
    }
    void PaintStatus() {
        string value=status.Text;
        bool warning=value.IndexOf("incomplete",StringComparison.OrdinalIgnoreCase)>=0 || value.IndexOf("Cannot",StringComparison.OrdinalIgnoreCase)>=0 || value.IndexOf("changed",StringComparison.OrdinalIgnoreCase)>=0 || value.IndexOf("interrupted",StringComparison.OrdinalIgnoreCase)>=0 || value.StartsWith("Remove the conflicting");
        status.ForeColor=warning ? WsgTheme.Warning : value.StartsWith("Installed") ? WsgTheme.Mint : WsgTheme.Text;
    }
    void SelectAppearance(bool light) {
        WsgTheme.Light=light; RefreshAppearance();
        try { File.WriteAllText(Path.Combine(Package,"Appearance.txt"),light ? "light" : "dark"); }
        catch(Exception e) { ParentDialogPosition.Show(this,"The appearance changed, but could not be saved.\n\n"+e.Message,Text); }
    }
    bool NewerOfficialRelease(NativeInstaller.Receipt installed) {
        if(installed==null||latestEngine==null||latestEngine.Addon!=NativeInstaller.UsesAddon(installed)||
            installed.engineZipHash==latestEngine.Sha256||String.IsNullOrEmpty(latestEngine.Tag)||
            !latestEngine.Tag.StartsWith("v",StringComparison.Ordinal))return false;
        try { return StandaloneAppUpdate.ParseVersion(latestEngine.Tag.Substring(1))>
            StandaloneAppUpdate.ParseVersion(installed.engineVersion); }
        catch(InvalidDataException) { return false; }
        catch(ArgumentException) { return false; }
    }
    void RefreshState()
    {
        bool found=Target.Length>0 && File.Exists(Path.Combine(Target,"FlightSimulator2024.exe"));
        bool present=found && File.Exists(Receipt);
        bool other=found && File.Exists(Path.Combine(Target,".MyOwnFGVRMod-install.json"));
        bool legacy=present && File.Exists(Path.Combine(Target,"StandaloneGlassGraphics.dll")) && File.Exists(Path.Combine(Target,"StandaloneGlass.ini"));
        var installed=CurrentReceipt();
        bool installedAddon=NativeInstaller.UsesAddon(installed);
        bool complete=found&&CompleteInstallation();
        bool switching=complete&&installedAddon!=AddonMode;
        installedSummary.Text=complete?"Installed: "+(installedAddon?"ReShade add-on":"Standalone DLL")+" v"+(installed.engineVersion??"unknown"):present?"Installed: incomplete — recovery needed":"Installed: not installed";
        installedSummary.ForeColor=WsgTheme.Text;
        modeHint.Text=AddonMode?"Requires ReShade with add-on support, installed separately.":"For users who do not use ReShade.";
        bool knownSettings=false;
        if(complete)try {
            var receipt=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<NativeInstaller.Receipt>(File.ReadAllText(Receipt));
            // 1.0.2 keeps the same keys/defaults; standalone reloads INI before saving.
            knownSettings=receipt.engineVersion=="1.0.0"||receipt.engineVersion=="1.0.1"||receipt.engineVersion=="1.0.2";
        } catch { }
        bool addonRunning=installedAddon&&SimulatorRunning();
        note.Text=addonRunning?"MSFS is running — change add-on settings in ReShade.":complete&&!knownSettings ? "Settings format not verified — use the original in-sim window." : "";
        LayoutFooter();
        bool recovery=found&&RecoveryPending();
        string installedStatus="";
        bool needsUpdate=false;
        if(recovery) { needsUpdate=true; installedStatus="Update interrupted - close MSFS, then click Update to recover"; }
        else if(complete) needsUpdate=NeedsInstalledUpdate(out installedStatus);
        else if(legacy) { needsUpdate=true; installedStatus="Update available — migrate to the official DLL; keep settings"; }
        bool officialUpdate=complete&&!switching&&!recovery&&NewerOfficialRelease(installed)&&
            (installedStatus.StartsWith("Installed -")||installedStatus.StartsWith("Installed official"));
        if(officialUpdate) { needsUpdate=true;installedStatus="Official "+(installedAddon?"add-on ":"DLL ")+latestEngine.Tag+" available — update below"; }
        if(switching&&!installedStatus.Contains("changed")&&!installedStatus.StartsWith("Cannot")) { needsUpdate=true;installedStatus="Ready to switch mode — settings will be kept"; }
        bool verified=installedStatus.IndexOf("changed",StringComparison.OrdinalIgnoreCase)<0&&!installedStatus.StartsWith("Cannot")&&!installedStatus.StartsWith("Package");
        bool canInstall=!busy&&found&&!other&&(!present||recovery||legacy||complete&&verified);
        bool selectionNeeds=FollowLatest?latestEngine!=null&&(!present||needsUpdate||switching||recovery||legacy):SelectedEngineNeedsInstall()||recovery||legacy;
        install.Enabled=canInstall&&selectionNeeds;uninstall.Enabled=!busy&&(present||recovery);
        install.Text=!present?"Install":switching?"Switch mode":FollowLatest?
            (officialUpdate?"Update to "+latestEngine.Tag:"Update"):"Install selected version";
        install.AccessibleDescription=officialUpdate&&FollowLatest?
            "A newer official release is available. Update preserves your settings.":"";
        installMode.Enabled=!busy;
        browse.Enabled=!busy; enabled.Enabled=!busy && complete && knownSettings&&!addonRunning;
        engineVersions.Enabled=!busy;
        glass.ReShadeMode=complete?installedAddon:AddonMode;
        glass.ShortcutRequiresUpdate=complete&&!installedAddon&&(installed.engineVersion=="1.0.0"||installed.engineVersion=="1.0.1");
        emptySettings.Visible=!complete;enabled.Visible=complete;glass.Visible=complete;note.Visible=complete&&note.Text.Length>0;
        glass.RefreshValues(!busy && complete && knownSettings&&!addonRunning,complete&&knownSettings);
        RefreshCompatibility();
        refreshing=true; enabled.Checked=complete && GetPrivateProfileInt("overlay","enabled",1,Ini)!=0; refreshing=false;
        if(!busy) status.Text=other ? "Remove the conflicting mod using its own installer first."
            : complete || recovery || legacy ? installedStatus : present ? "Installation incomplete - use Uninstall to recover"
            : found ? "Ready to install - close MSFS first" : "Choose the MSFS 2024 game folder with Browse.";
        // Alert once per installation/release during this session. Never interrupt a
        // modal dialog, an operation, or an explicit rollback/mode/dropdown choice.
        if(officialUpdate&&FollowLatest&&canInstall&&Enabled&&(!IsHandleCreated||IsWindowEnabled(Handle))&&
            !engineVersions.DroppedDown&&!installMode.DroppedDown) {
            string notice=Path.GetFullPath(Target)+"|"+installedAddon+"|"+latestEngine.Tag;
            if(announcedEngineUpdates.Add(notice))SelectPage("Setup",false);
        }
    }
    string FileHash(string path)
    {
        // Poll metadata, not entire DLL contents, while the user is flying.
        var info=new FileInfo(path);
        string stamp=info.Length+":"+info.LastWriteTimeUtc.Ticks;
        Tuple<string,string> cached;
        if(fileHashes.TryGetValue(path,out cached) && cached.Item1==stamp)return cached.Item2;
        string hash=StandaloneAppUpdate.HashFile(path);
        fileHashes[path]=Tuple.Create(stamp,hash);return hash;
    }
    NativeInstaller.Receipt CurrentReceipt() {
        try { return new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<NativeInstaller.Receipt>(File.ReadAllText(Receipt)); }
        catch { return null; }
    }
    bool SelectedEngineNeedsInstall() {
        var choice=engineVersions.SelectedItem as UpstreamRelease.Choice;
        if(choice==null||choice.Id==-1)return false;
        if(!File.Exists(Receipt))return true;
        try {
            var json=new System.Web.Script.Serialization.JavaScriptSerializer();
            var receipt=json.Deserialize<NativeInstaller.Receipt>(File.ReadAllText(Receipt));
            string installedName=NativeInstaller.UsesAddon(receipt)?UpstreamRelease.AddonName:"dxgi.dll";
            if(receipt.files==null||!receipt.files.ContainsKey(installedName))return false;
            string dll=Path.Combine(Target,installedName);
            if(!File.Exists(dll)||FileHash(dll)!=receipt.files[installedName])return false;
            if(NativeInstaller.UsesAddon(receipt)!=AddonMode)return true;
            if(!receipt.files.ContainsKey("glass-cockpit-dlss-fix.ini"))return true;
            if(choice.Id==0)return FileHash(dll)!=FileHash(Path.Combine(Package,BinaryName));
            return receipt.engineZipHash!=choice.Sha256;
        } catch { return false; }
    }
    bool NeedsInstalledUpdate(out string message)
    {
        message="Cannot verify installation - Update unavailable";
        try {
            var json = new System.Web.Script.Serialization.JavaScriptSerializer();
            var receipt = json.Deserialize<System.Collections.Generic.Dictionary<string, object>>(File.ReadAllText(Receipt));
            var payload = json.Deserialize<System.Collections.Generic.Dictionary<string, object>>(File.ReadAllText(Path.Combine(Package,"payload.json")));
            var installed = (System.Collections.Generic.Dictionary<string, object>)receipt["files"];
            var available = (System.Collections.Generic.Dictionary<string, object>)payload["files"];
            bool addon=installed.ContainsKey(UpstreamRelease.AddonName);
            if(addon) {
                var inventory=StandaloneAppUpdate.ReadInventory(Package,false);
                available[UpstreamRelease.AddonName]=inventory.files[UpstreamRelease.AddonName];
            }
            bool different=false;
            foreach(string name in new[] { addon?UpstreamRelease.AddonName:"dxgi.dll" }) {
                if(!installed.ContainsKey(name) || !available.ContainsKey(name))return false;
                string actual=FileHash(Path.Combine(Target,name));
                if(!String.Equals(actual,Convert.ToString(installed[name]),StringComparison.OrdinalIgnoreCase)) {
                    message="Installed files changed - update blocked; files preserved";return false;
                }
                string expected=Convert.ToString(available[name]);
                if(!String.Equals(FileHash(Path.Combine(Package,name)),expected,StringComparison.OrdinalIgnoreCase)) {
                    message="Package files do not match - extract a fresh complete ZIP";return false;
                }
                if(!String.Equals(actual,expected,StringComparison.OrdinalIgnoreCase))different=true;
                if(installed.ContainsKey(name)&&receipt.ContainsKey("engineReleaseId")&&Convert.ToInt64(receipt["engineReleaseId"])>0)different=false;
            }
            message=different ? "Mod update available - close MSFS, then click Update" : "Installed - files are up to date";
            if(receipt.ContainsKey("engineReleaseId")&&Convert.ToInt64(receipt["engineReleaseId"])>0)message="Installed official "+(addon?"add-on":"DLL")+" v"+Convert.ToString(receipt["engineVersion"])+" — selected version kept";
            return different;
        } catch { return false; }
    }
    async Task Detect()
    {
        busy=true; RefreshState(); status.Text="Finding MSFS 2024...";
        try {
            string detected=null;
            try { detected=await Task.Run(()=>SimulatorDiscovery.Find(null)); } catch(Exception) { }
            string saved=Path.Combine(Package,"SimulatorDirectory.txt");
            if(File.Exists(saved)) { var p=File.ReadAllText(saved).Trim(); if(File.Exists(Path.Combine(p,"FlightSimulator2024.exe"))) folder.Text=p; }
            if(Target.Length==0 && detected!=null) folder.Text=detected;
        } finally { busy=false; RefreshState();RestorePage();timer.Start(); }
    }
    async Task SetupSelectedEngine() {
        var choice=engineVersions.SelectedItem as UpstreamRelease.Choice;
        if(choice==null)return;
        if(choice.Id==-1) { await SetupLatestEngine();return; }
        foreach(var process in Process.GetProcessesByName("FlightSimulator2024")) { process.Dispose();ParentDialogPosition.Show(this,"Close MSFS before changing the installed fix.",Text);return; }
        string version=choice.Id==0?UpstreamRelease.Version:choice.Tag.Substring(1);
        if(version=="1.0.0"||(latestEngine!=null&&StandaloneAppUpdate.ParseVersion(version)<StandaloneAppUpdate.ParseVersion(latestEngine.Tag.Substring(1)))) {
            if(ParentDialogPosition.Show(this,"Install older official fix v"+version+" for rollback?\n\nOlder versions can contain known crashes. Version 1.0.0 can crash when loading FSLabs aircraft. The normal Install/Update button uses the latest release.",Text,MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        }
        if(choice.Id==0) { await Setup(false,null,true);return; }
        busy=true;RefreshState();status.Text="Downloading and verifying "+choice.Tag+"…";
        UpstreamRelease.Prepared selected=null;
        try { selected=await Task.Run(()=>UpstreamRelease.Load(Package,choice.Id,true,choice)); }
        catch(Exception e) { ParentDialogPosition.Show(this,e.Message,Text); }
        finally { busy=false;RefreshState(); }
        if(selected!=null)await Setup(false,selected,true);
    }
    async Task SetupLatestEngine() {
        foreach(var process in Process.GetProcessesByName("FlightSimulator2024")) { process.Dispose();ParentDialogPosition.Show(this,"Close MSFS before changing the installed fix.",Text);return; }
        busy=true;RefreshState();status.Text="Checking latest release and its SHA-256 checksums…";
        UpstreamRelease.Prepared selected=null;
        try {
            NativeInstaller.CheckReShadeConflicts(Target,AddonMode,CurrentReceipt());
            bool addon=AddonMode;
            selected=await Task.Run(()=> { var latest=UpstreamRelease.Latest(addon);return UpstreamRelease.Load(Package,latest.Id,true,latest); });
        } catch(Exception e) { ParentDialogPosition.Show(this,e.Message,Text); }
        finally { busy=false;RefreshState(); }
        if(selected!=null)await Setup(false,selected,true);
    }
    async Task Setup(bool remove,UpstreamRelease.Prepared selected=null,bool force=false)
    {
        if(!remove) { RefreshState(); if(!force&&!install.Enabled)return; }
        bool replace=false;
        bool addon=AddonMode;
        if(!remove) {
            NativeInstaller.Receipt old=null;
            if(File.Exists(Receipt))try { old=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<NativeInstaller.Receipt>(File.ReadAllText(Receipt)); } catch { }
            if(old!=null&&NativeInstaller.UsesAddon(old)!=addon) {
                if(ParentDialogPosition.Show(this,"Switch to "+(addon?"ReShade add-on":"standalone DLL")+"?\n\nThe managed copy of the other mode will be removed. Your glass settings and original backups will be kept."+(addon?"\n\nReShade with add-on support must already be installed separately.":""),Text,MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
            }
            string conflicts=String.Join(", ",Array.FindAll(addon?NativeInstaller.AddonPayload:Payload,n=>File.Exists(Path.Combine(Target,n))&&(old==null||old.files==null||!old.files.ContainsKey(n))));
            if(conflicts.Length>0) {
                if(ParentDialogPosition.Show(this,"Back up and replace these existing mod files?\n\n"+conflicts+"\n\nUninstall restores the backups.",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes) return;
                replace=true;
            }
        }
        busy=true; RefreshState(); status.Text=remove ? "Uninstalling...":"Installing...";
        string target=Target;bool succeeded=false;
        try {
            bool elevate=false;
            try {
                string message=await Task.Run(()=>NativeInstaller.Run(Package,target,remove,replace,null,selected,addon));
                succeeded=true;ParentDialogPosition.Show(this,message,Text,MessageBoxButtons.OK,MessageBoxIcon.Information);
            } catch(UnauthorizedAccessException) { elevate=true; }
            if(elevate) {
                if(ParentDialogPosition.Show(this,"Windows requires administrator permission to change files in this folder. Continue?",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
                var p=new ProcessStartInfo(Application.ExecutablePath,(remove ? "--uninstall ":"--install ")+"\""+target.TrimEnd('\\')+"\""+(replace ? " --replace":"")+(remove?"":(addon?" --addon":"")+(selected==null?" --bundled":" --engine "+selected.Release.Id))) {
                    UseShellExecute=true,Verb="runas",WorkingDirectory=Package };
                using(var child=Process.Start(p)) { await Task.Run(()=>child.WaitForExit());succeeded=child.ExitCode==0; }
            }
        } catch(Exception e) { ParentDialogPosition.Show(this,e.Message,Text); }
        finally { busy=false; RefreshState();if(succeeded)SelectPage(remove?"Setup":"Settings",true); }
    }
    static void RememberFonts(Control control,System.Collections.Generic.Dictionary<Control,Font> fonts) {
        fonts[control]=control.Font;foreach(Control child in control.Controls)RememberFonts(child,fonts);
    }
    static void ApplyTestFonts(System.Collections.Generic.Dictionary<Control,Font> fonts,float scale) {
        foreach(var item in fonts)item.Key.Font=new Font(item.Value.FontFamily,item.Value.Size*scale,item.Value.Style);
    }
    static void Check(bool condition,string message) { if(!condition)throw new Exception(message); }
    void Preview(string path) {
        Application.DoEvents();using(var b=new Bitmap(Width,Height)) { DrawToBitmap(b,new Rectangle(Point.Empty,Size));b.Save(path); }
    }
    void CheckHeaderLayout() {
        foreach(Control button in new Control[]{darkTheme,lightTheme}) {
            Check(button.ClientSize.Height>=TextRenderer.MeasureText(button.Text,button.Font).Height+8,"Theme button is too short for its text");
            Rectangle bounds=button.RectangleToScreen(button.ClientRectangle);
            for(Control parent=button.Parent;parent!=null;parent=parent.Parent)
                Check(parent.RectangleToScreen(parent.ClientRectangle).Contains(bounds),"Theme button clipped by header layout");
        }
        Check(darkTheme.Top==lightTheme.Top&&darkTheme.Height==lightTheme.Height,"Theme buttons are not aligned");
        foreach(Control line in new Control[]{credit,appUpdate}) {
            Check(line.Height>=TextRenderer.MeasureText("Agyp",line.Font).Height+4,"Footer text or underline can be clipped");
            Rectangle bounds=line.RectangleToScreen(line.ClientRectangle);
            for(Control parent=line.Parent;parent!=null;parent=parent.Parent)
                Check(parent.RectangleToScreen(parent.ClientRectangle).Contains(bounds),"Footer line clipped by parent");
        }
    }
    void CheckDropdownLayout() {
        foreach(ComboBox dropdown in new[]{installMode,engineVersions}) {
            Check(dropdown.Height>=dropdown.PreferredHeight,"Dropdown is shorter than its native preferred height");
            Rectangle bounds=dropdown.RectangleToScreen(dropdown.ClientRectangle);
            // Scroll viewports may intentionally hide a row; its own layout containers must not.
            for(Control parent=dropdown.Parent;parent!=setupScroll&&parent!=null;parent=parent.Parent)
                Check(parent.RectangleToScreen(parent.ClientRectangle).Contains(bounds),dropdown.AccessibleName+" dropdown "+bounds+" clipped by "+parent.GetType().Name+" "+parent.RectangleToScreen(parent.ClientRectangle)+"; font="+dropdown.Font.Size+", preferred="+dropdown.PreferredHeight);
            var table=dropdown.Parent as TableLayoutPanel;
            if(table!=null) {
                int row=table.GetRow(dropdown);
                Check(table.GetRowHeights()[row]>=dropdown.Height+dropdown.Margin.Vertical,"Dropdown exceeds its allocated table row");
            }
        }
    }
    int TestPages(string target,string preview,bool addon) {
        Check(File.Exists(Path.Combine(target,"CONTROL-TEST-ONLY")),"Missing fixture marker");
        folder.Text=target;RefreshState();RestorePage();
        latestEngine=new UpstreamRelease.Choice { Id=416640528,Tag="v1.0.1",Sha256=addon?UpstreamRelease.AddonHash:UpstreamRelease.ZipHash,Addon=addon };
        PopulateEngineChoices(new[]{latestEngine});
        StartPosition=FormStartPosition.Manual;Location=new Point(-10000,-10000);ShowInTaskbar=false;Show();Application.DoEvents();
        SelectPage("Settings",false);RefreshState();Preview(preview+".initial.png");
        Check(AddonMode==addon&&glass.ReShadeMode==addon,"Installed mode detection");
        Check(!install.Enabled&&uninstall.Enabled,"Matching installation should not offer replacement");
        Check(FollowLatest&&engineVersions.SelectedIndex==0,"Latest is the default selection");
        Check(glass.Enabled==!(addon&&SimulatorRunning()),"Running add-on editing guard");
        byte[] originalIni=File.ReadAllBytes(Ini),receipt=File.ReadAllBytes(Receipt);
        // Discovery must draw attention once, without stealing navigation on each timer tick.
        var currentRelease=latestEngine;
        var updateRelease=new UpstreamRelease.Choice { Id=43,Tag="v1.0.2",Sha256=new string('c',64),Addon=addon };
        latestEngine=updateRelease;SelectPage("Settings",true);RefreshState();
        Check(currentPage=="Setup"&&install.Enabled&&install.Text=="Update to v1.0.2","New official release must open Setup with a clear update action");
        Check(File.ReadAllText(Path.Combine(Package,"Navigation.txt"))=="Settings","Update attention must not overwrite the remembered page");
        foreach(bool light in new[]{false,true}) {
            SelectAppearance(light);RefreshState();
            Check(install.BackColor==WsgTheme.Mint,"Available update must use the primary accent in both themes");
            Preview(preview+"."+(light?"light":"dark")+"-update-available.png");
        }
        SelectPage("Settings",false);RefreshState();RefreshState();
        Check(currentPage=="Settings","Repeated polling must not steal navigation");
        latestEngine=new UpstreamRelease.Choice { Id=44,Tag="v1.0.3",Sha256=new string('d',64),Addon=addon };
        busy=true;RefreshState();Check(currentPage=="Settings","Busy operation must defer update navigation");busy=false;
        using(var modal=new Form { StartPosition=FormStartPosition.Manual,Location=Location,ShowInTaskbar=false }) {
            bool deferred=false;
            modal.Shown+=delegate { RefreshState();deferred=currentPage=="Settings";modal.Close(); };
            modal.ShowDialog(this);Check(deferred,"Modal dialog must defer update navigation");
        }
        engineVersions.DroppedDown=true;RefreshState();
        Check(currentPage=="Settings","Open dropdown must defer update navigation");engineVersions.DroppedDown=false;
        RefreshState();Check(currentPage=="Setup","Deferred update must appear when interaction finishes");
        SelectPage("Settings",false);engineVersions.SelectedIndex=1;
        latestEngine=new UpstreamRelease.Choice { Id=45,Tag="v1.0.4",Sha256=new string('e',64),Addon=addon };
        RefreshState();Check(currentPage=="Settings"&&engineVersions.SelectedIndex==1&&install.Text=="Install selected version","Explicit rollback choice must remain intact");
        latestEngine=currentRelease;engineVersions.SelectedIndex=0;RefreshState();
        Check(currentPage=="Settings"&&!install.Enabled,"Matching release must not prompt for an update");
        latestEngine=null;RefreshState();Check(currentPage=="Settings"&&!install.Enabled,"Offline discovery must not prompt");
        latestEngine=new UpstreamRelease.Choice { Id=46,Tag="v1.0.0",Sha256=new string('f',64),Addon=addon };
        RefreshState();Check(currentPage=="Settings"&&!install.Enabled,"Older latest release must not prompt a downgrade");
        latestEngine=new UpstreamRelease.Choice { Id=47,Tag="v1.0.5",Sha256=new string('f',64),Addon=!addon };
        RefreshState();Check(currentPage=="Settings"&&!install.Enabled,"Other mode's release must not prompt an update");
        latestEngine=currentRelease;
        try {
            var updated=CurrentReceipt();updated.engineVersion="1.0.2";
            File.WriteAllText(Receipt,new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(updated));RefreshState();
            Check(glass.Enabled==!(addon&&SimulatorRunning()),"Official 1.0.2 settings must remain editable");
            Check(!glass.ShortcutRequiresUpdate,"Official 1.0.2 must allow current shortcut rules");
        } finally { File.WriteAllBytes(Receipt,receipt);RefreshState(); }
        Check(Convert.ToBase64String(originalIni)==Convert.ToBase64String(File.ReadAllBytes(Ini)),"Update discovery wrote simulator settings");
        // Invalid receipts and interrupted transactions must stay on Setup without a crash.
        try {
            File.WriteAllText(Receipt,"invalid receipt");RefreshState();RestorePage();
            Check(currentPage=="Setup"&&!glass.Visible&&!install.Enabled,"Invalid receipt must offer recovery, not settings");
            File.WriteAllBytes(Receipt,receipt);
            string journal=Path.Combine(Target,NativeInstaller.TransactionName);Directory.CreateDirectory(journal);
            try { RefreshState();RestorePage();Check(currentPage=="Setup"&&!glass.Visible,"Pending recovery must start on Setup"); }
            finally { Directory.Delete(journal); }
        } finally { File.WriteAllBytes(Receipt,receipt);RefreshState(); }
        Size fixedSize=Size;
        foreach(string page in new[]{"Setup","Settings","Setup"}) {
            SelectPage(page,true);RefreshState();Check(currentPage==page,"Page unexpectedly changed");
            Check(Size==fixedSize,"Navigation resized the frame");
        }
        using(var reopened=new GlassWindow()) { reopened.folder.Text=target;reopened.RestorePage();Check(reopened.currentPage=="Setup","Remembered page did not restore"); }
        Check(Convert.ToBase64String(originalIni)==Convert.ToBase64String(File.ReadAllBytes(Ini)),"Navigation wrote simulator settings");
        SelectPage("Setup",false);engineVersions.Focus();engineVersions.DroppedDown=true;Application.DoEvents();
        Check(engineVersions.DroppedDown,"Version dropdown did not open");
        for(int tick=0;tick<3;tick++) { RefreshState();Application.DoEvents();Check(engineVersions.DroppedDown,"Refresh closed dropdown"); }
        int items=engineVersions.Items.Count;
        var future=new UpstreamRelease.Choice { Id=42,Tag="v1.9.0",Sha256=new string('b',64),Addon=addon };
        PopulateEngineChoices(new[]{latestEngine,future});appUpdate.Text="App update unavailable; current version kept.";Application.DoEvents();
        Check(engineVersions.Items.Count==items&&engineVersions.DroppedDown&&Size==fixedSize,"Background check changed open selection or frame");
        engineVersions.DroppedDown=false;
        var pendingWait=Stopwatch.StartNew();
        while(pendingEngineChoices!=null&&pendingWait.ElapsedMilliseconds<1000) { Application.DoEvents();System.Threading.Thread.Sleep(5); }
        Check(pendingEngineChoices==null&&engineVersions.Items.Count==3&&FollowLatest,"Deferred choices lost selection");
        engineVersions.SelectedIndex=2;RefreshState();Check(install.Enabled&&install.Text=="Install selected version","Explicit version selection action");
        Check(Convert.ToBase64String(originalIni)==Convert.ToBase64String(File.ReadAllBytes(Ini)),"Version choice wrote settings");
        engineVersions.SelectedIndex=0;
        installMode.SelectedIndex=addon?0:1;engineVersions.SelectedIndex=1;RefreshState();
        Check(install.Enabled&&install.Text=="Switch mode","Mode switch action");
        SelectPage("Settings",false);Check(glass.ReShadeMode==addon,"Pending selection must not change installed settings mode");
        installMode.SelectedIndex=addon?1:0;engineVersions.SelectedIndex=1;RefreshState();Check(!install.Enabled,"Same bundled version needs no replacement");
        // Corruption blocks replacement and leaves recovery available.
        string binary=Path.Combine(target,addon?UpstreamRelease.AddonName:"dxgi.dll");byte[] bytes=File.ReadAllBytes(binary);
        try { File.AppendAllText(binary,"modified");fileHashes.Clear();RefreshState();Check(!install.Enabled&&uninstall.Enabled&&status.Text.Contains("changed"),"Modified binary guard"); }
        finally { File.WriteAllBytes(binary,bytes);fileHashes.Clear();RefreshState(); }
        if(!addon) { SelectPage("Settings",false);glass.SelfTest(preview); }
        foreach(bool light in new[]{false,true}) {
            SelectAppearance(light);RefreshState();
            foreach(string page in new[]{"Setup","Settings"}) { SelectPage(page,false);Preview(preview+"."+(light?"light":"dark")+"-"+page+".png"); }
            if(!addon)glass.ReviewLayout(preview+(light?".light":".dark"));
        }
        Check(Convert.ToBase64String(originalIni)==Convert.ToBase64String(File.ReadAllBytes(Ini)),"UI checks changed settings");
        try {
            File.Delete(Receipt);RefreshState();RestorePage();Check(currentPage=="Setup","Missing installation starts on Setup");
            engineVersions.SelectedIndex=1;RefreshState();Check(install.Enabled&&!uninstall.Enabled,"Bundled installation should work offline");
            SelectPage("Settings",false);Check(emptySettings.Visible&&!glass.Visible&&!enabled.Visible,"Empty settings state");
            Preview(preview+".uninstalled.png");
        } finally { File.WriteAllBytes(Receipt,receipt);RefreshState(); }
        foreach(float scale in new[]{1f,1.25f,1.5f,2f})using(var scaled=new GlassWindow()) {
            scaled.folder.Text=target;scaled.installMode.SelectedIndex=addon?1:0;scaled.RefreshState();scaled.StartPosition=FormStartPosition.Manual;scaled.Location=new Point(-10000,-10000);scaled.ShowInTaskbar=false;scaled.Show();
            scaled.Scale(new SizeF(scale,scale));scaled.FitWorkingArea(new Rectangle(0,0,1920,1040));scaled.Location=new Point(-10000,-10000);
            Check(scaled.Height<=1040&&scaled.Width<=1920&&scaled.FormBorderStyle==FormBorderStyle.FixedDialog,"Scaled frame must fit without becoming resizable");
            Size scaledSize=scaled.Size;
            var fonts=new System.Collections.Generic.Dictionary<Control,Font>();RememberFonts(scaled,fonts);
            foreach(bool light in new[]{false,true})foreach(string page in new[]{"Setup","Settings"}) {
                scaled.SelectAppearance(light);ApplyTestFonts(fonts,scale);scaled.SelectPage(page,false);scaled.RefreshState();scaled.Preview(preview+"."+page+"-"+(light?"light":"dark")+"-"+(int)(scale*100)+".png");
                scaled.CheckHeaderLayout();if(page=="Setup")scaled.CheckDropdownLayout();
                if(page=="Setup")foreach(Control button in new Control[]{scaled.install,scaled.uninstall}) {
                    Rectangle bounds=button.RectangleToScreen(button.ClientRectangle);
                    for(Control parent=button.Parent;parent!=null;parent=parent.Parent)
                        Check(parent.RectangleToScreen(parent.ClientRectangle).Contains(bounds),"Setup action is clipped");
                }
                Check(scaled.Size==scaledSize,"Status changed frame size");
                var view=page=="Setup"?scaled.setupScroll:scaled.settingsPage;
                Check(!view.HorizontalScroll.Visible,"Horizontal scrolling is not acceptable");
                if(scale==1f&&page=="Setup"&&!addon)Check(!view.VerticalScroll.Visible,"Normal standalone Setup should fit without scrolling");
            }
            scaled.SelectPage("Settings",false);if(scaled.glass.Enabled)scaled.glass.ReviewLayout(preview+".scale-"+(int)(scale*100));
            if(scale==2f) {
                scaled.FitWorkingArea(new Rectangle(0,0,1280,720));scaled.Location=new Point(-10000,-10000);
                foreach(string page in new[]{"Setup","Settings"}) {
                    scaled.SelectPage(page,false);scaled.Preview(preview+".small-screen-"+page+".png");scaled.CheckHeaderLayout();if(page=="Setup")scaled.CheckDropdownLayout();
                    Check(!(page=="Setup"?scaled.setupScroll:scaled.settingsPage).HorizontalScroll.Visible,"Small screen requires horizontal scrolling");
                }
                if(scaled.glass.Enabled)scaled.glass.ReviewLayout(preview+".small-screen");
            }
        }
        string placement=Path.Combine(Package,"WindowPlacement.json");Rectangle saved=new Rectangle(80,90,820,820),loaded;
        WindowPlacement.Save(placement,saved);Check(WindowPlacement.TryRead(placement,out loaded)&&saved==loaded,"Position round-trip");
        Check(ParentDialogPosition.Center(new Rectangle(300,200,660,680),new Size(400,260),new Rectangle(0,0,1920,1080))==new Point(430,410),"Centered dialog regression");
        SelectPage("Settings",true);return 0;
    }
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try {
            if(args.Length>0 && (args[0]=="--apply-app-update" || args[0]=="--recover-app-update")) return StandaloneAppUpdate.Helper(args);
            if(args.Length>=2 && (args[0]=="--install" || args[0]=="--uninstall")) {
                bool replace=false,bundled=false,addon=false;long engineId=0;
                for(int i=2;i<args.Length;i++) {
                    if(args[i]=="--replace"&&!replace)replace=true;
                    else if(args[i]=="--bundled"&&!bundled)bundled=true;
                    else if(args[i]=="--addon"&&!addon)addon=true;
                    else if(args[i]=="--engine"&&engineId==0&&i+1<args.Length&&Int64.TryParse(args[++i],out engineId)&&engineId>0) {}
                    else throw new ArgumentException("Invalid installer arguments.");
                }
                if((args[0]=="--uninstall"&&(engineId!=0||bundled||addon))||(engineId!=0&&bundled))throw new ArgumentException("Invalid engine selection.");
                UpstreamRelease.Prepared selected=null;
                if(engineId!=0)selected=UpstreamRelease.Load(Package,engineId,false,null,addon);
                else if(args[0]=="--install"&&!bundled) { var latest=UpstreamRelease.Latest(addon);selected=UpstreamRelease.Load(Package,latest.Id,true,latest); }
                string message=NativeInstaller.Run(Package,args[1],args[0]=="--uninstall",replace,null,selected,addon);
                MessageBox.Show(message,"Standalone glass cockpit",MessageBoxButtons.OK,MessageBoxIcon.Information);return 0;
            }
            using(var gate=StandaloneAppUpdate.Enter(Package)) {
                if(gate==null) { MessageBox.Show("The standalone app is already open or updating.","Standalone glass cockpit");return 0; }
                string recoveryError=null;
                if(Array.IndexOf(args,"--self-test")<0&&Array.IndexOf(args,"--self-test-addon")<0) {
                    try { if(StandaloneAppUpdate.Startup(Package))return 0; }
                    catch { recoveryError="App update recovery needs attention. Extract the complete ZIP again."; }
                }
            using(var form=new GlassWindow()) {
                if(args.Length==3&&(args[0]=="--self-test"||args[0]=="--self-test-addon"))return form.TestPages(args[1],args[2],args[0]=="--self-test-addon");
                form.RememberWindow();
                form.Shown += async delegate {
                    try {
                        form.setupTab.Focus();
                        await form.Detect();
                        // Set initial focus once; never steal it from a user or on background refresh.
                        if(form.ContainsFocus&&form.setupTab.Focused) {
                            if(form.currentPage=="Settings"&&form.enabled.Enabled)form.enabled.Focus();else form.browse.Focus();
                        }
                        form.CheckUpstream();
                        if(recoveryError!=null) { form.appUpdate.Text=recoveryError;return; }
                        bool restart=await StandaloneAppUpdate.CheckAsync(Package,
                            text=> { if(!form.IsDisposed)form.appUpdate.Text=text; },
                            ()=>!form.IsDisposed && !form.Disposing && !form.busy);
                        if(restart && !form.IsDisposed) { form.busy=false;form.Close(); }
                    } catch(Exception e) { if(!form.IsDisposed)ParentDialogPosition.Show(form,e.Message,form.Text); }
                };
                Application.Run(form);
            }
            }
            return 0;
        } catch(Exception e) { if(Array.IndexOf(args,"--self-test")>=0||Array.IndexOf(args,"--self-test-addon")>=0) Console.Error.WriteLine(e); else MessageBox.Show(e.Message,"Standalone glass cockpit");return 1; }
    }
}

// Per-package UI preference only; never reads or writes simulator settings.
internal static class WindowPlacement
{
    public sealed class Saved { public int X, Y, Width, Height; }
    internal static bool TryRead(string path,out Rectangle bounds) {
        bounds=Rectangle.Empty;
        try {
            if(new FileInfo(path).Length>4096)return false;
            var saved=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Saved>(File.ReadAllText(path));
            if(saved==null || saved.Width<100 || saved.Height<100 || saved.Width>32768 || saved.Height>32768 || Math.Abs((long)saved.X)>1000000 || Math.Abs((long)saved.Y)>1000000)return false;
            bounds=new Rectangle(saved.X,saved.Y,saved.Width,saved.Height);return true;
        // JavaScriptSerializer can wrap conversion errors in a plain Exception.
        // A damaged optional UI preference must never prevent the app from opening.
        } catch(Exception) { }
        return false;
    }
    internal static Rectangle KeepVisible(Rectangle bounds,Rectangle area,Size minimum) {
        int width=Math.Min(area.Width,Math.Max(minimum.Width,bounds.Width));
        int height=Math.Min(area.Height,Math.Max(minimum.Height,bounds.Height));
        return new Rectangle(Math.Max(area.Left,Math.Min(bounds.X,area.Right-width)),Math.Max(area.Top,Math.Min(bounds.Y,area.Bottom-height)),width,height);
    }
    internal static void Save(string path,Rectangle bounds) {
        if(bounds.Width<100 || bounds.Height<100)return;
        try {
            var saved=new Saved { X=bounds.X,Y=bounds.Y,Width=bounds.Width,Height=bounds.Height };
            string temporary=path+".tmp";
            File.WriteAllText(temporary,new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(saved));
            if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
        } catch(IOException) { } catch(UnauthorizedAccessException) { }
    }
}
