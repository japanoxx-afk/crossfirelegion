using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;
public static class MovementTests {
 public static void Run(){
  byte[] bytes;using(var assembly=AssemblyDefinition.ReadAssembly(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MovementFixture.dll"))){MovementPatch.CreateHelper(assembly.MainModule);using(var output=new MemoryStream()){assembly.Write(output);bytes=output.ToArray();}}
  var fixture=Assembly.Load(bytes);var unit=fixture.GetType("Carbon.Simulation.MovementUnit");var collision=fixture.GetType("Carbon.Simulation.RTSCollision");var layer=fixture.GetType("Carbon.Simulation.Layer");var helper=fixture.GetType("Carbon.Simulation.YieldHelpers").GetMethod(MovementPatch.HelperName,BindingFlags.NonPublic|BindingFlags.Static);int cases=0;
  for(int friends=0;friends<4;friends++)for(int moving=0;moving<4;moving++)foreach(int firstLayer in new[]{1,2,4})foreach(int secondLayer in new[]{1,2,4}){
   object first=Activator.CreateInstance(unit),second=Activator.CreateInstance(unit),a=Activator.CreateInstance(collision),b=Activator.CreateInstance(collision);
   unit.GetField("Friendly").SetValue(first,(friends&1)!=0);unit.GetField("Friendly").SetValue(second,(friends&2)!=0);unit.GetField("X").SetValue(first,(moving&1)!=0?-3:0);unit.GetField("Y").SetValue(second,(moving&2)!=0?4:0);
   collision.GetField("ActiveCollisionLayer").SetValue(a,Enum.ToObject(layer,firstLayer));collision.GetField("ActiveCollisionLayer").SetValue(b,Enum.ToObject(layer,secondLayer));
   bool native=friends==3 && moving!=0;bool expected=native && !(firstLayer==1 && secondLayer==1 && moving==3);bool actual=(bool)helper.Invoke(null,new[]{first,second,a,b});if(actual!=expected)throw new Exception("Movement collision mismatch: friends="+friends+" moving="+moving+" layers="+firstLayer+","+secondLayer);cases++;
  }
  Console.WriteLine("PASS: emitted native movement helper executes "+cases+" cases; moving friendly ground pairs collide; air, enemies, mixed layers and stationary pass-through preserve native behavior.");
 }
}
