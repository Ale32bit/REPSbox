using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeraLua;
using System.IO;
using System.Runtime.Serialization;

namespace REPSboxVM;

class Runtime : IDisposable
{
    private Lua MainLua;
    private Lua Lua;
    public bool IsRunning = false;
    public bool KillScript = false;
    public Runtime()
    {
        KillScript = false;

        MainLua = new Lua(false);

        Sandbox.OpenLibraries(MainLua);
        Sandbox.Patch(MainLua);

        MainLua.PushString("REPSboxVM v2.0 - Powered by SwitchChat v3");
        MainLua.SetGlobal("_HOST");

        Lua = MainLua.NewThread();

        Lua.SetHook((luaState, ar) =>
        {
            var state = Lua.FromIntPtr(luaState);

            var arg = LuaDebug.FromIntPtr(ar);

            if (arg.Event == LuaHookEvent.Count)
            {
                if (KillScript)
                {
                    Lua.Error("Yield timeout exception");
                }
            }
        }, LuaHookMask.Count, 7000000);

        var initContent = File.ReadAllText("Lua/init.lua");
        var status = Lua.LoadString(initContent, "@INIT");
        if (status != LuaStatus.OK)
        {
            var error = Lua.ToString(-1);
            throw new LuaException(error);
        }
        IsRunning = true;
    }

    public void Run(string script)
    {
        KillScript = false;
        Lua.PushString(script);
        var status = Lua.Resume(null, 1, out var nres);
        if (status == LuaStatus.OK || status == LuaStatus.Yield)
        {
            Lua.Pop(nres);
            if (status != LuaStatus.OK) return;
            IsRunning = false;
            throw new LuaException(Lua.ToString(-1));
        }

        IsRunning = false;
        throw new LuaException(Lua.OptString(-1, "Unknown Error"));
    }

    public void Dispose()
    {
        Console.WriteLine("Disposing...");
        IsRunning = false;
        MainLua.Dispose();
    }
}

class LuaException : Exception
{
    public LuaException()
    {
    }

    public LuaException(string message) : base(message)
    {
    }

    public LuaException(string message, Exception innerException) : base(message, innerException)
    {
    }

}
