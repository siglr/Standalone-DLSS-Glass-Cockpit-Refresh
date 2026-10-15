using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Win32;

internal static class SimulatorDiscovery
{
    static Dictionary<string,object> Vdf(string path) {
        if(new FileInfo(path).Length>2*1024*1024)throw new InvalidDataException("Steam metadata too large.");
        var tokens=Regex.Matches(File.ReadAllText(path),"\"(?:\\\\.|[^\"\\\\])*\"|[{}]|//[^\\r\\n]*|[^\\s]+");int i=0;
        var result=Object(tokens,ref i,0,false);return result;
    }
    static Dictionary<string,object> Object(MatchCollection t,ref int i,int depth,bool nested) {
        if(depth>12)throw new InvalidDataException("Steam metadata too deep.");
        var d=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
        while(i<t.Count) {
            string k=t[i++].Value;if(k.StartsWith("//"))continue;
            if(k=="}") { if(!nested)throw new InvalidDataException();return d; }
            k=Unquote(k);while(i<t.Count&&t[i].Value.StartsWith("//"))i++;
            if(i>=t.Count)throw new InvalidDataException();string v=t[i++].Value;
            d.Add(k,v=="{"?(object)Object(t,ref i,depth+1,true):Unquote(v));
        }
        if(nested)throw new InvalidDataException();return d;
    }
    static string Unquote(string s) {
        if(s.Length<2||s[0]!='"'||s[s.Length-1]!='"')throw new InvalidDataException();
        return s.Substring(1,s.Length-2).Replace("\\\\","\\").Replace("\\\"","\"");
    }
    static bool Xbox(string path) {
        var settings=new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1024*1024 };
        using(var r=XmlReader.Create(Path.Combine(path,"MicrosoftGame.Config"),settings)) {
            while(r.Read())if(r.NodeType==XmlNodeType.Element&&r.Name=="Identity"&&r.GetAttribute("Name")=="Microsoft.Limitless")return true;
        }
        return false;
    }
    internal static string Find(string[] roots) {
        bool isolated=roots!=null&&roots.Length>0;
        if(!isolated)roots=DriveInfo.GetDrives().Where(d=>d.IsReady&&d.DriveType==DriveType.Fixed).Select(d=>d.RootDirectory.FullName).ToArray();
        var candidates=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var steam=new HashSet<string>(roots,StringComparer.OrdinalIgnoreCase);
        var confirmed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string root in roots) {
            candidates.Add(root);candidates.Add(Path.Combine(root,"Content"));
            try { foreach(string game in Directory.GetDirectories(Path.Combine(root,"XboxGames")))candidates.Add(Path.Combine(game,"Content")); }catch(IOException) {}catch(UnauthorizedAccessException) {}
        }
        if(!isolated) {
            foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64}) {
                try { using(var h=RegistryKey.OpenBaseKey(hive,view))using(var k=h.OpenSubKey(@"Software\Valve\Steam"))if(k!=null)foreach(string n in new[]{"SteamPath","InstallPath"}) { var p=k.GetValue(n) as string;if(!String.IsNullOrWhiteSpace(p))steam.Add(p); } }catch(System.Security.SecurityException) {}
            }
            steam.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
            // Xbox custom install roots are recorded by the Gaming Services .GamingRoot marker.
            foreach(string drive in roots)try {
                string markerPath=Path.Combine(drive,".GamingRoot");
                if(new FileInfo(markerPath).Length>=4096)continue;
                byte[] marker=File.ReadAllBytes(markerPath);
                if(marker.Length>8&&marker.Length<4096&&BitConverter.ToUInt32(marker,0)==0x58424752&&BitConverter.ToUInt32(marker,4)==1) {
                    string relative=System.Text.Encoding.Unicode.GetString(marker,8,marker.Length-8).TrimEnd('\0');
                    string p=Path.GetFullPath(Path.Combine(drive,relative));
                    if(p.StartsWith(drive,StringComparison.OrdinalIgnoreCase))foreach(string game in Directory.GetDirectories(p))candidates.Add(Path.Combine(game,"Content"));
                }
            }catch(IOException) {}catch(UnauthorizedAccessException) {}catch(ArgumentException) {}
        }
        var libraries=new HashSet<string>(steam,StringComparer.OrdinalIgnoreCase);
        foreach(string root in steam)foreach(string rel in new[]{"steamapps/libraryfolders.vdf","config/libraryfolders.vdf"})try {
            var folders=(Dictionary<string,object>)Vdf(Path.Combine(root,rel))["libraryfolders"];
            foreach(var pair in folders) {
                if(!Regex.IsMatch(pair.Key,"^[0-9]+$"))continue;
                string p=pair.Value as string;
                if(p==null)p=((Dictionary<string,object>)pair.Value)["path"] as string;
                if(!String.IsNullOrWhiteSpace(p)&&Path.IsPathRooted(p))libraries.Add(p);
            }
        }catch(Exception e) { if(!(e is InvalidDataException||e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is KeyNotFoundException||e is InvalidCastException))throw; }
        foreach(string library in libraries)try {
            var app=(Dictionary<string,object>)Vdf(Path.Combine(library,"steamapps/appmanifest_2537590.acf"))["AppState"];
            if((string)app["appid"]!="2537590")continue;
            string name=(string)app["installdir"];if(String.IsNullOrWhiteSpace(name)||Path.IsPathRooted(name))continue;
            string common=Path.GetFullPath(Path.Combine(library,"steamapps/common")).TrimEnd('\\')+"\\";
            string path=Path.GetFullPath(Path.Combine(common,name));
            if(!path.StartsWith(common,StringComparison.OrdinalIgnoreCase))continue;
            candidates.Add(path);confirmed.Add(path);
        }catch(Exception e) { if(!(e is InvalidDataException||e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is KeyNotFoundException||e is InvalidCastException))throw; }
        var found=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string candidate in candidates)try {
            string path=Path.GetFullPath(candidate);
            if(Regex.IsMatch(path,@"\\WindowsApps(\\|$)",RegexOptions.IgnoreCase)||!File.Exists(Path.Combine(path,"FlightSimulator2024.exe")))continue;
            if(confirmed.Contains(path)||(File.Exists(Path.Combine(path,"MicrosoftGame.Config"))?Xbox(path):isolated&&roots.Contains(candidate)))found.Add(path);
        }catch(IOException) {}catch(UnauthorizedAccessException) {}catch(XmlException) {}catch(ArgumentException) {}
        if(found.Count==1)return found.First();
        throw new InvalidOperationException(found.Count>1?"Multiple MSFS 2024 installations found. Choose the one you use.":"Choose the folder containing FlightSimulator2024.exe with Browse.");
    }
}
