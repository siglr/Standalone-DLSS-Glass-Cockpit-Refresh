using System;
using System.IO;
internal static class NativeInstallerRunner {
 static int Main(string[] args) {
  try {
   if(args[0]=="discover")Console.WriteLine(SimulatorDiscovery.Find(args.Length>1?new[]{args[1]}:null));
   else if(args[0]=="locked") {
    using(var locked=new FileStream(Path.Combine(args[2],"StandaloneGlassGraphics.dll"),FileMode.Open,FileAccess.Read,FileShare.Read)) {
     bool failed=false;try { NativeInstaller.Run(args[1],args[2],false,false,null); }catch(IOException) { failed=true; }
     if(!failed)throw new Exception("Locked DLL was replaced");
    }
   }
   else Console.WriteLine(NativeInstaller.Run(args[1],args[2],args[0]=="uninstall",Array.IndexOf(args,"replace")>=0,index=>{
    if(args[0]=="fail"&&index==2)throw new IOException("Injected failure");
    if(args[0]=="crash"&&index==2)Environment.Exit(71);
    if(args[0]=="commit-crash"&&index==-1)Environment.Exit(72);
   }));
   return 0;
  }catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}
 }
}
