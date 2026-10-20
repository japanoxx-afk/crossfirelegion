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
 const long Energy=2315574823061558829L, BaseId=0x43464c0000000200L;
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=10000000};
 static Dictionary<string,object> Map(params object[] pairs){var d=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)d[(string)pairs[i]]=pairs[i+1];return d;}
 static Dictionary<string,object> Ref(string type,long id){return Map("__type","Carbon.Attributes.Reference`1[["+type+", Carbon.Simulation, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]","ReferenceID",id);}
 static Dictionary<string,object> Dict(object value){return (Dictionary<string,object>)value;}
 public static Dictionary<string,object> Read(ZipArchive zip,string path){var e=zip.GetEntry(path);if(e==null)throw new Exception("Missing energy data: "+path);string text;using(var r=new StreamReader(e.Open()))text=r.ReadToEnd();text=Regex.Replace(text,"(\"(?:\\\\.|[^\"\\\\])*\")|//[^\\r\\n]*",m=>m.Groups[1].Success?m.Value:"");return Json.Deserialize<Dictionary<string,object>>(text.Replace("\"__type\"","\"__cfl_type\""));}
 static void Write(ZipArchive zip,string path,object value){var old=zip.GetEntry(path);if(old!=null)old.Delete();using(var w=new StreamWriter(zip.CreateEntry(path).Open(),new UTF8Encoding(false)))w.Write(Json.Serialize(value).Replace("\"__cfl_type\"","\"__type\""));}
 static Dictionary<string,object> Relative(){return Map("__type","Carbon.Simulation.Data.EntityRelationDescription","Items",new object[0]);}
 static Dictionary<string,object> Requirement(string type,string name,long id){return Map("__type","Carbon.Simulation."+type,"RelativeEntity",Relative(),"FailureReason",Ref("Carbon.Simulation.FailureID",0),"Name",name,"EditorDisplayName","","ID",id);}
 public static string ClassicScript(string original){const string before="actions.SetInventoryValue(commander, global.InventoryCommanderEnergy, 100)";if(original.Split(new[]{before},StringSplitOptions.None).Length!=2)throw new Exception("Unsupported classic starting energy script");return original.Replace(before,"actions.SetInventoryValue(commander, global.InventoryCommanderEnergy, 0)");}
 public static void Rewrite(string source,string output){
  File.Copy(source,output,true);
  using(var zip=ZipFile.Open(output,ZipArchiveMode.Update)){
   // Commander-owned trickle runs once per player, independent of headquarters count.
   const string path="Attributes/TrickleAttributesCollection/trickle_commander_default.txt";
   var collection=Read(zip,path);var entries=((IEnumerable)collection["TrickleEntries"]).Cast<object>().ToList();
   var energy=entries.Select(Dict).Single(x=>Convert.ToInt64(Dict(x["InventoryType"])["ReferenceID"])==Energy);
   energy["Amount"]=1;energy["IntervalLength"]=15L*(1L<<32);
   long[] techs={2339290755542870248L,2339290849844763958L,2339967664248480705L,2339967728677767464L,2340438954731591000L,2340439062029106770L};
   for(int i=0;i<techs.Length;i++){
    string name="cfl_energy_hq_"+i;var req=Requirement("TechCountRequirement",name,BaseId+i);
    req["TechToCheckCollection"]=new object[]{Map("__type","Carbon.Simulation.TechRequirement","Tech",Ref("Carbon.Simulation.Data.TechAttributes",techs[i]),"TechStateToCheck",4,"Operation",3,"ValueToUse",1)};
    Write(zip,"Attributes/TechCountRequirement/"+name+".txt",req);
   }
   for(int tier=2;tier<=3;tier++){
    string name="cfl_energy_tier"+tier;var req=Requirement("OrRequirement",name,BaseId+10+tier);
    req["Requirements"]=Enumerable.Range(0,6).Where(i=>tier==2 || i%2==1).Select(i=>(object)Ref("Carbon.Simulation.RequirementBase",BaseId+i)).ToArray();Write(zip,"Attributes/OrRequirement/"+name+".txt",req);
    var bonus=new Dictionary<string,object>(energy);bonus["IntervalLength"]=60L*(1L<<32);bonus["TrickleRequirement"]=Ref("Carbon.Simulation.RequirementBase",BaseId+10+tier);entries.Add(bonus);
   }
   collection["TrickleEntries"]=entries.ToArray();Write(zip,path,collection);
   foreach(string file in zip.Entries.Where(e=>e.FullName.StartsWith("Attributes/InventoryLoadoutAttributes/",StringComparison.Ordinal)).Select(e=>e.FullName).ToArray()){
    var loadout=Read(zip,file);var inventories=Dict(loadout["Inventories"]);bool changed=false;
    foreach(object item in (IEnumerable)inventories["Items"]){var inventory=Dict(item);if(Convert.ToInt64(Dict(inventory["InventoryID"])["ReferenceID"])==Energy){inventory["StartingAmount"]=0L;changed=true;}}
    if(changed)Write(zip,file,loadout);
   }
  }
 }
}

