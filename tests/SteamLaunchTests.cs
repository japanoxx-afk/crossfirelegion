using System;
using System.IO;
class SteamLaunchTests {
 static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Reject(string game,string name){bool failed=false;try{SteamLaunch.Prepare(game);}catch{failed=true;}Require(failed,name);}
 static void MakeGame(string game){foreach(string file in new[]{"Crossfire_Legion.exe","UnityPlayer.dll","Crossfire_Legion_Data\\Managed\\Carbon.Simulation.dll","Crossfire_Legion_Data\\Managed\\Game.Application.dll","Crossfire_Legion_Data\\Plugins\\x86_64\\steam_api64.dll"}){string path=Path.Combine(game,file);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,"");}}
 static void Direct(string game){var info=SteamLaunch.Prepare(game);Require(info.FileName==Path.Combine(game,"Crossfire_Legion.exe") && !info.UseShellExecute && info.WorkingDirectory==game,"Selected copy path or working directory changed");Require(info.EnvironmentVariables["SteamAppId"]==SteamLaunch.AppId && info.EnvironmentVariables["SteamGameId"]==SteamLaunch.AppId,"Steam child context missing");Require(String.IsNullOrEmpty(info.Arguments),"Unexpected arguments");}
 static int Main(string[] args){
  string root=Path.Combine(Path.GetTempPath(),"cfl-steam-test-"+Guid.NewGuid().ToString("N"));
  try{
   string game=Path.Combine(root,"steamapps","common","Crossfire Legion (Demo)"),manifest=Path.Combine(root,"steamapps","appmanifest_1072190.acf");MakeGame(game);
   Direct(game);Require(!File.Exists(Path.Combine(game,"steam_appid.txt")),"Prepare must not modify game files");
   File.WriteAllText(manifest,"\"appid\" \"1072190\"\n\"installdir\" \"Crossfire Legion (Demo)\"");
   var info=SteamLaunch.Prepare(game+Path.DirectorySeparatorChar);Require(info.FileName=="steam://run/1072190" && info.UseShellExecute,"Registered installation must launch via Steam protocol");
   File.WriteAllText(manifest,"\"appid\" \"1072190\"\n\"installdir\" \"Another Game\"");Direct(game);
   File.WriteAllText(manifest,"\"appid\" \"480\"\n\"installdir\" \"Crossfire Legion (Demo)\"");Reject(game,"Wrong manifest AppID accepted");
   string copy=Path.Combine(root,"다른 경로","선택한 게임 복사본");MakeGame(copy);Direct(copy);
   SteamLaunch.EnsureAppIdFile(copy);string appid=Path.Combine(copy,"steam_appid.txt");Require(File.ReadAllText(appid)==SteamLaunch.AppId,"Steam App ID file missing");
   File.WriteAllText(appid,SteamLaunch.AppId+"\r\n");SteamLaunch.EnsureAppIdFile(copy);Require(File.ReadAllText(appid).EndsWith("\r\n"),"Existing valid App ID overwritten");
   File.WriteAllText(appid,"480");Reject(copy,"Conflicting copy AppID accepted");bool blocked=false;try{SteamLaunch.EnsureAppIdFile(copy);}catch{blocked=true;}Require(blocked && File.ReadAllText(appid)=="480","Conflicting App ID overwritten");
   File.Delete(appid);File.Delete(Path.Combine(copy,"Crossfire_Legion_Data","Managed","Game.Application.dll"));Reject(copy,"Incomplete copy accepted");
   File.Delete(Path.Combine(game,"Crossfire_Legion.exe"));Reject(game,"Missing game accepted");
   if(args.Length>0){var actual=SteamLaunch.Prepare(args[0]);Require(actual.FileName=="steam://run/1072190","Actual installation resolution failed");Console.WriteLine("Actual Steam installation: PASS");}
   Console.WriteLine("PASS: registered Steam route; external and renamed copies launch selected executable; Unicode/spaces; working directory; child AppID; missing files and conflicting IDs; AppID creation/preservation. No game launched.");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}
