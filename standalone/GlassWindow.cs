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
    readonly Label status = new Label();
    readonly Label appUpdate = new Label();
    readonly Button install = new WsgButton { Text="Install", Tag="primary" };
    readonly Button uninstall = new WsgButton { Text="Uninstall" };
    readonly Button browse = new WsgButton { Text="Browse..." };
    readonly Button darkTheme=new WsgButton { Text="Dark", AccessibleName="Dark theme" };
    readonly Button lightTheme=new WsgButton { Text="Light", AccessibleName="Light theme" };
    readonly CheckBox enabled = new CheckBox { Text="Enable glass-cockpit refresh fix" };
    readonly Timer timer = new Timer { Interval=1000 };
    readonly GlassSettingsPanel glass;
    readonly Icon brandIcon;
    readonly PictureBox brand;
    readonly System.Collections.Generic.Dictionary<string,Tuple<string,string>> fileHashes =
        new System.Collections.Generic.Dictionary<string,Tuple<string,string>>(StringComparer.OrdinalIgnoreCase);
    bool busy, refreshing;
    string Target { get { return folder.Text; } }
    string Ini { get { return Path.Combine(Target, "StandaloneGlass.ini"); } }
    string Receipt { get { return Path.Combine(Target, ".StandaloneGlass-install.json"); } }
    static readonly string[] Payload = { "dxgi.dll", "StandaloneGlassGraphics.dll", "StandaloneGlass.ini" };
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
        ClientSize=new Size(660,634); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false;
        StartPosition=FormStartPosition.CenterScreen;
        var title=new Label { Text="Glass Cockpit Refresh", Font=new Font("Segoe UI",18,FontStyle.Bold), AutoSize=false };
        title.SetBounds(78,14,470,34);
        var subtitle=new Label { Text="MSFS 2024  /  Desktop + VR", ForeColor=WsgTheme.Muted };
        subtitle.SetBounds(80,48,400,24);
        var folderLabel=new Label { Text="MSFS 2024 GAME FOLDER", Font=new Font("Segoe UI",8,FontStyle.Bold) };
        folderLabel.SetBounds(18,84,520,18);
        folder.SetBounds(18,106,518,28); browse.SetBounds(546,103,96,32);
        var about=new LinkLabel { Text="About", TextAlign=ContentAlignment.MiddleRight, AccessibleDescription="Open the mod website for instructions and troubleshooting" };
        about.SetBounds(566,15,76,26);
        darkTheme.SetBounds(510,48,64,28); lightTheme.SetBounds(578,48,64,28);
        darkTheme.Click += delegate { SelectAppearance(false); };
        lightTheme.Click += delegate { SelectAppearance(true); };
        about.LinkClicked += delegate {
            try { Process.Start(new ProcessStartInfo("https://wesimglide.org/otherdata/tools/Standalone-DLSS-Glass-Cockpit-Refresh.html") { UseShellExecute=true }); }
            catch(Exception e) { MessageBox.Show(this,"Could not open the mod website.\n\n"+e.Message,Text); }
        };
        status.SetBounds(18,141,625,24); install.SetBounds(18,172,130,34); uninstall.SetBounds(160,172,130,34);
        enabled.SetBounds(20,223,610,30);
        var note=new Label { Text="Start MSFS normally. Changes apply during upscaling.\nUse official OFXR Bridge separately for frame generation.", Font=new Font("Segoe UI",9) };
        note.SetBounds(18,551,625,36);
        appUpdate.Font=new Font("Segoe UI",8.5f);
        var credit=new LinkLabel { Text="Based on Glass Cockpit DLSS Fix by IFreemz · modified/unofficial", Font=new Font("Segoe UI",8.5f), AccessibleDescription="GPL-3 graphics engine attribution and original source" };
        credit.SetBounds(18,589,625,20);
        credit.LinkClicked += delegate {
            try { Process.Start(new ProcessStartInfo("https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix") { UseShellExecute=true }); }
            catch(Exception e) { MessageBox.Show(this,e.Message,Text); }
        };
        Controls.Add(credit);
        appUpdate.SetBounds(18,613,625,20);
        Controls.AddRange(new Control[] { brand,title,subtitle,folderLabel,about,darkTheme,lightTheme,folder,browse,status,install,uninstall,enabled,note,appUpdate });
        subtitle.Tag=folderLabel.Tag=note.Tag=appUpdate.Tag="muted";
        glass = new GlassSettingsPanel(() => Ini, @"Local\StandaloneGlassTint");
        glass.SetBounds(18, 266, 624, 270); Controls.Add(glass); RefreshAppearance();
        HandleCreated += delegate { WsgTheme.TitleBar(this); };
        browse.Click += delegate { using(var d=new FolderBrowserDialog { SelectedPath=Target,Description="Choose the folder containing FlightSimulator2024.exe (Xbox Content or Steam game folder)." }) {
            if(d.ShowDialog(this)!=DialogResult.OK) return;
            if(!File.Exists(Path.Combine(d.SelectedPath,"FlightSimulator2024.exe"))) { MessageBox.Show(this,"Choose the folder containing FlightSimulator2024.exe.",Text);return; }
            folder.Text=d.SelectedPath;
            try { File.WriteAllText(Path.Combine(Package,"SimulatorDirectory.txt"),Target); } catch(Exception e) { MessageBox.Show(this,e.Message,Text); }
            RefreshState();
        }};
        enabled.CheckedChanged += delegate {
            if(refreshing) return;
            if(!File.Exists(Receipt) || !File.Exists(Ini) || !WritePrivateProfileString("cockpit","enabled",enabled.Checked ? "1":"0",Ini))
                MessageBox.Show(this,"Could not save the setting. Check settings file write access.",Text);
            RefreshState();
        };
        install.Click += async delegate { await Setup(false); };
        uninstall.Click += async delegate { await Setup(true); };
        timer.Tick += delegate { RefreshState(); };
        FormClosing += delegate(object sender,FormClosingEventArgs e) { if(busy) e.Cancel=true; };
        FormClosed += delegate { timer.Dispose(); brand.Image.Dispose(); brandIcon.Dispose(); };
    }
    void RefreshAppearance() {
        BackColor=WsgTheme.Background; ForeColor=WsgTheme.Text;
        WsgTheme.Apply(this); glass.ApplyTheme();
        foreach(Control control in Controls) if(Equals(control.Tag,"muted"))control.ForeColor=WsgTheme.Muted;
        status.ForeColor=WsgTheme.Mint;
        WsgTheme.SelectButton(darkTheme,!WsgTheme.Light); WsgTheme.SelectButton(lightTheme,WsgTheme.Light);
        if(IsHandleCreated)WsgTheme.TitleBar(this);
        Invalidate(true);
    }
    void SelectAppearance(bool light) {
        WsgTheme.Light=light; RefreshAppearance();
        try { File.WriteAllText(Path.Combine(Package,"Appearance.txt"),light ? "light" : "dark"); }
        catch(Exception e) { MessageBox.Show(this,"The appearance changed, but could not be saved.\n\n"+e.Message,Text); }
    }
    void RefreshState()
    {
        bool found=Target.Length>0 && File.Exists(Path.Combine(Target,"FlightSimulator2024.exe"));
        bool present=found && File.Exists(Receipt);
        bool other=found && File.Exists(Path.Combine(Target,".MyOwnFGVRMod-install.json"));
        bool complete=present && Array.TrueForAll(Payload,n=>File.Exists(Path.Combine(Target,n)));
        bool recovery=found && (Directory.Exists(Path.Combine(Target,NativeInstaller.TransactionName)) || File.Exists(Path.Combine(Target,".StandaloneGlass-backup",".update.json")));
        string installedStatus="";
        bool needsUpdate=false;
        if(recovery) { needsUpdate=true; installedStatus="Update interrupted - close MSFS, then click Update to recover"; }
        else if(complete) needsUpdate=NeedsInstalledUpdate(out installedStatus);
        install.Enabled=!busy && found && (!present || needsUpdate) && !other; uninstall.Enabled=!busy && (present || recovery);
        install.Text=present || recovery ? "Update" : "Install";
        browse.Enabled=!busy; enabled.Enabled=!busy && complete;
        glass.RefreshValues(!busy && complete);
        refreshing=true; enabled.Checked=complete && GetPrivateProfileInt("cockpit","enabled",1,Ini)!=0; refreshing=false;
        if(!busy) status.Text=other ? "Remove the conflicting mod using its own installer first."
            : complete || recovery ? installedStatus : present ? "Installation incomplete - use Uninstall to recover"
            : found ? "Ready to install Â· close MSFS first" : "Choose the MSFS 2024 game folder with Browse.";
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
            foreach(string name in new[] { "dxgi.dll", "StandaloneGlassGraphics.dll" }) {
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
            }
            message=different ? "Mod update available - close MSFS, then click Update" : "Installed - files are up to date";
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
    async Task Setup(bool remove)
    {
        if(!remove) { RefreshState(); if(!install.Enabled)return; }
        bool replace=false;
        if(!remove && !File.Exists(Receipt)) {
            string conflicts=String.Join(", ",Array.FindAll(Payload,n=>File.Exists(Path.Combine(Target,n))));
            if(conflicts.Length>0) {
                if(MessageBox.Show(this,"Back up and replace these existing mod files?\n\n"+conflicts+"\n\nUninstall restores the backups.",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes) return;
                replace=true;
            }
        }
        busy=true; RefreshState(); status.Text=remove ? "Uninstalling...":"Installing...";
        string target=Target;
        try {
            bool elevate=false;
            try {
                string message=await Task.Run(()=>NativeInstaller.Run(Package,target,remove,replace,null));
                MessageBox.Show(this,message,Text,MessageBoxButtons.OK,MessageBoxIcon.Information);
            } catch(UnauthorizedAccessException) { elevate=true; }
            if(elevate) {
                if(MessageBox.Show(this,"Windows requires administrator permission to change files in this folder. Continue?",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
                var p=new ProcessStartInfo(Application.ExecutablePath,(remove ? "--uninstall ":"--install ")+"\""+target.TrimEnd('\\')+"\""+(replace ? " --replace":"")) {
                    UseShellExecute=true,Verb="runas",WorkingDirectory=Package };
                using(var child=Process.Start(p)) await Task.Run(()=>child.WaitForExit());
            }
        } catch(Exception e) { MessageBox.Show(this,e.Message,Text); }
        finally { busy=false; RefreshState(); }
    }
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try {
            if(args.Length>0 && (args[0]=="--apply-app-update" || args[0]=="--recover-app-update")) return StandaloneAppUpdate.Helper(args);
            if(args.Length>=2 && (args[0]=="--install" || args[0]=="--uninstall")) {
                if(args.Length>3 || (args.Length==3&&args[2]!="--replace"))throw new ArgumentException("Invalid installer arguments.");
                string message=NativeInstaller.Run(Package,args[1],args[0]=="--uninstall",args.Length==3,null);
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
                    var detected=SimulatorDiscovery.Find(new[]{args[1]});
                    if(detected!=args[1])throw new Exception("Discovery failed: "+detected);
                    form.folder.Text=args[1]; form.RefreshState();
                    if(!form.enabled.Enabled || form.install.Enabled || form.install.Text != "Update" || !form.uninstall.Enabled) throw new Exception("Installed controls failed");
                    if(form.status.Text!="Installed - files are up to date")throw new Exception("Matching DLLs should not require an update");
                    string manifestPath=Path.Combine(Package,"payload.json"),manifestText=File.ReadAllText(manifestPath);
                    string packageDll=Path.Combine(Package,"dxgi.dll"),installedDll=Path.Combine(form.Target,"dxgi.dll");
                    byte[] packageBytes=File.ReadAllBytes(packageDll),installedBytes=File.ReadAllBytes(installedDll),receiptBytes=File.ReadAllBytes(form.Receipt);
                    try {
                        var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
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
                    foreach(bool on in new[] {false,true}) { form.enabled.Checked=on; if(GetPrivateProfileInt("cockpit","enabled",on?0:1,form.Ini)!=(on?1:0) || form.install.Enabled) throw new Exception("Settings changes must not enable Update"); }
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
                    byte[] themeIni=File.ReadAllBytes(form.Ini);
                    foreach(bool light in new[]{true,false}) {
                        (light ? form.lightTheme : form.darkTheme).PerformClick();
                        if(WsgTheme.Light!=light || File.ReadAllText(Path.Combine(Package,"Appearance.txt"))!=(light ? "light":"dark"))throw new Exception("Appearance persistence failed");
                        using(var reopened=new GlassWindow()) if(WsgTheme.Light!=light || reopened.BackColor!=form.BackColor)throw new Exception("Appearance restore failed");
                        if(Convert.ToBase64String(themeIni)!=Convert.ToBase64String(File.ReadAllBytes(form.Ini)))throw new Exception("Appearance changed glass settings");
                        for(int page=0;page<3;page++) {
                            form.glass.SelectedIndex=page;Application.DoEvents();
                            using(var b=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(b,new Rectangle(0,0,form.Width,form.Height));b.Save(args[2]+(light ? ".light-":".dark-")+page+".png"); }
                        }
                    }
                    form.glass.SelectedIndex=0;
                    using(var b=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(b,new Rectangle(0,0,form.Width,form.Height));b.Save(args[2]); }
                    return 0;
                }
                form.Shown += async delegate {
                    try {
                        await form.Detect();
                        if(recoveryError!=null) { form.appUpdate.Text=recoveryError;return; }
                        bool restart=await StandaloneAppUpdate.CheckAsync(Package,
                            text=> { if(!form.IsDisposed)form.appUpdate.Text=text; },
                            ()=>!form.IsDisposed && !form.Disposing && !form.busy);
                        if(restart && !form.IsDisposed) { form.busy=false;form.Close(); }
                    } catch(Exception e) { if(!form.IsDisposed)MessageBox.Show(form,e.Message,form.Text); }
                };
                Application.Run(form);
            }
            }
            return 0;
        } catch(Exception e) { if(Array.IndexOf(args,"--self-test")>=0) Console.Error.WriteLine(e); else MessageBox.Show(e.Message,"Standalone glass cockpit");return 1; }
    }
}
