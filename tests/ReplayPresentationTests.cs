using System;
using System.Reflection;
class ReplayPresentationTests {
 public class Flag {public bool Value {get;set;}}
 public class Controls {public Flag ReplayControlsVisible=new Flag();}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static int Main(string[] args){try{
  Environment.SetEnvironmentVariable("CFL_REPLAY_TEST_HOME",System.IO.Path.Combine(System.IO.Path.GetTempPath(),"CflPresentationTest-"+Guid.NewGuid().ToString("N")));
  var type=Assembly.LoadFrom(args[0]).GetType("CflRuntime");var flags=BindingFlags.NonPublic|BindingFlags.Static;
  Action<string,object> set=(n,v)=>type.GetField(n,flags).SetValue(null,v);
  Action toggle=()=>type.GetMethod("ToggleReplayControls").Invoke(null,null);
  Action reset=()=>type.GetMethod("ResetReplayControls",flags).Invoke(null,null);
  var controls=new Controls();controls.ReplayControlsVisible.Value=true;set("replayControlData",controls);
  toggle();Check(controls.ReplayControlsVisible.Value,"live session changed");
  set("replayActive",true);toggle();Check(!controls.ReplayControlsVisible.Value,"first O did not hide");toggle();Check(controls.ReplayControlsVisible.Value,"second O did not restore");
  controls.ReplayControlsVisible.Value=false;toggle();toggle();Check(!controls.ReplayControlsVisible.Value,"originally hidden control was shown");
  controls.ReplayControlsVisible.Value=true;toggle();reset();Check(controls.ReplayControlsVisible.Value,"session cleanup did not restore");Check(!(bool)type.GetField("replayDefaultViewSet",flags).GetValue(null),"default view leaked");
  Console.WriteLine("PASS: replay-only controls toggle, prior visibility restoration and session cleanup (fixture).");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
