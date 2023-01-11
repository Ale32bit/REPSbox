local args = {...};
local tEnv = args[1];
local utils = args[2]


package.loaded.command = {}
function tEnv.command.exec()
	error("Did you honestly think I would allow this?", 0)
end
