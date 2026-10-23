using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
public static partial class CflRuntime {
 static bool replayActive,replayUiHidden,replayUDown,replayODown,replayControlsHidden,replayDefaultViewSet;
 static object replayControlData;static bool replayControlsOriginal;
 public static void ReplayControlsUpdated(object data){
  if(!replayActive)return;
  try{
   replayControlData=data;
   if(!replayDefaultViewSet){
    object source=Get(UI(),"ReplayDataSource");if(Convert.ToInt32(Get(Get(source,"AvailableViews"),"Count"))==0)return;
    if(!(bool)Get(data,"mSpectatorModeEnabled"))Call(data,"ToggleFogOfWar",new object[]{null});
    replayDefaultViewSet=true;ReplayEvent("default_view=full_map");
   }
   if(replayControlsHidden)Set(Get(data,"ReplayControlsVisible"),"Value",false);
  }catch(Exception e){if(!replayDefaultViewSet){replayDefaultViewSet=true;ReplayEvent("default_view_failed "+e.GetBaseException().Message);}}
 }
 public static void ToggleReplayControls(){
  if(!replayActive||replayControlData==null)return;
  object visible=Get(replayControlData,"ReplayControlsVisible");
  if(!replayControlsHidden){replayControlsOriginal=(bool)Get(visible,"Value");Set(visible,"Value",false);replayControlsHidden=true;}
  else {Set(visible,"Value",replayControlsOriginal);replayControlsHidden=false;}
  ReplayEvent("controls_hidden="+replayControlsHidden);
 }
 static void ResetReplayControls(){if(replayControlsHidden&&replayControlData!=null)try{Set(Get(replayControlData,"ReplayControlsVisible"),"Value",replayControlsOriginal);}catch{}replayControlData=null;replayControlsHidden=false;replayDefaultViewSet=false;replayODown=false;}
 static object replayController,replayPanel;
 static long replayUiNext,replayUiErrorNext;static int replayUiFrame=-1;
 static readonly List<object> replayLabels=new List<object>();
 static readonly List<object[]> replayRows=new List<object[]>();
 static readonly Dictionary<object,bool> replayCanvases=new Dictionary<object,bool>();
 static readonly Dictionary<object,bool> replayRenderers=new Dictionary<object,bool>();
 static readonly List<WeakReference> replayVisuals=new List<WeakReference>();
 static System.Runtime.CompilerServices.ConditionalWeakTable<object,object> replayVisualKnown=new System.Runtime.CompilerServices.ConditionalWeakTable<object,object>();
 static readonly object[] replayKey={null};static MethodInfo replayKeyMethod;
 public static bool ReplayActive(){return replayActive;}
 static int replayMutedSpeech;
 static string replayAuditFrame;static long replayAuditAfter;
 public static bool ReplayMuteAcknowledgement(){if(!replayActive)return false;replayMutedSpeech++;return true;}
 public static bool ReplayHideVisuals(){return replayActive&&replayUiHidden;}
 public static void ReplayCombatVisual(object fx,object binding){if(!replayActive||fx==null)return;try{string path=(string)Get(Get(binding,"FXAsset"),"PathToAssetInBundle");if(path!=null&&path.Replace('\\','/').IndexOf("/fx_prfb_status_",StringComparison.OrdinalIgnoreCase)>=0)TrackReplayVisual(fx);}catch{}}
 static void TrackReplayVisual(object fx){object marker;if(!replayVisualKnown.TryGetValue(fx,out marker)){replayVisualKnown.Add(fx,new object());replayVisuals.Add(new WeakReference(fx));if(replayVisuals.Count%512==0)replayVisuals.RemoveAll(w=>!w.IsAlive);}if(replayUiHidden)HideReplayRenderers(fx);}
 public static void ReplaySelectionVisual(object handler){if(!replayActive)return;try{object fx=Get(handler,"FXInstance");if(fx!=null)TrackReplayVisual(fx);}catch{}}
 static void HideReplayRenderers(object fx){foreach(object renderer in (IEnumerable)fx.GetType().GetMethod("GetComponentsInChildren",new[]{typeof(Type),typeof(bool)}).Invoke(fx,new object[]{TypeByName("UnityEngine.Renderer"),true})){if(replayRenderers.ContainsKey(renderer))continue;replayRenderers[renderer]=(bool)Get(renderer,"forceRenderingOff");Set(renderer,"forceRenderingOff",true);}}
 static object PresComponent(object entity,string name){
  Type world=TypeByName("Carbon.Presentation.Pres"),component=TypeByName(name).MakeGenericType(world);
  object id=component.GetProperty("ComponentID",BindingFlags.Static|BindingFlags.Public|BindingFlags.FlattenHierarchy).GetValue(null,null);
  foreach(var m in TypeByName("Carbon.Core.Entities.EntityExtensions").GetMethods(BindingFlags.Static|BindingFlags.Public))if(m.Name=="GetComponent"&&m.IsGenericMethodDefinition&&m.GetGenericArguments().Length==2&&m.GetParameters().Length==2)return m.MakeGenericMethod(world,component).Invoke(null,new[]{entity,id});
  throw new MissingMethodException("EntityExtensions.GetComponent");
 }
 static object CreateReplayText(string name,object parent,int y,int height,int size){
  Type go=TypeByName("UnityEngine.GameObject"),rect=TypeByName("UnityEngine.RectTransform"),text=TypeByName("UnityEngine.UI.Text");
  object obj=Activator.CreateInstance(go,new object[]{name,new Type[]{rect,text}}),tr=Get(obj,"transform");
  tr.GetType().GetMethod("SetParent",new[]{TypeByName("UnityEngine.Transform"),typeof(bool)}).Invoke(tr,new[]{parent,(object)false});
  Set(tr,"anchorMin",Vector(1,1));Set(tr,"anchorMax",Vector(1,1));Set(tr,"pivot",Vector(1,1));Set(tr,"anchoredPosition",Vector(-20,y));Set(tr,"sizeDelta",Vector(500,height));
  object label=Component(obj,text);object font=TypeByName("UnityEngine.Resources").GetMethod("GetBuiltinResource",new[]{typeof(Type),typeof(string)}).Invoke(null,new object[]{TypeByName("UnityEngine.Font"),"Arial.ttf"});
  Set(label,"font",font);Set(label,"fontSize",size);SetEnumIfChanged(label,"alignment",2);Set(label,"raycastTarget",false);Set(label,"supportRichText",true);
  object outline=obj.GetType().GetMethod("AddComponent",new[]{typeof(Type)}).Invoke(obj,new object[]{TypeByName("UnityEngine.UI.Outline")});Set(outline,"effectDistance",Vector(1,-1));return label;
 }
 static string ReplayFaction(ulong id){return id==2291117226158273949UL?"블랙리스트":id==2290325998277348315UL?"글로벌 리스크":id==2290326114116378302UL?"뉴 호라이즌":"진영 "+id;}
 static string EscapeReplayText(string text){return (text??"").Replace("<","〈").Replace(">","〉");}
 static void BuildReplayResources(){
  if(timerHud==null)return;object ui=UI(),views=Get(Get(ui,"ReplayDataSource"),"AvailableViews");if(Convert.ToInt32(Get(views,"Count"))==0)return;
  object attributes=Get(Get(ui,"HUDResourceDataProvider"),"GameAttributes"),header=Get(Get(Get(replayController,"mReplayService"),"SimReplay"),"Header");
  var inventories=new List<object>();foreach(object reference in (IEnumerable)Get(attributes,"PrimaryResourceInventories")){object inv=Resolve(reference);if(!String.IsNullOrEmpty((string)Get(inv,"DisplayFormat")))inventories.Add(inv);}inventories.Add(Resolve(Get(attributes,"PrimaryPopulationInventory")));
  object parent=Get(timerHud,"transform");replayPanel=CreateReplayText("CFL Replay Resources",parent,-48,28,18);Set(replayPanel,"text","플레이어 · 자원 / 연료 / 인구수   [U: UI 숨김]");
  int index=0;foreach(object entity in (IEnumerable)views){
   object commander=PresComponent(entity,"Carbon.Simulation.Commander`1");int slot=Convert.ToInt32(Get(commander,"SlotID"));object loadout=null;foreach(object item in (IEnumerable)Get(header,"PlayerLoadouts"))if(Convert.ToInt32(Get(item,"SlotID"))==slot){loadout=item;break;}
   if(loadout==null)continue;var row=new List<object>();row.Add(entity);foreach(object inv in inventories)row.Add(Activator.CreateInstance(TypeByName("Game.UI.ResourceData"),new[]{entity,inv}));
   object label=CreateReplayText("CFL Replay Player "+slot,parent,-78-index*48,46,20);object color=Call(Get(ui,"ColorProvider"),"GetTeamColor",slot);Set(label,"color",color);
   string title=EscapeReplayText((string)Get(commander,"PlayerName"))+" · "+ReplayFaction(SuidBits(Get(Get(loadout,"Roster"),"Faction")));row.Add(title);replayRows.Add(row.ToArray());replayLabels.Add(label);index++;
  }
  ReplayEvent("resource_panel_ready players="+replayRows.Count+"; native presentation inventory values");
 }
 public static void ReplayInputTick(object input){
  if(!replayActive)return;
  try{
   int frame=Convert.ToInt32(Get(input,"mLastFrameTicked"));if(frame==replayUiFrame)return;replayUiFrame=frame;
   if(replayKeyMethod==null){replayKeyMethod=input.GetType().GetMethod("IsKeyDown");replayKey[0]=Enum.ToObject(replayKeyMethod.GetParameters()[0].ParameterType,117);}
   bool down=(bool)replayKeyMethod.Invoke(input,replayKey);bool locked=(bool)Get(input,"mKeyboardLock")||Convert.ToInt32(Get(Get(input,"mLockingKeyboardObjects"),"Count"))>0;
   if(down&&!replayUDown&&!locked)ToggleReplayUI();replayUDown=down;
   replayKey[0]=Enum.ToObject(replayKeyMethod.GetParameters()[0].ParameterType,111);bool oDown=(bool)replayKeyMethod.Invoke(input,replayKey);replayKey[0]=Enum.ToObject(replayKeyMethod.GetParameters()[0].ParameterType,117);
   if(oDown&&!replayODown&&!locked)ToggleReplayControls();replayODown=oDown;
   if(ReplayTryRetry())return;
   long now=Stopwatch.GetTimestamp();if(now<replayUiNext)return;replayUiNext=now+Stopwatch.Frequency/4;
   if(replayPanel==null)BuildReplayResources();
   for(int i=0;i<replayRows.Count;i++){
    object[] row=replayRows[i];var values=new List<string>();for(int j=1;j<row.Length-1;j++)values.Add((string)Get(row[j],"Display"));
    string value=(string)row[row.Length-1]+"\n"+String.Join("   /   ",values);if((string)Get(replayLabels[i],"text")!=value)Set(replayLabels[i],"text",value);
   }
   AuditPausedReplayResources(now);
  }catch(Exception e){replayUiNext=Stopwatch.GetTimestamp()+Stopwatch.Frequency*5;if(Stopwatch.GetTimestamp()>=replayUiErrorNext){replayUiErrorNext=replayUiNext;ReplayEvent("presentation_diagnostic_failed "+e.GetBaseException().Message);}}
 }
 static void AuditPausedReplayResources(long now){
  if(replayRows.Count==0||!(bool)Get(replayController,"Paused")){replayAuditFrame=null;return;}
  string frame=Convert.ToString(Get(Get(replayController,"mLastProcessedFrame"),"FrameNumber"));if(frame!=replayAuditFrame){replayAuditFrame=frame;replayAuditAfter=now+Stopwatch.Frequency;return;}if(now<replayAuditAfter)return;replayAuditAfter=Int64.MaxValue;
  object map=Get(Get(Get(UI(),"ReplayDataSource"),"mReplayControlService"),"mEntityMap");var item=map.GetType().GetProperty("Item",new[]{TypeByName("Carbon.Core.Entities.Entity`1").MakeGenericType(TypeByName("Carbon.Presentation.Pres"))});
  var getInventory=TypeByName("Carbon.Simulation.StoreHelper`1").MakeGenericType(TypeByName("Carbon.Simulation.Sim")).GetMethod("GetInventoryForEntity");
  foreach(object[] row in replayRows){object entity=item.GetValue(map,new[]{row[0]});for(int j=1;j<row.Length-1;j++){object attributes=Get(row[j],"mInventoryAttributes"),inventory=getInventory.Invoke(null,new[]{entity,attributes});string expected=String.Format((string)Get(attributes,"DisplayFormat"),Get(inventory,"CurrentAmount"),Get(inventory,"AbsoluteCap")),shown=(string)Get(row[j],"Display");ReplayEvent("resource_audit paused_frame="+frame+" player="+row[row.Length-1]+" inventory="+Get(attributes,"Name")+" sim="+expected+" displayed="+shown+" equal="+(expected==shown));}}
 }
 public static void ToggleReplayUI(){
  replayUiHidden=!replayUiHidden;
  if(replayUiHidden){Type canvas=TypeByName("UnityEngine.Canvas");foreach(object item in (IEnumerable)TypeByName("UnityEngine.Object").GetMethod("FindObjectsOfType",new[]{typeof(Type)}).Invoke(null,new object[]{canvas})){replayCanvases[item]=(bool)Get(item,"enabled");Set(item,"enabled",false);}for(int i=replayVisuals.Count-1;i>=0;i--){object fx=replayVisuals[i].Target;if(fx==null)replayVisuals.RemoveAt(i);else try{HideReplayRenderers(fx);}catch{}}}
  else RestoreReplayUI();ReplayEvent("ui_hidden="+replayUiHidden);
 }
 static void RestoreReplayUI(){foreach(var item in replayCanvases)try{Set(item.Key,"enabled",item.Value);}catch{}foreach(var item in replayRenderers)try{Set(item.Key,"forceRenderingOff",item.Value);}catch{}replayRenderers.Clear();replayCanvases.Clear();replayUiHidden=false;}
 static void EndReplayPresentation(){if(replayActive)ReplayEvent("presentation_end muted_acknowledgements="+replayMutedSpeech);replayMutedSpeech=0;replayAuditFrame=null;RestoreReplayUI();ResetReplayControls();replayActive=false;replayUDown=false;replayUiFrame=-1;replayUiNext=0;var obj=TypeByName("UnityEngine.Object");foreach(object label in replayLabels)try{obj.GetMethod("Destroy",new[]{obj}).Invoke(null,new[]{Get(label,"gameObject")});}catch{}if(replayPanel!=null)try{obj.GetMethod("Destroy",new[]{obj}).Invoke(null,new[]{Get(replayPanel,"gameObject")});}catch{}replayPanel=null;replayRows.Clear();replayLabels.Clear();replayController=null;replayVisuals.Clear();replayVisualKnown=new System.Runtime.CompilerServices.ConditionalWeakTable<object,object>();}
}
