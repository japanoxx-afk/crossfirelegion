using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Linq.Expressions;
[assembly:AssemblyVersion("0.2.0.0")]
public static partial class CflRuntime {
 public const int Slots=64;
 static readonly BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 class Access {public PropertyInfo Property;public FieldInfo Field;public MethodInfo Setter;public Func<object,object> Reader;}
 static readonly ConcurrentDictionary<Type,ConcurrentDictionary<string,Access>> members=new ConcurrentDictionary<Type,ConcurrentDictionary<string,Access>>();
 static readonly ConcurrentDictionary<string,Type> types=new ConcurrentDictionary<string,Type>();
 static readonly ConcurrentDictionary<Type,Func<object,object>> conversions=new ConcurrentDictionary<Type,Func<object,object>>();
 static Access Member(Type type,string name){
  var map=members.GetOrAdd(type,t=>new ConcurrentDictionary<string,Access>());Access found;if(map.TryGetValue(name,out found))return found;
  var result=new Access();
  for(Type t=type;t!=null;t=t.BaseType){
   var p=t.GetProperty(name,Flags|BindingFlags.DeclaredOnly);if(p!=null){if(result.Property==null)result.Property=p;if(result.Setter==null)result.Setter=p.GetSetMethod(true);}
   var f=t.GetField(name,Flags|BindingFlags.DeclaredOnly);if(f!=null && result.Field==null)result.Field=f;
  }
  if(result.Property==null && result.Field==null)throw new MissingMemberException(type.FullName,name);
  try{var arg=Expression.Parameter(typeof(object),"instance");Expression read=result.Property!=null?(Expression)Expression.Property(Expression.Convert(arg,result.Property.DeclaringType),result.Property):Expression.Field(Expression.Convert(arg,result.Field.DeclaringType),result.Field);result.Reader=Expression.Lambda<Func<object,object>>(Expression.Convert(read,typeof(object)),arg).Compile();}catch{result.Reader=value=>result.Property!=null?result.Property.GetValue(value,null):result.Field.GetValue(value);}
  return map.GetOrAdd(name,result);
 }
 static object Get(object o,string name){var a=Member(o.GetType(),name);return a.Reader(o);}
 static FieldInfo Field(Type t,string name){var field=Member(t,name).Field;if(field==null)throw new MissingFieldException(name);return field;}
 static void Set(object o,string name,object v){var a=Member(o.GetType(),name);if(a.Setter!=null)a.Setter.Invoke(o,new[]{v});else if(a.Field!=null)a.Field.SetValue(o,v);else throw new MissingMethodException("No setter for "+name);}
 static object Resolve(object reference){return conversions.GetOrAdd(reference.GetType(),BuildConversion)(reference);}
 static Func<object,object> BuildConversion(Type type){var method=type.GetMethod("op_Implicit",BindingFlags.Static|BindingFlags.Public);try{var arg=Expression.Parameter(typeof(object),"reference");return Expression.Lambda<Func<object,object>>(Expression.Convert(Expression.Call(method,Expression.Convert(arg,type)),typeof(object)),arg).Compile();}catch{return value=>method.Invoke(null,new[]{value});}}
 class PackBuffers {public readonly HashSet<object> Seen=new HashSet<object>();public readonly List<object> Deferred=new List<object>(Slots);}
 static readonly ConditionalWeakTable<object,PackBuffers> packBuffers=new ConditionalWeakTable<object,PackBuffers>();
 static readonly string[] productionFields={"mProductionButtonScratchpad","mUpgradeButtonScratchpad"};
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
  int next=0,extent=0;var buffer=packBuffers.GetOrCreateValue(selection);var seen=buffer.Seen;var deferred=buffer.Deferred;seen.Clear();deferred.Clear();
  try{
  foreach(object entry in (IEnumerable)Field(selection.GetType(),"mAbilityButtonScratchpad").GetValue(selection)){
   object button=Resolve(Get(entry,"Button"));if(button==null || !seen.Add(Get(button,"ID")))continue;
   int slot=(int)Get(entry,"Index");
   if(slot>=0 && slot<buttons.Length && buttons.GetValue(slot)==null){buttons.SetValue(button,slot);extent=Math.Max(extent,slot+1);}else deferred.Add(button);
  }
  foreach(string field in productionFields){
   foreach(object entry in (IEnumerable)Field(selection.GetType(),field).GetValue(selection)){
    object button=Resolve(Get(entry,"Button"));if(button==null || !seen.Add(Get(button,"ID")))continue;
    deferred.Add(button);
   }
  }
  foreach(object button in deferred){while(next<buttons.Length && buttons.GetValue(next)!=null)next++;if(next>=buttons.Length)throw new InvalidOperationException("CFL command grid capacity exceeded");buttons.SetValue(button,next);extent=Math.Max(extent,++next);}
  }finally{seen.Clear();deferred.Clear();}
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
 public static void RenderRowGaps(){SampleFrame();UpdateMatchTimer();foreach(var layout in layouts.Values){
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
 static object timerHud,timerObject,timerLabel;static long timerNext;static long timerSecond=-1;static double timerRate;static PropertyInfo timerFrameProperty;
 public static string MatchTimeText(long seconds){seconds=Math.Max(0,seconds);return seconds>=3600?String.Format(System.Globalization.CultureInfo.InvariantCulture,"{0:00}:{1:00}:{2:00}",seconds/3600,(seconds/60)%60,seconds%60):String.Format(System.Globalization.CultureInfo.InvariantCulture,"{0:00}:{1:00}",seconds/60,seconds%60);}
 public static void BeginMatchTimer(object hud){try{
  EndMatchTimer(null);timerHud=hud;timerSecond=-1;timerRate=0;timerNext=0;
  var go=TypeByName("UnityEngine.GameObject");var rect=TypeByName("UnityEngine.RectTransform");var text=TypeByName("UnityEngine.UI.Text");
  timerObject=Activator.CreateInstance(go,new object[]{"CFL Match Timer",new Type[]{rect,text}});
  object tr=Get(timerObject,"transform");tr.GetType().GetMethod("SetParent",new[]{TypeByName("UnityEngine.Transform"),typeof(bool)}).Invoke(tr,new object[]{Get(hud,"transform"),false});
  Set(tr,"anchorMin",Vector(.5f,1));Set(tr,"anchorMax",Vector(.5f,1));Set(tr,"pivot",Vector(.5f,1));Set(tr,"anchoredPosition",Vector(0,-8));Set(tr,"sizeDelta",Vector(160,36));
  timerLabel=Component(timerObject,text);var resources=TypeByName("UnityEngine.Resources");
  var font=resources.GetMethod("GetBuiltinResource",new[]{typeof(Type),typeof(string)}).Invoke(null,new object[]{TypeByName("UnityEngine.Font"),"Arial.ttf"});
  Set(timerLabel,"font",font);Set(timerLabel,"fontSize",26);SetEnumIfChanged(timerLabel,"fontStyle",1);SetEnumIfChanged(timerLabel,"alignment",1);Set(timerLabel,"raycastTarget",false);Set(timerLabel,"text","00:00");
  var outline=timerObject.GetType().GetMethod("AddComponent",new[]{typeof(Type)}).Invoke(timerObject,new object[]{TypeByName("UnityEngine.UI.Outline")});Set(outline,"effectDistance",Vector(1,-1));
  timerFrameProperty=TypeByName("Carbon.Simulation.IFrameController").GetProperty("LastProcessedSimFrame");InstallRowGapRender();
 }catch(Exception e){Log("Match timer initialization: "+e);EndMatchTimer(null);}}
 public static void EndMatchTimer(object hud){
  if(hud!=null && !Object.ReferenceEquals(timerHud,hud))return;
  if(timerObject!=null){var obj=TypeByName("UnityEngine.Object");obj.GetMethod("Destroy",new[]{obj}).Invoke(null,new[]{timerObject});}
  timerHud=timerObject=timerLabel=null;timerRate=0;timerSecond=-1;
 }
 static void UpdateMatchTimer(){
  if(timerLabel==null)return;long now=System.Diagnostics.Stopwatch.GetTimestamp();if(now<timerNext)return;timerNext=now+System.Diagnostics.Stopwatch.Frequency/4;
  try{
   object controller=Get(timerHud,"mSimController");if(controller==null)return;
   if(timerRate<=0){object ticks=TypeByName("Carbon.Simulation.IFrameController").GetProperty("TicksPerSecond").GetValue(controller,null);timerRate=(double)ticks.GetType().GetMethod("UnsafeDoubleValue",BindingFlags.Static|BindingFlags.Public).Invoke(null,new[]{ticks});if(timerRate<=0)return;}
   object frame=timerFrameProperty.GetValue(controller,null);long number=Convert.ToInt64(Get(frame,"FrameNumber"));long seconds=(long)(Math.Max(0,number-1)/timerRate);
   if(seconds==timerSecond)return;timerSecond=seconds;Set(timerLabel,"text",MatchTimeText(seconds));
  }catch(Exception e){Log("Match timer: "+e);EndMatchTimer(null);}
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
 static void SetEnumIfChanged(object target,string name,int value){var access=Member(target.GetType(),name);if(Convert.ToInt32(Get(target,name))!=value)Set(target,name,Enum.ToObject(access.Property!=null?access.Property.PropertyType:access.Field.FieldType,value));}
 static void SetFloatIfChanged(object target,string name,float value){if((float)Get(target,name)!=value)Set(target,name,value);}
 static bool IsLargeCombatText(object attributes){return attributes!=null && combatEffects.GetValue(attributes,ReadCombatEffect).Large;}
 static CombatEffect ReadCombatEffect(object attributes){return new CombatEffect{Large=ReadCombatEffectPath(attributes)};}
 static bool ReadCombatEffectPath(object attributes){
  if(attributes==null)return false;object asset=Get(attributes,"FXAsset");if(asset==null)return false;
  string path=(string)Get(asset,"PathToAssetInBundle");if(String.IsNullOrEmpty(path))return false;
  if(path.IndexOf('\\')>=0)path=path.Replace('\\','/');
  return path.EndsWith("/fx_prfb_status_blind.prefab",StringComparison.OrdinalIgnoreCase) || path.EndsWith("/fx_prfb_status_critical.prefab",StringComparison.OrdinalIgnoreCase);
 }
 public static float CombatTextScale(float original,object attributes){return IsLargeCombatText(attributes)?original*.25f:original;}
 public static float CombatTextAdditionalScale(float original,object attributes){return IsLargeCombatText(attributes)?1f:original;}
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
 static long frameLast,frameStart;static int frameCount,over50,over100,gcStart;static double frameMax;static int gcLast,gcStalls;static long lastSpikeLog;static Func<bool> applicationFocused;
 static readonly int[] frameBuckets=new int[501];
 static void SampleFrame(){
  if(unityFrame==null)return;int current=unityFrame();if(current==sampledFrame)return;sampledFrame=current;
  long now=System.Diagnostics.Stopwatch.GetTimestamp();
  if(frameLast==0){frameLast=frameStart=now;gcLast=gcStart=GC.CollectionCount(0);
   try{var app=TypeByName("UnityEngine.Application");applicationFocused=(Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),app.GetProperty("isFocused").GetGetMethod());var gc=TypeByName("UnityEngine.Scripting.GarbageCollector");Log("Diagnostics v0.2.18: incrementalGC="+gc.GetProperty("isIncremental").GetValue(null,null)+" managedMB="+(GC.GetTotalMemory(false)/1048576));}catch(Exception e){Log("Diagnostics capability: "+e.GetType().Name);}return;}
  double ms=(now-frameLast)*1000.0/System.Diagnostics.Stopwatch.Frequency;frameLast=now;

  int gcNow=GC.CollectionCount(0);if(ms>100 && gcNow!=gcLast)gcStalls++;
  if(ms>100 && (now-lastSpikeLog)/(double)System.Diagnostics.Stopwatch.Frequency>=1){lastSpikeLog=now;Log("Frame stall: ms="+ms.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+" gcDelta="+(gcNow-gcLast)+" managedMB="+(GC.GetTotalMemory(false)/1048576)+" focused="+(applicationFocused==null?"unknown":applicationFocused().ToString()));}gcLast=gcNow;
  frameCount++;frameBuckets[Math.Min(500,(int)Math.Ceiling(ms))]++;frameMax=Math.Max(frameMax,ms);if(ms>50)over50++;if(ms>100)over100++;
  if((now-frameStart)/(double)System.Diagnostics.Stopwatch.Frequency<60)return;
  int accumulated=0,p95=0;for(;p95<500;p95++){accumulated+=frameBuckets[p95];if(accumulated>=Math.Ceiling(frameCount*.95))break;}
  Log("Render intervals (includes menus/loading/background): samples="+frameCount+" p95_ms="+p95+" max_ms="+frameMax.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+" over50="+over50+" over100="+over100+" gc0="+(GC.CollectionCount(0)-gcStart)+" over100_with_gc="+gcStalls);
  Array.Clear(frameBuckets,0,frameBuckets.Length);frameCount=over50=over100=gcStalls=0;frameMax=0;frameStart=now;gcStart=GC.CollectionCount(0);
 }

 static readonly Dictionary<ulong,string> koreanMissing=new Dictionary<ulong,string>{
{23663UL,"시야를 넓혀 주는 사이버네틱 광학 장비를 갖춘 경량 정찰 보병입니다. 뉴 호라이즌의 선봉에서 적 정보를 수집합니다."},
{23664UL,"글로벌 리스크 보병을 지원하는 다목적 전투 공병입니다. 무기로 적 보병을 교란하고 이동 속도를 늦추며, 다양한 전투 도구와 보급품을 운용합니다."},
{23665UL,"코브라"},
{23666UL,"장거리 사격에 특화된 숙련된 사수입니다. 추적 화살로 명중한 적의 위치를 잠시 드러내며, 다중 사격으로 일정 범위에 피해를 줄 수 있습니다. 단일 보병 표적을 상대하는 데 효과적입니다."},
{23667UL,"크리드"},
{23668UL,"수십 년간 복무한 베테랑 요원입니다. 군 인맥을 활용해 글로벌 리스크의 정예 병력을 호출합니다."},
{23669UL,"아키텍트"},
{23670UL,"인류의 새로운 지평을 만들기 위해 수많은 수술을 집도한 냉혹한 외과의입니다. 실험 대상을 소모품으로 여기며 그 희생을 뉴 호라이즌의 야망을 위한 필연으로 받아들입니다."},
{23671UL,"고급 보호막 노드"},
{23672UL,"주변 유닛의 보호막을 재충전하는 구조물을 생성합니다."},
{23673UL,"오버클로킹"},
{23674UL,"활성화하면 생성된 모든 유닛의 보호막을 소모하고 잠시 공격력을 높입니다."},
{23675UL,"지상 투입"},
{23676UL,"불의 고리"},
{23677UL,"다중 사격"},
{23678UL,"코브라가 지정 범위에 화살을 퍼부어 광역 피해를 줍니다."},
{23679UL,"폭파 장약"},
{23680UL,"지정 지역에 장약을 던져 {0}초 후 폭발시킵니다. 구조물에 큰 피해를 주고 폭발에 휘말린 차량을 잠시 기절시킵니다."},
{23681UL,"유닛이 기절했습니다."},
{23682UL,"신호탄"},
{23683UL,"첨단 신호탄을 발사해 넓은 지역의 시야를 확보합니다."},
{23684UL,"이동할 수 없게 되는 대신 무기 사거리가 늘어납니다. 피해량은 감소합니다."},
{23685UL,"이동할 수 있는 상태로 돌아가며 무기 사거리와 피해량이 복구됩니다."},
{23689UL,"히드라"},
{23690UL,"전선을 돌파하고 근거리에서 보병에게 큰 피해를 주는 경차량입니다."},
{23691UL,"지정 위치로 돌진하며 경로상의 유닛에게 피해를 주고 밀쳐냅니다."},
{23692UL,"경차량"},
{23701UL,"보행 형태"},
{23702UL,"히드라가 보행 형태로 전환되어 미사일로 공격할 수 있습니다."},
{23703UL,"구체 형태"},
{23704UL,"히드라가 굴러가는 구체로 변형되어 더 빠르게 이동합니다."},
{23705UL,"전격 돌진 해제"},
{23706UL,"히드라의 전격 돌진 능력을 해제합니다."},
{23707UL,"범블비"},
{23708UL,"블랙리스트의 최신 은폐 기술을 탑재한 비행선입니다. 적 구조물이나 지상 병력에 은밀하게 접근해 대량의 혼합 폭탄을 투하합니다."},
{23709UL,"코브라의 추적 표식 능력을 해제합니다. 화살에 내장된 추적기가 적 유닛의 위치를 잠시 드러냅니다."},
{23710UL,"코브라의 다중 사격 능력을 해제합니다."},
{23711UL,"소이탄"},
{23712UL,"폭발 범위가 넓고 최초 피해는 낮지만, 지면이나 구조물을 불태워 지속 피해를 줍니다."},
{23713UL,"집중 폭격"},
{23714UL,"폭발 범위가 좁은 대신 명중 시 더 큰 피해를 줍니다."},
{23715UL,"비행선 자폭"},
{23716UL,"비행선을 지정 지역에 추락시켜 탑재한 폭탄을 모두 폭발시키고 막대한 피해를 줍니다."},
{23717UL,"지정 위치에 정예 보병 부대를 신속하게 투입합니다."},
{23718UL,"지정 위치에 정예 차량 부대를 신속하게 투입합니다."},
{23719UL,"비행선 자폭 해제"},
{23720UL,"화살에 내장된 추적기가 명중한 유닛의 위치를 지도에 잠시 표시합니다."},
{23721UL,"무기 재사용 대기 중"},
{23722UL,"공격 중에는 사용할 수 없습니다."},
{23723UL,"근거리 대지 폭격기"},
{23727UL,"고정 방어 모드 해제"},
{23728UL,"범블비가 고스트 키트 장갑을 끄고 은폐를 해제합니다."},
{23729UL,"요먼의 고정 방어 모드를 해제합니다. 이동할 수 없게 되는 대신 시야와 사거리가 늘어나며 은폐 유닛을 탐지합니다."},
{23730UL,"추적 표식 해제"},
{23731UL,"고스트 키트 매복 해제"},
{23732UL,"고급 보호막 노드"},
{23733UL,"이 유닛은 고급 보호막 노드의 효과 범위 안에 있습니다."},
{23734UL,"신호탄 해제"},
{23735UL,"정찰병의 신호탄 능력을 해제합니다. 첨단 신호탄을 발사해 넓은 지역의 시야를 확보합니다."},
{23736UL,"전투 공병의 폭파 장약 능력을 해제합니다. 지정 지역에 장약을 던져 {0}초 후 폭발시킵니다."},
{23737UL,"폭파 장약 해제"},
{23738UL,"감시 모드 해제"},
{23740UL,"와일드파이어"},
{23742UL,"블랙리스트의 화염 돌격 보병으로 오랫동안 전선에서 싸운 뒤 빠르게 지휘관으로 승진했습니다."},
{23743UL,"휘발성 탄약"},
{23744UL,"지정 범위의 아군이 잠시 휘발성 탄약 효과를 얻어 적을 불태우고 지속 피해를 줍니다."},
{23745UL,"초토화"},
{23746UL,"지정 지역을 일정 시간 불태웁니다. 범위 내 적은 화상 피해를 받고 이동 속도가 감소합니다."},
{23770UL,"정예 돌격병"},
{23771UL,"정예 저격수"},
{23772UL,"정예 방패병"},
{23773UL,"정예 로켓병"},
{23774UL,"정예 팔랑크스"},
{23775UL,"정예 오버로드"},
{23776UL,"정예 센추리온"},
{23794UL,"모든 유닛 사용 가능"},
{23795UL,"전체 해제"},
{23798UL,"전투 공병의 음파 무기는 적 보병을 교란해 이동 속도를 늦춥니다."},
{23799UL,"감속"},
{23803UL,"전격 돌진"},
{23807UL,"범블비의 비행선 자폭 능력을 해제합니다. 지정 지역에 추락하여 막대한 피해를 줍니다."},
{23808UL,"대상 유닛이 일정 시간 체력을 회복시키는 오라를 얻습니다."}
 };
 public static void FillKorean(object entries,string language){if(language!="ko")return;var map=(IDictionary<ulong,string>)entries;foreach(var pair in koreanMissing){string current;if(!map.TryGetValue(pair.Key,out current) || String.IsNullOrWhiteSpace(current) || current=="#N/A" || current=="LocID:"+pair.Key)map[pair.Key]=pair.Value;}}
 static void Log(object message){var debug=TypeByName("UnityEngine.Debug");if(debug!=null)debug.GetMethod("Log",new[]{typeof(object)}).Invoke(null,new[]{(object)("[CFL All Units] "+message)});}
}
