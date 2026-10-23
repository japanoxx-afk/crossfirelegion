using System;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;
public static class SelfTest {
 static void Require(bool value,string name){if(!value)throw new Exception(name);}
 public static int Run(string game){
  string test=Path.Combine(Path.GetTempPath(),"cfl-test-"+Guid.NewGuid().ToString("N"));
  try{
   if(game==null)throw new Exception("게임 경로가 필요합니다.");
   string source=Path.Combine(game,Patcher.RelativeDll),dll=Path.Combine(test,Patcher.RelativeDll);
   Directory.CreateDirectory(Path.GetDirectoryName(dll));
   string[] names={"Carbon.Simulation.dll","Game.UI.dll","Carbon.UI.Unity.dll","Game.Application.dll","Carbon.Presentation.Unity.dll","Game.Presentation.dll","Carbon.Localization.Core.dll","Systems.Online.Steam.dll","Carbon.Core.dll","..\\boot.config","..\\..\\Data\\Attributes.zip","..\\..\\Data\\Scripts\\Source\\Multiplayer\\Classic\\init.lua"};
   var originals=new System.Collections.Generic.Dictionary<string,string>();
   foreach(string name in names){string from=Path.Combine(Path.GetDirectoryName(source),name);if(File.Exists(from+".cfl-original"))from+=".cfl-original";string to=Path.Combine(Path.GetDirectoryName(dll),name);Directory.CreateDirectory(Path.GetDirectoryName(to));File.Copy(from,to);originals[name]=Patcher.Hash(to);}
   foreach(string dependency in Directory.GetFiles(Path.GetDirectoryName(source),"*.dll")){string target=Path.Combine(Path.GetDirectoryName(dll),Path.GetFileName(dependency));if(!File.Exists(target) && Path.GetFileName(dependency)!="CflRuntime.dll")File.Copy(dependency,target);}
   string original=Patcher.Hash(dll);
   using(var a=AssemblyDefinition.ReadAssembly(dll)){
    var m=a.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.RosterHelper").Methods.Single(x=>x.Name=="IsAbilityInRoster");
    Require(m.Body.Instructions.Count==8,"원본 코드 패턴");
   }
   Require(Patcher.IncrementalBootConfig("gc-max-time-slice=9\nother=1\n")=="other=1\r\ngc-max-time-slice=3\r\n","GC setting deduplication");
   Patcher.Apply(test);Require(Patcher.Status(test).Contains("적용됨"),"패치 상태");
   Require(File.ReadAllText(Path.Combine(Path.GetDirectoryName(dll),"..","boot.config")).Contains("gc-max-time-slice=3"),"Incremental GC startup setting missing");
   Require(File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dll),"..","..","Data","Scripts","Source","Multiplayer","Classic","init.lua"))).Contains("CFL_Energy_Start"),"Simulation timer script missing");
   VerifyCommanderEnergy(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dll),"..","..","Data","Attributes.zip")));
   Require(File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dll),"..","..","Data","Scripts","Source","Multiplayer","Classic","init.lua"))).Contains("actions.SetInventoryValue(commander, global.InventoryCommanderEnergy, 0)"),"Classic mode starting energy not zero");
   Require(!Patcher.NeedsUpgrade(test),"최신 패치 자동 갱신 반복 방지");
   VerifyCatalogTimeout(Path.GetDirectoryName(dll));
   VerifyMovement(dll);
   using(var pres=AssemblyDefinition.ReadAssembly(Path.Combine(Path.GetDirectoryName(dll),"Game.Presentation.dll"))){
    var speech=pres.MainModule.Types.Single(t=>t.FullName=="Game.Presentation.OrcaSpeechEventProcessor");
    foreach(string name in new[]{"OnSelectionChangedEvent","OnAbilityCommandScheduledEvent","OnAbilityFailureEvent","OnControlGroupUpdateEvent","OnConstructionStartedEvent","OnConstructionFinishedEvent","OnEntityProducedEvent","OnTechProgressEvent"})Require(speech.Methods.Single(m=>m.Name==name).Body.Instructions[0].Operand.ToString().Contains("ReplayMuteAcknowledgement"),"Replay-only acknowledgement guard: "+name);
   }
   using(var input=AssemblyDefinition.ReadAssembly(Path.Combine(Path.GetDirectoryName(dll),"Carbon.Presentation.Unity.dll")))Require(input.MainModule.Types.Single(t=>t.FullName=="Carbon.Presentation.Unity.InputService").Methods.Single(m=>m.Name=="Tick").Body.Instructions.Any(i=>i.Operand is MethodReference&&((MethodReference)i.Operand).Name=="ReplayInputTick"),"Replay U/resources input hook missing");
   using(var replayUI=AssemblyDefinition.ReadAssembly(Path.Combine(Path.GetDirectoryName(dll),"Game.UI.dll")))Require(replayUI.MainModule.Types.Single(t=>t.FullName=="Game.UI.UIReplayData").Methods.Single(m=>m.Name=="OnUpdate").Body.Instructions.Any(i=>i.Operand is MethodReference&&((MethodReference)i.Operand).Name=="ReplayControlsUpdated"),"Replay O/default spectator hook missing");
   string statePath=Path.Combine(Path.GetDirectoryName(dll),"CflMod.state.json");string stateText=File.ReadAllText(statePath);
   var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();var staleRuntime=serializer.Deserialize<PatchSet>(stateText);staleRuntime.Files.Single(f=>f.Name=="CflRuntime.dll").PatchedHash=new string('0',64);File.WriteAllText(statePath,serializer.Serialize(staleRuntime));Require(Patcher.NeedsUpgrade(test),"동일 개정의 이전 런타임 갱신 감지");File.WriteAllText(statePath,stateText);
var prior=serializer.Deserialize<PatchSet>(stateText);prior.Revision=6;File.WriteAllText(statePath,serializer.Serialize(prior));
   Require(Patcher.NeedsUpgrade(test),"이전 패치 자동 갱신 감지");File.WriteAllText(statePath,stateText);
   using(var app=AssemblyDefinition.ReadAssembly(Path.Combine(Path.GetDirectoryName(dll),"Game.Application.dll"))){
    var loader=app.MainModule.Types.Single(t=>t.FullName=="Game.Application.Layers.ApplicationAssetBundleLoadingState");
    Require(loader.Methods.Single(m=>m.Name=="OnEnter").Body.Instructions.Any(i=>i.Operand!=null && i.Operand.ToString().Contains("CflRuntime::BeginStartupLoad")),"초기 로딩 최적화 진입 후크");
    Require(loader.Methods.Single(m=>m.Name=="OnExit").Body.Instructions.Any(i=>i.Operand!=null && i.Operand.ToString().Contains("CflRuntime::EndStartupLoad")),"초기 로딩 우선순위 복원 후크");
    var awake=app.MainModule.Types.Single(t=>t.FullName=="Game.Application.MainComponent").Methods.Single(m=>m.Name=="Awake");
    Require(awake.Body.Instructions[0].Operand.ToString().Contains("CflRuntime::BeginStartupSession"),"Initial scene loading priority must start before Awake");
    var startup=app.MainModule.Types.Single(t=>t.FullName=="Game.Application.Main").Methods.Single(m=>m.Name=="Startup");
    Require(startup.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr && i.Operand.ToString().Contains("SetupAutoLoad")),"Attribute database startup timing missing");
    var services=app.MainModule.Types.Single(t=>t.FullName=="Game.Application.Layers.BootflowServicesInitState").Methods.Single(m=>m.Name=="SetupServices");
    Require(services.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr && i.Operand.ToString().Contains("SteamServiceProvider")),"Steam initialization timing missing");
    Require(services.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr && i.Operand.ToString().Contains("ITelemetryService::Init")),"Telemetry initialization timing missing");
    var auth=app.MainModule.Types.Single(t=>t.FullName=="Game.Application.Layers.MPAuthFlowState");
    var authStatus=auth.Methods.Single(m=>m.Name=="OnAuthenticationStatus");Require(authStatus.Body.Instructions.Any(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="SkipStartupMarket") && !authStatus.Body.Instructions.Any(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="GetMarketCatalog"),"Startup shop request must be skipped");
    Require(auth.Methods.Single(m=>m.Name=="OnEnter").Body.Instructions.Count(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="AuthenticateSession")==1,"Native authentication must run exactly once");
    Require(auth.Methods.Single(m=>m.Name=="HandleDeferredLobbyCallbacks").Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr && i.Operand.ToString().Contains("RetrieveMatchHistory")),"Profile/history timings missing");
    using(var originalApp=AssemblyDefinition.ReadAssembly(Path.Combine(Path.GetDirectoryName(dll),"Game.Application.dll.cfl-original"))){
     foreach(var type in app.MainModule.Types)foreach(var method in type.Methods.Where(m=>m.HasBody && m.Body.Instructions.Any(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.Name=="CflRuntime"))){
      var before=originalApp.MainModule.Types.Single(t=>t.FullName==type.FullName).Methods.Single(m=>m.FullName==method.FullName);
      var nativeCalls=before.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>i.Operand.ToString()).Where(text=>!(type.Name=="MPAuthFlowState" && method.Name=="OnAuthenticationStatus" && text.Contains("::GetMarketCatalog()")));
      var afterCalls=method.Body.Instructions.Where(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.Name!="CflRuntime").Select(i=>i.Operand.ToString());
      Require(nativeCalls.SequenceEqual(afterCalls) && before.Body.ExceptionHandlers.Count==method.Body.ExceptionHandlers.Count,"Startup native calls/exception handlers changed: "+method.FullName);
     }
    }
   }
   using(var core=AssemblyDefinition.ReadAssembly(Path.Combine(Path.GetDirectoryName(dll),"Carbon.Core.dll")))using(var beforeCore=AssemblyDefinition.ReadAssembly(Path.Combine(Path.GetDirectoryName(dll),"Carbon.Core.dll.cfl-original"))){
    foreach(string name in new[]{"InitSetUp","InitPostSetUp"}){
     var method=core.MainModule.Types.Single(t=>t.FullName=="Carbon.Core.Services.ServiceContext").Methods.Single(m=>m.Name==name);var before=beforeCore.MainModule.Types.Single(t=>t.FullName=="Carbon.Core.Services.ServiceContext").Methods.Single(m=>m.Name==name);
     Require(method.Body.Instructions.Any(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="StartupServiceBegin"),"Concrete service timing hook missing");
     Require(before.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>i.Operand.ToString()).SequenceEqual(method.Body.Instructions.Where(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.Name!="CflRuntime").Select(i=>i.Operand.ToString())),"Native service setup order changed");
    }
   }
   bool blocked=false;try{Patcher.Apply(test);}catch{blocked=true;}Require(blocked,"중복 적용 차단");
   string patched=Patcher.Hash(dll);string ui=Path.Combine(Path.GetDirectoryName(dll),"Game.UI.dll");byte[] uiBytes=File.ReadAllBytes(ui);File.AppendAllText(ui,"tamper");
   blocked=false;try{Patcher.Restore(test);}catch{blocked=true;}Require(blocked,"변경 파일 덮어쓰기 차단");
   Require(Patcher.Hash(dll)==patched,"복원 사전 검증의 부분 변경 방지");File.WriteAllBytes(ui,uiBytes);
   using(var a=AssemblyDefinition.ReadAssembly(dll)){
    var skillTech=a.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.Data.AbilityAttributes").Methods.Single(m=>m.Name=="get_RequiredTech");Require(skillTech.Body.Instructions.Any(x=>x.Operand!=null && x.Operand.ToString().Contains("CommanderSkillTech")),"Second commander skill HQ3 requirement hook missing");
    var initial=a.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.Data.AbilityAttributes").Methods.Single(m=>m.Name=="get_CooldownTimeInitial");Require(initial.Body.Instructions.Count==3 && initial.Body.Instructions[1].OpCode==OpCodes.Ldfld,"Original initial cooldown getter not restored");
    var regular=a.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.Data.AbilityAttributes").Methods.Single(m=>m.Name=="get_CooldownTime");Require(regular.Body.Instructions.Count==3 && regular.Body.Instructions[1].OpCode==OpCodes.Ldfld,"Regular cooldown getter changed");
    var fx=a.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.Data.BoundFXAttribute");
    Require(fx.Methods.Single(m=>m.Name=="get_FXScale").Body.Instructions.Any(i=>i.Operand!=null && i.Operand.ToString().Contains("CflRuntime::CombatTextScale")),"상태 텍스트 크기 후크");
    Require(fx.Methods.Single(m=>m.Name=="get_ScaleToEntitySize").Body.Instructions.Any(i=>i.Operand!=null && i.Operand.ToString().Contains("CflRuntime::CombatTextEntityScale")),"유닛 크기 확대 제한 후크");
    var execute=a.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.ExecuteScriptCommand").Methods.Single(method=>method.Name=="Execute");
    Require(execute.Body.Instructions.Any(i=>i.Operand!=null && i.Operand.ToString().Contains("CflRuntime::ExecuteCheat")),"시뮬레이션 자원 치트 후크");
    var fill=a.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.RosterHelper").Methods.Single(m=>m.Name=="FillRosterScratchpadForCategory");
    Require(fill.Body.Instructions.Any(i=>i.Operand!=null && i.Operand.ToString().Contains("CflRuntime::FillAll")),"전체 진영 유닛 등록");
    var check=a.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.SimRequirementValidator").Methods.Single(m=>m.Name=="SatisfiesRosterRequirement");
    Require(!check.Body.Instructions.Any(i=>i.Operand!=null && i.Operand.ToString().Contains("get_CheckRosterIndex")),"슬롯 위치 제한 제거");
   }
   using(var a=AssemblyDefinition.ReadAssembly(ui)){
    Require(!a.MainModule.Types.Single(t=>t.FullName=="Game.UI.CommandPanel").Methods.Any(m=>m.Name=="LateUpdate"),"매 프레임 테두리 검색 제거");
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
   using(var a=AssemblyDefinition.ReadAssembly(Path.Combine(Path.GetDirectoryName(dll),"Carbon.Presentation.Unity.dll"))){
    var spawn=a.MainModule.Types.Single(t=>t.FullName=="Carbon.Presentation.Unity.CarbonFXSpawner").Methods.Where(m=>m.Name=="SpawnAndHostFXPrefab").ToArray();
    if(spawn.Length!=2 || spawn.Any(m=>!m.Body.Instructions.Any(x=>x.Operand!=null && x.Operand.ToString().Contains("CombatTextAdditionalScale"))))throw new Exception("Missing combat text extra-scale hook");
   }
   using(var a=AssemblyDefinition.ReadAssembly(Path.Combine(Path.GetDirectoryName(dll),"Game.UI.dll"))){
    var hud=a.MainModule.Types.Single(t=>t.FullName=="Game.UI.HUD");
    Require(hud.Methods.Single(m=>m.Name=="Carbon.Core.Services.ISetUp.SetUp").Body.Instructions.Any(x=>x.Operand!=null && x.Operand.ToString().Contains("BeginMatchTimer")),"HUD timer creation hook");
    Require(hud.Methods.Single(m=>m.Name=="Carbon.Core.Services.ISetUp.TearDown").Body.Instructions.Any(x=>x.Operand!=null && x.Operand.ToString().Contains("EndMatchTimer")),"HUD timer cleanup hook");
    var position=a.MainModule.Types.Single(t=>t.FullName=="Game.UI.UIAnchoredPanel").Methods.Single(m=>m.Name=="UpdatePosition");
    if(position.Body.Instructions.Where(x=>x.OpCode==OpCodes.Ret).Any(x=>x.Previous.OpCode!=OpCodes.Stfld || !x.Previous.Operand.ToString().Contains("mTarget")))throw new Exception("Tooltip hover identity not restored on every return");
    if(!position.Body.Instructions.Take(3).Any(x=>x.OpCode==OpCodes.Stloc))throw new Exception("Original hover target not saved before layout hook");
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
   File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-result.txt"),"PASS: full-roster hook, positional requirement fix, 64 HUD slots, hotkey bound fix, duplicate rejection, tamper preflight, commander 0 energy and three-faction HQ rates 4/5/6, original initial cooldowns, nine-DLL, boot.config, Attributes.zip and Classic script byte-identical restore, legacy migration.");
   Directory.Delete(test,true);return 0;
  }catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-result.txt"),e.ToString());return 1;}
 }
 static void VerifyCommanderEnergy(string file){
  using(var zip=System.IO.Compression.ZipFile.OpenRead(file)){
   var collection=EnergyPatch.Read(zip,"Attributes/TrickleAttributesCollection/trickle_commander_default.txt");var entries=((System.Collections.IEnumerable)collection["TrickleEntries"]).Cast<object>().Select(x=>(System.Collections.Generic.Dictionary<string,object>)x).Where(x=>Convert.ToInt64(((System.Collections.Generic.Dictionary<string,object>)x["InventoryType"])["ReferenceID"])==2315574823061558829L).ToArray();
   Require(entries.Length==1 && Convert.ToInt32(entries[0]["Amount"])==0,"Legacy passive energy must not double-credit script income");
   foreach(string name in new[]{"ila_default_commander","ila_battlelines_commander"}){
    var loadout=EnergyPatch.Read(zip,"Attributes/InventoryLoadoutAttributes/"+name+".txt");var inventories=(System.Collections.Generic.Dictionary<string,object>)loadout["Inventories"];foreach(object item in (System.Collections.IEnumerable)inventories["Items"]){var inventory=(System.Collections.Generic.Dictionary<string,object>)item;if(Convert.ToInt64(((System.Collections.Generic.Dictionary<string,object>)inventory["InventoryID"])["ReferenceID"])==2315574823061558829L)Require(Convert.ToInt64(inventory["StartingAmount"])==0,"Starting commander energy incorrect");}
   }
  }
 }
 static void VerifyCatalogTimeout(string managed){
  string path=Path.Combine(managed,"Systems.Online.Steam.dll");
  using(var patched=AssemblyDefinition.ReadAssembly(path))using(var original=AssemblyDefinition.ReadAssembly(path+".cfl-original")){
   foreach(var type in original.MainModule.Types)foreach(var method in type.Methods.Where(m=>m.HasBody)){
    if(type.FullName=="Systems.Online.Steam.Market.SteamMarketCatalog" && new[]{"GetItem","GetItemByAttributeID","GetVirtualCurrencyItem"}.Contains(method.Name))continue;
    if(type.FullName=="Systems.Online.Steam.Market.Tasks.SteamGetMarketCatalogTask" && method.Name==".ctor")continue;
    if(type.FullName=="Systems.Online.Steam.Market.SteamMarketService" && method.Name=="HandleMarketCatalogRetrieved")continue;
    var after=patched.MainModule.Types.Single(t=>t.FullName==type.FullName).Methods.Single(m=>m.FullName==method.FullName);
    Require(method.Body.Instructions.Select(i=>i.ToString()).SequenceEqual(after.Body.Instructions.Select(i=>i.ToString())),"Unrelated Steam task changed: "+method.FullName);
   }
  }
  var loaded=new System.Collections.Generic.Dictionary<string,System.Reflection.Assembly>();ResolveEventHandler resolve=(sender,args)=>{string name=new System.Reflection.AssemblyName(args.Name).Name;System.Reflection.Assembly cached;if(loaded.TryGetValue(name,out cached))return cached;string dependency=Path.Combine(managed,name+".dll");if(!File.Exists(dependency))return null;cached=System.Reflection.Assembly.Load(File.ReadAllBytes(dependency));loaded[name]=cached;return cached;};
  AppDomain.CurrentDomain.AssemblyResolve+=resolve;
  try{
   var assembly=System.Reflection.Assembly.Load(File.ReadAllBytes(path));var type=assembly.GetType("Systems.Online.Steam.Market.Tasks.SteamGetMarketCatalogTask",true);
   var task=Activator.CreateInstance(type,new object[]{null,null});Require(Convert.ToSingle(type.GetProperty("TaskTimeout").GetValue(task,null))==15f,"Real market task timeout must be 15 seconds");
   var other=assembly.GetType("Systems.Online.Steam.SteamTask`1",true).MakeGenericType(typeof(object));var ordinary=Activator.CreateInstance(other,new object[]{null});Require(Convert.ToSingle(other.GetProperty("TaskTimeout").GetValue(ordinary,null))==90f,"Other Steam task timeout changed");
   var serviceType=assembly.GetType("Systems.Online.Steam.Market.SteamMarketService",true);var service=Activator.CreateInstance(serviceType,new object[]{null});int callbacks=0;object published=null;
   var databaseProperty=serviceType.GetProperty("AttributeDatabase");databaseProperty.SetValue(service,Activator.CreateInstance(databaseProperty.PropertyType.Assembly.GetType("Carbon.Attributes.AttributeDatabase",true)),null);
   Action<object> received=value=>{callbacks++;published=value;};var marketEvent=serviceType.GetEvent("OnMarketCatalogRetrieved");var parameter=System.Linq.Expressions.Expression.Parameter(marketEvent.EventHandlerType.GetMethod("Invoke").GetParameters()[0].ParameterType,"catalog");
   var action=System.Linq.Expressions.Expression.Invoke(System.Linq.Expressions.Expression.Constant(received),System.Linq.Expressions.Expression.Convert(parameter,typeof(object)));marketEvent.AddEventHandler(service,System.Linq.Expressions.Expression.Lambda(marketEvent.EventHandlerType,action,parameter).Compile());
   var runtime=System.Reflection.Assembly.Load("CflRuntime").GetType("CflRuntime",true);Require((bool)runtime.GetMethod("SkipStartupMarket").Invoke(null,new[]{service}),"Actual startup skip returned failure");
   var catalog=serviceType.GetProperty("Catalog").GetValue(service,null);Require(callbacks==1 && Object.ReferenceEquals(catalog,published) && catalog!=null,"Actual market callback/catalog state incorrect");Require(!((System.Collections.IEnumerable)catalog.GetType().GetProperty("Items").GetValue(catalog,null)).Cast<object>().Any(),"Skipped catalog must contain no priced items");
   var metadata=new object[]{new CatalogMetadataFixture{ID="owned-unit",MarketItemID=23},new ProfileCurrencyItemAttributes{ID="currency",MarketItemID=24}};runtime.GetMethod("RegisterLocalInventoryMetadata").Invoke(null,new object[]{catalog,metadata,assembly.GetType("Systems.Online.Steam.Market.SteamMarketItem",true)});
   var lookup=catalog.GetType().GetMethod("GetItem");var key=Activator.CreateInstance(lookup.GetParameters()[0].ParameterType,new object[]{23});var item=lookup.Invoke(catalog,new[]{key});Require(item!=null && (string)item.GetType().GetProperty("AttributeID").GetValue(item,null)=="owned-unit","Local inventory ID mapping failed; direct="+(runtime.GetMethod("LocalInventoryItem").Invoke(null,new object[]{null,catalog,key})!=null)+" runtime assemblies="+AppDomain.CurrentDomain.GetAssemblies().Count(x=>x.GetName().Name=="CflRuntime"));Require(Object.ReferenceEquals(item,catalog.GetType().GetMethod("GetItemByAttributeID").Invoke(catalog,new object[]{"owned-unit"})),"Local attribute lookup failed");Require(catalog.GetType().GetMethod("GetVirtualCurrencyItem").Invoke(catalog,new[]{key})==null,"Non-currency item returned as currency");
   var currencyKey=Activator.CreateInstance(lookup.GetParameters()[0].ParameterType,new object[]{24});Require(catalog.GetType().GetMethod("GetVirtualCurrencyItem").Invoke(catalog,new[]{currencyKey})!=null,"Local currency lookup failed");Require(!((System.Collections.IEnumerable)catalog.GetType().GetProperty("Items").GetValue(catalog,null)).Cast<object>().Any(),"Inventory metadata leaked into skipped shop");
   var simulation=System.Reflection.Assembly.Load(File.ReadAllBytes(Path.Combine(managed,"Carbon.Simulation.dll")));var movement=simulation.GetType("Carbon.Simulation.Data.GlobalMovementAttributes",true);var focus=movement.GetProperty("FocusClickShrinkAmount");var focusValue=focus.GetValue(Activator.CreateInstance(movement),null);Require(focusValue.Equals(focus.PropertyType.GetField("One").GetValue(null)),"Actual fixed-point focus offset must remain full size");
  }finally{AppDomain.CurrentDomain.AssemblyResolve-=resolve;}
 }
 static void VerifyMovement(string path){
  using(var after=AssemblyDefinition.ReadAssembly(path))using(var before=AssemblyDefinition.ReadAssembly(path+".cfl-original")){
   foreach(string name in new[]{"Carbon.Simulation.YieldHelpers","Carbon.Simulation.AvoidanceProcessor","Carbon.Simulation.SpreadingProcessor","Carbon.Simulation.Data.MoverAttributes","Carbon.Simulation.Data.RTSCollisionAttributes"})foreach(var method in before.MainModule.Types.Single(t=>t.FullName==name).Methods.Where(m=>m.HasBody)){
    var patched=after.MainModule.Types.Single(t=>t.FullName==name).Methods.Single(m=>m.FullName==method.FullName);Require(method.Body.Instructions.Select(i=>i.ToString()).SequenceEqual(patched.Body.Instructions.Select(i=>i.ToString())),"Unrelated movement rule changed: "+method.FullName);
   }
   var helper=after.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.YieldHelpers").Methods.Single(m=>m.Name==MovementPatch.HelperName);Require(helper.Parameters.Count==4 && !helper.Body.Instructions.Any(i=>i.OpCode==OpCodes.Box || i.OpCode==OpCodes.Newobj || (i.Operand!=null && i.Operand.ToString().Contains("CflRuntime"))),"Movement helper must use native values without boxing/reflection/runtime calls");
   var collision=after.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.RTSCollisionProcessor").Methods.Single(m=>m.Name=="FindCollisionsDiscreteVsDiscrete");Require(collision.Body.Instructions.Count(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name==MovementPatch.HelperName)==1,"Moving collision helper not connected");
   var original=before.MainModule.Types.Single(t=>t.FullName=="Carbon.Simulation.RTSCollisionProcessor").Methods.Single(m=>m.Name==collision.Name);Require(original.Body.Instructions.Where(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name!="EntityPairIgnoreCollision").Select(i=>i.Operand.ToString()).SequenceEqual(collision.Body.Instructions.Where(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name!=MovementPatch.HelperName).Select(i=>i.Operand.ToString())),"Other collision resolution calls changed");
  }
 }

}



public class CatalogMetadataFixture {public string ID;public int MarketItemID;}
public class ProfileCurrencyItemAttributes:CatalogMetadataFixture {}
