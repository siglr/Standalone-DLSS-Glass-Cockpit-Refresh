using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// Fixed-purpose native installer. Never interprets commands, downloads code or edits runtime registration.
internal static class NativeInstaller
{
    internal const string ReceiptName=".StandaloneGlass-install.json",BackupName=".StandaloneGlass-backup",TransactionName=".StandaloneGlass-transaction";
    internal static readonly string[] Payload={"dxgi.dll","glass-cockpit-dlss-fix.ini"};
    internal static readonly string[] LegacyPayload={"dxgi.dll","StandaloneGlassGraphics.dll","StandaloneGlass.ini"};
    internal sealed class Receipt { public string version {get;set;} public Dictionary<string,string> files {get;set;} public Dictionary<string,string> backups {get;set;} public object registration {get;set;} public string engineVersion {get;set;} public long engineReleaseId {get;set;} public string engineZipHash {get;set;} }
    internal sealed class Entry { public string name {get;set;} public string before {get;set;} public string after {get;set;} }
    internal sealed class Journal { public string owner {get;set;} public List<Entry> entries {get;set;} }
    internal sealed class OldEntry { public string oldHash {get;set;} public string newHash {get;set;} }
    internal sealed class OldJournal { public Dictionary<string,OldEntry> files {get;set;} public string oldReceiptHash {get;set;} public string newReceiptHash {get;set;} }
    static readonly JavaScriptSerializer Json=new JavaScriptSerializer { MaxJsonLength=1024*1024,RecursionLimit=12 };
    static void Need(bool b,string text) { if(!b)throw new InvalidDataException(text); }
    static bool Hash(string s) { return s!=null&&Regex.IsMatch(s,"\\A[0-9a-fA-F]{64}\\z"); }
    static bool Equal(string a,string b) { return String.Equals(a,b,StringComparison.OrdinalIgnoreCase); }
    internal static void CheckReShadeConflicts(string root) {
        Safe(root);
        if(!Directory.Exists(root))throw new InvalidDataException("Choose the simulator game folder first.");
        string reason=null;
        foreach(string name in new[]{"ReShade.ini","ReShade64.dll","ReShade32.dll"})
            if(File.Exists(Path.Combine(root,name)))reason=name;
        // ReShade also supports renamed add-ons and custom AddonPath directories. A configured
        // ReShade install is blocked rather than trying to remove files or guess which are loaded.
        foreach(string path in Directory.EnumerateFiles(root)) {
            string extension=Path.GetExtension(path);
            if(Equal(extension,".addon64")||Equal(extension,".addon"))reason=Path.GetFileName(path);
            if(Equal(extension,".ini")) {
                Safe(path);Need(new FileInfo(path).Length<=2*1024*1024,"Cannot safely inspect oversized INI for ReShade configuration: "+path);
                string text=File.ReadAllText(path);
                if(Regex.IsMatch(text,@"(?im)^\s*\[ADDON\]\s*$")&&Regex.IsMatch(text,@"(?im)^\s*AddonPath\s*="))reason=Path.GetFileName(path)+" (ReShade AddonPath)";
            }
        }
        foreach(string name in new[]{"reshade-addons","addons"}) {
            string dir=Path.Combine(root,name);Safe(dir);
            if(Directory.Exists(dir)&&Directory.EnumerateFiles(dir).Any(p=>Equal(Path.GetExtension(p),".addon64")||Equal(Path.GetExtension(p),".addon")))reason=name;
        }
        Need(reason==null,"ReShade/add-on conflict detected: "+reason+". Use IFreemz's ReShade add-on with ReShade, or remove that installation first. Do not run both fixes. No files were changed.");
    }
    static string Digest(string p) { Safe(p);return File.Exists(p)?StandaloneAppUpdate.HashFile(p):null; }
    internal static void Safe(string path) {
        string full=Path.GetFullPath(path);
        Need(!full.StartsWith(@"\\"),"Use a local disk folder, not a network/device path.");
        for(string p=full;!String.IsNullOrEmpty(p);p=Path.GetDirectoryName(p)) {
            if(File.Exists(p)||Directory.Exists(p))Need((File.GetAttributes(p)&FileAttributes.ReparsePoint)==0,"Linked folders/files are not supported: "+p);
        }
    }
    static T Read<T>(string path) { Safe(path);Need(new FileInfo(path).Length<=1024*1024,"Installation metadata is too large.");return Json.Deserialize<T>(File.ReadAllText(path)); }
    static void Write(string path,object value) { Safe(path);File.WriteAllText(path,Json.Serialize(value),new UTF8Encoding(false)); }
    static bool Owned(string n) { return Payload.Concat(LegacyPayload).Contains(n,StringComparer.Ordinal); }
    static bool Settings(string n) { return n=="StandaloneGlass.ini"||n=="glass-cockpit-dlss-fix.ini"; }
    static void SaveSettings(string package,string file) {
        string actual=Digest(file),stem=Path.GetFileNameWithoutExtension(file)+".saved";
        string saved=Path.Combine(package,stem+".ini");Safe(saved);
        if(File.Exists(saved)&&!Equal(Digest(saved),actual))saved=Path.Combine(package,stem+"."+actual.ToLowerInvariant()+".ini");
        Safe(saved);if(!File.Exists(saved))File.Copy(file,saved,false);
        Need(Equal(Digest(saved),actual),"Settings backup changed; preserved.");
    }
    internal static byte[] MigrateSettings(string legacy) {
        // Preserve comments/unknown sections. The upstream DLL uses overlay and advanced sections.
        string text=File.ReadAllText(legacy),strength=null;bool cockpit=false;
        var lines=new List<string>();
        foreach(string line in Regex.Split(text,"\\r?\\n")) {
            string trimmed=line.Trim();
            if(trimmed.StartsWith("[")&&trimmed.EndsWith("]")) {
                cockpit=Equal(trimmed,"[cockpit]");lines.Add(cockpit?"[overlay]":line);continue;
            }
            if(cockpit&&Regex.IsMatch(trimmed,@"^glass_strength\s*=",RegexOptions.IgnoreCase)) { strength=trimmed.Substring(trimmed.IndexOf('=')+1);continue; }
            lines.Add(line);
        }
        if(strength!=null) {
            int section=lines.FindIndex(s=>Equal(s.Trim(),"[advanced]"));
            if(section<0){lines.Add("[advanced]");lines.Add("glass_strength="+strength);}
            else lines.Insert(section+1,"glass_strength="+strength);
        }
        return new UTF8Encoding(false).GetBytes(String.Join("\r\n",lines));
    }
    static string TargetPath(string root,string name) {
        Need(Owned(name)||name==ReceiptName||(name.StartsWith(BackupName+"/",StringComparison.Ordinal)&&Owned(name.Substring(BackupName.Length+1))),"Unexpected installation filename.");
        string p=Path.Combine(root,name.Replace('/',Path.DirectorySeparatorChar));Safe(p);return p;
    }
    static void CheckReceipt(Receipt r) {
        Need(r!=null&&r.files!=null&&r.backups!=null&&r.registration==null,"Invalid standalone receipt; files preserved.");
        foreach(var p in r.files)Need(Owned(p.Key)&&Hash(p.Value),"Invalid receipt file.");
        foreach(var p in r.backups)Need(r.files.ContainsKey(p.Key)&&Hash(p.Value),"Invalid original backup entry.");
    }
    static void Atomic(string source,string dest,string temp,string expected) {
        Safe(source);Safe(dest);Safe(temp);Need(!File.Exists(temp)&&!Directory.Exists(temp),"Unrecognized temporary file; preserved.");
        Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(source,temp,false);
        try { Need(Equal(Digest(temp),expected),"Installation copy changed; replacement stopped.");if(File.Exists(dest))File.Replace(temp,dest,null);else File.Move(temp,dest); }finally { if(File.Exists(temp))File.Delete(temp); }
    }
    static void Cleanup(string root) {
        string stage=Path.Combine(root,TransactionName);Safe(stage);
        if(!Directory.Exists(stage))return;
        // Exact transaction file set only; unknown contents are never recursively deleted.
        var permitted=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"journal.json","journal.tmp","committed","replace.tmp"};
        for(int i=0;i<9;i++){permitted.Add("old-"+i);permitted.Add("new-"+i);}
        foreach(string f in Directory.GetFileSystemEntries(stage)) { Safe(f);Need(File.Exists(f)&&permitted.Contains(Path.GetFileName(f)),"Unexpected recovery file; preserved."); }
        foreach(string f in Directory.GetFiles(stage))File.Delete(f);Directory.Delete(stage);
        string backup=Path.Combine(root,BackupName);Safe(backup);
        if(Directory.Exists(backup)&&Directory.GetFileSystemEntries(backup).Length==0)Directory.Delete(backup);
    }
    internal static void Recover(string root) {
        string stage=Path.Combine(root,TransactionName);Safe(stage);if(!Directory.Exists(stage))return;
        string jp=Path.Combine(stage,"journal.json");
        if(!File.Exists(jp))throw new InvalidDataException("Incomplete installation staging needs review; preserve "+stage);
        var j=Read<Journal>(jp);Need(j!=null&&j.owner==Path.GetFullPath(root)&&j.entries!=null&&j.entries.Count<=9,"Invalid recovery record.");
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool committed=File.Exists(Path.Combine(stage,"committed"));
        for(int i=0;i<j.entries.Count;i++) {
            var e=j.entries[i];string p=TargetPath(root,e.name);
            Need(seen.Add(e.name)&&(e.before==null||Hash(e.before))&&(e.after==null||Hash(e.after)),"Invalid recovery entry.");
            string actual=Digest(p);
            Need(Equal(actual,e.before)||Equal(actual,e.after),"Changed file preserved during recovery: "+p);
            if(committed)Need(Equal(actual,e.after),"Committed installation changed; preserve recovery files.");
            else if(e.before!=null&&!Equal(actual,e.before))Need(Equal(Digest(Path.Combine(stage,"old-"+i)),e.before),"Original recovery copy changed.");
        }
        if(!committed)for(int i=j.entries.Count-1;i>=0;i--) {
            var e=j.entries[i];string p=TargetPath(root,e.name);if(Equal(Digest(p),e.before))continue;
            if(e.before==null)File.Delete(p);else Atomic(Path.Combine(stage,"old-"+i),p,Path.Combine(stage,"replace.tmp"),e.before);
        }
        Cleanup(root);
    }
    static void Transaction(string root,Dictionary<string,byte[]> changes,Action<int> fault) {
        string stage=Path.Combine(root,TransactionName);Safe(stage);Need(!Directory.Exists(stage),"Recover the previous installation first.");
        // Four known names across both layouts, their original backups, and the receipt.
        Need(changes.Count<=9,"Unexpected transaction size.");Directory.CreateDirectory(stage);
        var j=new Journal { owner=Path.GetFullPath(root),entries=new List<Entry>() };
        bool ready=false;
        try {
            foreach(var item in changes.OrderBy(p=>p.Key==ReceiptName?1:0).ThenBy(p=>p.Key,StringComparer.Ordinal)) {
                string dest=TargetPath(root,item.Key);int index=j.entries.Count;string before=Digest(dest);
                Need(!Directory.Exists(dest),"A folder conflicts with a mod file.");
                if(before!=null) { File.Copy(dest,Path.Combine(stage,"old-"+index));Need(Equal(Digest(Path.Combine(stage,"old-"+index)),before),"File changed while preparing installation."); }
                string after=item.Value==null?null:StandaloneAppUpdate.Hash(item.Value);
                if(item.Value!=null)File.WriteAllBytes(Path.Combine(stage,"new-"+index),item.Value);
                j.entries.Add(new Entry { name=item.Key,before=before,after=after });
            }
            Write(Path.Combine(stage,"journal.tmp"),j);File.Move(Path.Combine(stage,"journal.tmp"),Path.Combine(stage,"journal.json"));ready=true;
            for(int i=0;i<j.entries.Count;i++) {
                var e=j.entries[i];string dest=TargetPath(root,e.name);
                Need(Equal(Digest(dest),e.before),"File changed during installation; rolling back.");
                if(e.after==null) { if(File.Exists(dest))File.Delete(dest); }
                else Atomic(Path.Combine(stage,"new-"+i),dest,Path.Combine(stage,"replace.tmp"),e.after);
                if(fault!=null)fault(i+1);
            }
            File.WriteAllText(Path.Combine(stage,"committed"),"complete");if(fault!=null)fault(-1);
        } catch { if(ready)Recover(root);else Cleanup(root);throw; }
        Cleanup(root);
    }
    // Backward-compatible recovery for the PowerShell installer's update journal.
    static void RecoverLegacy(string root) {
        string backup=Path.Combine(root,BackupName),jp=Path.Combine(backup,".update.json");Safe(jp);if(!File.Exists(jp))return;
        var j=Read<OldJournal>(jp);Need(j!=null&&j.files!=null&&j.files.Count<=3&&Hash(j.oldReceiptHash)&&Hash(j.newReceiptHash),"Invalid legacy recovery record.");
        string receipt=TargetPath(root,ReceiptName),hash=Digest(receipt);bool committed=Equal(hash,j.newReceiptHash);
        Need(committed||Equal(hash,j.oldReceiptHash),"Legacy receipt changed; preserved.");
        foreach(var p in j.files) {
            Need(Owned(p.Key)&&(p.Value.oldHash==null||Hash(p.Value.oldHash))&&Hash(p.Value.newHash),"Invalid legacy recovery file.");
            string current=Digest(TargetPath(root,p.Key));
            if(!committed) {
                Need(current==null||Equal(current,p.Value.oldHash)||Equal(current,p.Value.newHash),"Changed legacy file; preserved.");
                if(p.Value.oldHash!=null&&!Equal(current,p.Value.oldHash))Need(Equal(Digest(Path.Combine(backup,".update-old-"+p.Key)),p.Value.oldHash),"Damaged legacy recovery copy.");
            }
        }
        if(!committed)foreach(var p in j.files) {
            string dest=TargetPath(root,p.Key);if(Equal(Digest(dest),p.Value.oldHash))continue;
            if(p.Value.oldHash==null) { if(File.Exists(dest))File.Delete(dest); }
            else Atomic(Path.Combine(backup,".update-old-"+p.Key),dest,Path.Combine(backup,".update-restore-"+p.Key),p.Value.oldHash);
        }
        foreach(var p in j.files)foreach(string prefix in new[]{".update-old-",".update-new-",".update-restore-"}) { string f=Path.Combine(backup,prefix+p.Key);Safe(f);if(File.Exists(f))File.Delete(f); }
        foreach(string n in new[]{".update-receipt-new",".update-receipt-old",".update-receipt-restore",".update.json"}) { string f=Path.Combine(backup,n);Safe(f);if(File.Exists(f))File.Delete(f); }
    }
    internal static string Run(string package,string root,bool remove,bool replace,Action<int> fault,UpstreamRelease.Prepared engine=null) {
        package=Path.GetFullPath(package);root=Path.GetFullPath(root);Safe(package);Safe(root);
        Need(!Equal(package,root),"Keep the app outside the simulator folder.");
        Need(!Regex.IsMatch(root,@"\\WindowsApps(\\|$)",RegexOptions.IgnoreCase),"Do not install into protected WindowsApps.");
        Need(File.Exists(Path.Combine(root,"FlightSimulator2024.exe")),"Choose the folder containing FlightSimulator2024.exe.");
        foreach(var p in Process.GetProcessesByName("FlightSimulator2024")) { p.Dispose();throw new InvalidOperationException("Close MSFS before changing installed files."); }
        var inventory=StandaloneAppUpdate.ReadInventory(package,false);
        Need(Equal(Digest(Path.Combine(package,StandaloneAppUpdate.Exe)),inventory.files[StandaloneAppUpdate.Exe]),"Launcher does not match its signed package.");
        if(!remove) {
            CheckReShadeConflicts(root);
            if(inventory.files.ContainsKey("glass-cockpit-dlss-fix.ini")&&!File.Exists(Path.Combine(root,ReceiptName))) {
                string existing=Digest(Path.Combine(root,"dxgi.dll"));
                Need(existing==null||Equal(existing,UpstreamRelease.DllHash)||(engine!=null&&Equal(existing,StandaloneAppUpdate.Hash(engine.Dll))),
                    "Another DXGI mod already occupies this folder. Remove it with its own installer before installing the standalone fix; it may be ReShade or another loader. The existing DLL was preserved.");
            }
        }
        using(var gate=StandaloneAppUpdate.Enter(root)) {
            Need(gate!=null,"Another installer is working on this simulator folder.");
            Recover(root);RecoverLegacy(root);
            string rp=TargetPath(root,ReceiptName);Receipt old=File.Exists(rp)?Read<Receipt>(rp):null;
            if(old!=null)CheckReceipt(old);
            var changes=new Dictionary<string,byte[]>(StringComparer.Ordinal);
            if(remove) {
                Need(old!=null,"No installation receipt was found.");
                foreach(var p in old.backups)Need(Equal(Digest(TargetPath(root,BackupName+"/"+p.Key)),p.Value),"Original uninstall backup changed or missing; preserved.");
                foreach(var p in old.files) {
                    string dest=TargetPath(root,p.Key),actual=Digest(dest),original;old.backups.TryGetValue(p.Key,out original);
                    if(actual!=null&&!Equal(actual,p.Value)&&!Equal(actual,original)) {
                        Need(Settings(p.Key),"Installed file changed; preserved: "+dest);
                        SaveSettings(package,dest);
                    }
                    changes[p.Key]=original==null?null:File.ReadAllBytes(TargetPath(root,BackupName+"/"+p.Key));
                }
                foreach(string n in old.backups.Keys)changes[BackupName+"/"+n]=null;
                changes[ReceiptName]=null;Transaction(root,changes,fault);
                return "Uninstalled. Original files restored; settings backups kept beside the app.";
            }
            Need(!File.Exists(Path.Combine(root,".MyOwnFGVRMod-install.json")),"Uninstall the other glass/combined mod first. Both use dxgi.dll.");
            var payload=Read<Receipt>(Path.Combine(package,"payload.json"));Need(payload!=null&&payload.files!=null&&payload.version==inventory.version,"Invalid simulator payload.");
            var names=payload.files.Keys.ToArray();
            Need(new HashSet<string>(names).SetEquals(Payload)||new HashSet<string>(names).SetEquals(LegacyPayload),"Unexpected simulator payload layout.");
            foreach(string n in names)Need(inventory.files.ContainsKey(n)&&Equal(Digest(Path.Combine(package,n)),payload.files[n])&&Equal(payload.files[n],inventory.files[n]),"Payload does not match the signed release: "+n);
            var next=new Receipt { version=inventory.version,files=new Dictionary<string,string>(),backups=new Dictionary<string,string>() };
            if(names.Contains("glass-cockpit-dlss-fix.ini")) {
                next.engineVersion=engine==null?UpstreamRelease.Version:engine.Release.Tag.Substring(1);
                next.engineReleaseId=engine==null?0:engine.Release.Id;
                next.engineZipHash=engine==null?UpstreamRelease.ZipHash:engine.Release.Sha256;
            } else Need(engine==null,"Selected upstream DLL requires the official-engine settings layout.");
            if(old!=null) {
                Need(new HashSet<string>(old.files.Keys).SetEquals(Payload)||new HashSet<string>(old.files.Keys).SetEquals(LegacyPayload),"Incomplete receipt; use Uninstall to recover first.");
                foreach(string n in old.files.Keys)Need(File.Exists(TargetPath(root,n)),"Incomplete installation; use Uninstall to recover first.");
                foreach(var p in old.files)Need(Settings(p.Key)||Equal(Digest(TargetPath(root,p.Key)),p.Value),"Installed file changed; preserved.");
                foreach(var p in old.backups) { Need(Equal(Digest(TargetPath(root,BackupName+"/"+p.Key)),p.Value),"Original backup changed; preserved.");next.backups.Add(p.Key,p.Value); }
                foreach(var p in old.files)next.files.Add(p.Key,p.Value);
            } else {
                Need(!Directory.Exists(Path.Combine(root,BackupName)),"An old backup folder exists; preserve/recover it first.");
            }
            foreach(string n in names.Where(n=>old==null||!old.files.ContainsKey(n))) {
                    string dest=TargetPath(root,n);if(!File.Exists(dest))continue;
                    Need(replace,"Existing mod files preserved. Confirm backup and replacement first.");
                    byte[] bytes=File.ReadAllBytes(dest);changes[BackupName+"/"+n]=bytes;next.backups[n]=StandaloneAppUpdate.Hash(bytes);
            }
            foreach(string n in names) {
                if(old!=null&&old.files.ContainsKey(n)&&Settings(n))continue;
                changes[n]=File.ReadAllBytes(Path.Combine(package,n));next.files[n]=payload.files[n];
                Need(Equal(StandaloneAppUpdate.Hash(changes[n]),payload.files[n]),"Payload changed while preparing installation.");
                if(Settings(n)&&File.Exists(TargetPath(root,n))) {
                    changes[n]=File.ReadAllBytes(TargetPath(root,n));next.files[n]=StandaloneAppUpdate.Hash(changes[n]);
                } else if(n=="glass-cockpit-dlss-fix.ini"&&old!=null&&old.files.ContainsKey("StandaloneGlass.ini")) {
                    changes[n]=MigrateSettings(TargetPath(root,"StandaloneGlass.ini"));next.files[n]=StandaloneAppUpdate.Hash(changes[n]);
                } else if(n=="glass-cockpit-dlss-fix.ini"&&engine!=null) {
                    // A newly selected release owns its defaults. Do not seed a future DLL with
                    // this wrapper's older default values. Existing and migrated preferences stay intact.
                    changes[n]=Encoding.UTF8.GetBytes("; Settings use the installed official DLL's defaults until changed.\r\n");
                    next.files[n]=StandaloneAppUpdate.Hash(changes[n]);
                }
            }
            if(old!=null)foreach(string n in old.files.Keys.Except(names)) {
                if(Settings(n))SaveSettings(package,TargetPath(root,n));
                string original;old.backups.TryGetValue(n,out original);
                changes[n]=original==null?null:File.ReadAllBytes(TargetPath(root,BackupName+"/"+n));
                if(original!=null)changes[BackupName+"/"+n]=null;
                next.files.Remove(n);next.backups.Remove(n);
            }
            if(engine!=null) { changes["dxgi.dll"]=engine.Dll;next.files["dxgi.dll"]=StandaloneAppUpdate.Hash(engine.Dll); }
            changes[ReceiptName]=Encoding.UTF8.GetBytes(Json.Serialize(next));Transaction(root,changes,fault);
            return old==null?"Installed. Start MSFS normally.":"Updated. Your settings and original uninstall backups were kept.";
        }
    }
}
