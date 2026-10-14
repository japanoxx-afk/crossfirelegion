using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
[assembly:AssemblyVersion("0.2.0.0")]
public static class CflRuntime {
 public const int Slots=64;
 static readonly BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static object Get(object o,string name){var p=o.GetType().GetProperty(name,Flags);return p!=null?p.GetValue(o,null):Field(o.GetType(),name).GetValue(o);}
 static FieldInfo Field(Type t,string name){while(t!=null){var f=t.GetField(name,Flags);if(f!=null)return f;t=t.BaseType;}throw new MissingFieldException(name);}
 static void Set(object o,string name,object v){var p=o.GetType().GetProperty(name,Flags);if(p!=null)p.SetValue(o,v,null);else Field(o.GetType(),name).SetValue(o,v);}
 static object Resolve(object reference){var m=reference.GetType().GetMethod("op_Implicit",BindingFlags.Static|BindingFlags.Public);return m.Invoke(null,new[]{reference});}
 public static void FillAll(object category,object destination){
  var add=destination.GetType().GetMethod("Add");var itemType=add.GetParameters()[0].ParameterType;
  var seen=new HashSet<object>();
  foreach(object slot in (IEnumerable)Get(category,"RosterSlots"))foreach(object reference in (IEnumerable)Get(slot,"Items")){
   object item=Resolve(reference);if(item==null || !seen.Add(Get(item,"ID")))continue;
   add.Invoke(destination,new[]{Activator.CreateInstance(itemType,new[]{item})});
  }
 }
 // Sequential packing removes collisions between alternative units and their upgrades.
 public static void Pack(object selection,Array buttons){
  int next=0;var seen=new HashSet<object>();
  foreach(string field in new[]{"mProductionButtonScratchpad","mUpgradeButtonScratchpad","mAbilityButtonScratchpad"}){
   foreach(object entry in (IEnumerable)Field(selection.GetType(),field).GetValue(selection)){
    object button=Resolve(Get(entry,"Button"));if(button==null || !seen.Add(Get(button,"ID")))continue;
    if(next>=buttons.Length)throw new InvalidOperationException("CFL command grid capacity exceeded");
    buttons.SetValue(button,next++);
   }
  }
 }
 static Type TypeByName(string name){foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){var t=a.GetType(name);if(t!=null)return t;}return null;}
 static object Vector(float x,float y){return Activator.CreateInstance(TypeByName("UnityEngine.Vector2"),new object[]{x,y});}
 static object Component(object value,Type type){return value.GetType().GetMethod("GetComponent",new[]{typeof(Type)}).Invoke(value,new object[]{type});}
 static bool IsCommandList(object list){
  var panel=TypeByName("Game.UI.CommandPanel");if(panel==null)return false;
  return list.GetType().GetMethod("GetComponentInParent",new[]{typeof(Type)}).Invoke(list,new object[]{panel})!=null;
 }
 public static void BeforeList(object list){try{if(IsCommandList(list))Field(list.GetType(),"m_MaxItems").SetValue(list,Slots);}catch(Exception e){Log(e);}}
 public static void AfterList(object list){try{
  if(!IsCommandList(list))return;
  object transform=Get(list,"transform");
  var layout=TypeByName("UnityEngine.UI.LayoutGroup");object group=Component(list,layout);if(group!=null)Set(group,"enabled",false);
  // Reuse the game's native clickable items, events, and tooltips.
  Set(transform,"anchorMin",Vector(1,0));Set(transform,"anchorMax",Vector(1,0));Set(transform,"pivot",Vector(1,0));
  Set(transform,"sizeDelta",Vector(480,480));Set(transform,"anchoredPosition",Vector(-8,8));
  object clippingNode=list;for(int depth=0;depth<2 && clippingNode!=null;depth++){
   foreach(string name in new[]{"UnityEngine.UI.Mask","UnityEngine.UI.RectMask2D"}){var type=TypeByName(name);if(type==null)continue;object mask=Component(clippingNode,type);if(mask!=null)Set(mask,"enabled",false);}
   object nodeTransform=Get(clippingNode,"transform");clippingNode=Get(nodeTransform,"parent");
  }
  int i=0;foreach(object item in (IEnumerable)Get(list,"ListItems")){
   object t=Get(item,"transform");Set(t,"anchorMin",Vector(0,1));Set(t,"anchorMax",Vector(0,1));Set(t,"pivot",Vector(0,1));
   object rect=Get(t,"rect");float w=(float)Get(rect,"width"),h=(float)Get(rect,"height");
   if(w<=0)w=80;if(h<=0)h=80;
   Set(t,"localScale",Activator.CreateInstance(TypeByName("UnityEngine.Vector3"),new object[]{58/w,58/h,1f}));
   Set(t,"anchoredPosition",Vector((i%8)*60,-(i/8)*60));i++;
  }
  Log("64-slot command HUD configured");
 }catch(Exception e){Log(e);}}
 static void Log(object message){var debug=TypeByName("UnityEngine.Debug");if(debug!=null)debug.GetMethod("Log",new[]{typeof(object)}).Invoke(null,new[]{(object)("[CFL All Units] "+message)});}
}
