using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
public static class MovementPatch {
 public const string HelperName="CflIgnoreCollisionForMovingGroundUnits";
 public static MethodDefinition CreateHelper(ModuleDefinition module){
  var yield=module.Types.Single(t=>t.FullName=="Carbon.Simulation.YieldHelpers");
  var native=yield.Methods.Single(m=>m.Name=="EntityPairIgnoreCollision" && m.Parameters.Count==2);
  var legacy=yield.Fields.FirstOrDefault(f=>f.Name=="CflReplayOriginalMovement");if(legacy==null){legacy=new FieldDefinition("CflReplayOriginalMovement",FieldAttributes.Public|FieldAttributes.Static,module.TypeSystem.Boolean);yield.Fields.Add(legacy);}
  var detailed=yield.Methods.Single(m=>m.Name=="EntityPairIgnoreCollision" && m.Parameters.Count==4);
  var collision=module.Types.Single(t=>t.FullName=="Carbon.Simulation.RTSCollision");
  var layer=collision.Fields.Single(f=>f.Name=="ActiveCollisionLayer");
  var velocity=(MethodReference)detailed.Body.Instructions.First(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="GetVelocity2").Operand;
  var unequal=(MethodReference)detailed.Body.Instructions.First(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="op_Inequality" && ((MethodReference)i.Operand).DeclaringType.Name=="Vector2").Operand;
  var zero=(FieldReference)detailed.Body.Instructions.First(i=>i.Operand is FieldReference && ((FieldReference)i.Operand).Name=="Zero").Operand;
  var helper=new MethodDefinition(HelperName,MethodAttributes.Assembly|MethodAttributes.Static|MethodAttributes.HideBySig,module.TypeSystem.Boolean);
  helper.Parameters.Add(new ParameterDefinition(native.Parameters[0].ParameterType));helper.Parameters.Add(new ParameterDefinition(native.Parameters[1].ParameterType));helper.Parameters.Add(new ParameterDefinition(collision));helper.Parameters.Add(new ParameterDefinition(collision));yield.Methods.Add(helper);
  var il=helper.Body.GetILProcessor();var collide=Instruction.Create(OpCodes.Ldc_I4_0);var ignore=Instruction.Create(OpCodes.Ldc_I4_1);
  var modern=Instruction.Create(OpCodes.Nop);il.Append(Instruction.Create(OpCodes.Ldsfld,legacy));il.Append(Instruction.Create(OpCodes.Brfalse,modern));il.Append(Instruction.Create(OpCodes.Ldarg_0));il.Append(Instruction.Create(OpCodes.Ldarg_1));il.Append(Instruction.Create(OpCodes.Call,native));il.Append(Instruction.Create(OpCodes.Ret));il.Append(modern);
  il.Append(Instruction.Create(OpCodes.Ldarg_0));il.Append(Instruction.Create(OpCodes.Ldarg_1));il.Append(Instruction.Create(OpCodes.Call,native));il.Append(Instruction.Create(OpCodes.Brfalse,collide));
  foreach(int index in new[]{2,3}){il.Append(Instruction.Create(OpCodes.Ldarg,helper.Parameters[index]));il.Append(Instruction.Create(OpCodes.Ldfld,layer));il.Append(Instruction.Create(OpCodes.Ldc_I4_1));il.Append(Instruction.Create(OpCodes.Bne_Un,ignore));}
  foreach(int index in new[]{0,1}){il.Append(Instruction.Create(OpCodes.Ldarg,helper.Parameters[index]));il.Append(Instruction.Create(OpCodes.Call,velocity));il.Append(Instruction.Create(OpCodes.Ldsfld,zero));il.Append(Instruction.Create(OpCodes.Call,unequal));il.Append(Instruction.Create(OpCodes.Brfalse,ignore));}
  il.Append(collide);il.Append(Instruction.Create(OpCodes.Ret));il.Append(ignore);il.Append(Instruction.Create(OpCodes.Ret));
  return helper;
 }
 public static void Apply(AssemblyDefinition assembly){
  var module=assembly.MainModule;var helper=CreateHelper(module);
  var processor=module.Types.Single(t=>t.FullName=="Carbon.Simulation.RTSCollisionProcessor").Methods.Single(m=>m.Name=="FindCollisionsDiscreteVsDiscrete");
  var call=processor.Body.Instructions.Single(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="EntityPairIgnoreCollision");
  var collisions=processor.Body.Variables.Where(v=>v.VariableType.FullName=="Carbon.Simulation.RTSCollision").ToArray();if(collisions.Length!=2)throw new Exception("Unsupported movement collision locals");
  var il=processor.Body.GetILProcessor();il.InsertBefore(call,Instruction.Create(OpCodes.Ldloc,collisions[0]));il.InsertBefore(call,Instruction.Create(OpCodes.Ldloc,collisions[1]));call.Operand=helper;
  // Native code uses long branches near this insertion; widen every short branch defensively.
  foreach(var instruction in processor.Body.Instructions)if(instruction.OpCode.OperandType==OperandType.ShortInlineBrTarget){string name=instruction.OpCode.Name.Substring(0,instruction.OpCode.Name.Length-2);instruction.OpCode=typeof(OpCodes).GetFields().Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).Single(code=>code.Name==name);}
  var focus=module.Types.Single(t=>t.FullName=="Carbon.Simulation.Data.GlobalMovementAttributes").Methods.Single(m=>m.Name=="get_FocusClickShrinkAmount");
  var one=focus.ReturnType.Resolve().Fields.Single(f=>f.Name=="One");var original=focus.DeclaringType.Fields.Single(f=>f.Name=="<FocusClickShrinkAmount>k__BackingField");focus.Body.Instructions.Clear();focus.Body.ExceptionHandlers.Clear();focus.Body.Variables.Clear();var updated=Instruction.Create(OpCodes.Ldsfld,module.ImportReference(one));var fi=focus.Body.GetILProcessor();fi.Append(Instruction.Create(OpCodes.Ldsfld,helper.DeclaringType.Fields.Single(f=>f.Name=="CflReplayOriginalMovement")));fi.Append(Instruction.Create(OpCodes.Brfalse,updated));fi.Append(Instruction.Create(OpCodes.Ldarg_0));fi.Append(Instruction.Create(OpCodes.Ldfld,original));fi.Append(Instruction.Create(OpCodes.Ret));fi.Append(updated);fi.Append(Instruction.Create(OpCodes.Ret));
 }
}
