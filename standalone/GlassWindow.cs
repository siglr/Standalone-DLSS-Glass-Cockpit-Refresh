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
    readonly Label engineLabel = new WsgLabel { Text="DLL version" };
    readonly ComboBox engineVersions=new WsgComboBox { DropDownStyle=ComboBoxStyle.DropDownList };
    readonly Button applyEngine=new WsgButton { Text="Install selected DLL" };
    UpstreamRelease.Choice latestEngine;
    UpstreamRelease.Choice[] pendingEngineChoices;
    string footerLayoutKey;
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
    bool busy, refreshing;
    string Target { get { return folder.Text; } }
    string Ini { get { return Path.Combine(Target, "glass-cockpit-dlss-fix.ini"); } }
    string Receipt { get { return Path.Combine(Target, ".StandaloneGlass-install.json"); } }
    static readonly string[] Payload = NativeInstaller.Payload;
    GlassWindow()
    {
        try { WsgTheme.Light=File.ReadAllText(Path.Combine(Package,"Appearance.txt")).Trim()=="light"; }
        catch(IOException) { WsgTheme.Light=false; } catch(UnauthorizedAccessException) { WsgTheme.Light=false; }
        Text="Standalone-DLSS-Glass-Cockpit-Refresh v" + global::ProductVersion.Value;
        Font=new Font("Segoe UI",10); AutoScaleDimensions=new SizeF(96,96); AutoScaleMode=AutoScaleMode.Dpi;
        BackColor=WsgTheme.Background; ForeColor=WsgTheme.Text;
        brandIcon=WsgTheme.LoadIcon(); Icon=brandIcon;
        brand=new PictureBox { Image=WsgTheme.LoadLogo(), SizeMode=PictureBoxSizeMode.Zoom, TabStop=false, AccessibleName="WeSimGlide logo" };
        brand.SetBounds(18,18,48,48);
#if GLASS_NO_AUTO_UPDATE
        Text += " (source build)";
#endif
        ClientSize=new Size(660,704); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false;
        StartPosition=FormStartPosition.CenterScreen;
        var title=new WsgLabel { Text="Glass Cockpit Refresh", Font=new Font("Segoe UI",18,FontStyle.Bold), AutoSize=false };
        title.SetBounds(78,14,420,34);
        var subtitle=new WsgLabel { Text="MSFS 2024  /  Desktop + VR", ForeColor=WsgTheme.Muted };
        subtitle.SetBounds(80,48,400,24);
        var folderLabel=new WsgLabel { Text="MSFS 2024 GAME FOLDER", Font=new Font("Segoe UI",8,FontStyle.Bold) };
        folderLabel.SetBounds(18,84,520,18);
        folder.SetBounds(18,106,518,28); browse.SetBounds(546,103,96,32);
        var about=new LinkLabel { Text="Help", TextAlign=ContentAlignment.MiddleCenter, AccessibleDescription="Show instructions and support guidance" };
        about.SetBounds(578,15,64,26);
        var website=new LinkLabel { Text="About", TextAlign=ContentAlignment.MiddleCenter, Left=510,Top=15,Width=64,Height=26 };
        website.LinkClicked+=delegate { OpenSupport("https://wesimglide.org/otherdata/tools/Standalone-DLSS-Glass-Cockpit-Refresh.html"); };
        Controls.Add(website);
        darkTheme.SetBounds(510,48,64,28); lightTheme.SetBounds(578,48,64,28);
        darkTheme.Click += delegate { SelectAppearance(false); };
        lightTheme.Click += delegate { SelectAppearance(true); };
        about.LinkClicked += delegate {
            ParentDialogPosition.Show(this,"Start MSFS normally. Glass changes apply during upscaling without leaving VR.\n\nClick the settings shortcut to record a new binding. Use it while MSFS has focus; the original window includes green tint and diagnostics. Choose a shortcut unused by your other tools.\n\nFrame generation requires the separate official OFXR Bridge.\n\nInstaller questions: use Installer support. Screen issues with the official DLL: use Screen issues.","Glass Cockpit Refresh — Help",MessageBoxButtons.OK,MessageBoxIcon.Information);
        };
        status.SetBounds(18,141,625,24); install.SetBounds(18,172,130,34); uninstall.SetBounds(160,172,130,34);
        var installerHelp=new LinkLabel { Text="Installer support", Left=316,Top=179,Width=162,Height=24 };
        var screenHelp=new LinkLabel { Text="Screen issues", Left=482,Top=179,Width=160,Height=24 };
        installerHelp.LinkClicked += delegate { OpenSupport("https://github.com/siglr/Standalone-DLSS-Glass-Cockpit-Refresh/issues"); };
        screenHelp.LinkClicked += delegate { OpenSupport(UpstreamRelease.Repository+"/issues"); };
        Controls.AddRange(new Control[]{installerHelp,screenHelp});
        enabled.SetBounds(20,223,610,30);
        note.Font=new Font("Segoe UI",9);
        note.SetBounds(18,551,625,36);
        appUpdate.Font=new Font("Segoe UI",8.5f);
        credit.Text="Installer: siglr / WSG · Glass Cockpit DLSS Fix by IFreemz";
        credit.Font=new Font("Segoe UI",8.5f);credit.AccessibleDescription="Separate installer by siglr and official GPL-3 graphics DLL by IFreemz";
        credit.SetBounds(18,589,625,20);
        credit.LinkClicked += delegate {
            try { Process.Start(new ProcessStartInfo("https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix") { UseShellExecute=true }); }
            catch(Exception e) { ParentDialogPosition.Show(this,e.Message,Text); }
        };
        Controls.Add(credit);
        upstream.SetBounds(18,636,625,20);upstream.Font=new Font("Segoe UI",8.5f);
        upstream.Text="Checking official DLL updates…";
        upstream.LinkClicked += delegate { try { Process.Start(new ProcessStartInfo(upstreamUrl) { UseShellExecute=true }); } catch(Exception e) { ParentDialogPosition.Show(this,e.Message,Text); } };
        Controls.Add(upstream);
        engineLabel.SetBounds(18,666,98,24);
        engineVersions.SetBounds(120,661,265,30);applyEngine.SetBounds(399,658,243,34);
        engineVersions.Items.Add(new UpstreamRelease.Choice { Id=0,Tag="v"+UpstreamRelease.Version });engineVersions.SelectedIndex=0;
        engineVersions.SelectedIndexChanged += delegate { RefreshState(); };
        engineVersions.DropDownClosed += delegate {
            // Let the native combo finish committing its selection before rebuilding anything.
            BeginInvoke(new Action(delegate {
                if(IsDisposed || engineVersions.DroppedDown)return;
                if(pendingEngineChoices!=null) {
                    var selected=engineVersions.SelectedItem as UpstreamRelease.Choice;
                    var choices=pendingEngineChoices;pendingEngineChoices=null;
                    PopulateEngineChoices(choices);
                    if(selected!=null)for(int i=0;i<engineVersions.Items.Count;i++) {
                        var item=(UpstreamRelease.Choice)engineVersions.Items[i];
                        if(item.Id==selected.Id && item.Tag==selected.Tag) { engineVersions.SelectedIndex=i;break; }
                    }
                }
                RefreshState();
            }));
        };
        applyEngine.Click += async delegate { await SetupSelectedEngine(); };
        Controls.AddRange(new Control[]{engineLabel,engineVersions,applyEngine});
        appUpdate.SetBounds(18,613,625,20);
        Controls.AddRange(new Control[] { brand,title,subtitle,folderLabel,about,darkTheme,lightTheme,folder,browse,status,install,uninstall,enabled,note,appUpdate });
        subtitle.Tag=folderLabel.Tag=note.Tag=appUpdate.Tag="muted";
        glass = new GlassSettingsPanel(() => Ini);
        glass.SetBounds(18, 266, 624, 270); Controls.Add(glass); RefreshAppearance();
        Control[] navigation={website,about,darkTheme,lightTheme,folder,browse,install,uninstall,installerHelp,screenHelp,enabled,glass,credit,upstream,engineVersions,applyEngine};
        for(int i=0;i<navigation.Length;i++)navigation[i].TabIndex=i;
        folder.AccessibleName="MSFS 2024 game folder";
        engineVersions.AccessibleName="Official DLL version";
        engineVersions.AccessibleDescription="Choose an official version to install or roll back to. Selecting alone does not install it.";
        install.AccessibleDescription="Install or update to the latest verified official DLL.";
        applyEngine.AccessibleDescription="Install the version selected in the DLL version list, including an older version for rollback.";
        hints.SetToolTip(install,install.AccessibleDescription);hints.SetToolTip(applyEngine,applyEngine.AccessibleDescription);
        hints.SetToolTip(engineVersions,engineVersions.AccessibleDescription);
        folder.TextChanged+=delegate { hints.SetToolTip(folder,folder.Text); };
        status.AutoEllipsis=true; status.TextChanged+=delegate { hints.SetToolTip(status,status.Text); PaintStatus(); };
        appUpdate.AutoEllipsis=true; appUpdate.TextChanged+=delegate { hints.SetToolTip(appUpdate,appUpdate.Text); };
        appUpdate.TextChanged+=delegate { LayoutFooter(); }; LayoutFooter();
        foreach(Control control in new Control[]{website,about,darkTheme,lightTheme,browse})control.Anchor=AnchorStyles.Top|AnchorStyles.Right;
        folder.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        HandleCreated += delegate { WsgTheme.TitleBar(this); };
        browse.Click += delegate { using(var d=new FolderBrowserDialog { SelectedPath=Target,Description="Choose the folder containing FlightSimulator2024.exe (Xbox Content or Steam game folder)." }) {
            using(var position=new ParentDialogPosition(this))
                if(d.ShowDialog(this)!=DialogResult.OK) return;
            if(!File.Exists(Path.Combine(d.SelectedPath,"FlightSimulator2024.exe"))) { ParentDialogPosition.Show(this,"Choose the folder containing FlightSimulator2024.exe.",Text);return; }
            folder.Text=d.SelectedPath;
            try { File.WriteAllText(Path.Combine(Package,"SimulatorDirectory.txt"),Target); } catch(Exception e) { ParentDialogPosition.Show(this,e.Message,Text); }
            RefreshState();
        }};
        enabled.CheckedChanged += delegate {
            if(refreshing) return;
            if(!File.Exists(Receipt) || !File.Exists(Ini) || !WritePrivateProfileString("overlay","enabled",enabled.Checked ? "1":"0",Ini))
                ParentDialogPosition.Show(this,"Could not save the setting. Check settings file write access.",Text);
            RefreshState();
        };
        install.Click += async delegate { await SetupLatestEngine(); };
        uninstall.Click += async delegate { await Setup(true); };
        timer.Tick += delegate { RefreshState(); };
        FormClosing += delegate(object sender,FormClosingEventArgs e) { if(busy) e.Cancel=true; };
        FormClosed += delegate { timer.Dispose(); hints.Dispose(); brand.Image.Dispose(); brandIcon.Dispose(); };
    }
    void LayoutFooter() {
        if(engineVersions.DroppedDown)return;
        float scale=glass.Height/270f;
        Func<int,int> px=value=>(int)Math.Round(value*scale);
        int y=glass.Bottom+px(12);
        bool showNote=note.Text.Length>0;
        bool showUpdate=appUpdate.Text.Length>0 && !appUpdate.Text.StartsWith("Source build:");
        string layoutKey=glass.Bounds.ToString()+"|"+showNote+"|"+showUpdate;
        if(footerLayoutKey==layoutKey)return;
        footerLayoutKey=layoutKey;
        note.Visible=showNote;
        if(showNote) { note.SetBounds(px(18),y,px(625),px(24));y+=px(28); }
        credit.SetBounds(px(18),y,px(625),px(20));y+=px(26);
        // Development mode is already identified in the title. Keep actionable update status visible.
        appUpdate.Visible=showUpdate;
        if(showUpdate) { appUpdate.SetBounds(px(18),y,px(625),px(20));y+=px(24); }
        upstream.SetBounds(px(18),y,px(625),px(20));y+=px(26);
        engineLabel.SetBounds(px(18),y+px(6),px(98),px(24));engineVersions.SetBounds(px(120),y+px(1),px(265),px(30));applyEngine.SetBounds(px(399),y-px(2),px(243),px(34));
        if(ClientSize.Height!=y+px(44))ClientSize=new Size(ClientSize.Width,y+px(44));
    }
    void RememberWindow() {
        footerLayoutKey=null;LayoutFooter();
        string path=Path.Combine(Package,"WindowPlacement.json");
        Load+=delegate {
            Rectangle saved;
            if(WindowPlacement.TryRead(path,out saved)) {
                StartPosition=FormStartPosition.Manual;
                // Keep the designed size, including when an earlier build saved resized bounds.
                Location=WindowPlacement.KeepVisible(new Rectangle(saved.Location,Size),Screen.FromRectangle(saved).WorkingArea,Size).Location;
            }
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
        try {
            var releases=await Task.Run(()=>Tuple.Create(UpstreamRelease.Latest(),UpstreamRelease.List()));
            if(!IsDisposed) {
                latestEngine=releases.Item1;
                PopulateEngineChoices(releases.Item2);
                upstreamUrl=UpstreamRelease.Repository+"/releases";
            }
        } catch { if(!IsDisposed) { latestEngine=null;upstream.Text="Could not verify the latest DLL — retry later"; } }
    }
    void PopulateEngineChoices(UpstreamRelease.Choice[] choices) {
        if(engineVersions.DroppedDown) { pendingEngineChoices=choices;return; }
        NativeInstaller.Receipt receipt=null;
        try { receipt=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<NativeInstaller.Receipt>(File.ReadAllText(Receipt)); } catch { }
        engineVersions.Items.Clear();
        engineVersions.Items.Add(new UpstreamRelease.Choice { Id=0,Tag="v"+UpstreamRelease.Version,
            IsLatest=latestEngine!=null&&latestEngine.Tag=="v"+UpstreamRelease.Version&&latestEngine.Sha256==UpstreamRelease.ZipHash });
        int selected=0;
        foreach(var choice in choices) {
            // The identical bundled release is already available offline; do not offer a duplicate download.
            if(choice.Tag=="v"+UpstreamRelease.Version&&choice.Sha256==UpstreamRelease.ZipHash)continue;
            choice.IsLatest=latestEngine!=null&&choice.Id==latestEngine.Id;
            int index=engineVersions.Items.Add(choice);
            if(choice.IsLatest)selected=index;
        }
        engineVersions.SelectedIndex=selected;
        string version=UpstreamRelease.Version,hash=UpstreamRelease.ZipHash;
        if(receipt!=null&&!String.IsNullOrEmpty(receipt.engineVersion)) { version=receipt.engineVersion;hash=receipt.engineZipHash; }
        var newest=choices.Length>0?choices[0]:null;
        bool newer=false;
        try { newer=newest!=null&&(StandaloneAppUpdate.ParseVersion(newest.Tag.Substring(1))>StandaloneAppUpdate.ParseVersion(version)||newest.Tag=="v"+version&&newest.Sha256!=hash); } catch { }
        upstream.Text=choices.Length==0 ? "No verified releases found" : newer ? "New official DLL available" : "Official DLL is up to date";
        RefreshState();
    }
    void RefreshAppearance() {
        BackColor=WsgTheme.Background; ForeColor=WsgTheme.Text;
        WsgTheme.Apply(this); glass.ApplyTheme();
        foreach(Control control in Controls) if(Equals(control.Tag,"muted"))control.ForeColor=WsgTheme.Muted;
        PaintStatus();
        WsgTheme.SelectButton(darkTheme,!WsgTheme.Light); WsgTheme.SelectButton(lightTheme,WsgTheme.Light);
        if(IsHandleCreated)WsgTheme.TitleBar(this);
        Invalidate(true);
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
    void RefreshState()
    {
        bool found=Target.Length>0 && File.Exists(Path.Combine(Target,"FlightSimulator2024.exe"));
        bool present=found && File.Exists(Receipt);
        bool other=found && File.Exists(Path.Combine(Target,".MyOwnFGVRMod-install.json"));
        bool legacy=present && File.Exists(Path.Combine(Target,"StandaloneGlassGraphics.dll")) && File.Exists(Path.Combine(Target,"StandaloneGlass.ini"));
        bool complete=present && Array.TrueForAll(Payload,n=>File.Exists(Path.Combine(Target,n)));
        bool knownSettings=false;
        if(complete)try {
            var receipt=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<NativeInstaller.Receipt>(File.ReadAllText(Receipt));
            knownSettings=receipt.engineVersion=="1.0.0"||receipt.engineVersion=="1.0.1";
        } catch { }
        note.Text=complete&&!knownSettings ? "Settings format not verified — use the original in-sim window." : "";
        LayoutFooter();
        bool recovery=found && (Directory.Exists(Path.Combine(Target,NativeInstaller.TransactionName)) || File.Exists(Path.Combine(Target,".StandaloneGlass-backup",".update.json")));
        string installedStatus="";
        bool needsUpdate=false;
        if(recovery) { needsUpdate=true; installedStatus="Update interrupted - close MSFS, then click Update to recover"; }
        else if(complete) needsUpdate=NeedsInstalledUpdate(out installedStatus);
        else if(legacy) { needsUpdate=true; installedStatus="Update available — migrate to the official DLL; keep settings"; }
        if(complete&&!needsUpdate&&latestEngine!=null&&(installedStatus.StartsWith("Installed -")||installedStatus.StartsWith("Installed official"))) {
            try {
                var receipt=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<NativeInstaller.Receipt>(File.ReadAllText(Receipt));
                if(receipt.engineZipHash!=latestEngine.Sha256) { needsUpdate=true;installedStatus="Latest official DLL "+latestEngine.Tag+" available — click Update"; }
            } catch { }
        }
        install.Enabled=!busy && found && (!present || needsUpdate) && !other; uninstall.Enabled=!busy && (present || recovery);
        install.Text=present || recovery ? "Update" : "Install";
        browse.Enabled=!busy; enabled.Enabled=!busy && complete && knownSettings;
        engineVersions.Enabled=!busy;applyEngine.Enabled=!busy&&found&&!other&&SelectedEngineNeedsInstall();
        glass.RefreshValues(!busy && complete && knownSettings);
        refreshing=true; enabled.Checked=complete && GetPrivateProfileInt("overlay","enabled",1,Ini)!=0; refreshing=false;
        if(!busy) status.Text=other ? "Remove the conflicting mod using its own installer first."
            : complete || recovery || legacy ? installedStatus : present ? "Installation incomplete - use Uninstall to recover"
            : found ? "Ready to install - close MSFS first" : "Choose the MSFS 2024 game folder with Browse.";
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
    bool SelectedEngineNeedsInstall() {
        var choice=engineVersions.SelectedItem as UpstreamRelease.Choice;
        if(choice==null)return false;
        if(!File.Exists(Receipt))return true;
        try {
            var json=new System.Web.Script.Serialization.JavaScriptSerializer();
            var receipt=json.Deserialize<NativeInstaller.Receipt>(File.ReadAllText(Receipt));
            if(receipt.files==null||!receipt.files.ContainsKey("dxgi.dll"))return false;
            string dll=Path.Combine(Target,"dxgi.dll");
            if(!File.Exists(dll)||FileHash(dll)!=receipt.files["dxgi.dll"])return false;
            if(!receipt.files.ContainsKey("glass-cockpit-dlss-fix.ini"))return true;
            if(choice.Id==0)return FileHash(dll)!=FileHash(Path.Combine(Package,"dxgi.dll"));
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
            bool different=false;
            foreach(string name in new[] { "dxgi.dll" }) {
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
            if(receipt.ContainsKey("engineReleaseId")&&Convert.ToInt64(receipt["engineReleaseId"])>0)message="Installed official DLL v"+Convert.ToString(receipt["engineVersion"])+" — selected version kept";
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
        } finally { busy=false; RefreshState(); timer.Start(); }
    }
    async Task SetupSelectedEngine() {
        var choice=engineVersions.SelectedItem as UpstreamRelease.Choice;
        if(choice==null)return;
        foreach(var process in Process.GetProcessesByName("FlightSimulator2024")) { process.Dispose();ParentDialogPosition.Show(this,"Close MSFS before changing the installed DLL.",Text);return; }
        string version=choice.Id==0?UpstreamRelease.Version:choice.Tag.Substring(1);
        if(version=="1.0.0"||(latestEngine!=null&&StandaloneAppUpdate.ParseVersion(version)<StandaloneAppUpdate.ParseVersion(latestEngine.Tag.Substring(1)))) {
            if(ParentDialogPosition.Show(this,"Install older official DLL v"+version+" for rollback?\n\nOlder versions can contain known crashes. Version 1.0.0 can crash when loading FSLabs aircraft. The normal Install/Update button uses the latest release.",Text,MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
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
        foreach(var process in Process.GetProcessesByName("FlightSimulator2024")) { process.Dispose();ParentDialogPosition.Show(this,"Close MSFS before changing the installed DLL.",Text);return; }
        busy=true;RefreshState();status.Text="Checking latest release and its SHA-256 checksums…";
        UpstreamRelease.Prepared selected=null;
        try {
            NativeInstaller.CheckReShadeConflicts(Target);
            selected=await Task.Run(()=> { var latest=UpstreamRelease.Latest();return UpstreamRelease.Load(Package,latest.Id,true,latest); });
        } catch(Exception e) { ParentDialogPosition.Show(this,e.Message,Text); }
        finally { busy=false;RefreshState(); }
        if(selected!=null)await Setup(false,selected,true);
    }
    async Task Setup(bool remove,UpstreamRelease.Prepared selected=null,bool force=false)
    {
        if(!remove) { RefreshState(); if(!force&&!install.Enabled)return; }
        bool replace=false;
        if(!remove) {
            NativeInstaller.Receipt old=null;
            if(File.Exists(Receipt))try { old=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<NativeInstaller.Receipt>(File.ReadAllText(Receipt)); } catch { }
            string conflicts=String.Join(", ",Array.FindAll(Payload,n=>File.Exists(Path.Combine(Target,n))&&(old==null||old.files==null||!old.files.ContainsKey(n))));
            if(conflicts.Length>0) {
                if(ParentDialogPosition.Show(this,"Back up and replace these existing mod files?\n\n"+conflicts+"\n\nUninstall restores the backups.",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes) return;
                replace=true;
            }
        }
        busy=true; RefreshState(); status.Text=remove ? "Uninstalling...":"Installing...";
        string target=Target;
        try {
            bool elevate=false;
            try {
                string message=await Task.Run(()=>NativeInstaller.Run(Package,target,remove,replace,null,selected));
                ParentDialogPosition.Show(this,message,Text,MessageBoxButtons.OK,MessageBoxIcon.Information);
            } catch(UnauthorizedAccessException) { elevate=true; }
            if(elevate) {
                if(ParentDialogPosition.Show(this,"Windows requires administrator permission to change files in this folder. Continue?",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
                var p=new ProcessStartInfo(Application.ExecutablePath,(remove ? "--uninstall ":"--install ")+"\""+target.TrimEnd('\\')+"\""+(replace ? " --replace":"")+(remove?"":selected==null?" --bundled":" --engine "+selected.Release.Id)) {
                    UseShellExecute=true,Verb="runas",WorkingDirectory=Package };
                using(var child=Process.Start(p)) await Task.Run(()=>child.WaitForExit());
            }
        } catch(Exception e) { ParentDialogPosition.Show(this,e.Message,Text); }
        finally { busy=false; RefreshState(); }
    }
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try {
            if(args.Length>0 && (args[0]=="--apply-app-update" || args[0]=="--recover-app-update")) return StandaloneAppUpdate.Helper(args);
            if(args.Length>=2 && (args[0]=="--install" || args[0]=="--uninstall")) {
                bool replace=false,bundled=false;long engineId=0;
                for(int i=2;i<args.Length;i++) {
                    if(args[i]=="--replace"&&!replace)replace=true;
                    else if(args[i]=="--bundled"&&!bundled)bundled=true;
                    else if(args[i]=="--engine"&&engineId==0&&i+1<args.Length&&Int64.TryParse(args[++i],out engineId)&&engineId>0) {}
                    else throw new ArgumentException("Invalid installer arguments.");
                }
                if((args[0]=="--uninstall"&&(engineId!=0||bundled))||(engineId!=0&&bundled))throw new ArgumentException("Invalid engine selection.");
                UpstreamRelease.Prepared selected=null;
                if(engineId!=0)selected=UpstreamRelease.Load(Package,engineId,false);
                else if(args[0]=="--install"&&!bundled) { var latest=UpstreamRelease.Latest();selected=UpstreamRelease.Load(Package,latest.Id,true,latest); }
                string message=NativeInstaller.Run(Package,args[1],args[0]=="--uninstall",replace,null,selected);
                MessageBox.Show(message,"Standalone glass cockpit",MessageBoxButtons.OK,MessageBoxIcon.Information);return 0;
            }
            using(var gate=StandaloneAppUpdate.Enter(Package)) {
                if(gate==null) { MessageBox.Show("The standalone app is already open or updating.","Standalone glass cockpit");return 0; }
                string recoveryError=null;
                if(Array.IndexOf(args,"--self-test")<0) {
                    try { if(StandaloneAppUpdate.Startup(Package))return 0; }
                    catch { recoveryError="App update recovery needs attention. Extract the complete ZIP again."; }
                }
            using(var form=new GlassWindow()) {
                if(args.Length==3 && args[0]=="--self-test") {
                    if(!File.Exists(Path.Combine(args[1],"CONTROL-TEST-ONLY"))) throw new Exception("Missing fixture marker");
                    if(ParentDialogPosition.Center(new Rectangle(300,200,660,680),new Size(400,260),new Rectangle(0,0,1920,1080))!=new Point(430,410))
                        throw new Exception("Dialog must center over its parent, not the desktop");
                    if(ParentDialogPosition.Center(new Rectangle(-1900,20,660,680),new Size(800,900),new Rectangle(-1920,0,1920,1080))!=new Point(-1920,0))
                        throw new Exception("Dialog must stay visible on a secondary monitor near its edge");
                    var detected=SimulatorDiscovery.Find(new[]{args[1]});
                    if(detected!=args[1])throw new Exception("Discovery failed: "+detected);
                    form.folder.Text=args[1]; form.RefreshState();
                    if(!form.enabled.Enabled || form.install.Enabled || form.install.Text != "Update" || !form.uninstall.Enabled) throw new Exception("Installed controls failed");
                    if(form.applyEngine.Enabled)throw new Exception("Matching bundled DLL must not offer replacement");
                    if(form.status.Text!="Installed - files are up to date")throw new Exception("Matching DLLs should not require an update");
                    string manifestPath=Path.Combine(Package,"payload.json"),manifestText=File.ReadAllText(manifestPath);
                    string packageDll=Path.Combine(Package,"dxgi.dll"),installedDll=Path.Combine(form.Target,"dxgi.dll");
                    byte[] packageBytes=File.ReadAllBytes(packageDll),installedBytes=File.ReadAllBytes(installedDll),receiptBytes=File.ReadAllBytes(form.Receipt);
                    try {
                        var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
                        var bundled=new UpstreamRelease.Choice { Id=416640528,Tag="v"+UpstreamRelease.Version,Sha256=UpstreamRelease.ZipHash };
                        var future=new UpstreamRelease.Choice { Id=1,Tag="v1.2.0",Sha256=new string('a',64) };
                        form.latestEngine=future;
                        form.PopulateEngineChoices(new[]{future,bundled});
                        if(form.engineVersions.Items.Count!=2||form.engineVersions.SelectedIndex!=1||!form.applyEngine.Enabled||!form.install.Enabled||!form.upstream.Text.Contains("New official"))throw new Exception("Latest DLL must be selected by default and offer Update");
                        var selectedReceipt=serializer.Deserialize<NativeInstaller.Receipt>(File.ReadAllText(form.Receipt));
                        selectedReceipt.engineVersion="1.2.0";selectedReceipt.engineReleaseId=1;selectedReceipt.engineZipHash=future.Sha256;
                        File.WriteAllText(form.Receipt,serializer.Serialize(selectedReceipt));
                        form.PopulateEngineChoices(new[]{future,bundled});
                        if(form.engineVersions.SelectedIndex!=1||form.applyEngine.Enabled||form.install.Enabled||!form.upstream.Text.Contains("up to date"))throw new Exception("Installed upstream selection must stay current without reinstallation");
                        if(form.enabled.Enabled||!form.note.Text.Contains("not verified"))throw new Exception("Unknown settings schema must use the original window");
                        File.WriteAllBytes(form.Receipt,receiptBytes);form.latestEngine=bundled;form.PopulateEngineChoices(new[]{bundled});
                        var manifest=serializer.Deserialize<System.Collections.Generic.Dictionary<string,object>>(manifestText);
                        File.WriteAllText(packageDll,"INERT newer DLL fixture");
                        ((System.Collections.Generic.Dictionary<string,object>)manifest["files"])["dxgi.dll"]=StandaloneAppUpdate.HashFile(packageDll);
                        File.WriteAllText(manifestPath,serializer.Serialize(manifest));form.RefreshState();
                        if(!form.install.Enabled || !form.status.Text.Contains("Mod update available"))throw new Exception("Different DLLs need Update action");
                        File.Copy(packageDll,installedDll,true);
                        var receipt=serializer.Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(form.Receipt));
                        ((System.Collections.Generic.Dictionary<string,object>)receipt["files"])["dxgi.dll"]=StandaloneAppUpdate.HashFile(installedDll);
                        File.WriteAllText(form.Receipt,serializer.Serialize(receipt));form.RefreshState();
                        if(form.install.Enabled || !form.status.Text.Contains("up to date"))throw new Exception("Update should disable after replacement");
                        File.WriteAllText(installedDll,"INERT unexpected edit");form.RefreshState();
                        if(form.install.Enabled || !form.status.Text.Contains("files changed"))throw new Exception("Modified DLL must not offer routine update");
                        File.WriteAllText(manifestPath,"invalid json");form.RefreshState();
                        if(form.install.Enabled || !form.status.Text.Contains("Cannot verify"))throw new Exception("Invalid metadata must disable Update");
                        File.Delete(installedDll);form.RefreshState();
                        if(form.install.Enabled || !form.uninstall.Enabled)throw new Exception("Incomplete installation recovery state");
                    } finally { File.WriteAllBytes(packageDll,packageBytes);File.WriteAllBytes(installedDll,installedBytes);File.WriteAllBytes(form.Receipt,receiptBytes);File.WriteAllText(manifestPath,manifestText);form.fileHashes.Clear();form.RefreshState(); }
                    foreach(bool on in new[] {false,true}) { form.enabled.Checked=on; if(GetPrivateProfileInt("overlay","enabled",on?0:1,form.Ini)!=(on?1:0) || form.install.Enabled) throw new Exception("Settings changes must not enable Update"); }
                    File.Delete(form.Receipt);form.RefreshState();
                    if(!form.install.Enabled || form.install.Text!="Install")throw new Exception("Fresh installation must offer Install");
                    File.WriteAllBytes(form.Receipt,receiptBytes);
                    string recoveryFolder=Path.Combine(form.Target,".StandaloneGlass-backup"),journal=Path.Combine(recoveryFolder,".update.json");
                    Directory.CreateDirectory(recoveryFolder);File.WriteAllText(journal,"{}");form.RefreshState();
                    if(!form.install.Enabled || !form.status.Text.Contains("interrupted"))throw new Exception("Interrupted update needs recovery action");
                    File.Delete(journal);Directory.Delete(recoveryFolder);form.RefreshState();
                    string other=Path.Combine(args[1],".MyOwnFGVRMod-install.json");File.WriteAllText(other,"{}");form.RefreshState();
                    if(form.install.Enabled || !form.status.Text.Contains("Remove the conflicting mod")) throw new Exception("Conflict state failed");File.Delete(other);
                    form.RefreshState(); form.appUpdate.Text="App update unavailable; current version kept. Checks again next launch.";
                    form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-10000,-10000);form.ShowInTaskbar=false;
                    form.Show();Application.DoEvents();form.glass.SelfTest(args[2]);
                    form.engineVersions.Focus();form.engineVersions.DroppedDown=true;Application.DoEvents();
                    if(!form.engineVersions.DroppedDown)throw new Exception("Could not open version dropdown for regression check");
                    for(int tick=0;tick<3;tick++) {
                        form.RefreshState();Application.DoEvents();
                        if(!form.engineVersions.DroppedDown)throw new Exception("Status refresh closed the version dropdown");
                    }
                    var chosenEngine=(UpstreamRelease.Choice)form.engineVersions.SelectedItem;
                    var currentItems=form.engineVersions.Items.Count;
                    form.PopulateEngineChoices(new[]{new UpstreamRelease.Choice { Id=42,Tag="v1.9.0",Sha256=new string('b',64) }});
                    form.appUpdate.Text="";
                    Application.DoEvents();
                    if(!form.engineVersions.DroppedDown || form.engineVersions.Items.Count!=currentItems)throw new Exception("Background update disturbed version selection");
                    form.engineVersions.DroppedDown=false;Application.DoEvents();
                    if(form.pendingEngineChoices!=null || form.engineVersions.Items.Count!=2 || ((UpstreamRelease.Choice)form.engineVersions.SelectedItem).Tag!=chosenEngine.Tag)throw new Exception("Deferred releases lost selected version");
                    form.appUpdate.Text="App update unavailable; current version kept. Checks again next launch.";
                    byte[] themeIni=File.ReadAllBytes(form.Ini);
                    foreach(bool light in new[]{true,false}) {
                        (light ? form.lightTheme : form.darkTheme).PerformClick();
                        if(WsgTheme.Light!=light || File.ReadAllText(Path.Combine(Package,"Appearance.txt"))!=(light ? "light":"dark"))throw new Exception("Appearance persistence failed");
                        using(var reopened=new GlassWindow()) if(WsgTheme.Light!=light || reopened.BackColor!=form.BackColor)throw new Exception("Appearance restore failed");
                        if(Convert.ToBase64String(themeIni)!=Convert.ToBase64String(File.ReadAllBytes(form.Ini)))throw new Exception("Appearance changed glass settings");
                        using(var recorder=new ShortcutRecorder("Ctrl+Shift+F6")) {
                            recorder.StartPosition=FormStartPosition.Manual;recorder.Location=new Point(-10000,-10000);recorder.Show(form);Application.DoEvents();
                            var processKey=typeof(ShortcutRecorder).GetMethod("ProcessCmdKey",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                            processKey.Invoke(recorder,new object[]{new Message(),Keys.Control|Keys.D5});
                            if(recorder.Shortcut!="Ctrl+5")throw new Exception("Recorder key dispatch failed");
                            using(var b=new Bitmap(recorder.Width,recorder.Height)) { recorder.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(args[2]+(light ? ".light-recorder.png":".dark-recorder.png")); }
                            processKey.Invoke(recorder,new object[]{new Message(),Keys.Enter});
                            if(recorder.DialogResult!=DialogResult.OK)throw new Exception("Recorder confirmation failed");
                        }
                        using(var recorder=new ShortcutRecorder("Pause")) {
                            recorder.RecordKey(Keys.Control|Keys.D5);
                            typeof(Form).GetMethod("OnDeactivate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(recorder,new object[]{EventArgs.Empty});
                            if(recorder.DialogResult!=DialogResult.Cancel)throw new Exception("Focus loss must cancel recording");
                        }
                        for(int page=0;page<3;page++) {
                            form.glass.SelectedIndex=page;Application.DoEvents();
                            using(var b=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(b,new Rectangle(0,0,form.Width,form.Height));b.Save(args[2]+(light ? ".light-":".dark-")+page+".png"); }
                        }
                        form.glass.ReviewLayout(args[2]+(light ? ".light":".dark"));
                        form.glass.SelectedIndex=0;
                        form.glass.RefreshValues(false);
                        if(form.glass.Enabled)throw new Exception("Unavailable settings must stay disabled");
                        using(var b=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(b,new Rectangle(0,0,form.Width,form.Height));b.Save(args[2]+(light ? ".light-disabled.png":".dark-disabled.png")); }
                        form.glass.RefreshValues(true);
                        // Draw the actual popup item renderer: DrawToBitmap alone omits the native popup.
                        using(var b=new Bitmap(form.engineVersions.Width,form.engineVersions.ItemHeight*2))
                        using(var g=Graphics.FromImage(b)) {
                            var draw=typeof(WsgComboBox).GetMethod("OnDrawItem",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                            for(int item=0;item<2;item++) {
                                var bounds=new Rectangle(0,item*form.engineVersions.ItemHeight,b.Width,form.engineVersions.ItemHeight);
                                draw.Invoke(form.engineVersions,new object[]{new DrawItemEventArgs(g,form.engineVersions.Font,bounds,Math.Min(item,form.engineVersions.Items.Count-1),item==0 ? DrawItemState.Selected : DrawItemState.None)});
                            }
                            if(b.GetPixel(b.Width-1,b.Height-1).ToArgb()!=WsgTheme.Panel.ToArgb())throw new Exception("Dropdown background must follow theme");
                            b.Save(args[2]+(light ? ".light-dropdown.png":".dark-dropdown.png"));
                        }
                        if(Convert.ToBase64String(themeIni)!=Convert.ToBase64String(File.ReadAllBytes(form.Ini)))throw new Exception("Disabled preview changed glass settings");
                    }
                    form.glass.SelectedIndex=0;
                    using(var b=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(b,new Rectangle(0,0,form.Width,form.Height));b.Save(args[2]); }
                    // Geometry/font scaling simulation. Does not change the user's display DPI.
                    foreach(float scale in new[]{1.25f,1.5f,2f})using(var scaled=new GlassWindow()) {
                        scaled.folder.Text=args[1];scaled.RefreshState();
                        scaled.StartPosition=FormStartPosition.Manual;scaled.Location=new Point(-10000,-10000);scaled.ShowInTaskbar=false;
                        scaled.Show();Application.DoEvents();
                        var fonts=new System.Collections.Generic.Dictionary<Control,Font>();
                        Action<Control> collect=null;collect=c=> { fonts[c]=c.Font;foreach(Control child in c.Controls)collect(child); };collect(scaled);
                        scaled.AutoScaleMode=AutoScaleMode.None;scaled.Scale(new SizeF(scale,scale));
                        foreach(var entry in fonts)entry.Key.Font=new Font(entry.Value.FontFamily,entry.Value.Size*scale,entry.Value.Style);
                        scaled.footerLayoutKey=null;scaled.LayoutFooter();Application.DoEvents();
                        foreach(Control control in scaled.Controls)if(control.Visible && !scaled.ClientRectangle.Contains(control.Bounds))throw new Exception("Scaled control outside window: "+control.Text);
                        if(scaled.engineVersions.TabIndex>=scaled.applyEngine.TabIndex || scaled.folder.TabIndex>=scaled.browse.TabIndex)throw new Exception("Keyboard order does not match the workflow");
                        using(var b=new Bitmap(scaled.Width,scaled.Height)) { scaled.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(args[2]+".scale-"+(int)(scale*100)+".png"); }
                        scaled.glass.ReviewLayout(args[2]+".scale-"+(int)(scale*100));
                    }
                    string placement=Path.Combine(Package,"WindowPlacement.json");
                    Rectangle savedBounds=new Rectangle(80,90,820,820),loadedBounds;
                    WindowPlacement.Save(placement,savedBounds);
                    if(!WindowPlacement.TryRead(placement,out loadedBounds) || loadedBounds!=savedBounds)throw new Exception("Window placement did not round-trip");
                    var leftMonitor=new Rectangle(-1920,0,1920,1080);
                    var offscreen=WindowPlacement.KeepVisible(new Rectangle(-9000,7000,4000,3000),leftMonitor,new Size(660,680));
                    if(!leftMonitor.Contains(offscreen))throw new Exception("Disconnected-monitor position was not recovered");
                    foreach(string invalid in new[]{"broken JSON","null","{\"Width\":-1}","{\"Width\":999999999999999999999999}"}) {
                        File.WriteAllText(placement,invalid);
                        if(WindowPlacement.TryRead(placement,out loadedBounds))throw new Exception("Invalid placement was accepted");
                    }
                    WindowPlacement.Save(placement,savedBounds);
                    using(var positioned=new GlassWindow()) {
                        positioned.RememberWindow();
                        typeof(Form).GetMethod("OnLoad",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(positioned,new object[]{EventArgs.Empty});
                        var expected=WindowPlacement.KeepVisible(new Rectangle(savedBounds.Location,positioned.Size),Screen.FromRectangle(savedBounds).WorkingArea,positioned.Size);
                        if(positioned.Bounds!=expected || positioned.FormBorderStyle!=FormBorderStyle.FixedDialog)throw new Exception("Position must restore without restoring an old resized window");
                        positioned.folder.Text=args[1];positioned.RefreshState();
                        if(positioned.Bounds!=expected)throw new Exception("Status refresh resized the restored window");
                        positioned.Location=new Point(expected.X+10,expected.Y+10);
                        typeof(Form).GetMethod("OnFormClosing",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(positioned,new object[]{new FormClosingEventArgs(CloseReason.UserClosing,false)});
                        if(!WindowPlacement.TryRead(placement,out loadedBounds) || loadedBounds!=positioned.Bounds)throw new Exception("Closing did not save window placement");
                    }
                    return 0;
                }
                form.RememberWindow();
                form.Shown += async delegate {
                    try {
                        await form.Detect();
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
        } catch(Exception e) { if(Array.IndexOf(args,"--self-test")>=0) Console.Error.WriteLine(e); else MessageBox.Show(e.Message,"Standalone glass cockpit");return 1; }
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
