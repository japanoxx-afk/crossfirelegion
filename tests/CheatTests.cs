using System;
using System.Collections.Generic;
namespace Game.Common.UI {public class GameUIBase {
 public static GameUIBase Instance{get;set;}
 public CheatMatch MatchDataSource=new CheatMatch();public CheatSlots MatchPlayerSlotDataSource=new CheatSlots();
 public CheatMission MissionDataSource=new CheatMission();public CheatChat ChatDataSource=new CheatChat();
}}
namespace Game.Online.Data {public class ChatMessage {public string SenderID{get;set;}public string SenderName{get;set;}public string Message{get;set;}public string RecipientID{get;set;}public string RecipientName{get;set;}public CheatSource Source{get;set;}}}
public enum CheatSource{None,One,Two,All}
public class CheatChat {public void HandleMessageReceived(object message){}}
public class CheatMatch {public bool replay,observer,automatch;public bool IsReplayMode(){return replay;}public bool IsObserverPlayer(){return observer;}public bool IsAutoMatchMode(){return automatch;}public object GetMatch(){return this;}}
public struct CheatPlayerID {public static readonly CheatPlayerID kInvalidID=new CheatPlayerID();public int value;}
public class CheatPlayer {public CheatPlayerID ID=new CheatPlayerID{value=1};public bool IsLocal;public string Name="Player";}
public class CheatGroup {public bool IsOpen;public int SlotID;public CheatPlayer Player=new CheatPlayer();}
public class CheatSlot {public string Type="Human";public CheatGroup GroupData=new CheatGroup();}
public class CheatSlots {public List<CheatSlot> Slots=new List<CheatSlot>();public bool valid=true;public bool GetMatchPlayerSlots(object match,List<CheatSlot> dest){dest.AddRange(Slots);return valid;}}
public class CheatMission {public CheatScheduler mCommandScheduler=new CheatScheduler();}
public class CheatScheduler {public object LocalSimCommander=new object();public List<CheatCommand> Commands=new List<CheatCommand>();public bool ScheduleCommand(CheatCommand c){Commands.Add(c);return true;}}
public class CheatCommand {public object Owner;public string ScriptToRun;}
namespace Carbon.Simulation {
 public class Sim{}
 public static class SimCommandFactory {public static CheatCommand CreateExecuteScriptCommand(object owner,string script){return new CheatCommand{Owner=owner,ScriptToRun=script};}}
 public static class StoreHelper<T>{public static CheatStore Store=new CheatStore();public static CheatStore GetOrAddStoreComponent(object owner){return Store;}}
}
public struct CheatReal {
 public int Value;public static CheatReal FromInt(int n){return new CheatReal{Value=n};}public static implicit operator CheatReal(int n){return new CheatReal{Value=n};}
 public static bool operator <(CheatReal a,CheatReal b){return a.Value<b.Value;}
 public static bool operator >(CheatReal a,CheatReal b){return a.Value>b.Value;}
}
public class CheatAsset {public string Name;}
public class CheatReference {public CheatAsset Asset;public static implicit operator CheatAsset(CheatReference r){return r.Asset;}}
public class CheatCapacity {public CheatReference Inventory;}
public class CheatInventory {
 public CheatCapacity Attributes;public CheatReal ActiveCap=7500;public CheatReal Amount;
 public void SetCurrentAmount(CheatReal amount){Amount=amount<ActiveCap?amount:ActiveCap;}
 public CheatInventory(string name){Attributes=new CheatCapacity{Inventory=new CheatReference{Asset=new CheatAsset{Name=name}}};}
}
public class CheatManager {public Dictionary<int,CheatInventory> Inventories=new Dictionary<int,CheatInventory>();}
public class CheatStore {public CheatManager InventoryManager=new CheatManager();}
public class CheatInput {public string text;}
public class CheatPanel {public CheatInput mInputField=new CheatInput();}
public static class CheatTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static void Run(){
  var ui=new Game.Common.UI.GameUIBase();Game.Common.UI.GameUIBase.Instance=ui;
  var local=new CheatSlot();local.GroupData.IsOpen=true;local.GroupData.Player.IsLocal=true;local.GroupData.SlotID=3;ui.MatchPlayerSlotDataSource.Slots.Add(local);
  var empty=new CheatSlot();empty.GroupData.Player.ID=CheatPlayerID.kInvalidID;ui.MatchPlayerSlotDataSource.Slots.Add(empty);ui.MatchPlayerSlotDataSource.Slots.Add(new CheatSlot{Type="AI"});
  var panel=new CheatPanel();panel.mInputField.text="hello";Check(!CflRuntime.TryCheat(panel) && panel.mInputField.text=="hello","Normal chat changed");
  panel.mInputField.text="  MONEY  ";Check(CflRuntime.TryCheat(panel),"Money not consumed");Check(ui.MissionDataSource.mCommandScheduler.Commands.Count==1 && panel.mInputField.text=="","Money not scheduled/cleared");
  var money=ui.MissionDataSource.mCommandScheduler.Commands[0];var store=Carbon.Simulation.StoreHelper<Carbon.Simulation.Sim>.Store;
  store.InventoryManager.Inventories.Add(0,new CheatInventory("ia_resource1"));store.InventoryManager.Inventories.Add(1,new CheatInventory("ia_resource2"));store.InventoryManager.Inventories.Add(2,new CheatInventory("ia_population"));
  Check(CflRuntime.ExecuteMoney(money),"Money sim command missing");Check(store.InventoryManager.Inventories[0].Amount.Value==9999 && store.InventoryManager.Inventories[1].Amount.Value==9999 && store.InventoryManager.Inventories[2].Amount.Value==0,"Money must set both resources above cap and leave population untouched");
  Check(!CflRuntime.ExecuteMoney(new CheatCommand{ScriptToRun="ordinary lua"}),"Non-cheat script consumed");
  panel.mInputField.text="speed";CflRuntime.TryCheat(panel);string speed=ui.MissionDataSource.mCommandScheduler.Commands[1].ScriptToRun;
  Check(speed.Contains("commanders[4]") && speed.Contains("if not __cflProduction10[3]") && speed.Contains("ba_campaign_cvs_test_object_m07_production_rate"),"Speed must target actual local slot and avoid stacking");
  int before=ui.MissionDataSource.mCommandScheduler.Commands.Count;
  ui.MatchPlayerSlotDataSource.Slots.Add(new CheatSlot());panel.mInputField.text="money";CflRuntime.TryCheat(panel);Check(ui.MissionDataSource.mCommandScheduler.Commands.Count==before,"Multiplayer cheat accepted");ui.MatchPlayerSlotDataSource.Slots.RemoveAt(3);
  foreach(string mode in new[]{"replay","observer","automatch"}){typeof(CheatMatch).GetField(mode).SetValue(ui.MatchDataSource,true);panel.mInputField.text="speed";CflRuntime.TryCheat(panel);Check(ui.MissionDataSource.mCommandScheduler.Commands.Count==before,"Forbidden mode accepted: "+mode);typeof(CheatMatch).GetField(mode).SetValue(ui.MatchDataSource,false);}
  var watcher=new CheatSlot{Type="Observer"};ui.MatchPlayerSlotDataSource.Slots.Add(watcher);panel.mInputField.text="money";CflRuntime.TryCheat(panel);Check(ui.MissionDataSource.mCommandScheduler.Commands.Count==before,"Remote observer accepted");ui.MatchPlayerSlotDataSource.Slots.Remove(watcher);
  ui.MatchPlayerSlotDataSource.valid=false;panel.mInputField.text="money";CflRuntime.TryCheat(panel);Check(ui.MissionDataSource.mCommandScheduler.Commands.Count==before,"Missing match metadata accepted");
  Console.WriteLine("PASS: Enter cheat dispatch, exact resource amounts above cap, population unchanged, local slot selection, normal chat preserved, multiplayer/replay/observer/automatch/unknown match rejected.");
 }
}
