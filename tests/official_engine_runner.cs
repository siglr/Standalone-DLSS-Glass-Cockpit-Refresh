using System;
using System.IO;
using System.Web.Script.Serialization;
internal static class OfficialEngineRunner {
    static int Main(string[] args) {
        try {
            if(args[0]=="list") {
                foreach(var choice in UpstreamRelease.ParseChoices(File.ReadAllText(args[1])))Console.WriteLine(choice.Id+" "+choice.Tag);
            } else if(args[0]=="live-list") {
                foreach(var choice in UpstreamRelease.List())Console.WriteLine(choice.Id+" "+choice.Tag);
            } else if(args[0]=="latest"||args[0]=="live-latest") {
                var choice=args[0]=="live-latest"?UpstreamRelease.Latest():UpstreamRelease.ValidateLatest(new JavaScriptSerializer().Deserialize<UpstreamRelease.Release>(File.ReadAllText(args[1])));
                Console.WriteLine(choice.Id+" "+choice.Tag);
            } else if(args[0]=="live-download") {
                var prepared=UpstreamRelease.Load(args[1],Int64.Parse(args[2]),true);
                Console.WriteLine(StandaloneAppUpdate.Hash(prepared.Dll));
            } else if(args[0]=="archive"||args[0]=="engine") {
                var release=new JavaScriptSerializer().Deserialize<UpstreamRelease.Release>(File.ReadAllText(args[1]));
                var choice=UpstreamRelease.Validate(release);
                if(choice==null)throw new Exception("Unverifiable release");
                var prepared=UpstreamRelease.ReadArchive(choice,File.ReadAllBytes(args[2]));
                if(args[0]=="engine")Console.WriteLine(NativeInstaller.Run(args[3],args[4],false,false,null,prepared));
                else Console.WriteLine(StandaloneAppUpdate.Hash(prepared.Dll));
            } else if(args[0]=="recover")NativeInstaller.Recover(args[1]);
            else Console.WriteLine(NativeInstaller.Run(args[1],args[2],args[0]=="uninstall",Array.IndexOf(args,"replace")>=0,index=>{
                if(args[0]=="fail"&&index==2)throw new IOException("Injected migration failure");
                if(args[0].StartsWith("fail-")&&index==Int32.Parse(args[0].Substring(5)))throw new IOException("Injected failure at boundary "+index);
                if(args[0]=="crash"&&index==2)Environment.Exit(71);
            }));
            return 0;
        } catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}
    }
}
