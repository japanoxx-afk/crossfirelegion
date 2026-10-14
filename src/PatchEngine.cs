using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;
public class PatchRecord {public string OriginalHash;public string PatchedHash;public string OriginalMethodHash;}
public class PatchFile {public string Name;public string OriginalHash;public string PatchedHash;public string PreviousHash;}
public class PatchSet {public int Revision;public List<PatchFile> Files=new List<PatchFile>();}
public static class Patcher {
 public const string RelativeDll="Crossfire_Legion_Data\\Managed\\Carbon.Simulation.dll";
 public const int Revision=3;
 static readonly string[] Assemblies={"Carbon.Simulation.dll","Game.UI.dll","Carbon.UI.Unity.dll"};
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
 public static string Hash(string path){using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 static string Managed(string game){return Path.GetDirectoryName(Path.Combine(game,RelativeDll));}
 static string State(string game){return Path.Combine(Managed(game),"CflMod.state.json");}
 static MethodDefinition Find(AssemblyDefinition a,string type,string name){return a.MainModule.Types.Single(t=>t.FullName==type).Methods.Single(m=>m.Name==name);}
 public static string Status(string game){
  if(File.Exists(State(game))){var s=Json.Deserialize<PatchSet>(File.ReadAllText(State(game)));bool valid=s.Files.All(f=>File.Exists(Path.Combine(Managed(game),f.Name))&&Hash(Path.Combine(Managed(game),f.Name))==f.PatchedHash);return !valid?"패치 파일 변경 또는 복구 필요":s.Revision==Revision?"전체 유닛 + 기본 레이아웃 자동 확장 HUD 적용됨":"이전 HUD 패치 · 적용 버튼으로 갱신하세요";}
  if(!File.Exists(Path.Combine(game,RelativeDll)))return "게임 경로를 확인하세요.";
  using(var a=AssemblyDefinition.ReadAssembly(Path.Combine(game,RelativeDll)))return Find(a,"Carbon.Simulation.RosterHelper","IsAbilityInRoster").Body.Instructions.Count==2?"이전 표시 전용 패치 · 패치 적용으로 업그레이드하세요":"원본 상태 · 덱 제한 적용 중";
 }
 static void CheckStopped(){if(System.Diagnostics.Process.GetProcessesByName("Crossfire_Legion").Length!=0)throw new Exception("게임을 종료한 후 다시 시도하세요.");}
 static void Body(MethodDefinition m,params Instruction[] instructions){m.Body.Instructions.Clear();m.Body.ExceptionHandlers.Clear();m.Body.Variables.Clear();m.Body.InitLocals=false;foreach(var i in instructions)m.Body.Instructions.Add(i);}
 static MethodReference Hook(ModuleDefinition module,string method,int count){var scope=module.AssemblyReferences.FirstOrDefault(r=>r.Name=="CflRuntime");if(scope==null){scope=new AssemblyNameReference("CflRuntime",new Version(0,2,0,0));module.AssemblyReferences.Add(scope);}var m=new MethodReference(method,module.TypeSystem.Void,new TypeReference("","CflRuntime",module,scope)){HasThis=false};for(int i=0;i<count;i++)m.Parameters.Add(new ParameterDefinition(module.TypeSystem.Object));return m;}
 static void Rewrite(string name,string source,string output){using(var resolver=new DefaultAssemblyResolver()){resolver.AddSearchDirectory(Path.GetDirectoryName(source));using(var a=AssemblyDefinition.ReadAssembly(source,new ReaderParameters{AssemblyResolver=resolver})){
  var module=a.MainModule;
  if(name=="Carbon.Simulation.dll"){
   var fill=Find(a,"Carbon.Simulation.RosterHelper","FillRosterScratchpadForCategory");
   if(!fill.Body.Instructions.Any(i=>i.Operand!=null && i.Operand.ToString().Contains("RosterSlotAttributes::get_Items")))throw new Exception("지원하지 않는 진영 유닛 등록 코드");
   Body(fill,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Call,Hook(module,"FillAll",2)),Instruction.Create(OpCodes.Ret));
   var check=Find(a,"Carbon.Simulation.SimRequirementValidator","SatisfiesRosterRequirement");
   var positional=check.Body.Instructions.Single(i=>i.Operand!=null && i.Operand.ToString().Contains("RosterRequirement::get_CheckRosterIndex"));
   positional.OpCode=OpCodes.Pop;positional.Operand=null;check.Body.GetILProcessor().InsertAfter(positional,Instruction.Create(OpCodes.Ldc_I4_0));
  }else if(name=="Game.UI.dll"){
   var ctor=Find(a,"Game.UI.Data.UISelectionData",".ctor");
   if(ctor.Body.Instructions[1].OpCode!=OpCodes.Ldc_I4_S || Convert.ToInt32(ctor.Body.Instructions[1].Operand)!=12)throw new Exception("지원하지 않는 HUD 슬롯 생성 코드");
   ctor.Body.Instructions[1].Operand=(sbyte)64;
   var provider=ctor.Body.Instructions.Single(i=>i.OpCode==OpCodes.Newobj && i.Operand.ToString().Contains("UIValueProvider`2<Game.UI.Data.UIAbilitySlot"));
   if(provider.Previous.Previous.OpCode!=OpCodes.Ldc_I4_S || Convert.ToInt32(provider.Previous.Previous.Operand)!=12)throw new Exception("지원하지 않는 HUD 데이터 공급자 코드");
   provider.Previous.Previous.Operand=(sbyte)64;
   var pack=Hook(module,"Pack",1);pack.Parameters.Add(new ParameterDefinition(module.ImportReference(typeof(Array))));
   Body(Find(a,"Game.UI.Data.UISelectionData","SetProductionButtonIndex"),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,pack),Instruction.Create(OpCodes.Ret));
   Body(Find(a,"Game.UI.Data.UISelectionData","SetUpgradeButtonIndex"),Instruction.Create(OpCodes.Ret));
   Body(Find(a,"Game.UI.Data.UISelectionData","SetAbilityButtonIndex"),Instruction.Create(OpCodes.Ret));
   var update=Find(a,"Game.UI.Data.UISelectionData","UpdateAbilities");
   var boundary=update.Body.Instructions.Single(i=>i.OpCode==OpCodes.Bgt && ((Instruction)i.Operand).Offset==0x226);
   boundary.OpCode=OpCodes.Bge;boundary.Operand=update.Body.Instructions.Single(i=>i.Offset==0x1ff);
  }else{
   var init=Find(a,"Carbon.UI.Unity.UIList","Initialize");var il=init.Body.GetILProcessor();var first=init.Body.Instructions[0];
   il.InsertBefore(first,Instruction.Create(OpCodes.Ldarg_0));il.InsertBefore(first,Instruction.Create(OpCodes.Call,Hook(module,"BeforeList",1)));
   foreach(var ret in init.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()){ret.OpCode=OpCodes.Ldarg_0;il.InsertAfter(ret,Instruction.Create(OpCodes.Call,Hook(module,"AfterList",1)));il.InsertAfter(ret.Next,Instruction.Create(OpCodes.Ret));}
  }
  a.Write(output);
 }}}
 public static void Apply(string game){
  CheckStopped();string dir=Managed(game),statePath=State(game);
  if(File.Exists(statePath)){
   var previous=Json.Deserialize<PatchSet>(File.ReadAllText(statePath));
   if(previous.Revision<Revision && previous.Files.All(f=>File.Exists(Path.Combine(dir,f.Name))&&Hash(Path.Combine(dir,f.Name))==f.PatchedHash)){Restore(game);Apply(game);return;}
   throw new Exception("이미 최신 패치가 적용되었거나 복구가 필요합니다. 원본 복원 후 다시 적용하세요.");
  }
  var state=new PatchSet{Revision=Revision};var staged=new Dictionary<string,string>();
  string stage=Path.Combine(dir,".cfl-stage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
  try{
   foreach(string name in Assemblies){
    string target=Path.Combine(dir,name),backup=target+".cfl-original",source=target;
    if(File.Exists(backup)){
     if(name!="Carbon.Simulation.dll" || !File.Exists(target+".cfl-patch.json"))throw new Exception("확인되지 않은 기존 백업: "+name);
     var old=Json.Deserialize<PatchRecord>(File.ReadAllText(target+".cfl-patch.json"));
     if(Hash(backup)!=old.OriginalHash || (Hash(target)!=old.PatchedHash && Hash(target)!=old.OriginalHash))throw new Exception("이전 패치/백업이 변경되어 업그레이드를 중단했습니다.");
     source=backup;
    }
    string temp=Path.Combine(stage,name);Rewrite(name,source,temp);
    using(var verify=AssemblyDefinition.ReadAssembly(temp)){if(!verify.MainModule.AssemblyReferences.Any(r=>r.Name=="CflRuntime"))throw new Exception("패치 후크 검증 실패: "+name);}
    state.Files.Add(new PatchFile{Name=name,OriginalHash=Hash(source),PatchedHash=Hash(temp),PreviousHash=Hash(target)});staged[name]=temp;
   }
   string runtime=Path.Combine(stage,"CflRuntime.dll");
   using(var resource=typeof(Patcher).Assembly.GetManifestResourceStream("CflRuntime.dll"))using(var output=File.Create(runtime)){if(resource==null)throw new Exception("런타임 리소스 없음");resource.CopyTo(output);}
   if(File.Exists(Path.Combine(dir,"CflRuntime.dll")))throw new Exception("추적되지 않은 CflRuntime.dll이 있어 중단했습니다.");
   state.Files.Add(new PatchFile{Name="CflRuntime.dll",PatchedHash=Hash(runtime)});staged["CflRuntime.dll"]=runtime;
   foreach(var f in state.Files.Where(f=>f.OriginalHash!=null)){string original=Path.Combine(dir,f.Name),backup=original+".cfl-original";if(!File.Exists(backup))File.Copy(original,backup);}
   File.WriteAllText(statePath,Json.Serialize(state));
   try{foreach(var f in state.Files){string target=Path.Combine(dir,f.Name);if(File.Exists(target))File.Replace(staged[f.Name],target,null);else File.Move(staged[f.Name],target);}}
   catch{Restore(game);throw;}
  }finally{if(Directory.Exists(stage))Directory.Delete(stage,true);}
 }
 public static void Restore(string game){
  CheckStopped();string dir=Managed(game),statePath=State(game);
  if(!File.Exists(statePath)){
   string dll=Path.Combine(game,RelativeDll),backup=dll+".cfl-original",record=dll+".cfl-patch.json";
   if(!File.Exists(backup)||!File.Exists(record))throw new Exception("복원 가능한 백업이 없습니다.");
   var old=Json.Deserialize<PatchRecord>(File.ReadAllText(record));
   if(Hash(backup)!=old.OriginalHash || (Hash(dll)!=old.PatchedHash && Hash(dll)!=old.OriginalHash))throw new Exception("이전 패치/백업 검증 실패");
   string temp=dll+".restore";File.Copy(backup,temp,true);File.Replace(temp,dll,null);File.Delete(backup);File.Delete(record);return;
  }
  var state=Json.Deserialize<PatchSet>(File.ReadAllText(statePath));
  foreach(var f in state.Files){
   if(!Assemblies.Contains(f.Name) && f.Name!="CflRuntime.dll")throw new Exception("잘못된 복원 파일 이름");
   string file=Path.Combine(dir,f.Name);
   if(f.OriginalHash!=null && (!File.Exists(file+".cfl-original") || Hash(file+".cfl-original")!=f.OriginalHash))throw new Exception("백업 검증 실패: "+f.Name);
   if(File.Exists(file) && Hash(file)!=f.PatchedHash && Hash(file)!=f.OriginalHash && Hash(file)!=f.PreviousHash)throw new Exception("파일이 변경되어 복원을 중단했습니다: "+f.Name);
  }
  foreach(var f in state.Files){string file=Path.Combine(dir,f.Name);if(f.OriginalHash==null){if(File.Exists(file))File.Delete(file);}else{string temp=file+".restore";File.Copy(file+".cfl-original",temp,true);if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}}
  foreach(var f in state.Files.Where(f=>f.OriginalHash!=null))File.Delete(Path.Combine(dir,f.Name)+".cfl-original");
  File.Delete(statePath);string legacy=Path.Combine(game,RelativeDll)+".cfl-patch.json";if(File.Exists(legacy))File.Delete(legacy);
 }
}
