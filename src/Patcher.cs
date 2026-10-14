using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;

public class PatchRecord {
 public string OriginalHash;
 public string PatchedHash;
 public string OriginalMethodHash;
}
public static class Patcher {
 public const string RelativeDll="Crossfire_Legion_Data\\Managed\\Carbon.Simulation.dll";
 public static string Hash(string path){using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 static MethodDefinition Find(AssemblyDefinition a){return a.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.RosterHelper").Methods.Single(m=>m.Name=="IsAbilityInRoster" && m.Parameters.Count==2);}
 static bool Patched(MethodDefinition m){return m.Body.Instructions.Count==2 && m.Body.Instructions[0].OpCode==OpCodes.Ldc_I4_1 && m.Body.Instructions[1].OpCode==OpCodes.Ret;}
 static string MethodHash(MethodDefinition m){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",m.Body.Instructions.Select(i=>i.ToString()))))).Replace("-","").ToLowerInvariant();}
 public static string Status(string game){
  string dll=Path.Combine(game,RelativeDll);
  if(!File.Exists(dll))return "게임 경로를 확인하세요.";
  using(var a=AssemblyDefinition.ReadAssembly(dll))return Patched(Find(a))?"모든 유닛 생산 패치 적용됨":"원본 상태 · 덱 제한 적용 중";
 }
 static void CheckStopped(){if(System.Diagnostics.Process.GetProcessesByName("Crossfire_Legion").Length!=0)throw new Exception("게임을 종료한 후 다시 시도하세요.");}
 public static void Apply(string game){
  CheckStopped();
  string dll=Path.Combine(game,RelativeDll), backup=dll+".cfl-original", record=dll+".cfl-patch.json", temp=dll+".cfl-temp";
  using(var a=AssemblyDefinition.ReadAssembly(dll)){
   var m=Find(a);
   if(Patched(m))throw new Exception("이미 패치되어 있습니다.");
   // Fail closed: require the exact known roster membership implementation.
   string[] expected={"ldarg.0","call","call","call","ldfld","ldarg.1","callvirt","ret"};
   if(m.ReturnType.FullName!="System.Boolean" || !m.HasBody || m.Body.Instructions.Count!=expected.Length || !m.Body.Instructions.Select(i=>i.OpCode.Name).SequenceEqual(expected) || !m.Body.Instructions[6].Operand.ToString().Contains("::Contains(!0)"))throw new Exception("지원하지 않는 게임 코드입니다. 패치를 중단했습니다.");
   string original=Hash(dll);
   if(File.Exists(backup) && Hash(backup)!=original)throw new Exception("기존 백업과 현재 게임 버전이 다릅니다. 기존 패치를 복원하고 다시 시도하세요.");
   var info=new PatchRecord{OriginalHash=original,OriginalMethodHash=MethodHash(m)};
   if(!File.Exists(backup))File.Copy(dll,backup);
   m.Body.Instructions.Clear();m.Body.ExceptionHandlers.Clear();m.Body.Variables.Clear();m.Body.InitLocals=false;
   m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_1));m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
   a.Write(temp);
   using(var verify=AssemblyDefinition.ReadAssembly(temp)){if(!Patched(Find(verify)))throw new Exception("패치 검증 실패");}
   info.PatchedHash=Hash(temp);
   File.WriteAllText(record,new JavaScriptSerializer().Serialize(info));
  }
  File.Replace(temp,dll,null);
 }
 public static void Restore(string game){
  CheckStopped();string dll=Path.Combine(game,RelativeDll),backup=dll+".cfl-original",record=dll+".cfl-patch.json";
  if(!File.Exists(backup)||!File.Exists(record))throw new Exception("복원 가능한 백업이 없습니다.");
  var info=new JavaScriptSerializer().Deserialize<PatchRecord>(File.ReadAllText(record));
  if(Hash(backup)!=info.OriginalHash)throw new Exception("백업 검증 실패");
  string current=Hash(dll);
  if(current!=info.PatchedHash && current!=info.OriginalHash)throw new Exception("게임 파일이 변경되었습니다. 새 파일을 덮어쓰지 않도록 복원을 중단했습니다.");
  string temp=dll+".cfl-restore";File.Copy(backup,temp,true);File.Replace(temp,dll,null);
  File.Delete(record);File.Delete(backup);
 }
}
