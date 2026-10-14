using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
public static partial class CflRuntime {
 const string SpeedCommand="-- CFL_SINGLE_PLAYER_SPEED100_V1",PopCommand="-- CFL_SINGLE_PLAYER_POP_V1";
 const string MoneyCommand="-- CFL_SINGLE_PLAYER_MONEY_V1";
 static object Call(object target,string name,params object[] args){return target.GetType().GetMethod(name,Flags).Invoke(target,args);}
 static object UI(){return TypeByName("Game.Common.UI.GameUIBase").GetProperty("Instance",BindingFlags.Public|BindingFlags.Static).GetValue(null,null);}
 // Fail closed when match metadata is unavailable. AI opponents are allowed.
 static int CheatSlot(object ui){
  object match=Get(ui,"MatchDataSource");
  if((bool)Call(match,"IsReplayMode") || (bool)Call(match,"IsObserverPlayer") || (bool)Call(match,"IsAutoMatchMode"))return -1;
  object source=Get(ui,"MatchPlayerSlotDataSource");var method=source.GetType().GetMethod("GetMatchPlayerSlots",Flags);
  var collectionType=method.GetParameters()[1].ParameterType;
  object slots=Activator.CreateInstance(collectionType);
  if(!(bool)method.Invoke(source,new[]{Call(match,"GetMatch"),slots}))return -1;
  int humans=0,local=-1;
  foreach(object slot in (IEnumerable)slots){
   string kind=Get(slot,"Type").ToString();object group=Get(slot,"GroupData");
   object player=Get(group,"Player");if(player==null)continue;
   object id=Get(player,"ID");object invalid=id.GetType().GetField("kInvalidID",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
   if(id.Equals(invalid))continue;
   if(kind=="Human"){
    humans++;
    if((bool)Get(player,"IsLocal"))local=(int)Get(group,"SlotID");
   }else if(kind=="Observer")return -1;
  }
  return humans==1 && local>=0 && local<64?local:-1;
 }
 static void CheatNotice(object ui,string text){
  try{
   object message=Activator.CreateInstance(TypeByName("Game.Online.Data.ChatMessage"));
   Set(message,"Message","[CFL] "+text);Set(message,"RecipientID",String.Empty);Set(message,"RecipientName",String.Empty);Set(message,"SenderID",String.Empty);Set(message,"SenderName","CFL");
   var source=message.GetType().GetProperty("Source");source.SetValue(message,Enum.ToObject(source.PropertyType,3),null);
   Call(Get(ui,"ChatDataSource"),"HandleMessageReceived",message);
  }catch(Exception e){Log(e);}
 }
 public static bool TryCheat(object panel){
  object input=Get(panel,"mInputField");string code=((string)Get(input,"text")??String.Empty).Trim().ToLowerInvariant();
  if(code!="money" && code!="speed" && code!="pop")return false;
  Set(input,"text",String.Empty);
  object ui=null;
  try{
   ui=UI();int slot=CheatSlot(ui);
   if(slot<0){CheatNotice(ui,"싱글플레이에서만 사용할 수 있습니다.");return true;}
   object scheduler=Get(Get(ui,"MissionDataSource"),"mCommandScheduler"),owner=Get(scheduler,"LocalSimCommander");
   string script=code=="money"?MoneyCommand:code=="speed"?SpeedCommand:PopCommand;
   object command=TypeByName("Carbon.Simulation.SimCommandFactory").GetMethod("CreateExecuteScriptCommand",BindingFlags.Public|BindingFlags.Static).Invoke(null,new[]{owner,script});
   if(!(bool)Call(scheduler,"ScheduleCommand",command))throw new InvalidOperationException("Cheat command was not scheduled");
   CheatNotice(ui,code=="money"?"money: 자원 9999 설정 명령 접수":code=="speed"?"speed: 생산·건설 속도 100배 명령 접수 (중첩 없음)":"pop: 최대 생산 인구 한도 명령 접수");
  }catch(Exception e){Log(e);if(ui!=null)CheatNotice(ui,"Cheat failed; see Player.log.");}
  return true;
 }
 // Custom buffs are cloned and registered under distinct IDs, never modifying authored assets.
 static object Real(int value){return TypeByName("Carbon.Core.Numerics.Q64.Real").GetMethod("FromInt",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{value});}
 static object Attribute(object database,string name){
  foreach(var method in database.GetType().GetMethods(Flags))if(method.Name=="GetObject" && !method.IsGenericMethod && method.GetParameters().Length==1 && method.GetParameters()[0].ParameterType==typeof(string))return method.Invoke(database,new object[]{name});
  throw new MissingMethodException("AttributeDatabase.GetObject(string)");
 }
 static object CustomBuff(object database,string template,string name,ulong id,object value,bool children){
  object existing=Attribute(database,name);if(existing!=null){Set(existing,"Value",value);Set(existing,"IntValue",(int)value.GetType().GetMethod("IntValue",BindingFlags.Public|BindingFlags.Static).Invoke(null,new[]{value}));return existing;}
  object original=Attribute(database,template);if(original==null)throw new InvalidOperationException("Missing buff: "+template);
  object buff=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(original,null);
  Set(buff,"Name",name);Set(buff,"ID",Activator.CreateInstance(Get(buff,"ID").GetType(),new object[]{id}));
  Set(buff,"Value",value);Set(buff,"IntValue",(int)value.GetType().GetMethod("IntValue",BindingFlags.Public|BindingFlags.Static).Invoke(null,new[]{value}));Set(buff,"ApplyToChildren",children);
  Call(database,"AddObject",buff,false);return buff;
 }
 static void ApplyCheatBuffs(object owner,object simulation,params object[] buffs){
  var type=TypeByName("Carbon.Simulation.Data.BuffAttributes");Array array=Array.CreateInstance(type,buffs.Length);for(int i=0;i<buffs.Length;i++)array.SetValue(buffs[i],i);
  var execute=TypeByName("Carbon.Simulation.ModifyBuffAction").GetMethod("Execute",BindingFlags.Public|BindingFlags.Static);
  var operation=execute.GetParameters()[2].ParameterType;
  // Remove then add the same custom IDs so repeated commands never accumulate multipliers.
  foreach(string name in new[]{"Remove","Add"})execute.Invoke(null,new object[]{owner,array,Enum.Parse(operation,name),simulation,false});
 }
 public static bool ExecuteCheat(object command,object simulation){
  string code=(string)Get(command,"ScriptToRun");if(code==MoneyCommand)return ExecuteMoney(command);
  if(code!=SpeedCommand && code!=PopCommand)return false;
  try{
   object database=Get(simulation,"mAttributeDatabase"),owner=Get(command,"Owner");
   if(code==SpeedCommand){
    var production=CustomBuff(database,"ba_campaign_cvs_test_object_m07_production_rate","cfl_production100_v1",0x43464c0000000101UL,Real(100),true);
    var construction=CustomBuff(database,"ba_campaign_production_time_fast","cfl_construction100_v1",0x43464c0000000102UL,Real(100),true);
    ApplyCheatBuffs(owner,simulation,production,construction);
   }else{
    var helper=TypeByName("Carbon.Simulation.StoreHelper`1").MakeGenericType(TypeByName("Carbon.Simulation.Sim"));
    object store=helper.GetMethod("GetOrAddStoreComponent",BindingFlags.Public|BindingFlags.Static).Invoke(null,new[]{owner});
    foreach(object inventory in (IEnumerable)Get(Get(Get(store,"InventoryManager"),"Inventories"),"Values")){
     if((string)Get(Resolve(Get(Get(inventory,"Attributes"),"Inventory")),"Name")!="ia_population")continue;
     object limit=Get(inventory,"HardCap");
     var buff=CustomBuff(database,"ba_pop_increase_200","cfl_population_max_v1",0x43464c0000000103UL,limit,false);
     ApplyCheatBuffs(owner,simulation,buff);Set(inventory,"ActiveCap",limit);return true;
    }
    throw new InvalidOperationException("Missing population inventory");
   }
  }catch(Exception e){Log(e);}
  return true;
 }

 // Runs on the simulation command path, never directly from the presentation thread.
 public static bool ExecuteMoney(object command){
  if((string)Get(command,"ScriptToRun")!=MoneyCommand)return false;
  try{
   object owner=Get(command,"Owner");
   var helper=TypeByName("Carbon.Simulation.StoreHelper`1").MakeGenericType(TypeByName("Carbon.Simulation.Sim"));
   object store=helper.GetMethod("GetOrAddStoreComponent",BindingFlags.Public|BindingFlags.Static).Invoke(null,new[]{owner});
   object inventories=Get(Get(store,"InventoryManager"),"Inventories");var resources=new List<object>();
   foreach(object inventory in (IEnumerable)Get(inventories,"Values")){
    string name=(string)Get(Resolve(Get(Get(inventory,"Attributes"),"Inventory")),"Name");
    if(name=="ia_resource1" || name=="ia_resource2")resources.Add(inventory);
   }
   if(resources.Count!=2)throw new InvalidOperationException("CFL: both resource inventories are required");
   foreach(object inventory in resources){
    var amount=inventory.GetType().GetMethod("SetCurrentAmount").GetParameters()[0].ParameterType;
    object value=amount.GetMethod("FromInt",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(int)},null).Invoke(null,new object[]{9999});
    object cap=Get(inventory,"ActiveCap");
    bool below=(bool)amount.GetMethod("op_LessThan",BindingFlags.Public|BindingFlags.Static).Invoke(null,new[]{cap,value});
    if(below)Set(inventory,"ActiveCap",value);
    Call(inventory,"SetCurrentAmount",value);
   }
  }catch(Exception e){Log(e);}
  return true;
 }
}
