using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Collections.Generic;
public static class Updater {
 public const string Repo="japanoxx-afk/crossfirelegion";
 public static async Task<string> Update(string root, string current){
  ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
  using(var web=new WebClient()){
   web.Headers["User-Agent"]="CrossfireLegionLauncher/"+current;
   var json=new JavaScriptSerializer();
   var release=json.Deserialize<Dictionary<string,object>>(await web.DownloadStringTaskAsync("https://api.github.com/repos/"+Repo+"/releases/latest"));
   string tag=(string)release["tag_name"], version=tag.TrimStart('v');
   Version latest=Version.Parse(version);
   if(version!=latest.ToString())throw new Exception("잘못된 릴리스 버전입니다.");
   if(latest<=Version.Parse(current))return null;
   string prefix="https://github.com/"+Repo+"/releases/download/"+Uri.EscapeDataString(tag)+"/";
   string staging=Path.Combine(root,"versions",".download-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
   try{
    string zip=Path.Combine(staging,"update.zip");
    await web.DownloadFileTaskAsync(prefix+"update.zip",zip);
    string expected=(await web.DownloadStringTaskAsync(prefix+"update.sha256")).Trim().Split(' ')[0].ToLowerInvariant();
    if(expected.Length!=64 || Patcher.Hash(zip)!=expected)throw new Exception("업데이트 무결성 검사 실패");
    using(var archive=ZipFile.OpenRead(zip)){
     string[] names={"Application.exe","Mono.Cecil.dll","THIRD-PARTY-NOTICES.txt"};
     if(archive.Entries.Count!=names.Length)throw new Exception("예상하지 못한 업데이트 파일 구성");
     foreach(string name in names){var entry=archive.GetEntry(name);if(entry==null || entry.Length>50*1024*1024)throw new Exception("잘못된 업데이트 파일");entry.ExtractToFile(Path.Combine(staging,name));}
    }
    File.Delete(zip);
    var binaryVersion=System.Reflection.AssemblyName.GetAssemblyName(Path.Combine(staging,"Application.exe")).Version;
    if(binaryVersion.Major!=latest.Major || binaryVersion.Minor!=latest.Minor || binaryVersion.Build!=latest.Build)throw new Exception("릴리스와 실행 파일 버전이 다릅니다.");
    string destination=Path.Combine(root,"versions",version);
    if(Directory.Exists(destination))throw new Exception("대상 버전 폴더가 이미 있습니다: "+destination);
    Directory.Move(staging,destination);
    string pointer=Path.Combine(root,"current.txt"), temp=pointer+".new";
    File.WriteAllText(temp,version);File.Replace(temp,pointer,Path.Combine(root,"previous.txt"));
    return Path.Combine(destination,"Application.exe");
   }finally{if(Directory.Exists(staging))Directory.Delete(staging,true);}
  }
 }
}
