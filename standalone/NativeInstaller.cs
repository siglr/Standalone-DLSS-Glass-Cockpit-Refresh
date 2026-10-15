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
    internal static readonly string[] Payload={"dxgi.dll","StandaloneGlassGraphics.dll","StandaloneGlass.ini"};
    internal sealed class Receipt { public string version {get;set;} public Dictionary<string,string> files {get;set;} public Dictionary<string,string> backups {get;set;} public object registration {get;set;} }
    internal sealed class Entry { public string name {get;set;} public string before {get;set;} public string after {get;set;} }
    internal sealed class Journal { public string owner {get;set;} public List<Entry> entries {get;set;} }
    internal sealed class OldEntry { public string oldHash {get;set;} public string newHash {get;set;} }
    internal sealed class OldJournal { public Dictionary<string,OldEntry> files {get;set;} public string oldReceiptHash {get;set;} public string newReceiptHash {get;set;} }
    static readonly JavaScriptSerializer Json=new JavaScriptSerializer { MaxJsonLength=1024*1024,RecursionLimit=12 };
    static void Need(bool b,string text) { if(!b)throw new InvalidDataException(text); }
    static bool Hash(string s) { return s!=null&&Regex.IsMatch(s,"\\A[0-9a-fA-F]{64}\\z"); }
    static bool Equal(string a,string b) { return String.Equals(a,b,StringComparison.OrdinalIgnoreCase); }
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
    static bool Owned(string n) { return Payload.Contains(n,StringComparer.Ordinal); }
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
        for(int i=0;i<7;i++){permitted.Add("old-"+i);permitted.Add("new-"+i);}
        foreach(string f in Directory.GetFileSystemEntries(stage)) { Safe(f);Need(File.Exists(f)&&permitted.Contains(Path.GetFileName(f)),"Unexpected recovery file; preserved."); }
        foreach(string f in Directory.GetFiles(stage))File.Delete(f);Directory.Delete(stage);
        string backup=Path.Combine(root,BackupName);Safe(backup);
        if(Directory.Exists(backup)&&Directory.GetFileSystemEntries(backup).Length==0)Directory.Delete(backup);
    }
    internal static void Recover(string root) {
        string stage=Path.Combine(root,TransactionName);Safe(stage);if(!Directory.Exists(stage))return;
        string jp=Path.Combine(stage,"journal.json");
        if(!File.Exists(jp))throw new InvalidDataException("Incomplete installation staging needs review; preserve "+stage);
        var j=Read<Journal>(jp);Need(j!=null&&j.owner==Path.GetFullPath(root)&&j.entries!=null&&j.entries.Count<=7,"Invalid recovery record.");
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
        // At most three installed files, three original backups and the receipt.
        Need(changes.Count<=7,"Unexpected transaction size.");Directory.CreateDirectory(stage);
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
    internal static string Run(string package,string root,bool remove,bool replace,Action<int> fault) {
        package=Path.GetFullPath(package);root=Path.GetFullPath(root);Safe(package);Safe(root);
        Need(!Equal(package,root),"Keep the app outside the simulator folder.");
        Need(!Regex.IsMatch(root,@"\\WindowsApps(\\|$)",RegexOptions.IgnoreCase),"Do not install into protected WindowsApps.");
        Need(File.Exists(Path.Combine(root,"FlightSimulator2024.exe")),"Choose the folder containing FlightSimulator2024.exe.");
        foreach(var p in Process.GetProcessesByName("FlightSimulator2024")) { p.Dispose();throw new InvalidOperationException("Close MSFS before changing installed files."); }
        var inventory=StandaloneAppUpdate.ReadInventory(package,false);
        Need(Equal(Digest(Path.Combine(package,StandaloneAppUpdate.Exe)),inventory.files[StandaloneAppUpdate.Exe]),"Launcher does not match its signed package.");
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
                        Need(p.Key=="StandaloneGlass.ini","Installed file changed; preserved: "+dest);
                        string saved=Path.Combine(package,"StandaloneGlass.saved.ini");Safe(saved);
                        if(File.Exists(saved)&&!Equal(Digest(saved),actual))saved=Path.Combine(package,"StandaloneGlass.saved."+actual.ToLowerInvariant()+".ini");
                        Safe(saved);if(!File.Exists(saved))File.Copy(dest,saved,false);Need(Equal(Digest(saved),actual),"Settings backup changed; preserved.");
                    }
                    changes[p.Key]=original==null?null:File.ReadAllBytes(TargetPath(root,BackupName+"/"+p.Key));
                }
                foreach(string n in old.backups.Keys)changes[BackupName+"/"+n]=null;
                changes[ReceiptName]=null;Transaction(root,changes,fault);
                return "Uninstalled. Original files restored; settings backups kept beside the app.";
            }
            Need(!File.Exists(Path.Combine(root,".MyOwnFGVRMod-install.json")),"Uninstall the other glass/combined mod first. Both use dxgi.dll.");
            var payload=Read<Receipt>(Path.Combine(package,"payload.json"));Need(payload!=null&&payload.files!=null&&payload.files.Count==3&&payload.version==inventory.version,"Invalid simulator payload.");
            foreach(string n in Payload)Need(payload.files.ContainsKey(n)&&Equal(Digest(Path.Combine(package,n)),payload.files[n])&&Equal(payload.files[n],inventory.files[n]),"Payload does not match the signed release: "+n);
            var next=new Receipt { version=inventory.version,files=new Dictionary<string,string>(),backups=new Dictionary<string,string>() };
            if(old!=null) {
                foreach(string n in Payload)Need(old.files.ContainsKey(n)&&File.Exists(TargetPath(root,n)),"Incomplete installation; use Uninstall to recover first.");
                foreach(var p in old.files)Need(p.Key=="StandaloneGlass.ini"||Equal(Digest(TargetPath(root,p.Key)),p.Value),"Installed file changed; preserved.");
                foreach(var p in old.backups) { Need(Equal(Digest(TargetPath(root,BackupName+"/"+p.Key)),p.Value),"Original backup changed; preserved.");next.backups.Add(p.Key,p.Value); }
                foreach(var p in old.files)next.files.Add(p.Key,p.Value);
            } else {
                Need(!Directory.Exists(Path.Combine(root,BackupName)),"An old backup folder exists; preserve/recover it first.");
                foreach(string n in Payload) {
                    string dest=TargetPath(root,n);if(!File.Exists(dest))continue;
                    Need(replace,"Existing mod files preserved. Confirm backup and replacement first.");
                    byte[] bytes=File.ReadAllBytes(dest);changes[BackupName+"/"+n]=bytes;next.backups[n]=StandaloneAppUpdate.Hash(bytes);
                }
            }
            foreach(string n in Payload) {
                if(old!=null&&n=="StandaloneGlass.ini")continue;
                changes[n]=File.ReadAllBytes(Path.Combine(package,n));next.files[n]=payload.files[n];
                Need(Equal(StandaloneAppUpdate.Hash(changes[n]),payload.files[n]),"Payload changed while preparing installation.");
            }
            changes[ReceiptName]=Encoding.UTF8.GetBytes(Json.Serialize(next));Transaction(root,changes,fault);
            return old==null?"Installed. Start MSFS normally.":"Updated. Your settings and original uninstall backups were kept.";
        }
    }
}
