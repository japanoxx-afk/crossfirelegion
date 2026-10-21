using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

public static partial class CflRuntime {
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
