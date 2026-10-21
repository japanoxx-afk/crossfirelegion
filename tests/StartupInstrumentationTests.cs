using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
public static class StartupFixture {
 public static int Calls,Finalized;
 public static bool AddService(bool fail){Calls++;if(fail)throw new InvalidOperationException();return true;}
 public static int Run(bool fail,bool skip){
  try{if(skip)return 4;int value=AddService(fail)?7:8;if(AddService(false))value++;if(AddService(false))value++;return value;}
  catch(InvalidOperationException){return -3;}finally{Finalized++;}
 }
}
public static class StartupInstrumentationTests {
 public static void Run(){
  byte[] rewritten;
  using(var assembly=AssemblyDefinition.ReadAssembly(typeof(StartupFixture).Assembly.Location)){
   var method=assembly.MainModule.Types.Single(t=>t.Name=="StartupFixture").Methods.Single(m=>m.Name=="Run");
   var calls=method.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>i.Operand.ToString()).ToArray();int handlers=method.Body.ExceptionHandlers.Count;
   foreach(string helper in new[]{"TraceStartupCalls","TraceStartupMethod"})typeof(Patcher).GetMethod(helper,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{method});
   var after=method.Body.Instructions.Where(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.Name!="CflRuntime").Select(i=>i.Operand.ToString()).ToArray();
   if(!calls.SequenceEqual(after) || method.Body.ExceptionHandlers.Count!=handlers)throw new Exception("Startup tracing changed native calls or exception handlers");
   foreach(var instruction in method.Body.Instructions){var hook=instruction.Operand as MethodReference;if(hook!=null && hook.DeclaringType.Name=="CflRuntime")instruction.Operand=assembly.MainModule.Types.Single(t=>t.Name=="CflRuntime").Methods.Single(m=>m.Name==hook.Name);}
   using(var bytes=new MemoryStream()){assembly.Write(bytes);rewritten=bytes.ToArray();}
  }
  var fixture=Assembly.Load(rewritten).GetType("StartupFixture");var run=fixture.GetMethod("Run");
  if((int)run.Invoke(null,new object[]{false,false})!=9 || (int)run.Invoke(null,new object[]{true,false})!=-3 || (int)run.Invoke(null,new object[]{false,true})!=4 || (int)fixture.GetField("Calls").GetValue(null)!=4 || (int)fixture.GetField("Finalized").GetValue(null)!=3)throw new Exception("Instrumented startup branch, return value, catch or finally changed");
  Console.WriteLine("PASS: startup instrumentation executes with native call order, stack return values, branch, catch and finally preserved.");
 }
}
