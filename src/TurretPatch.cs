using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
public static class TurretPatch {
 static MethodReference Hook(ModuleDefinition m,string name,int count){var scope=m.AssemblyReferences.FirstOrDefault(x=>x.Name=="CflRuntime");if(scope==null){scope=new AssemblyNameReference("CflRuntime",new Version(0,2,0,0));m.AssemblyReferences.Add(scope);}var r=new MethodReference(name,m.TypeSystem.Void,new TypeReference("","CflRuntime",m,scope));for(int i=0;i<count;i++)r.Parameters.Add(new ParameterDefinition(m.TypeSystem.Object));return r;}
 static void Args(ILProcessor il,Instruction at,MethodDefinition m,int[] args){foreach(int arg in args){il.InsertBefore(at,Instruction.Create(OpCodes.Ldarg,arg==0?m.Body.ThisParameter:m.Parameters[arg-1]));if(arg>0&&m.Parameters[arg-1].ParameterType.IsValueType)il.InsertBefore(at,Instruction.Create(OpCodes.Box,m.Parameters[arg-1].ParameterType));}}
 static void Entry(MethodDefinition m,string hook,params int[] args){var il=m.Body.GetILProcessor();var at=m.Body.Instructions[0];Args(il,at,m,args);il.InsertBefore(at,Instruction.Create(OpCodes.Call,Hook(m.Module,hook,args.Length)));}
 public static void Apply(AssemblyDefinition a){var m=a.MainModule;
  if(a.Name.Name=="Carbon.Simulation"){
   var t=m.Types.Single(x=>x.FullName=="Carbon.Simulation.UnderConstructionProcessor");var start=t.Methods.Single(x=>x.Name=="TryStartConstruction");var il=start.Body.GetILProcessor();var call=start.Body.Instructions.Single(x=>x.Operand is MethodReference&&((MethodReference)x.Operand).Name=="StartConstruction");var at=call.Next;Args(il,at,start,new[]{1,3,4});il.InsertBefore(at,Instruction.Create(OpCodes.Call,Hook(m,"TurretConstructionBegin",3)));
   Entry(t.Methods.Single(x=>x.Name=="CompleteBuilding"),"TurretConstructionEnd",1,2);
   Entry(t.Methods.Single(x=>x.Name=="CancelConstruction"),"TurretConstructionEnd",2,3);
   Entry(m.Types.Single(x=>x.FullName=="Carbon.Simulation.EntityProcess`2").Methods.Single(x=>x.Name=="DespawnEntity"),"TurretEntityDespawn",1,0);
   Entry(m.Types.Single(x=>x.FullName=="Carbon.Simulation.SimController").Methods.Single(x=>x.Name=="EndSimulation"),"TurretSessionEnd",0);
  }
  if(a.Name.Name=="Carbon.Presentation"){
   var t=m.Types.Single(x=>x.FullName=="Carbon.Presentation.FogOfWarViewProcessor");Entry(t.Methods.Single(x=>x.Name=="Process"),"TurretViewTick",0);Entry(t.Methods.Single(x=>x.Name=="TearDown"),"TurretViewEnd",0);
   var update=t.Methods.Single(x=>x.Name=="UpdateEntityVisibility");var il=update.Body.GetILProcessor();var first=update.Body.Instructions[0];Args(il,first,update,new[]{1});var h=Hook(m,"TurretViewHidden",1);h.ReturnType=m.TypeSystem.Boolean;il.InsertBefore(first,Instruction.Create(OpCodes.Call,h));il.InsertBefore(first,Instruction.Create(OpCodes.Brfalse,first));il.InsertBefore(first,Instruction.Create(OpCodes.Ldc_I4_0));il.InsertBefore(first,Instruction.Create(OpCodes.Starg,update.Parameters[1]));
  }
 }
}
