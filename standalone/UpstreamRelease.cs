using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// Fixed upstream discovery/download. Installation requires an explicit version selection.
// The bundled DLL stays pinned by our signed package inventory and offline validation.
internal static class UpstreamRelease
{
    internal const string Repository="https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix";
    internal const string Version="1.0.2";
    internal const string ZipHash="010abd02f3b25f35fe06ff7376ce43920ef808fbf6afe5c0b100a20785d1eb94";
    internal const string DllHash="5b505a00e5a072f6c2a9b37a6f19800f248850ace6fc51dcb802c0043a560d03";
    internal const string AddonName="glass-cockpit-dlss-fix.addon64";
    internal const string AddonHash="9474b66e2e22bc005233463574fc9ba9c107d4bfa8364e5c3478588f0e8f2036";
    internal sealed class Asset { public string name {get;set;} public string digest {get;set;} public string browser_download_url {get;set;} public long size {get;set;} }
    internal sealed class Release { public long id {get;set;} public string tag_name {get;set;} public string body {get;set;} public bool draft {get;set;} public bool prerelease {get;set;} public Asset[] assets {get;set;} }
    internal sealed class Choice {
        internal long Id; internal string Tag, Sha256, DllSha256, Url; internal long Size;
        internal bool Addon;
        internal bool IsLatest { get;set; }
        public override string ToString() { return Id==-1?"Latest official release (recommended)":Id==0 ? "v"+Version+" (bundled / offline)" : Tag+(IsLatest?" (latest)":""); }
    }
    internal sealed class Prepared {
        internal readonly Choice Release; internal readonly byte[] Dll;
        internal Prepared(Choice release,byte[] dll) { Release=release;Dll=dll; }
    }
    const string Api="https://api.github.com/repos/IFreemz/msfs2024-glass-cockpit-dlss-fix/releases";
    // Accept only the author's explicitly named standalone checksum line, never another asset's hash.
    internal static string[] NoteHashes(string body) {
        if(body==null||body.Length>1024*1024)return null;
        var matches=Regex.Matches(body,@"(?im)^\s*-\s*`glass-cockpit-dlss-fix-standalone\.zip`\s*:\s*`([0-9a-f]{64})`\s*\(its\s*`dxgi\.dll`\s*:\s*`([0-9a-f]{64})`\)\s*$");
        var zip=Regex.Matches(body,@"(?im)^\s*-\s*`glass-cockpit-dlss-fix-standalone\.zip`\s*:\s*`([0-9a-f]{64})`\s*$");
        var dll=Regex.Matches(body,@"(?im)^\s*-\s*`dxgi\.dll`\s*\(inside the zip\)\s*:\s*`([0-9a-f]{64})`\s*$");
        if(matches.Count==1&&zip.Count==0&&dll.Count==0)return new[]{matches[0].Groups[1].Value.ToLowerInvariant(),matches[0].Groups[2].Value.ToLowerInvariant()};
        if(matches.Count==0&&zip.Count==1&&dll.Count==1)return new[]{zip[0].Groups[1].Value.ToLowerInvariant(),dll[0].Groups[1].Value.ToLowerInvariant()};
        return null;
    }
    internal static Choice Validate(Release release,bool addon=false) {
        if(release==null||release.id<=0||release.draft||release.prerelease||release.tag_name==null||!Regex.IsMatch(release.tag_name,@"\Av\d{1,5}\.\d{1,5}\.\d{1,5}\z")||release.assets==null)return null;
        try { StandaloneAppUpdate.ParseVersion(release.tag_name.Substring(1)); }
        catch(InvalidDataException) { return null; }
        string assetName=addon?AddonName:"glass-cockpit-dlss-fix-standalone.zip";
        var assets=release.assets.Where(item=>item!=null&&item.name==assetName).ToArray();
        if(assets.Length!=1)return null;
        var a=assets[0];
        var hashes=NoteHashes(release.body);
        if(addon) {
            var matches=Regex.Matches(release.body??"",@"(?im)^\s*-\s*`glass-cockpit-dlss-fix\.addon64`\s*:\s*`([0-9a-f]{64})`\s*$");
            hashes=matches.Count==1?new[]{matches[0].Groups[1].Value.ToLowerInvariant(),matches[0].Groups[1].Value.ToLowerInvariant()}:null;
        }
        string url=Repository+"/releases/download/"+release.tag_name+"/"+assetName;
        if(a.browser_download_url!=url||a.size<=0||a.size>32*1024*1024||a.digest==null||!Regex.IsMatch(a.digest,@"\Asha256:[0-9a-f]{64}\z"))return null;
        if(hashes==null||hashes[0]!=a.digest.Substring(7))return null;
        return new Choice { Id=release.id,Tag=release.tag_name,Sha256=hashes[0],DllSha256=hashes[1],Url=url,Size=a.size,Addon=addon };
    }
    internal static Choice[] ParseChoices(string json,bool addon=false) {
        if(json.Length>2*1024*1024)throw new InvalidDataException("Upstream response too large.");
        var releases=new JavaScriptSerializer { MaxJsonLength=2*1024*1024,RecursionLimit=16 }.Deserialize<Release[]>(json);
        if(releases==null||releases.Length>100)throw new InvalidDataException("Invalid upstream release list.");
        return releases.Select(r=>Validate(r,addon)).Where(c=>c!=null).GroupBy(c=>c.Tag).Select(g=>g.First())
            .OrderByDescending(c=>StandaloneAppUpdate.ParseVersion(c.Tag.Substring(1))).Take(10).ToArray();
    }
    internal static Choice[] List(bool addon=false) { return ParseChoices(Encoding.UTF8.GetString(Download(Api+"?per_page=100",2*1024*1024,false)),addon); }
    internal static Choice Latest(bool addon=false) {
        // GitHub's latest stable release must verify; never silently install an older release instead.
        var release=new JavaScriptSerializer { MaxJsonLength=2*1024*1024,RecursionLimit=16 }.Deserialize<Release>(
            Encoding.UTF8.GetString(Download(Api+"/latest",2*1024*1024,false)));
        return ValidateLatest(release,addon);
    }
    internal static Choice ValidateLatest(Release release,bool addon=false) {
        var choice=Validate(release,addon);
        if(choice==null)throw new InvalidDataException("The latest official release cannot be verified against its release-note checksums. Nothing was installed. Try again later, or explicitly choose a verified rollback version.");
        return choice;
    }
    internal static Prepared Load(string package,long id,bool download,Choice expected=null,bool addon=false) {
        if(expected!=null)addon=expected.Addon;
        if(id<=0)throw new InvalidDataException("Invalid upstream release ID.");
        string json=Encoding.UTF8.GetString(Download(Api+"/"+id,1024*1024,false));
        var release=new JavaScriptSerializer { MaxJsonLength=1024*1024,RecursionLimit=16 }.Deserialize<Release>(json);
        var choice=Validate(release,addon);
        if(choice==null||choice.Id!=id)throw new InvalidDataException("This upstream standalone release cannot be verified.");
        if(expected!=null&&(choice.Sha256!=expected.Sha256||choice.DllSha256!=expected.DllSha256))throw new InvalidDataException("The upstream release changed. Check for releases again before installing.");
        string folder=Path.Combine(package,"upstream-downloads");NativeInstaller.Safe(folder);
        string zip=Path.Combine(folder,id+(addon?".addon64":".zip"));NativeInstaller.Safe(zip);
        byte[] bytes;
        if(download) {
            bytes=Download(choice.Url,32*1024*1024,true);
            var prepared=ReadArchive(choice,bytes);
            Directory.CreateDirectory(folder);File.WriteAllBytes(zip,bytes);
            return prepared;
        }
        if(!File.Exists(zip)||new FileInfo(zip).Length>32*1024*1024)throw new InvalidDataException("Download the selected DLL again.");
        return ReadArchive(choice,File.ReadAllBytes(zip));
    }
    internal static Prepared ReadArchive(Choice choice,byte[] bytes) {
        if(bytes.LongLength!=choice.Size||StandaloneAppUpdate.Hash(bytes)!=choice.Sha256)throw new InvalidDataException("Upstream archive checksum differs. Nothing was installed.");
        if(choice.Addon) { CheckDll(bytes);return new Prepared(choice,bytes); }
        using(var stream=new MemoryStream(bytes))using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)) {
            var expected=new HashSet<string>(new[]{"dxgi.dll","LICENSE","NOTICE.md","README.txt"},StringComparer.Ordinal);
            long total=0;
            foreach(var entry in zip.Entries) {
                if(!expected.Remove(entry.FullName)||entry.Length>32*1024*1024)throw new InvalidDataException("Unexpected upstream archive layout.");
                total+=entry.Length;
            }
            if(expected.Count!=0||total>64*1024*1024)throw new InvalidDataException("Incomplete upstream archive, including licenses.");
            using(var input=zip.GetEntry("dxgi.dll").Open())using(var output=new MemoryStream()) {
                var buffer=new byte[8192];int count;
                while((count=input.Read(buffer,0,buffer.Length))>0) {
                    if(output.Length+count>32*1024*1024)throw new InvalidDataException("Expanded DLL exceeds its limit.");
                    output.Write(buffer,0,count);
                }
                byte[] dll=output.ToArray();
                if(StandaloneAppUpdate.Hash(dll)!=choice.DllSha256)throw new InvalidDataException("Upstream DLL differs from its release-note SHA-256. Nothing was installed.");
                CheckDll(dll);
                return new Prepared(choice,dll);
            }
        }
    }
    static void CheckDll(byte[] dll) {
        if(dll.Length<256||dll[0]!='M'||dll[1]!='Z')throw new InvalidDataException("Upstream file is not a Windows DLL.");
        int offset=BitConverter.ToInt32(dll,60);
        if(offset<64||offset>dll.Length-24||BitConverter.ToUInt32(dll,offset)!=0x4550||BitConverter.ToUInt16(dll,offset+4)!=0x8664||(BitConverter.ToUInt16(dll,offset+22)&0x2000)==0)
            throw new InvalidDataException("Upstream file is not an x64 DLL.");
    }
    static byte[] Download(string url,long limit,bool asset) {
        ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
        for(int redirects=0;redirects<=3;redirects++) {
            var uri=new Uri(url);
            bool allowed=uri.Scheme=="https"&&uri.Port==443&&uri.UserInfo.Length==0&&uri.Fragment.Length==0&&
                (asset ? (uri.Host=="github.com"&&uri.AbsolutePath.StartsWith("/IFreemz/msfs2024-glass-cockpit-dlss-fix/releases/download/",StringComparison.Ordinal))||uri.Host=="release-assets.githubusercontent.com" : uri.Host=="api.github.com"&&url.StartsWith(Api,StringComparison.Ordinal));
            if(!allowed)throw new InvalidDataException("Unexpected upstream download address.");
            var request=(HttpWebRequest)WebRequest.Create(uri);request.AllowAutoRedirect=false;request.Timeout=8000;request.ReadWriteTimeout=8000;
            request.UserAgent="Standalone-DLSS-Glass-Cockpit-Refresh/"+ProductVersion.Value;
            request.CachePolicy=new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            using(var response=(HttpWebResponse)request.GetResponse()) {
                if(asset&&(int)response.StatusCode>=300&&(int)response.StatusCode<400) { url=new Uri(uri,response.Headers["Location"]).AbsoluteUri;continue; }
                if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>limit)throw new InvalidDataException("Upstream download unavailable.");
                using(var input=response.GetResponseStream())using(var output=new MemoryStream()) {
                    var watch=System.Diagnostics.Stopwatch.StartNew();var buffer=new byte[8192];int count;
                    while((count=input.Read(buffer,0,buffer.Length))>0) {
                        if(output.Length+count>limit||watch.Elapsed.TotalSeconds>60)throw new InvalidDataException("Upstream response exceeded its limit.");
                        output.Write(buffer,0,count);
                    }
                    return output.ToArray();
                }
            }
        }
        throw new InvalidDataException("Too many upstream redirects.");
    }
}

