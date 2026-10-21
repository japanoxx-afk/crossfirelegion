-- Classic-match commander energy. Runs on the game's simulation timer.
local cflEnergyStates = {}
local cflEnergyStarted = false
local cflEnergySteps = 0
local function cflEnergyRate(state)
    if state.hq3 > 0 then return 6 end
    if state.hq2 > 0 then return 5 end
    return 4
end
local function cflEnergyTick()
    cflEnergySteps = cflEnergySteps + 1
    for index, state in ipairs(cflEnergyStates) do
        local rate = cflEnergyRate(state)
        state.credit = state.credit + rate
        local amount = math.floor(state.credit / 12)
        state.credit = state.credit - amount * 12
        if amount > 0 then
            actions.ModifyInventory(state.commander, global.InventoryCommanderEnergy, amount, transactionOperator.Add)
            state.granted = state.granted + amount
        end
        if cflEnergySteps % 12 == 0 then
            print("[CFL Energy] player=" .. index .. " elapsed=" .. (cflEnergySteps * 5) .. " rate=" .. rate .. "/min granted=" .. state.granted)
            state.granted = 0
        end
    end
    Timer.StartNew(5, cflEnergyTick)
end
for faction = 1, 3 do
    for tier = 2, 3 do
        local targetTier = tier
        local tech = assets.GetCarbonAssetByName("ta_f" .. faction .. "_hq_tier" .. tier, assetTypes.TechAttributes)
        Trigger.Setup({{
            Name = "CFL_Energy_HQ_" .. faction .. "_" .. tier,
            Event = Trigger.Event.TechModified(Trigger.Flags.Any, tech, techState.Completed),
            Action = function(data)
                local owner = data.techOwner
                for _, state in ipairs(cflEnergyStates) do
                    if owner == state.commander or utilities.GetCommanderOwnership(owner) == state.commander then
                        local key = targetTier == 3 and "hq3" or "hq2"
                        local delta = data.techModifyOperation == modificationOperation.Remove and -1 or 1
                        state[key] = math.max(0, state[key] + delta)
                        print("[CFL Energy] HQ changed: tier=" .. targetTier .. " rate=" .. cflEnergyRate(state) .. "/min")
                    end
                end
            end
        }})
    end
end
Trigger.Setup({{
    Name = "CFL_Energy_Start",
    OneShot = true,
    Event = Trigger.Event.SetupComplete(),
    Action = function()
        if cflEnergyStarted then return end
        cflEnergyStarted = true
        for _, commander in ipairs(playerCommanders) do
            actions.SetInventoryValue(commander, global.InventoryCommanderEnergy, 0)
            table.insert(cflEnergyStates, {commander=commander, credit=0, granted=0, hq2=0, hq3=0})
        end
        print("[CFL Energy] simulation timer started; players=" .. #cflEnergyStates)
        Timer.StartNew(5, cflEnergyTick)
    end
}})
