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
   var release=json.Deserialize<Dictionary<string,object>>(await web.DownloadStringTaskAsync("https://api.github.com/repos/"+Repo+"/releases/latest?check="+DateTime.UtcNow.Ticks));
   string tag=(string)release["tag_name"], version=tag.TrimStart('v');
   Version latest=Version.Parse(version);
   if(version!=latest.ToString())throw new Exception("잘못된 릴리스 버전입니다.");
   if(latest<=Version.Parse(current)){Activate(root,current);return null;}
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
    return InstallVerified(root,version,staging);
   }finally{if(Directory.Exists(staging))Directory.Delete(staging,true);}
  }
 }
 public static string InstallVerified(string root,string version,string staging){
  Version.Parse(version);Directory.CreateDirectory(Path.Combine(root,"versions"));string destination=Path.Combine(root,"versions",version);
  if(Directory.Exists(destination)){
   bool matches=true;foreach(string name in new[]{"Application.exe","Mono.Cecil.dll","THIRD-PARTY-NOTICES.txt"}){string existing=Path.Combine(destination,name);if(!File.Exists(existing) || Patcher.Hash(existing)!=Patcher.Hash(Path.Combine(staging,name)))matches=false;}
   if(!matches){
    string backup=Path.Combine(root,"versions",".previous-"+version+"-"+Guid.NewGuid().ToString("N"));
    try{Directory.Move(destination,backup);}catch(IOException){throw new Exception("교체할 버전의 런처가 실행 중이거나 파일이 잠겨 있습니다. 다른 런처 창을 닫고 다시 시도하세요.");}
    try{Directory.Move(staging,destination);}catch{Directory.Move(backup,destination);throw;}
   }
  }else Directory.Move(staging,destination);
  Activate(root,version);return Path.Combine(destination,"Application.exe");
 }
 public static void Activate(string root,string version){
  Version.Parse(version);string pointer=Path.Combine(root,"current.txt");
  if(File.Exists(pointer) && File.ReadAllText(pointer).Trim()==version)return;
  string temp=pointer+"."+Guid.NewGuid().ToString("N")+".new";
  try{File.WriteAllText(temp,version);if(File.Exists(pointer))File.Replace(temp,pointer,Path.Combine(root,"previous.txt"));else File.Move(temp,pointer);}finally{if(File.Exists(temp))File.Delete(temp);}
 }

}
