using System;
using System.IO;
using Mono.Cecil;
using System.Linq;
public static class SelfTest {
 static void Require(bool value,string name){if(!value)throw new Exception(name);}
 public static int Run(string game){
  string test=Path.Combine(Path.GetTempPath(),"cfl-test-"+Guid.NewGuid().ToString("N"));
  try{
   if(game==null)throw new Exception("게임 경로가 필요합니다.");
   string source=Path.Combine(game,Patcher.RelativeDll),dll=Path.Combine(test,Patcher.RelativeDll);
   Directory.CreateDirectory(Path.GetDirectoryName(dll));File.Copy(source,dll);
   string original=Patcher.Hash(dll);
   using(var a=AssemblyDefinition.ReadAssembly(dll)){
    var m=a.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.RosterHelper").Methods.Single(x=>x.Name=="IsAbilityInRoster");
    Require(m.Body.Instructions.Count==8,"원본 코드 패턴");
   }
   Patcher.Apply(test);Require(Patcher.Status(test).Contains("적용됨"),"패치 상태");
   bool blocked=false;try{Patcher.Apply(test);}catch{blocked=true;}Require(blocked,"중복 적용 차단");
   string patched=Patcher.Hash(dll);File.AppendAllText(dll,"tamper");
   blocked=false;try{Patcher.Restore(test);}catch{blocked=true;}Require(blocked,"변경 파일 덮어쓰기 차단");
   File.Copy(source,dll,true);Patcher.Restore(test);Require(Patcher.Hash(dll)==original,"원본 정확한 복원");
   Require(!File.Exists(dll+".cfl-original"),"복원 후 백업 정리");
   File.WriteAllText(Path.Combine(test,"PASS.txt"),"Patch, duplicate rejection, tamper rejection, restore: PASS");
   File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-result.txt"),"PASS: patch, duplicate rejection, tamper rejection, byte-identical restore. Original SHA256: "+original);
   Directory.Delete(test,true);return 0;
  }catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-result.txt"),e.ToString());return 1;}
 }
}
