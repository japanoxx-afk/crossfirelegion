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
 public class Transform {public Transform parent;public Vector2 anchorMin,anchorMax,pivot,sizeDelta,anchoredPosition;public Vector3 localScale;public Rect rect=new Rect{width=80,height=80};}
 public static class Debug {public static void Log(object o){Console.WriteLine(o);}}
}
namespace UnityEngine.UI {public class LayoutGroup {public bool enabled=true;}}
namespace Game.UI {public class CommandPanel {}}
public class MockUIItem {public UnityEngine.Transform transform=new UnityEngine.Transform();}
public class MockList {
 public bool command=true;public int m_MaxItems=12;public UnityEngine.Transform transform=new UnityEngine.Transform();public UnityEngine.UI.LayoutGroup layout=new UnityEngine.UI.LayoutGroup();public List<MockUIItem> ListItems=new List<MockUIItem>();
 public object GetComponentInParent(Type type){return command?new Game.UI.CommandPanel():null;}
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
  Check(list.m_MaxItems==64 && !list.layout.enabled,"Native list capacity and layout");Check(list.transform.sizeDelta.x==480,"HUD dimensions");Check(list.ListItems[63].transform.anchoredPosition.x==420 && list.ListItems[63].transform.anchoredPosition.y==-420,"64th slot is in grid");
  var other=new MockList{command=false};CflRuntime.BeforeList(other);CflRuntime.AfterList(other);Check(other.m_MaxItems==12 && other.layout.enabled,"Non-command list changed");
  Console.WriteLine("PASS: full roster alternatives, de-duplication, 46 unique packed buttons, 8x8 geometry, non-command UI unchanged.");
  if(args.Length>0){int code=SelfTest.Run(args[0]);Console.WriteLine(System.IO.File.ReadAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-result.txt")));return code;}
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
