using System;
using System.IO;
class SteamLaunchTests {
 static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Reject(string game,string name){bool failed=false;try{SteamLaunch.Prepare(game);}catch{failed=true;}Require(failed,name);}
 static int Main(string[] args){
  string root=Path.Combine(Path.GetTempPath(),"cfl-steam-test-"+Guid.NewGuid().ToString("N"));
  try{
   string game=Path.Combine(root,"steamapps","common","Crossfire Legion (Demo)"),manifest=Path.Combine(root,"steamapps","appmanifest_1072190.acf");
   Directory.CreateDirectory(game);File.WriteAllText(Path.Combine(game,"Crossfire_Legion.exe"),"");
   Reject(game,"Missing manifest accepted");
   File.WriteAllText(manifest,"\"appid\" \"1072190\"\n\"installdir\" \"Crossfire Legion (Demo)\"");
   var info=SteamLaunch.Prepare(game+Path.DirectorySeparatorChar);
   Require(info.FileName=="steam://run/1072190" && info.UseShellExecute,"Must launch via Steam protocol");
   Require(string.IsNullOrEmpty(info.Arguments),"Unexpected direct executable arguments");
   File.WriteAllText(manifest,"\"appid\" \"1072190\"\n\"installdir\" \"Another Game\"");Reject(game,"Wrong folder accepted");
   File.WriteAllText(manifest,"\"appid\" \"480\"\n\"installdir\" \"Crossfire Legion (Demo)\"");Reject(game,"Wrong AppID accepted");
   File.Delete(Path.Combine(game,"Crossfire_Legion.exe"));Reject(game,"Missing game accepted");
   if(args.Length>0){var actual=SteamLaunch.Prepare(args[0]);Require(actual.FileName=="steam://run/1072190","Actual installation resolution failed");Console.WriteLine("Actual Steam installation: PASS");}
   Console.WriteLine("PASS: Steam URL, shell execution, trailing slash, missing manifest, folder mismatch, AppID mismatch, missing executable. No game launched.");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}
