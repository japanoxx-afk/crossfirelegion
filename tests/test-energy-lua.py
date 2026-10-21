from pathlib import Path
from lupa import LuaRuntime
lua=LuaRuntime(unpack_returned_tuples=True)
harness='''
now=0; pending={}; triggers={}; balances={}; logs={}; playerCommanders={1,2,3}
assets={GetCarbonAssetByName=function(name) return name end}; assetTypes={TechAttributes=1}
global={InventoryCommanderEnergy="energy"}; transactionOperator={Add=1}; modificationOperation={Add=1,Remove=2}; techState={Completed=4}
utilities={GetCommanderOwnership=function(owner) return owner end}
actions={SetInventoryValue=function(owner,inventory,value) balances[owner]=value end,ModifyInventory=function(owner,inventory,value,op) assert(op==1);balances[owner]=balances[owner]+value end}
print=function(message) table.insert(logs,message) end
Trigger={Flags={Any=0},Event={SetupComplete=function()return {}end,TechModified=function(owner,tech,state)return {tech=tech}end},Setup=function(list)for _,trigger in ipairs(list)do triggers[trigger.Name]=trigger end end}
Timer={StartNew=function(seconds,callback)table.insert(pending,{due=now+seconds,callback=callback})end}
function advance(seconds)local target=now+seconds;while true do local index=nil;local due=target+1;for i,item in ipairs(pending)do if item.due<due then index=i;due=item.due end end;if not index then break end;now=due;local item=table.remove(pending,index);item.callback()end;now=target end
function upgrade(faction,tier,operation)triggers["CFL_Energy_HQ_"..faction.."_"..tier].Action({techOwner=faction,techModifyOperation=operation})end
'''
lua.execute(harness)
lua.execute(Path('src/CommanderEnergy.lua').read_text(encoding='utf-8-sig'))
checks='''
triggers.CFL_Energy_Start.Action();triggers.CFL_Energy_Start.Action();assert(#pending==1)
advance(14);for i=1,3 do assert(balances[i]==0) end
advance(1);for i=1,3 do assert(balances[i]==1) end
advance(45);for i=1,3 do assert(balances[i]==4);upgrade(i,2,1) end
advance(60);for i=1,3 do assert(balances[i]==9);upgrade(i,3,1) end
advance(60);for i=1,3 do assert(balances[i]==15);upgrade(i,3,1) end
advance(60);for i=1,3 do assert(balances[i]==21);upgrade(i,3,2);upgrade(i,3,2) end
advance(60);for i=1,3 do assert(balances[i]==26);upgrade(i,2,2) end
advance(60);for i=1,3 do assert(balances[i]==30) end
assert(#pending==1)
'''
lua.execute(checks)
print('PASS: actual Lua script executes starting energy 0, first credit at 15s, three factions 4/5/6 per minute, downgrade, no HQ stacking, no duplicate setup timer.')

import sys
if len(sys.argv)>1:
    native=LuaRuntime(unpack_returned_tuples=True)
    native.execute(harness)
    native.execute("""
    eventType={EveryFrame=1};events={AddListener=function(event,callback) frameCallback=callback end}
    utilities.TicksPerSecond=function()return 10 end
    table.containsValue=function(list,value)for _,item in pairs(list)do if item==value then return true end end return false end
    table.removeValue=function(list,value)for i,item in pairs(list)do if item==value then table.remove(list,i);return end end end
    """)
    native.execute(Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
    native.execute("advance=function(seconds)for tick=1,seconds*10 do frameCallback({})end now=now+seconds end")
    native.execute(Path('src/CommanderEnergy.lua').read_text(encoding='utf-8-sig'))
    native.execute(checks.replace(';assert(#pending==1)','').replace('assert(#pending==1)',''))
    print('PASS: same income scenarios execute with the installed game Timer.lua implementation at 10 simulation ticks/sec.')
