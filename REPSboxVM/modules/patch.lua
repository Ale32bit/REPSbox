local args = {...};
local tEnv = args[1];
local utils = args[2]
-- patch

local oldChar = string.char

tEnv.string.char = function(...)
	local out = ""
	for k, v in ipairs({...}) do
	    if utils.inArray(utils.illegalBytecode, n) then
    	    out = out .. "#"
	    else
	        out = out .. oldChar(v)
	    end
    end
    
    return out
end

