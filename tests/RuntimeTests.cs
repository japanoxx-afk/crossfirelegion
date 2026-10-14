using System;
using System.Collections.Generic;
public class Reference<T>{public T Value;public static implicit operator T(Reference<T> r){return r.Value;}}
public class Item {public long ID;}
public class Loadout {public Item RosterItem;public Loadout(Item item){RosterItem=item;}}
public class Slot {public Reference<Item>[] Items;}
public class Category {public List<Slot> RosterSlots;}
public class Button {public long ID;}
public class Entry {public Reference<Button> Button;}
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
public class MockUIItem {public UnityEngine.Transform transform=new UnityEngine.Transform();}
public class MockList {
 public enum Invalid {Hide,ScaleZero}
 public int m_MaxItems=12;public Invalid m_InvalidDataBehavior=Invalid.ScaleZero;public UnityEngine.Transform transform=new UnityEngine.Transform{parent=new UnityEngine.Transform{hasPanel=true,anchoredPosition=new UnityEngine.Vector2(-6,12)}};public UnityEngine.UI.GridLayoutGroup layout=new UnityEngine.UI.GridLayoutGroup();public List<MockUIItem> ListItems=new List<MockUIItem>();
 public object GetComponent(Type type){return layout;}
}
class RuntimeTests {
 static void Check(bool value,string name){if(!value)throw new Exception(name);}
 static Entry Make(long id){return new Entry{Button=new Reference<Button>{Value=new Button{ID=id}}};}
 static int Main(string[] args){try{
  var shared=new Item{ID=2};var category=new Category{RosterSlots=new List<Slot>{new Slot{Items=new[]{new Reference<Item>{Value=new Item{ID=1}},new Reference<Item>{Value=shared}}},new Slot{Items=new[]{new Reference<Item>{Value=shared},new Reference<Item>{Value=new Item{ID=3}}}}}};
  var roster=new List<Loadout>();CflRuntime.FillAll(category,roster);Check(roster.Count==3 && roster[2].RosterItem.ID==3,"All alternatives registered once");
  var selection=new Selection();for(int i=0;i<25;i++)selection.mProductionButtonScratchpad.Add(Make(i));for(int i=25;i<45;i++)selection.mUpgradeButtonScratchpad.Add(Make(i));selection.mAbilityButtonScratchpad.Add(Make(45));selection.mAbilityButtonScratchpad.Add(Make(1));
  var buttons=new Button[64];CflRuntime.Pack(selection,buttons);for(int i=0;i<46;i++)Check(buttons[i].ID==i,"Button lost or overwritten: "+i);Check(buttons[46]==null,"Duplicate not removed");
  var list=new MockList();for(int i=0;i<64;i++)list.ListItems.Add(new MockUIItem());CflRuntime.BeforeList(list);CflRuntime.AfterList(list);
  Check(list.m_MaxItems==64 && list.layout.enabled && list.m_InvalidDataBehavior==MockList.Invalid.Hide,"Native grid and hidden empty slots");Check(list.layout.constraintCount==8 && list.transform.parent.sizeDelta.x==620,"Expanded native grid dimensions");
  Check(list.transform.sizeDelta.x==0 && list.ListItems[63].transform.sizeDelta.x==0 && list.ListItems[63].transform.localScale.x==1,"Zero-size native templates must not be manually resized or scaled");
  CflRuntime.Pack(new Selection(),new Button[64]);Check(list.transform.parent.sizeDelta.x==316 && list.transform.parent.sizeDelta.y==244 && list.transform.parent.anchoredPosition.y==12,"Original HUD geometry restored for small selections");
  var other=new MockList();other.transform.parent.hasPanel=false;other.transform.parent.parent=new UnityEngine.Transform{hasPanel=true};CflRuntime.BeforeList(other);CflRuntime.AfterList(other);Check(other.m_MaxItems==12 && other.layout.enabled && other.m_InvalidDataBehavior==MockList.Invalid.ScaleZero,"Nested tooltip list inside command panel changed");
  Console.WriteLine("PASS: full roster, 46 distinct buttons, native zero-size templates preserved, grid kept enabled, original 316x244 HUD for small selections, nested lists excluded.");
  if(args.Length>0){int code=SelfTest.Run(args[0]);Console.WriteLine(System.IO.File.ReadAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-result.txt")));return code;}
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
