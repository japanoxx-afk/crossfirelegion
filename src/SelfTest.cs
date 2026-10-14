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
   Directory.CreateDirectory(Path.GetDirectoryName(dll));
   string[] names={"Carbon.Simulation.dll","Game.UI.dll","Carbon.UI.Unity.dll"};
   var originals=new System.Collections.Generic.Dictionary<string,string>();
   foreach(string name in names){string from=Path.Combine(Path.GetDirectoryName(source),name);if(File.Exists(from+".cfl-original"))from+=".cfl-original";string to=Path.Combine(Path.GetDirectoryName(dll),name);File.Copy(from,to);originals[name]=Patcher.Hash(to);}
   foreach(string dependency in Directory.GetFiles(Path.GetDirectoryName(source),"*.dll")){string target=Path.Combine(Path.GetDirectoryName(dll),Path.GetFileName(dependency));if(!File.Exists(target) && Path.GetFileName(dependency)!="CflRuntime.dll")File.Copy(dependency,target);}
   string original=Patcher.Hash(dll);
   using(var a=AssemblyDefinition.ReadAssembly(dll)){
    var m=a.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.RosterHelper").Methods.Single(x=>x.Name=="IsAbilityInRoster");
    Require(m.Body.Instructions.Count==8,"원본 코드 패턴");
   }
   Patcher.Apply(test);Require(Patcher.Status(test).Contains("적용됨"),"패치 상태");
   bool blocked=false;try{Patcher.Apply(test);}catch{blocked=true;}Require(blocked,"중복 적용 차단");
   string patched=Patcher.Hash(dll);string ui=Path.Combine(Path.GetDirectoryName(dll),"Game.UI.dll");byte[] uiBytes=File.ReadAllBytes(ui);File.AppendAllText(ui,"tamper");
   blocked=false;try{Patcher.Restore(test);}catch{blocked=true;}Require(blocked,"변경 파일 덮어쓰기 차단");
   Require(Patcher.Hash(dll)==patched,"복원 사전 검증의 부분 변경 방지");File.WriteAllBytes(ui,uiBytes);
   using(var a=AssemblyDefinition.ReadAssembly(dll)){
    var execute=a.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.ExecuteScriptCommand").Methods.Single(method=>method.Name=="Execute");
    Require(execute.Body.Instructions.Any(i=>i.Operand!=null && i.Operand.ToString().Contains("CflRuntime::ExecuteMoney")),"시뮬레이션 자원 치트 후크");
    var fill=a.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.RosterHelper").Methods.Single(m=>m.Name=="FillRosterScratchpadForCategory");
    Require(fill.Body.Instructions.Any(i=>i.Operand!=null && i.Operand.ToString().Contains("CflRuntime::FillAll")),"전체 진영 유닛 등록");
    var check=a.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.SimRequirementValidator").Methods.Single(m=>m.Name=="SatisfiesRosterRequirement");
    Require(!check.Body.Instructions.Any(i=>i.Operand!=null && i.Operand.ToString().Contains("get_CheckRosterIndex")),"슬롯 위치 제한 제거");
   }
   using(var a=AssemblyDefinition.ReadAssembly(ui)){
    var chat=a.MainModule.Types.Single(t=>t.FullName=="Game.UI.HUDChatPanel");
    Require(chat.Methods.Single(m=>m.Name=="SendMessage").Body.Instructions.Any(i=>i.Operand!=null && i.Operand.ToString().Contains("CflRuntime::TryCheat")),"채팅 치트 후크");
    Require(!chat.Methods.Single(m=>m.Name=="Update").Body.Instructions.Any(i=>i.Operand!=null && i.Operand.ToString().Contains("mIsCampaign")),"캠페인 Enter 입력 경로");
    var type=a.MainModule.Types.Single(t=>t.FullName=="Game.UI.Data.UISelectionData");
    Require(Convert.ToInt32(type.Methods.Single(m=>m.Name==".ctor").Body.Instructions[1].Operand)==64,"HUD 슬롯 64개");
    var provider=type.Methods.Single(m=>m.Name==".ctor").Body.Instructions.Single(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Newobj && i.Operand.ToString().Contains("UIValueProvider`2<Game.UI.Data.UIAbilitySlot"));
    Require(Convert.ToInt32(provider.Previous.Previous.Operand)==64,"HUD 공급자 12칸 잘림 방지");
    var boundary=type.Methods.Single(m=>m.Name=="UpdateAbilities").Body.Instructions.Single(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Bge);
    Require(((Mono.Cecil.Cil.Instruction)boundary.Operand).OpCode==Mono.Cecil.Cil.OpCodes.Ldarg_0,"단축키 범위 밖 슬롯 저장 유지");
   }
   using(var a=AssemblyDefinition.ReadAssembly(Path.Combine(Path.GetDirectoryName(dll),"Carbon.UI.Unity.dll"))){
    var refresh=a.MainModule.Types.Single(t=>t.FullName=="Carbon.UI.Unity.UIList").Methods.Single(m=>m.Name=="RefreshDataWithoutLinks");
    Require(refresh.Body.Instructions.Any(i=>i.Operand!=null && i.Operand.ToString().Contains("CflRuntime::AfterRefresh")),"기본 단축키 빈칸 보존 후크");
   }
   Patcher.Restore(test);foreach(string name in names)Require(Patcher.Hash(Path.Combine(Path.GetDirectoryName(dll),name))==originals[name],"원본 정확한 복원: "+name);
   Require(!File.Exists(Path.Combine(Path.GetDirectoryName(dll),"CflRuntime.dll")),"런타임 복원 정리");
   Require(!File.Exists(dll+".cfl-original"),"복원 후 백업 정리");
   // Simulate the previous release and verify upgrade preserves the real original backup.
   File.Copy(dll,dll+".cfl-original");
   using(var a=AssemblyDefinition.ReadAssembly(dll)){
    var m=a.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.RosterHelper").Methods.Single(x=>x.Name=="IsAbilityInRoster");m.Body.Instructions.Clear();m.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4_1));m.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));a.Write(dll+".legacy");
   }
   File.Replace(dll+".legacy",dll,null);
   File.WriteAllText(dll+".cfl-patch.json",new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new PatchRecord{OriginalHash=original,PatchedHash=Patcher.Hash(dll)}));
   Patcher.Apply(test);Patcher.Restore(test);Require(Patcher.Hash(dll)==original,"이전 패치 업그레이드 및 원본 복원");
   File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-result.txt"),"PASS: full-roster hook, positional requirement fix, 64 HUD slots, hotkey bound fix, duplicate rejection, tamper preflight, three-DLL byte-identical restore, legacy migration.");
   Directory.Delete(test,true);return 0;
  }catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-result.txt"),e.ToString());return 1;}
 }
}
