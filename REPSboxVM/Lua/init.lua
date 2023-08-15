-- todo: cleanup this old mess not needed anymore

local modules = {
    "patch",
    "command"
}

local json = require("json")
local serpent = require("serpent")

local utils = {}

local function deepCopy(t)
    for k, v in pairs(t) do
        if type(v) == "table" then
            t[k] = deepCopy(v)
        else
            t[k] = v
        end
    end
    return t
end
utils.deepCopy = deepCopy

local nativeEnv = _ENV
utils.nativeEnv = nativeEnv

local illegalBytecode = {0, 1, 2, 3, 4, 5, 6, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31}
utils.illegalBytecode = illegalBytecode

local function inArray(tab, val)
    for _, v in ipairs(tab) do
        if v == val then
            return true
        end
    end
    return false
end

utils.inArray = inArray

local function sanitize(script)
    if not script then
        return nil
    end
    while true do
        local matches = 0
        local match = script:match("\\%d+")
        if match then
            local n = match:gsub("\\", "")
            if inArray(illegalBytecode, tonumber(n)) then
                matches = matches + 1
                script = script:gsub(match, "\\35")
            end
        end
        match = script:match("\\u%d+")
        if match then
            local n = match:gsub("\\u", "")
            if inArray(illegalBytecode, tonumber(n)) then
                matches = matches + 1
                script = script:gsub(match, "\\35")
            end
        end
        if matches == 0 then
            break
        end
    end
    return script
end
utils.sanitize = sanitize

local outputs = 0
local outputValue = {}
local function log(value, typ)
    if outputs >= 1024 then
        error("Output limit exceeded.", 0)
    end
    outputs = outputs + 1
    table.insert(
        outputValue,
        {
            value = sanitize(tostring(value)),
            type = typ or type(value),
            error = false
        }
    )
end
utils.log = log

local function logError(e)
    table.insert(
        outputValue,
        {
            value = sanitize(tostring(e)),
            type = "string",
            error = true
        }
    )
end
utils.logError = logError

table.pack = function(...)
    return {n = select("#", ...), ...}
end

local tEnv = {
    print = function(...)
        local args = {...}
        for k, v in ipairs(args) do
            args[k] = tostring(v)
        end
        log(table.concat(args, " "), "out")
    end,
    _ENV = _ENV,
}

for k, v in pairs(math) do
    if not _G[k] then
        tEnv[k] = v
    end
end

setmetatable(tEnv, {
    __index = _G,
})

-- load modules

--[[for _, file in ipairs(modules) do
    local func, err = loadfile("./modules/" .. file .. ".lua")
    if func then
        func(tEnv, utils)
    else
        log("WARNING: Failed to load module " .. file .. ": " .. err, "info")
    end
end]]

math.randomseed(os.time())

while true do
    outputs = 0;
    outputValue = {}
    local script = coroutine.yield()
    script = script or ""

    if script:sub(1, 1) == " " then
        script = script:sub(2)
    end

    script = script:gsub("^=", "return ")

    local nForcePrint = 0
    local func, e = load(script, "=(REPSbox)", "t", tEnv)
    local func2, e2 = load("return " .. script, "=(REPSbox)", "t", tEnv)
    if not func then
        if func2 then
            func = func2
            e = nil
            nForcePrint = 1
        end
    else
        if func2 then
            func = func2
        end
    end

    if func then
        local tResults = table.pack(pcall(func))
        if tResults[1] then
            local n = 1
            while n < tResults.n or (n <= nForcePrint) do
                local value = tResults[n + 1]
                if type(value) == "table" then
                    local metatable = getmetatable(value)
                    if type(metatable) == "table" and type(metatable.__tostring) == "function" then
                        log(value)
                    else
                        local ok, serialised =
                            pcall(
                            serpent.block,
                            value,
                            {
                                comment = false,
                                fatal = true,
                                nocode = true,
                                maxlevel = 1,
                                metatostring = false,
                                sparse = true
                            }
                        )
                        if ok then
                            log(serialised, "table")
                        else
                            log(value)
                        end
                    end
                else
                    log(value)
                end
                n = n + 1
            end
        else
            logError(tResults[2])
        end
    else
        logError(e)
    end

    print(json.encode(outputValue))
end
