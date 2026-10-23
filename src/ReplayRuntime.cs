using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
public static partial class CflRuntime {
 static Dictionary<string,string> replayRequest;
 static string replayFingerprint;
 static readonly object replayGate=new object();
 static readonly Dictionary<object,Dictionary<string,string>> replayRecords=new Dictionary<object,Dictionary<string,string>>();
 static string ReplayGame(){return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(CflRuntime).Assembly.Location),"..",".."));}
 static bool replayInvalid;
 static bool replayCanRetry,replayRetryPending;static string replayProfile="current",replayLauncher;static Dictionary<string,string> replayRetry;
 static void SetReplayMovement(bool original){var type=TypeByName("Carbon.Simulation.YieldHelpers");var field=type==null?null:type.GetField("CflReplayOriginalMovement",BindingFlags.Static|BindingFlags.Public);if(field!=null)field.SetValue(null,original);replayProfile=original?"original_movement":"current";}
 static string ReplayCompatibility(string file){return ReplayProtocol.Metadata(file)+".compat.ini";}
 static bool ReplayTryRetry(){if(!replayRetryPending)return false;replayRetryPending=false;RestoreReplayUI();
  // A divergent session has already initialized native shared caches. Do not reuse
  // those caches for the alternate rules: let the game exit normally, then restart.
  var retry=replayRetry;replayRetry=null;retry["Game"]=ReplayGame();retry["Launcher"]=replayLauncher;retry["ExpiresUtc"]=DateTime.UtcNow.AddMinutes(5).Ticks.ToString();ReplayProtocol.Write(Path.Combine(ReplayProtocol.Home,"play-request.ini"),retry);
  var start=new System.Diagnostics.ProcessStartInfo(replayLauncher,"--resume-replay \""+ReplayGame().TrimEnd('\\')+"\" "+System.Diagnostics.Process.GetCurrentProcess().Id){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(replayLauncher),CreateNoWindow=true};System.Diagnostics.Process.Start(start);
  ReplayEvent("compatibility_retry original movement; fresh process requested; native normal exit");Call(UI(),"ExitApplication");return true;
 }
 public static bool ReplayIsInvalid(){return replayInvalid;}
 public static void ReplayException(object failure){try{if(replayActive)ReplayEvent("SIMULATION_EXCEPTION frame="+Get(Get(replayController,"mLastProcessedFrame"),"FrameNumber")+" paused="+Get(replayController,"Paused")+" "+failure);}catch{}}
 public static void ReplaySimulationStarted(object controller){try{
  object service=Get(controller,"mReplayService"),replay=Get(service,"SimReplay");if(replay==null)return;
  EndReplayPresentation();replayActive=Convert.ToString(Get(service,"ReplayMode"))=="ReplayingGame";replayController=controller;if(!replayActive)SetReplayMovement(false);
  if(replayActive){var meta=ReplayProtocol.Read(ReplayProtocol.Metadata((string)Get(replay,"Filename")));replayCanRetry=ReplayProtocol.Value(meta,"Fingerprint")==""&&replayProfile=="current"&&File.Exists(replayLauncher);ReplayEvent("playback_profile="+replayProfile+" fallback_allowed="+replayCanRetry);}
  var contract=TypeByName("Carbon.Simulation.IFrameController");object rate=contract.GetProperty("TicksPerSecond").GetValue(controller,null);double ticks=Convert.ToDouble(rate.GetType().GetMethod("UnsafeDoubleValue",BindingFlags.Public|BindingFlags.Static).Invoke(null,new[]{rate}));
  Dictionary<string,string> info;lock(replayGate){if(replayRecords.TryGetValue(replay,out info)){info["TicksPerSecond"]=ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);ReplayProtocol.Write(ReplayProtocol.Metadata(info["File"]),info);}}
  ReplayEvent("simulation_started mode="+Get(service,"ReplayMode")+" ticks_per_second="+ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
 }catch(Exception e){ReplayEvent("simulation_start_diagnostic_failed "+e.Message);}}
 static void ReplayEvent(string message){try{Directory.CreateDirectory(ReplayProtocol.Home);lock(replayGate){File.AppendAllText(Path.Combine(ReplayProtocol.Home,"events.log"),DateTime.UtcNow.ToString("o")+" "+message+Environment.NewLine);ReplayProtocol.Write(Path.Combine(ReplayProtocol.Home,"status.ini"),new Dictionary<string,string>{{"State",message},{"Utc",DateTime.UtcNow.ToString("o")}});}Log("Replay "+message);}catch{}}
 public static void ReplayMenuReady(){
  try{
   EndReplayPresentation();
   string request=Path.Combine(ReplayProtocol.Home,"play-request.ini");
   if(File.Exists(request)){
    var data=ReplayProtocol.Read(request);long expires;
    if(Int64.TryParse(ReplayProtocol.Value(data,"ExpiresUtc"),out expires) && expires>DateTime.UtcNow.Ticks && ReplayProtocol.SamePath(ReplayProtocol.Value(data,"Game"),ReplayGame())){replayRequest=data;File.Delete(request);ReplayEvent("play_request_consumed");}
   }
   if(replayRetry!=null){replayRequest=replayRetry;replayRetry=null;}
   ThreadPool.QueueUserWorkItem(delegate{try{string hash=ReplayProtocol.Fingerprint(ReplayGame());lock(replayGate)replayFingerprint=hash;}catch(Exception e){ReplayEvent("fingerprint_failed "+e.Message);}});
  }catch(Exception e){ReplayEvent("request_failed "+e.Message);}
 }
 public static void ReplayMenuTick(){
  if(replayRequest==null)return;
  var data=replayRequest;replayRequest=null;
  try{
   string file=ReplayProtocol.Value(data,"File");
   replayLauncher=ReplayProtocol.Value(data,"Launcher");
   if(ReplayProtocol.Digest(file)!=ReplayProtocol.Value(data,"FileHash"))throw new InvalidDataException("리플레이가 실행 요청 후 변경되었습니다.");
   if(ReplayProtocol.Fingerprint(ReplayGame())!=ReplayProtocol.Value(data,"Fingerprint"))throw new InvalidDataException("게임 패치가 실행 요청 후 변경되었습니다.");
   // Validate the complete native command stream while still in the menu. A malformed
   // tail must not strand the application after the phase transition has begun.
   var replayType=TypeByName("Carbon.Simulation.Replay");object candidate=Activator.CreateInstance(replayType,new object[]{file});replayType.GetMethod("LoadFromFile").Invoke(candidate,null);
   string build=ReplayProtocol.GameBuild(ReplayGame());if(build!=""&&(string)Get(Get(candidate,"Header"),"BuildName")!=build)throw new InvalidDataException("기록과 게임의 빌드가 다릅니다. 같은 버전에서 재생하세요.");
   if(Convert.ToUInt64(Get(Get(candidate,"LastRecordedFrame"),"FrameNumber"))==0)throw new InvalidDataException("정상 시뮬레이션 틱이 없는 기록입니다.");
   replayInvalid=false;
   var compatibility=ReplayProtocol.Read(ReplayCompatibility(file));bool original=ReplayProtocol.Value(data,"LegacyMovement")=="1"||(ReplayProtocol.Value(compatibility,"FileHash")==ReplayProtocol.Digest(file)&&ReplayProtocol.Value(compatibility,"Profile")=="original_movement");SetReplayMovement(original);
   object ui=TypeByName("Game.UI.OrcaUI").GetProperty("Instance",BindingFlags.Static|BindingFlags.Public).GetValue(null,null);
   object front=Get(ui,"FrontEndDataSource");
   front.GetType().GetMethod("EnterReplayMode",new[]{typeof(string),typeof(bool)}).Invoke(front,new object[]{file,true});
   ReplayEvent("play_dispatched file="+Path.GetFileName(file));
  }catch(Exception e){ReplayEvent("play_failed "+e.GetBaseException().Message);}
 }
 public static void ReplayRecordingStarted(object replay){try{
  string path=(string)Get(replay,"Filename");
  var info=new Dictionary<string,string>();info["Format"]="1";info["ModVersion"]=ReplayProtocol.ModVersion;info["RulesHash"]=ReplayProtocol.RulesHash;info["File"]=path;info["State"]="recording";info["StartedUtc"]=DateTime.UtcNow.ToString("o");info["ProcessId"]=System.Diagnostics.Process.GetCurrentProcess().Id.ToString();info["ProcessStartUtc"]=System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks.ToString();
  lock(replayGate){info["Fingerprint"]=replayFingerprint??"";replayRecords[replay]=info;}
  ReplayProtocol.Write(ReplayProtocol.Metadata(path),info);ReplayEvent("recording_started file="+Path.GetFileName(path));
 }catch(Exception e){ReplayEvent("recording_metadata_failed "+e.Message);}}
 public static void ReplayRecordingEnded(object replay){try{
  Dictionary<string,string> info;lock(replayGate){if(!replayRecords.TryGetValue(replay,out info))return;replayRecords.Remove(replay);}
  // No Unity objects or simulation collections cross onto the worker.
  string path=info["File"];if(ReplayProtocol.Value(info,"State")=="failed")return;
  info["LastFrame"]=Convert.ToString(Get(Get(replay,"LastRecordedFrame"),"FrameNumber"));info["State"]="finalizing";ReplayProtocol.Write(ReplayProtocol.Metadata(path),info);
  ThreadPool.QueueUserWorkItem(delegate{try{
   info["FileHash"]=ReplayProtocol.Digest(path);info["Bytes"]=new FileInfo(path).Length.ToString();info["EndedUtc"]=DateTime.UtcNow.ToString("o");info["State"]="closed";
   ReplayProtocol.Write(ReplayProtocol.Metadata(path),info);ReplayEvent("recording_closed file="+Path.GetFileName(path)+" bytes="+info["Bytes"]);
  }catch(Exception e){ReplayEvent("recording_finalize_failed "+e.Message);}});
 }catch(Exception e){ReplayEvent("recording_end_failed "+e.Message);}}
 public static void ReplayRecordingFailed(object replay,object failure){try{
  var stream=Get(replay,"mActiveStream") as Stream;try{if(stream!=null)stream.Dispose();}catch{}Set(replay,"mActiveStream",null);
  Dictionary<string,string> info;lock(replayGate){if(!replayRecords.TryGetValue(replay,out info)){info=new Dictionary<string,string>();info["File"]=(string)Get(replay,"Filename");replayRecords[replay]=info;}info["State"]="failed";info["Error"]=Convert.ToString(failure);replayRecords.Remove(replay);}
  ReplayProtocol.Write(ReplayProtocol.Metadata(info["File"]),info);ReplayEvent("RECORDING_FAILED file="+Path.GetFileName(info["File"])+"; recording disabled; match continues; "+Convert.ToString(failure));
 }catch(Exception e){ReplayEvent("recording_failure_diagnostic_failed "+e.Message);}}
 public static void ReplayLoaded(object replay){try{replayInvalid=false;ReplayEvent("native_loaded file="+Path.GetFileName((string)Get(replay,"Filename"))+" last_frame="+Get(Get(replay,"LastRecordedFrame"),"FrameNumber"));}catch(Exception e){ReplayEvent("load_diagnostic_failed "+e.Message);}}
 public static void ReplayChecksumMismatch(object controller,object expected){try{
  replayInvalid=true;Set(controller,"Paused",true);object replay=Get(Get(controller,"mReplayService"),"SimReplay");string file=(string)Get(replay,"Filename");ReplayEvent("CHECKSUM_MISMATCH after_frame="+Get(Get(controller,"mLastProcessedFrame"),"FrameNumber")+" recorded_next_frame="+Get(Get(replay,"LastReplayedFrame"),"FrameNumber")+" last_frame="+Get(Get(replay,"LastRecordedFrame"),"FrameNumber")+" profile="+replayProfile+" expected="+Get(expected,"Sum")+" actual="+Get(Get(controller,"mSimChecksum"),"Sum")+"; playback paused; reproduction invalid");
  if(replayCanRetry){replayCanRetry=false;replayRetry=new Dictionary<string,string>{{"File",file},{"FileHash",ReplayProtocol.Digest(file)},{"Fingerprint",ReplayProtocol.Fingerprint(ReplayGame())},{"LegacyMovement","1"}};replayRetryPending=true;}
  else {RestoreReplayUI();CheatNotice(UI(),"리플레이 상태가 기록과 달라 재생을 중단했습니다. 게임·패치 버전 또는 기록 손상을 확인하세요. 진단 로그에 불일치 틱을 남겼습니다.");}
 }catch(Exception e){ReplayEvent("checksum_diagnostic_failed "+e.Message);}}
 public static void ReplaySimulationEnded(object controller){try{
  object service=Get(controller,"mReplayService");if(Convert.ToString(Get(service,"ReplayMode"))!="ReplayingGame")return;
  object replay=Get(service,"SimReplay");ulong frame=Convert.ToUInt64(Get(Get(controller,"mLastProcessedFrame"),"FrameNumber")),last=Convert.ToUInt64(Get(Get(replay,"LastRecordedFrame"),"FrameNumber"));
  if(frame>=last)ReplayFinished(controller);else ReplayEvent("playback_ended_early frame="+frame+" last_frame="+last+" checksum_mismatch="+replayInvalid);
 }catch(Exception e){ReplayEvent("end_diagnostic_failed "+e.Message);}finally{EndReplayPresentation();SetReplayMovement(false);}}
 public static void ReplayFinished(object controller){try{object replay=Get(Get(controller,"mReplayService"),"SimReplay");string file=(string)Get(replay,"Filename");var info=ReplayProtocol.Read(ReplayProtocol.Metadata(file));info["PlaybackState"]=replayInvalid?"mismatch":ReplayProtocol.Value(info,"State")=="closed"?"checksum_eof":"checksum_eof_unconfirmed";if(!replayInvalid && replayProfile=="original_movement")ReplayProtocol.Write(ReplayCompatibility(file),new Dictionary<string,string>{{"FileHash",ReplayProtocol.Digest(file)},{"Profile","original_movement"},{"ValidatedUtc",DateTime.UtcNow.ToString("o")}});info["PlaybackUtc"]=DateTime.UtcNow.ToString("o");ReplayProtocol.Write(ReplayProtocol.Metadata(file),info);ReplayEvent("playback_eof last_frame="+Get(Get(replay,"LastRecordedFrame"),"FrameNumber")+" checksum_mismatch="+replayInvalid);}catch(Exception e){ReplayEvent("eof_diagnostic_failed "+e.Message);}}
}
