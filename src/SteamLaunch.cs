using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
public static class SteamLaunch {
 public const string AppId="1072190";
 static string Folder(string game){if(String.IsNullOrWhiteSpace(game))throw new Exception("게임 폴더를 선택하세요.");return Path.GetFullPath(game).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);}
 static void CheckAppIdFile(string full){string file=Path.Combine(full,"steam_appid.txt");if(File.Exists(file) && File.ReadAllText(file).Trim()!=AppId)throw new Exception("선택한 폴더의 steam_appid.txt가 Crossfire Legion의 App ID와 다릅니다. 기존 파일을 확인하세요. 파일을 자동으로 덮어쓰지 않았습니다.");}
 public static ProcessStartInfo Prepare(string game){
  string full=Folder(game);
  foreach(string file in new[]{"Crossfire_Legion.exe","UnityPlayer.dll","Crossfire_Legion_Data\\Managed\\Carbon.Simulation.dll","Crossfire_Legion_Data\\Managed\\Game.Application.dll","Crossfire_Legion_Data\\Plugins\\x86_64\\steam_api64.dll"})if(!File.Exists(Path.Combine(full,file)))throw new Exception("게임 파일을 찾지 못했습니다: "+file+". Crossfire Legion 게임 폴더를 선택하세요.");
  var common=Directory.GetParent(full);
  if(common!=null && string.Equals(common.Name,"common",StringComparison.OrdinalIgnoreCase) && common.Parent!=null && string.Equals(common.Parent.Name,"steamapps",StringComparison.OrdinalIgnoreCase)){
   string manifest=Path.Combine(common.Parent.FullName,"appmanifest_"+AppId+".acf");
   if(File.Exists(manifest)){
    string text=File.ReadAllText(manifest);
    var id=Regex.Match(text,"\"appid\"\\s+\"([0-9]+)\"",RegexOptions.IgnoreCase);
    var dir=Regex.Match(text,"\"installdir\"\\s+\"([^\"]+)\"",RegexOptions.IgnoreCase);
    if(!id.Success || id.Groups[1].Value!=AppId || !dir.Success)throw new Exception("Crossfire Legion의 Steam 설치 정보가 올바르지 않습니다. Steam에서 설치 상태를 확인하세요.");
    if(string.Equals(dir.Groups[1].Value,Path.GetFileName(full),StringComparison.OrdinalIgnoreCase))return new ProcessStartInfo("steam://run/"+AppId){UseShellExecute=true};
   }
  }
  // A Steam URL would launch the registered installation instead of the selected copy.
  CheckAppIdFile(full);
  var direct=new ProcessStartInfo(Path.Combine(full,"Crossfire_Legion.exe")){UseShellExecute=false,WorkingDirectory=full};
  direct.EnvironmentVariables["SteamAppId"]=AppId;direct.EnvironmentVariables["SteamGameId"]=AppId;
  return direct;
 }
 public static void EnsureAppIdFile(string game){
  string full=Folder(game);CheckAppIdFile(full);string file=Path.Combine(full,"steam_appid.txt");if(File.Exists(file))return;
  try{using(var stream=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.Read)){byte[] bytes=Encoding.ASCII.GetBytes(AppId);stream.Write(bytes,0,bytes.Length);}}
  catch(IOException){if(!File.Exists(file))throw;CheckAppIdFile(full);}
 }
 public static string Start(string game){
  var start=Prepare(game);
  if(Process.GetProcessesByName("Crossfire_Legion").Length!=0)throw new Exception("게임이 이미 실행 중입니다. Steam 연결 오류 화면이 열려 있다면 게임을 완전히 종료한 뒤 다시 실행하세요.");
  if(!start.UseShellExecute){
   if(Process.GetProcessesByName("steam").Length==0)throw new Exception("다른 경로의 게임을 실행하려면 먼저 Steam 클라이언트를 실행하고 게임을 보유한 계정으로 로그인하세요.");
   try{EnsureAppIdFile(game);}catch(Exception e){throw new Exception("선택한 게임 폴더에 Steam App ID를 설정하지 못했습니다. 폴더 쓰기 권한과 steam_appid.txt를 확인하세요.",e);}
  }
  try{Process.Start(start);}catch(System.ComponentModel.Win32Exception e){throw new Exception("게임 실행 요청에 실패했습니다. Steam 로그인 상태와 선택한 게임 폴더를 확인하세요.",e);}
  return start.UseShellExecute?"Steam에 등록된 설치 경로로 실행을 요청했습니다.":"선택한 게임을 실행했습니다: "+start.FileName+" · Steam 로그인 및 게임 보유 권한이 필요합니다.";
 }
}
