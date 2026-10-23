using System;
using System.IO;
using System.IO.Compression;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
public static class EnergyPatch {
 const long Energy=2315574823061558829L;
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=10000000};
 static Dictionary<string,object> Dict(object value){return (Dictionary<string,object>)value;}
 public static Dictionary<string,object> Read(ZipArchive zip,string path){var e=zip.GetEntry(path);if(e==null)throw new Exception("Missing energy data: "+path);string text;using(var r=new StreamReader(e.Open()))text=r.ReadToEnd();text=Regex.Replace(text,"(\"(?:\\\\.|[^\"\\\\])*\")|//[^\\r\\n]*",m=>m.Groups[1].Success?m.Value:"");return Json.Deserialize<Dictionary<string,object>>(text.Replace("\"__type\"","\"__cfl_type\""));}
 static void Write(ZipArchive zip,string path,object value){var old=zip.GetEntry(path);var stamp=old==null?new DateTimeOffset(2000,1,1,0,0,0,TimeSpan.Zero):old.LastWriteTime;if(old!=null)old.Delete();var entry=zip.CreateEntry(path);entry.LastWriteTime=stamp;using(var w=new StreamWriter(entry.Open(),new UTF8Encoding(false)))w.Write(Json.Serialize(value).Replace("\"__cfl_type\"","\"__type\""));}
 public static string ClassicScript(string original){const string before="actions.SetInventoryValue(commander, global.InventoryCommanderEnergy, 100)";if(original.Split(new[]{before},StringSplitOptions.None).Length!=2)throw new Exception("Unsupported classic starting energy script");using(var stream=typeof(EnergyPatch).Assembly.GetManifestResourceStream("CommanderEnergy.lua")){if(stream==null)throw new Exception("Missing commander energy script");using(var reader=new StreamReader(stream))return original.Replace(before,"actions.SetInventoryValue(commander, global.InventoryCommanderEnergy, 0)")+"\r\n"+reader.ReadToEnd();}}
 public static void Rewrite(string source,string output){
  File.Copy(source,output,true);
  using(var zip=ZipFile.Open(output,ZipArchiveMode.Update)){
   foreach(string file in zip.Entries.Where(e=>e.FullName.StartsWith("Attributes/ConstructionAttributes/",StringComparison.Ordinal)).Select(e=>e.FullName).ToArray()){
    var construction=Read(zip,file);if(TurretRules.IsTurret(Path.GetFileNameWithoutExtension(file))){Dict(construction["DeployerBlueprint"])["ReferenceID"]=0L;Write(zip,file,construction);}
   }
   // Script owns passive energy in Classic; keep the old trickle disabled to avoid double credits.
   const string path="Attributes/TrickleAttributesCollection/trickle_commander_default.txt";
   var collection=Read(zip,path);var entries=((IEnumerable)collection["TrickleEntries"]).Cast<object>().ToList();
   var energy=entries.Select(Dict).Single(x=>Convert.ToInt64(Dict(x["InventoryType"])["ReferenceID"])==Energy);
   energy["Amount"]=0;energy["IntervalLength"]=60L*(1L<<32);
   collection["TrickleEntries"]=entries.ToArray();Write(zip,path,collection);
   foreach(string file in zip.Entries.Where(e=>e.FullName.StartsWith("Attributes/InventoryLoadoutAttributes/",StringComparison.Ordinal)).Select(e=>e.FullName).ToArray()){
    var loadout=Read(zip,file);var inventories=Dict(loadout["Inventories"]);bool changed=false;
    foreach(object item in (IEnumerable)inventories["Items"]){var inventory=Dict(item);if(Convert.ToInt64(Dict(inventory["InventoryID"])["ReferenceID"])==Energy){inventory["StartingAmount"]=0L;changed=true;}}
    if(changed)Write(zip,file,loadout);
   }
  }
 }
}

