using System;
using System.IO;
class UpdaterTests {
 static void Main(){string root=Path.Combine(Path.GetTempPath(),"cfl-update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{
  string source=AppDomain.CurrentDomain.BaseDirectory;Func<string> stage=()=>{string p=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);foreach(string n in new[]{"Application.exe","Mono.Cecil.dll","THIRD-PARTY-NOTICES.txt"})File.Copy(Path.Combine(source,n),Path.Combine(p,n));return p;};
  string first=Updater.InstallVerified(root,"1.0.0",stage());if(!File.Exists(first)||File.ReadAllText(Path.Combine(root,"current.txt"))!="1.0.0")throw new Exception("First install failed");
  File.WriteAllText(Path.Combine(root,"current.txt"),"0.1.0");Updater.InstallVerified(root,"1.0.0",stage());if(File.ReadAllText(Path.Combine(root,"current.txt"))!="1.0.0")throw new Exception("Retry activation failed");
  File.WriteAllText(first,"incomplete");Updater.InstallVerified(root,"1.0.0",stage());if(new FileInfo(first).Length<1000||Directory.GetDirectories(Path.Combine(root,"versions"),".previous-*").Length!=1)throw new Exception("Repair/backup failed");
  Console.WriteLine("PASS: first install, existing version reuse, pointer repair, incomplete folder replacement with preserved backup.");
 }catch(Exception e){Console.WriteLine(e.GetType().FullName+": "+e.Message);Environment.ExitCode=1;}finally{try{Directory.Delete(root,true);}catch{}}}
}
