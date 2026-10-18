using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

// Test-only console harness for the production updater, not distributed.
internal static class AppUpdateRunner
{
    static int Main(string[] args) {
        try {
            string mode=args[0],root=args[1];
            if(mode=="verify") {
                Console.WriteLine(StandaloneAppUpdate.ReadInventory(root,true).version);
                StandaloneAppUpdate.ParseRelease(File.ReadAllBytes(args[2]));
            } else if(mode=="version") {
                if(!(StandaloneAppUpdate.ParseVersion("0.1.10")>StandaloneAppUpdate.ParseVersion("0.1.9")))throw new Exception("Version ordering");
                if(StandaloneAppUpdate.ParseVersion("1.0.2")!=StandaloneAppUpdate.ParseVersion("1.0.2.0"))throw new Exception("Legacy version normalization");
                if(!(StandaloneAppUpdate.ParseVersion("1.0.2.1")>StandaloneAppUpdate.ParseVersion("1.0.2")))throw new Exception("Revision ordering");
                if(!(StandaloneAppUpdate.ParseVersion("1.0.2.10")>StandaloneAppUpdate.ParseVersion("1.0.2.9")))throw new Exception("Numeric revision ordering");
                if(!(StandaloneAppUpdate.ParseVersion("1.0.3.1")>StandaloneAppUpdate.ParseVersion("1.0.2.99")))throw new Exception("Upstream ordering");
                foreach(string s in new[]{"1.2","01.2.3","1.2.3.4.5","1.2.3.01","1.2.3.-1","1.2.3.65535","65535.2.3","v1.2.3","1.2.3\n","-1.2.3"}) {
                    bool rejected=false;try{StandaloneAppUpdate.ParseVersion(s);}catch{rejected=true;}
                    if(!rejected)throw new Exception("Accepted version "+s);
                }
                foreach(string url in new[]{"http://wesimglide.org/otherdata/tools/Standalone-DLSS-Glass-Cockpit-Refresh.zip","https://example.org/update.zip",StandaloneAppUpdate.Archive+"?redirect=1"}) {
                    bool rejected=false;try{StandaloneAppUpdate.CheckUrl(new Uri(url));}catch{rejected=true;}
                    if(!rejected)throw new Exception("Accepted URL "+url);
                }
            } else if(mode=="prepare") {
                var release=StandaloneAppUpdate.ParseRelease(File.ReadAllBytes(args[2]));
                StandaloneAppUpdate.Prepare(root,release,(uri,limit)=>{
                    if(uri.AbsoluteUri!=StandaloneAppUpdate.Archive||limit!=release.size)throw new Exception("Download contract");
                    return File.ReadAllBytes(args[3]);
                });
            } else if(mode=="apply"||mode=="fail"||mode=="crash"||mode=="commit-crash") {
                StandaloneAppUpdate.Apply(root,index=>{
                    if(mode=="fail"&&index==8)throw new IOException("Injected replacement failure");
                    if(mode=="crash"&&index==8)Environment.Exit(71);
                    if(mode=="commit-crash"&&index==-1)Environment.Exit(72);
                });
            } else if(mode=="recover")StandaloneAppUpdate.Recover(root);
            else if(mode=="startup")Console.WriteLine("Launched="+StandaloneAppUpdate.Startup(root));
            else if(mode=="handoff") {
                using(var gate=StandaloneAppUpdate.Enter(root)) {
                    if(gate==null)throw new Exception("Gate unavailable");
                    StandaloneAppUpdate.Launch(root,false);
                    // Verify helper really waits for its parent before replacing files.
                    Thread.Sleep(600);
                    if(File.Exists(Path.Combine(root,"reopened.txt")))throw new Exception("Helper did not wait");
                    if(StandaloneAppUpdate.ReadInventory(root,true).version!=ProductVersion.Value)throw new Exception("Replaced before exit");
                }
            } else if(mode=="check"||mode=="offline"||mode=="defer") {
                bool restart=StandaloneAppUpdate.CheckForUpdateAsync(root,Console.WriteLine,()=>mode!="defer",(uri,limit)=>{
                    StandaloneAppUpdate.CheckUrl(uri);
                    if(mode=="offline")throw new System.Net.WebException("Simulated unavailable server");
                    return File.ReadAllBytes(uri.AbsoluteUri==StandaloneAppUpdate.Feed?args[2]:args[3]);
                },()=>File.WriteAllText(Path.Combine(root,"launch-requested.txt"),"yes")).GetAwaiter().GetResult();
                Console.WriteLine("Restart="+restart);
            } else if(mode=="gate") {
                using(var gate=StandaloneAppUpdate.Enter(root)) {
                    bool blocked=false;
                    var t=new Thread(()=>{using(var second=StandaloneAppUpdate.Enter(root))blocked=second==null;});t.Start();t.Join();
                    if(!blocked)throw new Exception("Two instances acquired updater lock");
                }
            } else if(mode=="locked") {
                using(var locked=new FileStream(Path.Combine(root,"StandaloneGlassGraphics.dll"),FileMode.Open,FileAccess.Read,FileShare.Read)) {
                    bool failed=false;try { StandaloneAppUpdate.Apply(root,null); }catch(IOException) { failed=true; }
                    if(!failed)throw new Exception("Locked file was replaced");
                }
            } else throw new Exception("Unknown test mode");
            return 0;
        }catch(Exception e) { Console.Error.WriteLine(e);return 1; }
    }
}
