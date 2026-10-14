using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
[assembly:AssemblyVersion("0.2.0.0")]
public static partial class CflRuntime {
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
 // Native commands and submenus retain their authored hotkey slots.
 public static void Pack(object selection,Array buttons){
  int next=0,extent=0;var seen=new HashSet<object>();var deferred=new List<object>();
  foreach(object entry in (IEnumerable)Field(selection.GetType(),"mAbilityButtonScratchpad").GetValue(selection)){
   object button=Resolve(Get(entry,"Button"));if(button==null || !seen.Add(Get(button,"ID")))continue;
   int slot=(int)Get(entry,"Index");
   if(slot>=0 && slot<buttons.Length && buttons.GetValue(slot)==null){buttons.SetValue(button,slot);extent=Math.Max(extent,slot+1);}else deferred.Add(button);
  }
  foreach(string field in new[]{"mProductionButtonScratchpad","mUpgradeButtonScratchpad"}){
   foreach(object entry in (IEnumerable)Field(selection.GetType(),field).GetValue(selection)){
    object button=Resolve(Get(entry,"Button"));if(button==null || !seen.Add(Get(button,"ID")))continue;
    deferred.Add(button);
   }
  }
  foreach(object button in deferred){while(next<buttons.Length && buttons.GetValue(next)!=null)next++;if(next>=buttons.Length)throw new InvalidOperationException("CFL command grid capacity exceeded");buttons.SetValue(button,next);extent=Math.Max(extent,++next);}
  visibleButtons=extent;
  foreach(var list in new List<object>(layouts.Keys)){try{ApplyLayout(layouts[list],extent);}catch(Exception e){layouts.Remove(list);Log(e);}}
 }
 static Type TypeByName(string name){foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){var t=a.GetType(name);if(t!=null)return t;}return null;}
 static object Vector(float x,float y){return Activator.CreateInstance(TypeByName("UnityEngine.Vector2"),new object[]{x,y});}
 static object Component(object value,Type type){return value.GetType().GetMethod("GetComponent",new[]{typeof(Type)}).Invoke(value,new object[]{type});}
 static bool IsCommandList(object list){
  var panel=TypeByName("Game.UI.CommandPanel");if(panel==null)return false;
  // Only the command grid directly owned by CommandPanel, never tooltips or nested lists.
  object parent=Get(Get(list,"transform"),"parent");
  return parent!=null && (string)Get(parent,"name")!="CommanderAbilityPanel" && Component(parent,panel)!=null;
 }
 // CommanderAbilityPanel also uses Game.UI.CommandPanel; it must never enter the production grid.
 public static bool LayoutGlobalSkills(object list){
  object transform=Get(list,"transform"),panel=Get(transform,"parent");
  if(panel==null || (string)Get(panel,"name")!="CommanderAbilityPanel")return false;
  object grid=Component(list,TypeByName("UnityEngine.UI.GridLayoutGroup"));if(grid==null)return true;
  object cell=Get(grid,"cellSize"),spacing=Get(grid,"spacing"),padding=Get(grid,"padding");
  float width=2*(float)Get(cell,"x")+(float)Get(spacing,"x")+(int)Get(padding,"left")+(int)Get(padding,"right")+16;
  float height=(float)Get(cell,"y")+(int)Get(padding,"top")+(int)Get(padding,"bottom")+12;
  var constraint=grid.GetType().GetProperty("constraint");constraint.SetValue(grid,Enum.ToObject(constraint.PropertyType,1),null);Set(grid,"constraintCount",2);
  Set(panel,"anchorMin",Vector(0,1));Set(panel,"anchorMax",Vector(0,1));Set(panel,"pivot",Vector(0,0));
  Set(panel,"sizeDelta",Vector(width,height));Set(panel,"anchoredPosition",Vector(12,12));
  object root=Get(panel,"parent");if(root!=null){
   object powerups=root.GetType().GetMethod("Find",new[]{typeof(string)}).Invoke(root,new object[]{"UIListPowerupAbilities"});
   if(powerups!=null){object position=Get(powerups,"anchoredPosition");float rootHeight=(float)Get(Get(root,"rect"),"height");Set(powerups,"anchoredPosition",Vector((float)Get(position,"x"),rootHeight+height+24));}
  }
  return true;
 }
 class Layout {public object Panel,Grid,EmptyGrid,Position,Viewport,Scale;public float CellX,CellY,SpacingX,SpacingY;public int Left,Right,Top,Bottom;}
 static readonly Dictionary<object,Layout> layouts=new Dictionary<object,Layout>();
 static int visibleButtons;
 public static void BeforeList(object list){try{if(IsCommandList(list)){Field(list.GetType(),"m_MaxItems").SetValue(list,Slots);var invalid=Field(list.GetType(),"m_InvalidDataBehavior");invalid.SetValue(list,Enum.ToObject(invalid.FieldType,0));}}catch(Exception e){Log(e);}}
 public static void AfterList(object list){try{
  if(LayoutGlobalSkills(list))return;
  if(!IsCommandList(list))return;
  object transform=Get(list,"transform");
  object group=Component(list,TypeByName("UnityEngine.UI.GridLayoutGroup"));if(group==null)return;
  if(!layouts.ContainsKey(list)){
   object panel=Get(transform,"parent"),cell=Get(group,"cellSize"),spacing=Get(group,"spacing"),padding=Get(group,"padding");
   object empty=panel.GetType().GetMethod("Find",new[]{typeof(string)}).Invoke(panel,new object[]{"StaticElements/EmptyGrid"});
   layouts[list]=new Layout{Panel=panel,Grid=group,EmptyGrid=empty,Position=Get(panel,"anchoredPosition"),Viewport=Get(panel,"parent"),Scale=Get(panel,"localScale"),CellX=(float)Get(cell,"x"),CellY=(float)Get(cell,"y"),SpacingX=(float)Get(spacing,"x"),SpacingY=(float)Get(spacing,"y"),Left=(int)Get(padding,"left"),Right=(int)Get(padding,"right"),Top=(int)Get(padding,"top"),Bottom=(int)Get(padding,"bottom")};
  }
  HideCommandFrame();
  ApplyLayout(layouts[list],visibleButtons);
  Log("Native command grid registered; capacity 64; button transforms preserved");
 }catch(Exception e){Log(e);}}
 public static void AfterRefresh(object list){try{
  if(LayoutGlobalSkills(list))return;
  if(!IsCommandList(list))return;
  object provider=Get(list,"DataProvider");if(provider==null)return;
  var getItem=provider.GetType().GetMethod("GetItemAtIndex",new[]{typeof(int)});
  int i=0;foreach(object item in (IEnumerable)Get(list,"ListItems")){
   object data=getItem.Invoke(provider,new object[]{i});bool valid=data!=null && (bool)data.GetType().GetMethod("get_HasValue").Invoke(data,null);
   if(!valid && i<12){
    object go=Get(item,"gameObject");go.GetType().GetMethod("SetActive",new[]{typeof(bool)}).Invoke(go,new object[]{true});
    Set(Get(item,"transform"),"localScale",Activator.CreateInstance(TypeByName("UnityEngine.Vector3"),new object[]{0f,0f,0f}));
   }
   i++;
  }
 }catch(Exception e){Log(e);}}
 // Hide only the decorative command-area sprite; selection portrait and hotkeys are separate images.
 public static void HideCommandFrame(){
  var images=TypeByName("UnityEngine.UI.Image");var resources=TypeByName("UnityEngine.Resources");if(images==null || resources==null)return;
  var find=resources.GetMethod("FindObjectsOfTypeAll",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(Type)},null);
  foreach(object image in (IEnumerable)find.Invoke(null,new object[]{images})){
   object sprite=Get(image,"sprite");if(sprite!=null && (string)Get(sprite,"name")=="ui_hud_command_ability_BG")Set(image,"enabled",false);
  }
 }
 static void ApplyLayout(Layout layout,int count){
  float x=(float)Get(layout.Position,"x"),y=(float)Get(layout.Position,"y");
  float viewportWidth=1920,viewportHeight=1080;
  if(layout.Viewport!=null){object rect=Get(layout.Viewport,"rect");float w=(float)Get(rect,"width"),h=(float)Get(rect,"height");if(w>0 && h>0){viewportWidth=w;viewportHeight=h;}}
  int columns=4,rows=Math.Max(3,(count+columns-1)/columns);
  float width=columns*layout.CellX+(columns-1)*layout.SpacingX+layout.Left+layout.Right;
  float height=rows*layout.CellY+(rows-1)*layout.SpacingY+layout.Top+layout.Bottom;
  Set(layout.Grid,"enabled",true);var constraint=layout.Grid.GetType().GetProperty("constraint");constraint.SetValue(layout.Grid,Enum.ToObject(constraint.PropertyType,1),null);Set(layout.Grid,"constraintCount",columns);
  float scale=Math.Min(1f,Math.Min((viewportHeight-y-16)/height,(viewportWidth-Math.Abs(x)-16)/width));
  Set(layout.Panel,"localScale",Activator.CreateInstance(TypeByName("UnityEngine.Vector3"),new object[]{(float)Get(layout.Scale,"x")*scale,(float)Get(layout.Scale,"y")*scale,(float)Get(layout.Scale,"z")}));
  Set(layout.Panel,"sizeDelta",Vector(width,height));
  Set(layout.Panel,"anchoredPosition",Vector(x,y));
  if(layout.EmptyGrid!=null){object go=Get(layout.EmptyGrid,"gameObject");go.GetType().GetMethod("SetActive",new[]{typeof(bool)}).Invoke(go,new object[]{count<=12});}
 }
 static void Log(object message){var debug=TypeByName("UnityEngine.Debug");if(debug!=null)debug.GetMethod("Log",new[]{typeof(object)}).Invoke(null,new[]{(object)("[CFL All Units] "+message)});}
}
