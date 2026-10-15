using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
[assembly:AssemblyVersion("0.2.0.0")]
public static partial class CflRuntime {
 public const int Slots=64;
 static readonly BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 class Access {public PropertyInfo Property;public FieldInfo Field;public MethodInfo Setter;}
 static readonly ConcurrentDictionary<Type,ConcurrentDictionary<string,Access>> members=new ConcurrentDictionary<Type,ConcurrentDictionary<string,Access>>();
 static readonly ConcurrentDictionary<string,Type> types=new ConcurrentDictionary<string,Type>();
 static readonly ConcurrentDictionary<Type,MethodInfo> conversions=new ConcurrentDictionary<Type,MethodInfo>();
 static Access Member(Type type,string name){
  var map=members.GetOrAdd(type,t=>new ConcurrentDictionary<string,Access>());Access found;if(map.TryGetValue(name,out found))return found;
  var result=new Access();
  for(Type t=type;t!=null;t=t.BaseType){
   var p=t.GetProperty(name,Flags|BindingFlags.DeclaredOnly);if(p!=null){if(result.Property==null)result.Property=p;if(result.Setter==null)result.Setter=p.GetSetMethod(true);}
   var f=t.GetField(name,Flags|BindingFlags.DeclaredOnly);if(f!=null && result.Field==null)result.Field=f;
  }
  if(result.Property==null && result.Field==null)throw new MissingMemberException(type.FullName,name);
  return map.GetOrAdd(name,result);
 }
 static object Get(object o,string name){var a=Member(o.GetType(),name);return a.Property!=null?a.Property.GetValue(o,null):a.Field.GetValue(o);}
 static FieldInfo Field(Type t,string name){var field=Member(t,name).Field;if(field==null)throw new MissingFieldException(name);return field;}
 static void Set(object o,string name,object v){var a=Member(o.GetType(),name);if(a.Setter!=null)a.Setter.Invoke(o,new[]{v});else if(a.Field!=null)a.Field.SetValue(o,v);else throw new MissingMethodException("No setter for "+name);}
 static object Resolve(object reference){var m=conversions.GetOrAdd(reference.GetType(),t=>t.GetMethod("op_Implicit",BindingFlags.Static|BindingFlags.Public));return m.Invoke(null,new[]{reference});}
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
 static Type TypeByName(string name){Type cached;if(types.TryGetValue(name,out cached))return cached;foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){var t=a.GetType(name);if(t!=null){types.TryAdd(name,t);return t;}}return null;}
 static object Vector(float x,float y){return Activator.CreateInstance(TypeByName("UnityEngine.Vector2"),new object[]{x,y});}
 static object Component(object value,Type type){return value.GetType().GetMethod("GetComponent",new[]{typeof(Type)}).Invoke(value,new object[]{type});}
 static bool IsCommandList(object list){
  var panel=TypeByName("Game.UI.CommandPanel");if(panel==null)return false;
  // Only the command grid directly owned by CommandPanel, never tooltips or nested lists.
  object parent=Get(Get(list,"transform"),"parent");
  return parent!=null && (string)Get(parent,"name")!="CommanderAbilityPanel" && Component(parent,panel)!=null;
 }
 // CommanderAbilityPanel also uses Game.UI.CommandPanel; it must never enter the production grid.
 static readonly HashSet<object> configuredGlobals=new HashSet<object>();
 public static bool LayoutGlobalSkills(object list){
  if(configuredGlobals.Contains(list))return true;
  object transform=Get(list,"transform"),panel=Get(transform,"parent");
  if(panel==null || (string)Get(panel,"name")!="CommanderAbilityPanel")return false;
  object grid=Component(list,TypeByName("UnityEngine.UI.GridLayoutGroup"));if(grid==null)return true;
  var constraint=grid.GetType().GetProperty("constraint");constraint.SetValue(grid,Enum.ToObject(constraint.PropertyType,1),null);Set(grid,"constraintCount",1);
  // Authored level20 geometry: the narrow slot beside the minimap, left of the energy meter.
  Set(panel,"anchorMin",Vector(1,1));Set(panel,"anchorMax",Vector(1,1));Set(panel,"pivot",Vector(1,1));
  Set(panel,"sizeDelta",Vector(82,152));Set(panel,"anchoredPosition",Vector(-32,0));
  object root=Get(panel,"parent");if(root!=null){
   object powerups=root.GetType().GetMethod("Find",new[]{typeof(string)}).Invoke(root,new object[]{"UIListPowerupAbilities"});
   if(powerups!=null){object position=Get(powerups,"anchoredPosition");float rootHeight=(float)Get(Get(root,"rect"),"height");Set(powerups,"anchoredPosition",Vector((float)Get(position,"x"),rootHeight+32));}
  }
  configuredGlobals.Add(list);return true;
 }
 class Layout {public object Panel,Grid,EmptyGrid,Position,Viewport,Scale,List;public object Probe;public float ProbeY;public int Count=-1;public float ViewWidth,ViewHeight,Gap;public bool GapPending;public float CellX,CellY,SpacingX,SpacingY;public int Left,Right,Top,Bottom;}
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
   layouts[list]=new Layout{List=list,Panel=panel,Grid=group,EmptyGrid=empty,Position=Get(panel,"anchoredPosition"),Viewport=Get(panel,"parent"),Scale=Get(panel,"localScale"),CellX=(float)Get(cell,"x"),CellY=(float)Get(cell,"y"),SpacingX=(float)Get(spacing,"x"),SpacingY=(float)Get(spacing,"y"),Left=(int)Get(padding,"left"),Right=(int)Get(padding,"right"),Top=(int)Get(padding,"top"),Bottom=(int)Get(padding,"bottom")};
  }
  InstallRowGapRender();
  ApplyLayout(layouts[list],visibleButtons);
  Log("Native command grid registered; capacity 64; button transforms preserved");
 }catch(Exception e){Log(e);}}
 public static void AfterRefresh(object list){try{
  if(LayoutGlobalSkills(list))return;
  if(!IsCommandList(list))return;
  object provider=Get(list,"DataProvider");if(provider==null)return;
  Layout current;if(layouts.TryGetValue(list,out current))ApplyLayout(current,visibleButtons);
  var getItem=provider.GetType().GetMethod("GetItemAtIndex",new[]{typeof(int)});
  int i=0;foreach(object item in (IEnumerable)Get(list,"ListItems")){
   if(i>=12)break;
   object data=getItem.Invoke(provider,new object[]{i});bool valid=data!=null && (bool)data.GetType().GetMethod("get_HasValue").Invoke(data,null);
   if(!valid && i<12){
    object go=Get(item,"gameObject");if(!(bool)Get(go,"activeSelf"))go.GetType().GetMethod("SetActive",new[]{typeof(bool)}).Invoke(go,new object[]{true});
    object tr=Get(item,"transform");if((float)Get(Get(tr,"localScale"),"x")!=0)Set(tr,"localScale",Activator.CreateInstance(TypeByName("UnityEngine.Vector3"),new object[]{0f,0f,0f}));
   }
   i++;
  }
 }catch(Exception e){Log(e);}}
 public static void ForgetList(object list){layouts.Remove(list);configuredGlobals.Remove(list);}
 static bool renderInstalled;
 static Func<int> unityFrame;static int sampledFrame=-1;
 static void InstallRowGapRender(){
  if(renderInstalled)return;var time=TypeByName("UnityEngine.Time");if(time!=null)unityFrame=(Func<int>)Delegate.CreateDelegate(typeof(Func<int>),time.GetProperty("frameCount").GetGetMethod());var canvas=TypeByName("UnityEngine.Canvas");if(canvas==null)return;
  var registry=TypeByName("UnityEngine.UI.CanvasUpdateRegistry");if(registry!=null)registry.GetProperty("instance",BindingFlags.Static|BindingFlags.Public).GetValue(null,null);
  var render=canvas.GetEvent("willRenderCanvases",BindingFlags.Static|BindingFlags.Public);
  render.AddEventHandler(null,Delegate.CreateDelegate(render.EventHandlerType,typeof(CflRuntime).GetMethod("RenderRowGaps")));renderInstalled=true;
 }
 public static bool AfterRefreshChanged(bool changed,object list){Layout layout;if(changed && layouts.TryGetValue(list,out layout)){AfterRefresh(list);layout.GapPending=true;}return changed;}
 // Run after Unity's native grid rebuild, only for lists dirtied since the last render.
 public static void RenderRowGaps(){SampleFrame();foreach(var layout in layouts.Values){
  if(!layout.GapPending){if(layout.Gap==0 || layout.Probe==null)continue;try{if(Math.Abs((float)Get(Get(layout.Probe,"anchoredPosition"),"y")-layout.ProbeY)<.01f)continue;}catch{continue;}}layout.GapPending=false;
  try{
   int rows=Math.Max(3,(layout.Count+3)/4),i=0;
   foreach(object item in (IEnumerable)Get(layout.List,"ListItems")){
    if(i>=layout.Count)break;int row=i++/4;object tr=Get(item,"transform");object position=Get(tr,"anchoredPosition"),pivot=Get(tr,"pivot");
    float y=-layout.Top-row*(layout.CellY+layout.SpacingY)-layout.CellY*(1-(float)Get(pivot,"y"));
    if(row>=rows-3)y-=layout.Gap;
    if((i-1)==(rows-3)*4){layout.Probe=tr;layout.ProbeY=y;}
    if(Math.Abs((float)Get(position,"y")-y)>.01f)Set(tr,"anchoredPosition",Vector((float)Get(position,"x"),y));
   }
  }catch(Exception e){Log(e);}
 }}
 static void ApplyLayout(Layout layout,int count){
  float x=-6f,y=12f;

  float viewportWidth=1920,viewportHeight=1080;
  if(layout.Viewport!=null){object rect=Get(layout.Viewport,"rect");float w=(float)Get(rect,"width"),h=(float)Get(rect,"height");if(w>0 && h>0){viewportWidth=w;viewportHeight=h;}}
  object position=Get(layout.Panel,"anchoredPosition");
  if(layout.Count==count && layout.ViewWidth==viewportWidth && layout.ViewHeight==viewportHeight && (float)Get(position,"x")==x && (float)Get(position,"y")==y)return;
  layout.Count=count;layout.ViewWidth=viewportWidth;layout.ViewHeight=viewportHeight;layout.Gap=count>12?36f:0f;layout.GapPending=true;
  Set(layout.Panel,"anchorMin",Vector(1,0));Set(layout.Panel,"anchorMax",Vector(1,0));Set(layout.Panel,"pivot",Vector(1,0));
  var fitterType=TypeByName("UnityEngine.UI.ContentSizeFitter");if(fitterType!=null){object fitter=Component(layout.List,fitterType);if(fitter!=null)Set(fitter,"enabled",false);}
  int columns=4,rows=Math.Max(3,(count+columns-1)/columns);
  float width=columns*layout.CellX+(columns-1)*layout.SpacingX+layout.Left+layout.Right;
  float height=rows*layout.CellY+(rows-1)*layout.SpacingY+layout.Top+layout.Bottom+layout.Gap;
  Set(layout.Grid,"enabled",true);var constraint=layout.Grid.GetType().GetProperty("constraint");constraint.SetValue(layout.Grid,Enum.ToObject(constraint.PropertyType,1),null);Set(layout.Grid,"constraintCount",columns);
  float scale=Math.Min(1f,Math.Min((viewportHeight-y-16)/height,(viewportWidth-Math.Abs(x)-16)/width));
  Set(layout.Panel,"localScale",Activator.CreateInstance(TypeByName("UnityEngine.Vector3"),new object[]{(float)Get(layout.Scale,"x")*scale,(float)Get(layout.Scale,"y")*scale,(float)Get(layout.Scale,"z")}));
  Set(layout.Panel,"sizeDelta",Vector(width,height));
  Set(layout.Panel,"anchoredPosition",Vector(x,y));
  if(layout.EmptyGrid!=null){object go=Get(layout.EmptyGrid,"gameObject");go.GetType().GetMethod("SetActive",new[]{typeof(bool)}).Invoke(go,new object[]{count<=12});}
 }
 class CombatEffect {public bool Large;}
 static readonly ConditionalWeakTable<object,CombatEffect> combatEffects=new ConditionalWeakTable<object,CombatEffect>();
 public static void PositionProductionTooltip(object tooltip){try{
  if(tooltip.GetType().FullName!="Game.UI.UITooltipComponent")return;
  object target=Get(tooltip,"mTarget");if(target==null)return;
  foreach(var layout in layouts.Values){
   object ancestor=target;for(int depth=0;ancestor!=null && depth<8;depth++,ancestor=Get(ancestor,"parent")){
    if(!Object.ReferenceEquals(ancestor,layout.Panel))continue;
    object tip=Get(tooltip,"RectTransform"),scale=Get(tip,"lossyScale");
    float sx=Math.Abs((float)Get(scale,"x")),sy=Math.Abs((float)Get(scale,"y"));if(sx<=0 || sy<=0)return;
    object rect=Get(tip,"rect");float width=(float)Get(rect,"width"),height=(float)Get(rect,"height");
    var vector=TypeByName("UnityEngine.Vector3");if(tipCorners==null){tipCorners=Array.CreateInstance(vector,4);viewCorners=Array.CreateInstance(vector,4);}
    layout.Panel.GetType().GetMethod("GetWorldCorners").Invoke(layout.Panel,new object[]{tipCorners});
    object parent=Get(tip,"parent");parent.GetType().GetMethod("GetWorldCorners").Invoke(parent,new object[]{viewCorners});
    float top=(float)Get(tipCorners.GetValue(1),"y"),ceiling=(float)Get(viewCorners.GetValue(1),"y");
    bool above=TooltipFitsAbove(top,ceiling,height*sy,8*sy);
    Set(tooltip,"mTarget",layout.Panel);
    SetEnumIfChanged(tooltip,"m_VerticalAlignment",2);
    SetEnumIfChanged(tooltip,"m_HorizontalAlignment",above?3:2);
    SetFloatIfChanged(tooltip,"m_HorizontalPadding",above?0:-width-8);
    SetFloatIfChanged(tooltip,"m_VerticalPadding",above?8:-height);return;
   }
  }
 }catch(Exception e){Log(e);}}
 static Array tipCorners,viewCorners;
 public static bool TooltipFitsAbove(float top,float ceiling,float height,float gap){return top+height+gap<=ceiling;}
 static void SetEnumIfChanged(object target,string name,int value){var field=Field(target.GetType(),name);if(Convert.ToInt32(field.GetValue(target))!=value)field.SetValue(target,Enum.ToObject(field.FieldType,value));}
 static void SetFloatIfChanged(object target,string name,float value){if((float)Get(target,name)!=value)Set(target,name,value);}
 static bool IsLargeCombatText(object attributes){return attributes!=null && combatEffects.GetValue(attributes,ReadCombatEffect).Large;}
 static CombatEffect ReadCombatEffect(object attributes){return new CombatEffect{Large=ReadCombatEffectPath(attributes)};}
 static bool ReadCombatEffectPath(object attributes){
  if(attributes==null)return false;object asset=Get(attributes,"FXAsset");if(asset==null)return false;
  string path=(string)Get(asset,"PathToAssetInBundle");if(String.IsNullOrEmpty(path))return false;
  if(path.IndexOf('\\')>=0)path=path.Replace('\\','/');
  return path.EndsWith("/fx_prfb_status_blind.prefab",StringComparison.OrdinalIgnoreCase) || path.EndsWith("/fx_prfb_status_critical.prefab",StringComparison.OrdinalIgnoreCase);
 }
 public static float CombatTextScale(float original,object attributes){return IsLargeCombatText(attributes)?original*.5f:original;}
 public static bool CombatTextEntityScale(bool original,object attributes){return original && !IsLargeCombatText(attributes);}
 // Asset integration gets a larger frame budget only during initial application preload.
 static object startupPriority;static System.Diagnostics.Stopwatch startupWatch;
 public static void BeginStartupLoad(){try{
  if(startupWatch!=null)return;
  var property=TypeByName("UnityEngine.Application").GetProperty("backgroundLoadingPriority",BindingFlags.Public|BindingFlags.Static);
  startupPriority=property.GetValue(null,null);property.SetValue(null,Enum.Parse(property.PropertyType,"High"),null);
  startupWatch=System.Diagnostics.Stopwatch.StartNew();Log("Startup preload: high loading priority enabled");
 }catch(Exception e){Log(e);}}
 public static void EndStartupLoad(){try{
  if(startupWatch==null)return;
  TypeByName("UnityEngine.Application").GetProperty("backgroundLoadingPriority",BindingFlags.Public|BindingFlags.Static).SetValue(null,startupPriority,null);
  startupWatch.Stop();Log("Startup preload finished in "+startupWatch.Elapsed.TotalSeconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" s; loading priority restored");startupWatch=null;startupPriority=null;
 }catch(Exception e){Log(e);}}

 // Fixed-size counters: no per-frame strings, collections or disk writes.
 static long frameLast,frameStart;static int frameCount,over50,over100,gcStart;static double frameMax;static int gcLast,gcStalls;
 static readonly int[] frameBuckets=new int[501];
 static void SampleFrame(){
  if(unityFrame==null)return;int current=unityFrame();if(current==sampledFrame)return;sampledFrame=current;
  long now=System.Diagnostics.Stopwatch.GetTimestamp();
  if(frameLast==0){frameLast=frameStart=now;gcLast=gcStart=GC.CollectionCount(0);return;}
  double ms=(now-frameLast)*1000.0/System.Diagnostics.Stopwatch.Frequency;frameLast=now;

  int gcNow=GC.CollectionCount(0);if(ms>100 && gcNow!=gcLast)gcStalls++;gcLast=gcNow;
  frameCount++;frameBuckets[Math.Min(500,(int)Math.Ceiling(ms))]++;frameMax=Math.Max(frameMax,ms);if(ms>50)over50++;if(ms>100)over100++;
  if((now-frameStart)/(double)System.Diagnostics.Stopwatch.Frequency<60)return;
  int accumulated=0,p95=0;for(;p95<500;p95++){accumulated+=frameBuckets[p95];if(accumulated>=Math.Ceiling(frameCount*.95))break;}
  Log("Render intervals (includes menus/loading/background): samples="+frameCount+" p95_ms="+p95+" max_ms="+frameMax.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+" over50="+over50+" over100="+over100+" gc0="+(GC.CollectionCount(0)-gcStart)+" over100_with_gc="+gcStalls);
  Array.Clear(frameBuckets,0,frameBuckets.Length);frameCount=over50=over100=gcStalls=0;frameMax=0;frameStart=now;gcStart=GC.CollectionCount(0);
 }

 static void Log(object message){var debug=TypeByName("UnityEngine.Debug");if(debug!=null)debug.GetMethod("Log",new[]{typeof(object)}).Invoke(null,new[]{(object)("[CFL All Units] "+message)});}
}
