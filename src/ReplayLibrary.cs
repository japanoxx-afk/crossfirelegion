using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Web.Script.Serialization;
using System.IO.Compression;
public sealed class ReplayEntry {
 public string File,Name,Map,Players,Build,Mode,State,Error;
 public long Bytes;
 public Dictionary<string,string> Metadata;
 public override string ToString(){return Name+" · "+Players+" · "+State;}
}
public static class ReplayLibrary {
 public static ReplayEntry Inspect(string path){
  var entry=new ReplayEntry{File=Path.GetFullPath(path),Name=Path.GetFileNameWithoutExtension(path),State="기존 기록 · 호환성 미확인",Players="",Map="",Build="",Mode="",Metadata=new Dictionary<string,string>()};
  try{
   entry.Metadata=ReplayProtocol.Read(ReplayProtocol.Metadata(path));
   using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var reader=new BinaryReader(stream)){
    entry.Bytes=stream.Length;int size=reader.ReadInt32();if(size<2 || size>1024*1024 || size>stream.Length-4)throw new InvalidDataException("잘못되거나 불완전한 헤더");
    var json=new JavaScriptSerializer{MaxJsonLength=1024*1024,RecursionLimit=64};var header=json.Deserialize<Dictionary<string,object>>(new UTF8Encoding(false,true).GetString(reader.ReadBytes(size)));
    foreach(string key in new[]{"LevelToLoad","RNGSeed","PlayerLoadouts","GameModeID","BuildName"})if(!header.ContainsKey(key))throw new InvalidDataException("필수 헤더 누락: "+key);
    entry.Build=Convert.ToString(header["BuildName"]);entry.Map=Convert.ToString(header["LevelToLoad"]);entry.Mode=Convert.ToString(header["GameModeID"]);
    var names=new List<string>();foreach(object item in (IEnumerable)header["PlayerLoadouts"]){var slot=item as Dictionary<string,object>;if(slot!=null && slot.ContainsKey("Name") && !String.IsNullOrWhiteSpace(Convert.ToString(slot["Name"])))names.Add(Convert.ToString(slot["Name"]));}entry.Players=String.Join(" / ",names);
    if(stream.Position==stream.Length)throw new InvalidDataException("기록된 틱이 없습니다.");
   }
   string state=ReplayProtocol.Value(entry.Metadata,"State");if(state=="closed")entry.State="기록 종료 · 전체 재생 미검증";
   if(ReplayProtocol.Value(entry.Metadata,"PlaybackState")=="checksum_eof")entry.State+=" · 재생 EOF/체크섬 통과 이력";
   else if(state=="recording" || state=="finalizing")entry.State=RecorderAlive(entry.Metadata)?"녹화 중 / 마무리 중":"미완성 기록 · 정상 종료 확인 안 됨";
   else if(state=="failed"){entry.State="녹화 실패";entry.Error=ReplayProtocol.Value(entry.Metadata,"Error");}
  }catch(Exception e){entry.State="읽기 실패";entry.Error=e.Message;}
  return entry;
 }
 static bool RecorderAlive(Dictionary<string,string> data){try{int id;long start;if(!Int32.TryParse(ReplayProtocol.Value(data,"ProcessId"),out id)||!Int64.TryParse(ReplayProtocol.Value(data,"ProcessStartUtc"),out start))return false;using(var p=Process.GetProcessById(id))return !p.HasExited&&p.StartTime.ToUniversalTime().Ticks==start;}catch{return false;}}
 public static ReplayEntry[] List(){var files=new List<string>();if(Directory.Exists(ReplayProtocol.NativeFolder))files.AddRange(Directory.GetFiles(ReplayProtocol.NativeFolder,"*.rply"));string imported=Path.Combine(ReplayProtocol.Home,"imports");if(Directory.Exists(imported))files.AddRange(Directory.GetFiles(imported,"*.rply",SearchOption.AllDirectories));return files.OrderByDescending(File.GetLastWriteTimeUtc).Select(Inspect).ToArray();}
 public static string Request(string game,ReplayEntry entry,bool allowLegacy){
  if(entry.Error!=null)throw new InvalidDataException(entry.Error);
  if(new FileInfo(entry.File).Length>64L*1024*1024)throw new InvalidDataException("현재 재생 검증 범위는 파일당 64MB까지입니다.");
  if(RecorderAlive(entry.Metadata))throw new IOException("녹화가 끝난 뒤 재생하세요.");
  if(Process.GetProcessesByName("Crossfire_Legion").Length!=0)throw new InvalidOperationException("게임을 정상 종료한 후 리플레이를 실행하세요.");
  SteamLaunch.Prepare(game);string fingerprint=ReplayProtocol.Fingerprint(game);string recorded=ReplayProtocol.Value(entry.Metadata,"Fingerprint");
  if(recorded!="" && recorded!=fingerprint)throw new InvalidDataException("기록 당시 게임·패치와 현재 버전이 다릅니다. 동일 버전에서 재생하세요.");
  if((recorded=="" || ReplayProtocol.Value(entry.Metadata,"State")!="closed") && !allowLegacy)throw new InvalidDataException("패치 버전 또는 정상 종료 정보가 없습니다. 호환성 미확인 재생을 명시적으로 선택하세요.");
  string hash=ReplayProtocol.Digest(entry.File),stored=ReplayProtocol.Value(entry.Metadata,"FileHash");if(stored!="" && stored!=hash)throw new InvalidDataException("기록 파일 체크섬이 다릅니다.");
  string request=Path.Combine(ReplayProtocol.Home,"play-request.ini");ReplayProtocol.Write(request,new Dictionary<string,string>{{"Game",Path.GetFullPath(game)},{"File",entry.File},{"FileHash",hash},{"Fingerprint",fingerprint},{"ExpiresUtc",DateTime.UtcNow.AddMinutes(5).Ticks.ToString()}});return request;
 }
 public static string Import(string file){
  string destination=Path.Combine(ReplayProtocol.Home,"imports",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(destination);string target=Path.Combine(destination,"replay.rply");
  if(file.EndsWith(".rply",StringComparison.OrdinalIgnoreCase))File.Copy(file,target);
  else using(var zip=ZipFile.OpenRead(file)){
   if(zip.Entries.Count!=2 || zip.GetEntry("replay.rply")==null || zip.GetEntry("metadata.ini")==null)throw new InvalidDataException("지원하지 않는 리플레이 묶음입니다.");
   foreach(var e in zip.Entries)if(e.Length>(e.FullName=="metadata.ini"?2*1024*1024:512L*1024*1024))throw new InvalidDataException("리플레이 묶음이 너무 큽니다.");
   zip.GetEntry("replay.rply").ExtractToFile(target);zip.GetEntry("metadata.ini").ExtractToFile(Path.Combine(destination,"metadata.ini"));
   var data=ReplayProtocol.Read(Path.Combine(destination,"metadata.ini"));if(ReplayProtocol.Value(data,"FileHash")!=ReplayProtocol.Digest(target))throw new InvalidDataException("가져온 리플레이 체크섬 오류");data["File"]=target;ReplayProtocol.Write(ReplayProtocol.Metadata(target),data);
  }
  var result=Inspect(target);if(result.Error!=null)throw new InvalidDataException(result.Error);return target;
 }
 public static void Export(ReplayEntry entry,string file){
  if(RecorderAlive(entry.Metadata))throw new IOException("녹화가 끝난 뒤 내보내세요.");if(entry.Error!=null)throw new InvalidDataException(entry.Error);
  string temp=Path.Combine(ReplayProtocol.Home,"export-"+Guid.NewGuid().ToString("N")+".ini");var info=new Dictionary<string,string>(entry.Metadata);foreach(string key in new[]{"File","ProcessId","ProcessStartUtc","Error"})info.Remove(key);info["FileHash"]=ReplayProtocol.Digest(entry.File);ReplayProtocol.Write(temp,info);
  try{using(var zip=ZipFile.Open(file,ZipArchiveMode.Create)){zip.CreateEntryFromFile(entry.File,"replay.rply",CompressionLevel.Optimal);zip.CreateEntryFromFile(temp,"metadata.ini");}}finally{System.IO.File.Delete(temp);}
 }
}
