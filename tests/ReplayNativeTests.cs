using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
class ReplayNativeTests {
 sealed class BrokenStream:MemoryStream {public override void Write(byte[] b,int o,int c){throw new IOException("fixture disk full");}public override void WriteByte(byte b){throw new IOException("fixture disk full");}}
 static int Main(string[] args){try{
  string managed=args[0];Environment.SetEnvironmentVariable("CFL_REPLAY_TEST_HOME",Path.Combine(Path.GetTempPath(),"CflNativeReplayTests-"+Guid.NewGuid().ToString("N")));AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string path=Path.Combine(managed,new AssemblyName(e.Name).Name+".dll");return File.Exists(path)?Assembly.LoadFrom(path):null;};
  var assembly=Assembly.LoadFrom(Path.Combine(managed,"Carbon.Simulation.dll"));var type=assembly.GetType("Carbon.Simulation.Replay");var replay=FormatterServices.GetUninitializedObject(type);var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  type.GetMethod("EndWrite").Invoke(replay,null);
  string fixture=Path.Combine(Path.GetTempPath(),"cfl-native-failure-"+Guid.NewGuid()+".rply");type.GetProperty("Filename").GetSetMethod(true).Invoke(replay,new object[]{fixture});
  var stream=new BrokenStream();type.GetField("mActiveStream",flags).SetValue(replay,stream);
  var core=Assembly.LoadFrom(Path.Combine(managed,"Carbon.Core.dll"));var writer=Activator.CreateInstance(core.GetType("Carbon.Core.IO.Streams.BinaryStreamWriter"),new object[]{stream,false});type.GetField("mActiveWriter",flags).SetValue(replay,writer);
  var method=type.GetMethod("WriteCommandsForFrame");var parameters=method.GetParameters();var frame=Activator.CreateInstance(parameters[0].ParameterType);var checksum=Activator.CreateInstance(parameters[1].ParameterType);var commands=Activator.CreateInstance(parameters[2].ParameterType);
  bool success=(bool)method.Invoke(replay,new[]{frame,checksum,commands});if(success)throw new Exception("failed write reported success");if(type.GetField("mActiveStream",flags).GetValue(replay)!=null)throw new Exception("failed stream retained");type.GetMethod("EndWrite").Invoke(replay,null);
  File.Delete(ReplayProtocol.Metadata(fixture));
  var frameType=parameters[0].ParameterType;Func<uint,object> makeFrame=n=>frameType.GetMethod("CreateSimFrameNumber",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(uint)},null).Invoke(null,new object[]{n});frame=makeFrame(1);
  var replayFrame=assembly.GetType("Carbon.Simulation.ReplayFrame");var queueField=type.GetField("mReplayCommands",flags);object queue=Activator.CreateInstance(queueField.FieldType);queueField.SetValue(replay,queue);
  object expected=Activator.CreateInstance(parameters[1].ParameterType,new object[]{123});object recordedFrame=Activator.CreateInstance(replayFrame,new object[]{frame,expected,Array.CreateInstance(assembly.GetType("Carbon.Simulation.Commands.SimCommandBase"),0)});queue.GetType().GetMethod("Enqueue").Invoke(queue,new[]{recordedFrame});
  type.GetProperty("OverLastRecordedFrame").GetSetMethod(true).Invoke(replay,new[]{makeFrame(20)});type.GetProperty("LastRecordedFrame").GetSetMethod(true).Invoke(replay,new[]{makeFrame(20)});
  var serviceType=assembly.GetType("Carbon.Simulation.ReplayService");object service=FormatterServices.GetUninitializedObject(serviceType);serviceType.GetProperty("SimReplay").GetSetMethod(true).Invoke(service,new[]{replay});
  var controlType=assembly.GetType("Carbon.Simulation.SimController");object controller=FormatterServices.GetUninitializedObject(controlType);controlType.GetField("mReplayService",flags).SetValue(controller,service);controlType.GetField("mNextCommands",flags).SetValue(controller,commands);controlType.GetField("mSimChecksum",flags).SetValue(controller,checksum);controlType.GetField("mIsDecoupled",flags).SetValue(controller,true);
  var tick=controlType.GetMethod("TickSimOnAdvanceReplay",flags);tick.Invoke(controller,new[]{frame});Type runtime=null;foreach(var loaded in AppDomain.CurrentDomain.GetAssemblies())if(loaded.GetName().Name=="CflRuntime")runtime=loaded.GetType("CflRuntime");if(!(bool)runtime.GetMethod("ReplayIsInvalid").Invoke(null,null))throw new Exception("checksum mismatch did not latch");tick.Invoke(controller,new[]{frame});
  runtime.GetMethod("ReplayLoaded").Invoke(null,new[]{replay});controlType.GetField("mLastProcessedFrame",flags).SetValue(controller,makeFrame(19));runtime.GetMethod("ReplaySimulationEnded").Invoke(null,new[]{controller});if(ReplayProtocol.Value(ReplayProtocol.Read(ReplayProtocol.Metadata(fixture)),"PlaybackState")=="checksum_eof")throw new Exception("early exit marked complete");
  controlType.GetField("mLastProcessedFrame",flags).SetValue(controller,makeFrame(20));runtime.GetMethod("ReplaySimulationEnded").Invoke(null,new[]{controller});if(ReplayProtocol.Value(ReplayProtocol.Read(ReplayProtocol.Metadata(fixture)),"PlaybackState")!="checksum_eof")throw new Exception("last frame exit not marked complete");
  Console.WriteLine("PASS: actual patched native write guard, disk failure persistence, null-stream end, checksum mismatch latch; early exit differs from last-frame completion.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
