using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
public static class ReplayProtocol {
 public const string RulesHash="development";
 public const string ModVersion="development";
 public static string Home {get{string test=Environment.GetEnvironmentVariable("CFL_REPLAY_TEST_HOME");return String.IsNullOrEmpty(test)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CrossfireLegionLauncher","replays"):Path.GetFullPath(test);}}
 public static string NativeFolder {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData","LocalLow","Blackbird Interactive","Crossfire Legion","Replays");}}
 public static string Digest(string path){using(var s=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var h=SHA256.Create())return Hex(h.ComputeHash(s));}
 static string Hex(byte[] bytes){return BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();}
 public static string Metadata(string path){using(var h=SHA256.Create())return Path.Combine(Home,"metadata",Hex(h.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToLowerInvariant())))+".ini");}
 public static Dictionary<string,string> Read(string path){var result=new Dictionary<string,string>(StringComparer.Ordinal);if(!File.Exists(path))return result;var f=new FileInfo(path);if(f.Length>2*1024*1024)throw new InvalidDataException("리플레이 정보 파일이 너무 큽니다.");foreach(string line in File.ReadAllLines(path)){int i=line.IndexOf('=');if(i>0)result.Add(line.Substring(0,i),Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(i+1))));}return result;}
 public static void Write(string path,IDictionary<string,string> values){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var lines=new List<string>();foreach(var item in values)lines.Add(item.Key+"="+Convert.ToBase64String(Encoding.UTF8.GetBytes(item.Value??"")));File.WriteAllLines(temp,lines.ToArray(),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
 public static string Value(IDictionary<string,string> values,string key){string result;return values.TryGetValue(key,out result)?result:"";}
 public static readonly string[] FingerprintFiles={"Crossfire_Legion_Data/Managed/Carbon.Simulation.dll","Crossfire_Legion_Data/Managed/Game.Application.dll","Crossfire_Legion_Data/Managed/Game.Gameplay.dll","Crossfire_Legion_Data/Managed/Game.Common.dll","Crossfire_Legion_Data/Managed/Carbon.Core.dll","Crossfire_Legion_Data/Managed/Carbon.Attributes.dll","Crossfire_Legion_Data/Managed/Carbon.Scripting.Lua.dll","Data/Attributes.zip","Data/Scripts/Source/Multiplayer/Classic/init.lua"};
 public static string Fingerprint(string game){var text=new StringBuilder(RulesHash+"\\n");foreach(string relative in FingerprintFiles){string path=Path.Combine(game,relative.Replace('/',Path.DirectorySeparatorChar));text.Append(relative).Append(':').Append(File.Exists(path)?Digest(path):"missing").Append('\n');}using(var h=SHA256.Create())return Hex(h.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));}
 public static bool SamePath(string a,string b){return String.Equals(Path.GetFullPath(a).TrimEnd('\\','/'),Path.GetFullPath(b).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase);}
}
