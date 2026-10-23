using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;
using System.Linq;
public static partial class CflRuntime {
 static readonly Dictionary<object,object> turretBuilders=new Dictionary<object,object>();
 static readonly ConcurrentQueue<object[]> turretViews=new ConcurrentQueue<object[]>();
 static readonly HashSet<object> turretHiddenViews=new HashSet<object>();
 static object WorldComponent(object entity,string componentName,string worldName){
  Type world=TypeByName(worldName),component=TypeByName(componentName);if(component.IsGenericTypeDefinition)component=component.MakeGenericType(world);
  object id=component.GetProperty("ComponentID",BindingFlags.Static|BindingFlags.Public|BindingFlags.FlattenHierarchy).GetValue(null,null);
  var get=TypeByName("Carbon.Core.Entities.EntityExtensions").GetMethods().Single(m=>m.Name=="GetComponent"&&m.IsGenericMethodDefinition&&m.GetGenericArguments().Length==2&&m.GetParameters().Length==2);
  return get.MakeGenericMethod(world,component).Invoke(null,new[]{entity,id});
 }
 static bool ValidSim(object entity){return (bool)TypeByName("Carbon.Core.Entities.EntityExtensions").GetMethods().Single(m=>m.Name=="IsValid"&&m.IsGenericMethodDefinition).MakeGenericMethod(TypeByName("Carbon.Simulation.Sim")).Invoke(null,new[]{entity});}
 static void SuspendBuilder(object worker,bool hidden,object process){
  var method=TypeByName("Carbon.Simulation.SuspensionHelper").GetMethods().Single(m=>m.Name=="SetSuspensionState").MakeGenericMethod(TypeByName("Carbon.Simulation.Sim"));
  method.Invoke(null,new[]{worker,Enum.ToObject(method.GetParameters()[1].ParameterType,hidden?0:1),process});
  turretViews.Enqueue(new object[]{worker,hidden});
 }
 public static void TurretConstructionBegin(object worker,object building,object process){
  // Called only after native validation and StartConstruction succeeded, on the simulation thread.
  try{
   if(turretBuilders.ContainsKey(building)||turretBuilders.ContainsValue(worker))return;
   object construction=WorldComponent(building,"Carbon.Simulation.UnderConstructionComponent","Carbon.Simulation.Sim");
   if(construction==null||!TurretRules.IsTurretID(SuidBits(Get(Get(construction,"ConstructionAttributes"),"ID"))))return;
   if(WorldComponent(worker,"Carbon.Simulation.ConstructorComponent","Carbon.Simulation.Sim")==null||WorldComponent(worker,"Carbon.Simulation.SuspendedComponent`1","Carbon.Simulation.Sim")!=null)return;
   turretBuilders.Add(building,worker);SuspendBuilder(worker,true,process);Log("Turret builder reserved: "+worker+" -> "+building);
  }catch(Exception e){Log("Turret construction begin failed: "+e.GetBaseException().Message);throw;}
 }
 public static void TurretConstructionEnd(object building,object process){
  object worker;if(!turretBuilders.TryGetValue(building,out worker))return;
  turretBuilders.Remove(building);if(ValidSim(worker)){SuspendBuilder(worker,false,process);Log("Turret builder restored: "+worker);}
 }
 public static void TurretEntityDespawn(object entity,object process){
  if(entity.GetType().GetGenericArguments()[0].FullName!="Carbon.Simulation.Sim")return;
  TurretConstructionEnd(entity,process);
  // An independently destroyed builder must never be resurrected.
  object key=null;foreach(var item in turretBuilders)if(item.Value.Equals(entity)){key=item.Key;break;}if(key!=null)turretBuilders.Remove(key);
 }
 public static void TurretSessionEnd(object controller){turretBuilders.Clear();}
 public static bool TurretViewHidden(object entity){return turretHiddenViews.Contains(entity);}
 public static void TurretViewTick(object processor){
  object[] change;
  while(turretViews.TryDequeue(out change))try{
   object mapper=Get(processor,"mEntityMapper"),entity=mapper.GetType().GetProperty("Item",new[]{change[0].GetType()}).GetValue(mapper,new[]{change[0]});
   if((bool)change[1]){turretHiddenViews.Add(entity);var method=processor.GetType().GetMethod("UpdateEntityVisibility",Flags);method.Invoke(processor,new[]{entity,Enum.ToObject(method.GetParameters()[1].ParameterType,0)});}
   else {turretHiddenViews.Remove(entity);if((bool)Get(processor,"mSpectatorMode"))Call(processor,"EnableSpectatorModeForEntity",entity);else {object commander=Get(Get(processor,"mCommandScheduler"),"LocalSimCommander");object view=mapper.GetType().GetProperty("Item",new[]{commander.GetType()}).GetValue(mapper,new[]{commander});Call(processor,"ShowOrHideEntity",entity,view);}}
  }catch(Exception e){Log("Turret view update failed: "+e.GetBaseException().Message);}
 }
 public static void TurretViewEnd(object processor){object[] change;while(turretViews.TryDequeue(out change)){}turretHiddenViews.Clear();}
}
