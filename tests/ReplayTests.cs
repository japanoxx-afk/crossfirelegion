using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.IO.Compression;
class ReplayTests {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Reject(Action action,string message){try{action();}catch(Exception){return;}throw new Exception(message);}
 static string Fixture(string root,string json,byte[] tail){string p=Path.Combine(root,Guid.NewGuid()+".rply");using(var b=new BinaryWriter(File.Create(p))){byte[] header=Encoding.UTF8.GetBytes(json);b.Write(header.Length);b.Write(header);b.Write(tail);}return p;}
 static int Main(){string root=Path.Combine(Path.GetTempPath(),"CflReplayTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Environment.SetEnvironmentVariable("CFL_REPLAY_TEST_HOME",root);try{
  string ini=Path.Combine(root,"roundtrip.ini");var values=new Dictionary<string,string>{{"한글","줄1\n줄2=abc"},{"empty",""}};ReplayProtocol.Write(ini,values);Check(ReplayProtocol.Read(ini)["한글"]==values["한글"],"metadata Unicode roundtrip");values["한글"]="changed";ReplayProtocol.Write(ini,values);Check(ReplayProtocol.Read(ini)["한글"]=="changed","atomic replace");
  string header="{\"LevelToLoad\":3254523015882078221,\"RNGSeed\":12,\"BuildName\":\"fixture\",\"GameModeID\":\"gamemode-mpclassic\",\"PlayerLoadouts\":[{\"Name\":\"테스트\"}]}";
  string file=Fixture(root,header,new byte[12]);var entry=ReplayLibrary.Inspect(file);Check(entry.Error==null&&entry.Players=="테스트","valid header");Check(ReplayLibrary.Inspect(Fixture(root,"{}",new byte[12])).Error!=null,"missing header fields");Check(ReplayLibrary.Inspect(Fixture(root,header,new byte[0])).Error!=null,"header-only file");
  string broken=Path.Combine(root,"broken.rply");File.WriteAllBytes(broken,new byte[]{255,255,255,127});Check(ReplayLibrary.Inspect(broken).Error!=null,"bounded header");
  string archive=Path.Combine(root,"sample.cflreplay.zip");ReplayLibrary.Export(entry,archive);using(var z=ZipFile.OpenRead(archive))Check(z.Entries.Count==2&&z.GetEntry("replay.rply")!=null,"export pair");string imported=ReplayLibrary.Import(archive);Check(ReplayProtocol.Digest(imported)==ReplayProtocol.Digest(file),"import exact bytes");
  string malicious=Path.Combine(root,"bad.zip");using(var z=ZipFile.Open(malicious,ZipArchiveMode.Create)){z.CreateEntry("../escape.rply");z.CreateEntry("metadata.ini");}Reject(()=>ReplayLibrary.Import(malicious),"path traversal accepted");
  Check(ReplayLibrary.List().Any(e=>e.Error==null),"no readable records");
  Console.WriteLine("PASS: bounded native headers, Unicode/atomic metadata, incomplete files, exact-byte bundled import/export, traversal rejection; native file headers inspected without modifying originals.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{Directory.Delete(root,true);}}
}
