using System;
using System.Collections.Generic;
public class Reference<T>{public T Value;public static implicit operator T(Reference<T> r){return r.Value;}}
public class Item {public long ID;}
public class Loadout {public Item RosterItem;public Loadout(Item item){RosterItem=item;}}
public class Slot {public Reference<Item>[] Items;}
public class Category {public List<Slot> RosterSlots;}
public class Button {public long ID;}
public class Entry {public Reference<Button> Button;public int Index;}
public class Selection {
 public List<Entry> mProductionButtonScratchpad=new List<Entry>();
 public List<Entry> mUpgradeButtonScratchpad=new List<Entry>();
 public List<Entry> mAbilityButtonScratchpad=new List<Entry>();
}
namespace UnityEngine {
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
 public struct Rect {public float width,height;}
 public class Transform {public bool hasPanel;public Transform parent;public Vector2 anchorMin,anchorMax,pivot,sizeDelta,anchoredPosition;public Vector3 localScale=new Vector3(1,1,1);public Rect rect=new Rect{width=0,height=0};public object GetComponent(Type type){return hasPanel?new Game.UI.CommandPanel():null;}public object Find(string path){return null;}}
 public static class Debug {public static void Log(object o){Console.WriteLine(o);}}
}
namespace UnityEngine.UI {
 public class LayoutGroup {public bool enabled=true;}
 public class Padding {public int left=10,right=10,top=12,bottom=12;}
 public enum Constraint {Flexible,FixedColumnCount,FixedRowCount}
 public class GridLayoutGroup:LayoutGroup {public UnityEngine.Vector2 cellSize=new UnityEngine.Vector2(68,68),spacing=new UnityEngine.Vector2(8,8);public Padding padding=new Padding();public Constraint constraint{get;set;}public int constraintCount{get;set;}}
}
namespace Game.UI {public class CommandPanel {}}
public class MockGameObject {public bool active;public void SetActive(bool value){active=value;}}
public class MockData {public bool HasValue{get;set;}}
public class MockProvider {public MockData[] Data=new MockData[64];public MockData GetItemAtIndex(int index){return Data[index];}}
public class MockUIItem {public MockGameObject gameObject=new MockGameObject();public UnityEngine.Transform transform=new UnityEngine.Transform();}
public class MockList {
 public enum Invalid {Hide,ScaleZero}
 public int m_MaxItems=12;public Invalid m_InvalidDataBehavior=Invalid.ScaleZero;public UnityEngine.Transform transform=new UnityEngine.Transform{parent=new UnityEngine.Transform{hasPanel=true,anchoredPosition=new UnityEngine.Vector2(-6,12)}};public UnityEngine.UI.GridLayoutGroup layout=new UnityEngine.UI.GridLayoutGroup();public List<MockUIItem> ListItems=new List<MockUIItem>();
 public MockProvider DataProvider=new MockProvider();
 public object GetComponent(Type type){return layout;}
}
class RuntimeTests {
 static void Check(bool value,string name){if(!value)throw new Exception(name);}
 static Entry Make(long id){return new Entry{Button=new Reference<Button>{Value=new Button{ID=id}}};}
 static int Main(string[] args){try{
  var shared=new Item{ID=2};var category=new Category{RosterSlots=new List<Slot>{new Slot{Items=new[]{new Reference<Item>{Value=new Item{ID=1}},new Reference<Item>{Value=shared}}},new Slot{Items=new[]{new Reference<Item>{Value=shared},new Reference<Item>{Value=new Item{ID=3}}}}}};
  var roster=new List<Loadout>();CflRuntime.FillAll(category,roster);Check(roster.Count==3 && roster[2].RosterItem.ID==3,"All alternatives registered once");
  var selection=new Selection();for(int i=0;i<25;i++)selection.mProductionButtonScratchpad.Add(Make(i));for(int i=25;i<45;i++)selection.mUpgradeButtonScratchpad.Add(Make(i));selection.mAbilityButtonScratchpad.Add(Make(45));selection.mAbilityButtonScratchpad.Add(Make(1));
  var buttons=new Button[64];CflRuntime.Pack(selection,buttons);var ids=new HashSet<long>();foreach(var b in buttons)if(b!=null)Check(ids.Add(b.ID),"Duplicate not removed");Check(ids.Count==46,"Button lost or overwritten");
  var worker=new Selection();var build=Make(100);build.Index=10;var stop=Make(101);stop.Index=9;worker.mAbilityButtonScratchpad.Add(build);worker.mAbilityButtonScratchpad.Add(stop);
  var workerButtons=new Button[64];CflRuntime.Pack(worker,workerButtons);Check(workerButtons[10].ID==100 && workerButtons[9].ID==101 && workerButtons[0]==null,"Worker build C and native holes must be preserved");
  worker.mProductionButtonScratchpad.Add(Make(102));workerButtons=new Button[64];CflRuntime.Pack(worker,workerButtons);Check(workerButtons[0].ID==102 && workerButtons[10].ID==100,"Production must not overwrite build menu");
  var buildMenu=new Selection();var back=Make(200);back.Index=8;buildMenu.mAbilityButtonScratchpad.Add(back);var structure=Make(201);structure.Index=0;buildMenu.mAbilityButtonScratchpad.Add(structure);var menuButtons=new Button[64];CflRuntime.Pack(buildMenu,menuButtons);Check(menuButtons[8].ID==200 && menuButtons[0].ID==201,"Build submenu and back Z positions");
  CflRuntime.Pack(selection,new Button[64]);
  var list=new MockList();for(int i=0;i<64;i++)list.ListItems.Add(new MockUIItem());CflRuntime.BeforeList(list);CflRuntime.AfterList(list);
  Check(list.m_MaxItems==64 && list.layout.enabled && list.m_InvalidDataBehavior==MockList.Invalid.Hide,"Native grid and hidden empty slots");Check(list.layout.constraintCount==8 && list.transform.parent.sizeDelta.x==620,"Expanded native grid dimensions");
  Check(list.transform.sizeDelta.x==0 && list.ListItems[63].transform.sizeDelta.x==0 && list.ListItems[63].transform.localScale.x==1,"Zero-size native templates must not be manually resized or scaled");
  CflRuntime.Pack(new Selection(),new Button[64]);Check(list.transform.parent.sizeDelta.x==316 && list.transform.parent.sizeDelta.y==244 && list.transform.parent.anchoredPosition.y==12,"Original HUD geometry restored for small selections");
  list.DataProvider.Data[10]=new MockData{HasValue=true};list.ListItems[10].gameObject.SetActive(true);CflRuntime.AfterRefresh(list);
  Check(list.ListItems[0].gameObject.active && list.ListItems[0].transform.localScale.x==0 && list.ListItems[10].gameObject.active && list.ListItems[10].transform.localScale.x==1 && !list.ListItems[12].gameObject.active,"Native holes must retain space without hiding build C or adding empty rows");
  var other=new MockList();other.transform.parent.hasPanel=false;other.transform.parent.parent=new UnityEngine.Transform{hasPanel=true};CflRuntime.BeforeList(other);CflRuntime.AfterList(other);Check(other.m_MaxItems==12 && other.layout.enabled && other.m_InvalidDataBehavior==MockList.Invalid.ScaleZero,"Nested tooltip list inside command panel changed");
  Console.WriteLine("PASS: full roster, 46 distinct buttons, build C at slot 10, submenu/back Z, no production/menu collisions, invisible native holes retain space, native layout preserved, nested lists excluded.");
  if(args.Length>0){int code=SelfTest.Run(args[0]);Console.WriteLine(System.IO.File.ReadAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-result.txt")));return code;}
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
