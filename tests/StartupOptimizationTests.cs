using System;
using System.Collections;
using System.Collections.Generic;
namespace Systems.Online.Steam.Market {
 public class SteamMarketCatalog {public string Currency;public List<object> Items=new List<object>();public SteamMarketCatalog(string currency){Currency=currency;}}
 public class SteamMarketService {public SteamMarketCatalog mMarketCatalog;public Action<SteamMarketCatalog> OnMarketCatalogRetrieved;public int Requests;public bool GetMarketCatalog(){Requests++;return true;}}
}
public class OtherMarketService {public int Requests;public bool GetMarketCatalog(){Requests++;return true;}}
public class MetadataMarketItem {public enum Kind{Item=0,Currency=2}public string AttributeID{get;set;}public string Name{get;set;}public Kind ItemType{get;set;}public int ID;public MetadataMarketItem(int id){ID=id;}}
public class MetadataKey {public int ID;}
public class StartupSceneOwner {public List<string> m_SceneNames=new List<string>{"first"};}
public class StartupSceneSequence:IEnumerator,IDisposable {
 public int Moves;public bool Disposed,Fail;public object Current{get{return Moves;}}
 public bool MoveNext(){Moves++;if(Fail)throw new InvalidOperationException("scene failure");return Moves<3;}
 public void Reset(){throw new NotSupportedException();}public void Dispose(){Disposed=true;}
}
public static class StartupOptimizationTests {
 static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Drain(IEnumerator sequence){while(sequence.MoveNext()){}var disposable=sequence as IDisposable;if(disposable!=null)disposable.Dispose();}
 public static void Run(){
  var market=new Systems.Online.Steam.Market.SteamMarketService();int callbacks=0;market.OnMarketCatalogRetrieved=catalog=>{callbacks++;Require(Object.ReferenceEquals(catalog,market.mMarketCatalog),"Catalog must be published before callback");};
  Require(CflRuntime.SkipStartupMarket(market) && market.Requests==0 && callbacks==1 && market.mMarketCatalog.Items.Count==0,"Startup catalog request was not skipped correctly");
  int mappings=CflRuntime.RegisterLocalInventoryMetadata(market.mMarketCatalog,new object[]{new CatalogMetadataFixture{ID="owned",MarketItemID=23},new ProfileCurrencyItemAttributes{ID="currency",MarketItemID=24}},typeof(MetadataMarketItem));Require(mappings==2,"Inventory metadata registration failed");
  var mapped=(MetadataMarketItem)CflRuntime.LocalInventoryItem(null,market.mMarketCatalog,new MetadataKey{ID=23});Require(mapped.AttributeID=="owned" && Object.ReferenceEquals(mapped,CflRuntime.LocalInventoryItem(null,market.mMarketCatalog,"owned")),"Inventory ID/attribute mapping mismatch");Require(CflRuntime.LocalInventoryCurrency(null,market.mMarketCatalog,new MetadataKey{ID=23})==null && CflRuntime.LocalInventoryCurrency(null,market.mMarketCatalog,new MetadataKey{ID=24})!=null,"Currency metadata filter mismatch");Require(CflRuntime.LocalInventoryItem(null,market.mMarketCatalog,new MetadataKey{ID=999})==null && market.mMarketCatalog.Items.Count==0,"Unknown inventory item or shop entry invented");Require(Object.ReferenceEquals(mapped,CflRuntime.LocalInventoryItem(mapped,market.mMarketCatalog,new MetadataKey{ID=999})),"Native item result replaced");
  var other=new OtherMarketService();Require(CflRuntime.SkipStartupMarket(other) && other.Requests==1,"Other platform market behavior changed");
  var owner=new StartupSceneOwner();var first=new StartupSceneSequence();var wrapped=CflRuntime.LoadStartupScenesOnce(first,owner);Require(wrapped.MoveNext() && (int)wrapped.Current==1,"Scene yield changed");Drain(wrapped);Require(first.Moves==3 && first.Disposed,"Original scene sequence not completed/disposed");
  var duplicate=new StartupSceneSequence();Drain(CflRuntime.LoadStartupScenesOnce(duplicate,owner));Require(duplicate.Moves==0 && duplicate.Disposed,"Completed duplicate loader was executed");
  owner.m_SceneNames.Add("changed");var changed=new StartupSceneSequence();Drain(CflRuntime.LoadStartupScenesOnce(changed,owner));Require(changed.Moves==3,"Changed scene list must load again");
  var retryOwner=new StartupSceneOwner();var failed=new StartupSceneSequence{Fail=true};try{CflRuntime.LoadStartupScenesOnce(failed,retryOwner).MoveNext();throw new Exception("Scene exception swallowed");}catch(InvalidOperationException){}
  var retry=new StartupSceneSequence();Drain(CflRuntime.LoadStartupScenesOnce(retry,retryOwner));Require(retry.Moves==3,"Failed scene load incorrectly cached");
  var cancelledOwner=new StartupSceneOwner();var cancelled=CflRuntime.LoadStartupScenesOnce(new StartupSceneSequence(),cancelledOwner);cancelled.MoveNext();((IDisposable)cancelled).Dispose();var restarted=new StartupSceneSequence();Drain(CflRuntime.LoadStartupScenesOnce(restarted,cancelledOwner));Require(restarted.Moves==3,"Cancelled scene load incorrectly cached");
 }
 public static void AfterMenu(){var sequence=new StartupSceneSequence();Require(Object.ReferenceEquals(sequence,CflRuntime.LoadStartupScenesOnce(sequence,new StartupSceneOwner())),"Scene reuse optimization must stop after startup");}
}
