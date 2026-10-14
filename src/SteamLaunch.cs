using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
public static class SteamLaunch {
 public const string AppId="1072190";
 public static ProcessStartInfo Prepare(string game){
  string full=Path.GetFullPath(game).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
  if(!File.Exists(Path.Combine(full,"Crossfire_Legion.exe")))throw new Exception("게임 실행 파일을 찾지 못했습니다. Steam에 설치된 게임 폴더를 선택하세요.");
  var common=Directory.GetParent(full);
  if(common==null || !string.Equals(common.Name,"common",StringComparison.OrdinalIgnoreCase) || common.Parent==null || !string.Equals(common.Parent.Name,"steamapps",StringComparison.OrdinalIgnoreCase))throw new Exception("Steam 라이브러리의 steamapps\\common 아래 게임 폴더를 선택하세요.");
  string manifest=Path.Combine(common.Parent.FullName,"appmanifest_"+AppId+".acf");
  if(!File.Exists(manifest))throw new Exception("Crossfire: Legion의 Steam 설치 정보를 찾지 못했습니다. Steam에서 설치 상태를 확인하세요.");
  string text=File.ReadAllText(manifest);
  var id=Regex.Match(text,"\"appid\"\\s+\"([0-9]+)\"",RegexOptions.IgnoreCase);
  var dir=Regex.Match(text,"\"installdir\"\\s+\"([^\"]+)\"",RegexOptions.IgnoreCase);
  if(!id.Success || id.Groups[1].Value!=AppId || !dir.Success || !string.Equals(dir.Groups[1].Value,Path.GetFileName(full),StringComparison.OrdinalIgnoreCase))throw new Exception("선택한 폴더와 Steam 설치 정보가 일치하지 않습니다. Steam에서 게임의 설치 폴더를 확인하세요.");
  // Steam owns launch, login, application context, and launch options.
  return new ProcessStartInfo("steam://run/"+AppId){UseShellExecute=true};
 }
 public static void Start(string game){
  var start=Prepare(game);
  if(Process.GetProcessesByName("Crossfire_Legion").Length!=0)throw new Exception("게임이 이미 실행 중입니다. Steam 연결 오류 화면이 열려 있다면 게임을 완전히 종료한 뒤 다시 실행하세요.");
  try{Process.Start(start);}catch(System.ComponentModel.Win32Exception e){throw new Exception("Steam 실행 요청에 실패했습니다. Steam 클라이언트를 설치·실행하고 로그인한 뒤 다시 시도하세요.",e);}
 }
}
