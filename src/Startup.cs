using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Collections;
using System.Runtime.CompilerServices;

public static partial class CflRuntime {
 sealed class LocalInventoryCatalog {public readonly Dictionary<int,object> ByID=new Dictionary<int,object>();public readonly Dictionary<string,object> ByAttribute=new Dictionary<string,object>(StringComparer.Ordinal);}
 static readonly ConditionalWeakTable<object,LocalInventoryCatalog> localInventoryCatalogs=new ConditionalWeakTable<object,LocalInventoryCatalog>();
 public static int RegisterLocalInventoryMetadata(object catalog,IEnumerable attributes,Type itemType){
  var mapping=localInventoryCatalogs.GetValue(catalog,key=>new LocalInventoryCatalog());
  foreach(object attribute in attributes){int id=Convert.ToInt32(Get(attribute,"MarketItemID"));if(id<=0 || mapping.ByID.ContainsKey(id))continue;string attributeID=Get(attribute,"ID").ToString();object item=Activator.CreateInstance(itemType,new object[]{id});Set(item,"AttributeID",attributeID);Set(item,"Name",attributeID);bool currency=false;for(Type type=attribute.GetType();type!=null;type=type.BaseType)if(type.Name=="ProfileCurrencyItemAttributes")currency=true;SetEnumIfChanged(item,"ItemType",currency?2:0);mapping.ByID.Add(id,item);mapping.ByAttribute[attributeID]=item;}
  return mapping.ByID.Count;
 }
 public static object LocalInventoryItem(object original,object catalog,object key){
  if(original!=null)return original;LocalInventoryCatalog mapping;if(catalog==null || key==null || !localInventoryCatalogs.TryGetValue(catalog,out mapping))return null;object item;
  if(key is string)return mapping.ByAttribute.TryGetValue((string)key,out item)?item:null;
  return mapping.ByID.TryGetValue(Convert.ToInt32(Get(key,"ID")),out item)?item:null;
 }
 public static object LocalInventoryCurrency(object original,object catalog,object key){if(original!=null)return original;object item=LocalInventoryItem(null,catalog,key);return item!=null && Convert.ToInt32(Get(item,"ItemType"))==2?item:null;}
 public static bool SkipStartupMarket(object service){
  if(service==null)throw new ArgumentNullException("service");
  var type=service.GetType();
  if(type.FullName!="Systems.Online.Steam.Market.SteamMarketService")return Convert.ToBoolean(type.GetMethod("GetMarketCatalog").Invoke(service,null));
  var catalogType=type.Assembly.GetType("Systems.Online.Steam.Market.SteamMarketCatalog",true);
  var callback=(Delegate)Get(service,"OnMarketCatalogRetrieved");
  object catalog=Activator.CreateInstance(catalogType,new object[]{String.Empty});
  object database=null;try{database=Get(service,"AttributeDatabase");}catch(MissingMemberException){}
  int mapped=0;
  if(database!=null){
   var online=System.Reflection.Assembly.Load(Array.Find(type.Assembly.GetReferencedAssemblies(),reference=>reference.Name=="Carbon.Online"));
   Type attributes=online.GetType("Carbon.Online.Inventory.ProfileItemAttributes",true);
   var query=Array.Find(type.GetProperty("AttributeDatabase").PropertyType.GetMethods(),method=>method.Name=="GetAll" && method.IsGenericMethodDefinition && method.GetParameters().Length==1).MakeGenericMethod(attributes);
   mapped=RegisterLocalInventoryMetadata(catalog,(IEnumerable)query.Invoke(database,new object[]{true}),type.Assembly.GetType("Systems.Online.Steam.Market.SteamMarketItem",true));
  }
  Set(service,"mMarketCatalog",catalog);
  Log("Startup market query skipped; shop list empty; local inventory ID mappings="+mapped+"; inventory/profile flow continues");
  if(callback!=null)callback.DynamicInvoke(new[]{catalog});
  return true;
 }
 sealed class SceneLoadRecord {public bool Complete;public string Signature;}
 static readonly ConditionalWeakTable<object,SceneLoadRecord> startupScenes=new ConditionalWeakTable<object,SceneLoadRecord>();
 sealed class OnceSceneEnumerator:IEnumerator,IDisposable {
  readonly IEnumerator source;readonly SceneLoadRecord record;readonly string signature,label;bool started,finished;
  public OnceSceneEnumerator(IEnumerator source,SceneLoadRecord record,string signature,object owner){this.source=source;this.record=record;this.signature=signature;label="SceneLoader/"+RuntimeHelpers.GetHashCode(owner);}
  public object Current{get{return source.Current;}}
  public bool MoveNext(){if(finished)return false;if(!started){started=true;StartupStepBegin(label);}bool more;try{more=source.MoveNext();}catch{finished=true;StartupStepEnd(label);Log("Startup scene loader failed; completion was not cached: "+label);throw;}if(!more){finished=true;record.Signature=signature;record.Complete=true;StartupStepEnd(label);}return more;}
  public void Reset(){throw new NotSupportedException();}
  public void Dispose(){if(started && !finished){finished=true;StartupStepEnd(label);Log("Startup scene loader disposed before completion: "+label);}var disposable=source as IDisposable;if(disposable!=null)disposable.Dispose();}
 }
 static IEnumerator EmptySceneLoad(){yield break;}
 public static IEnumerator LoadStartupScenesOnce(IEnumerator original,object loader){
  if(original==null || loader==null || startupSession==null || startupComplete)return original;
  try{
   var names=(IEnumerable)Get(loader,"m_SceneNames");var parts=new List<string>();foreach(object name in names)parts.Add(Convert.ToString(name));string signature=String.Join("\0",parts.ToArray());
   var record=startupScenes.GetValue(loader,key=>new SceneLoadRecord());
   if(record.Complete && record.Signature==signature){Log("Startup completed scene loader duplicate skipped: "+RuntimeHelpers.GetHashCode(loader));var disposable=original as IDisposable;if(disposable!=null)disposable.Dispose();return EmptySceneLoad();}
   return new OnceSceneEnumerator(original,record,signature,loader);
  }catch(Exception e){Log(e);return original;}
 }
 public static void StartupServiceBegin(object service,string phase){if(service!=null && startupSession!=null && !startupComplete)StartupStepBegin("service:"+phase+":"+service.GetType().FullName);}
 public static void StartupServiceEnd(object service,string phase){if(service!=null && startupSession!=null && !startupComplete)StartupStepEnd("service:"+phase+":"+service.GetType().FullName);}
 public static void CatalogTimeoutPolicy(){Log("Startup market catalog timeout=15s (native default 90s); native failure fallback and Steam authentication preserved");}
 public static void CatalogRequestStatus(object request){try{int status=Convert.ToInt32(Get(request,"Status"));if(status==3 || status==4)Log("Startup market catalog completion: status="+status+" (3=success, 4=failure); native catalog/fallback handler follows");}catch(Exception e){Log(e);}}
 static readonly object startupLock=new object();
 static readonly Dictionary<string,Stack<long>> startupSteps=new Dictionary<string,Stack<long>>();
 static Stopwatch startupSession;
 static bool startupComplete;
 static string StartupKey(string name){return name.StartsWith("request:",StringComparison.Ordinal)?name:System.Threading.Thread.CurrentThread.ManagedThreadId+":"+name;}
 static string StartupSeconds(double value){return value.ToString("F3",CultureInfo.InvariantCulture);}
 public static void BeginStartupSession(){try{
  lock(startupLock){if(startupSession!=null)return;startupSession=Stopwatch.StartNew();startupComplete=false;}
  double age=-1;try{using(var process=Process.GetCurrentProcess())age=(DateTime.Now-process.StartTime).TotalSeconds;}catch{}
  Log("Startup session begin; process_age_s="+StartupSeconds(age)+"; cold/warm cache state unknown");
  BeginStartupLoad();
 }catch(Exception e){Log(e);}}
 public static void StartupStepBegin(string name){try{lock(startupLock){
  if(startupSession==null || startupComplete)return;
  string key=StartupKey(name);
  Stack<long> stack;if(!startupSteps.TryGetValue(key,out stack)){stack=new Stack<long>();startupSteps.Add(key,stack);}
  stack.Push(Stopwatch.GetTimestamp());Log("Startup begin: "+name+" elapsed_s="+StartupSeconds(startupSession.Elapsed.TotalSeconds));
 }}catch(Exception e){Log(e);}}
 public static void StartupStepEnd(string name){try{lock(startupLock){
  if(startupSession==null || startupComplete)return;
  Stack<long> stack;string key=StartupKey(name);
  if(!startupSteps.TryGetValue(key,out stack) || stack.Count==0)return;
  double seconds=(Stopwatch.GetTimestamp()-stack.Pop())/(double)Stopwatch.Frequency;
  Log("Startup end: "+name+" duration_s="+StartupSeconds(seconds)+" elapsed_s="+StartupSeconds(startupSession.Elapsed.TotalSeconds));
 }}catch(Exception e){Log(e);}}
 public static void StartupAuthResult(object state,int request,bool success){try{
  if(startupSession==null || startupComplete)return;
  if(Convert.ToInt32(Get(state,"mPlayerServiceRequestId"))!=request)return;
  if(Convert.ToBoolean(Get(state,"mPendingProfileRefresh")))StartupStepEnd("request:RefreshPlayerProfile");
  else if(Convert.ToBoolean(Get(state,"mPendingStatsRefresh")))StartupStepEnd("request:RetrievePlayerStats");
  else if(Convert.ToBoolean(Get(state,"mPendingMatchHistoryRefresh")))StartupStepEnd("request:RetrieveMatchHistory");
  Log("Startup profile callback: success="+success+" profile_pending="+Get(state,"mPendingProfileRefresh")+" stats_pending="+Get(state,"mPendingStatsRefresh")+" history_pending="+Get(state,"mPendingMatchHistoryRefresh"));
 }catch(Exception e){Log(e);}}
 public static void StartupAuthenticationStatus(int status){try{
  if(startupSession==null || startupComplete)return;
  Log("Startup authentication status="+status);
  if(status==3 || status==4)StartupStepEnd("request:AuthenticateSession");
 }catch(Exception e){Log(e);}}
 public static void StartupMenuReady(){try{
  lock(startupLock){if(startupSession==null || startupComplete)return;
   Log("Startup main-menu flow ready: elapsed_s="+StartupSeconds(startupSession.Elapsed.TotalSeconds)+"; logical transition, visual readiness not measured");
   foreach(var step in startupSteps)if(step.Value.Count>0)Log("Startup unfinished scope: "+step.Key);
   startupComplete=true;startupSteps.Clear();
  }
  EndStartupLoad();
 }catch(Exception e){Log(e);}}
}
