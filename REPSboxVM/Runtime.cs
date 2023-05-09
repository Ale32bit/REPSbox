using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeraLua;
using System.IO;
using System.Runtime.Serialization;
using System.Timers;

namespace REPSboxVM;

class Runtime : IDisposable
{
    private Lua MainLua;
    private Lua Lua;
    public bool IsRunning = false;
    public bool KillScript = false;
    public readonly Timer YieldTimer = new() {
        AutoReset = false,
        Enabled = false,
        Interval = 1000,
    };

    public readonly string UUID;

    private static readonly Dictionary<string, StringBuilder> Outputs = new();

    public Runtime(string uuid)
    {
        UUID = uuid;
        KillScript = false;

        Outputs[UUID] = new();

        MainLua = new Lua(false);

        Sandbox.OpenLibraries(MainLua);
        Sandbox.Patch(MainLua);

        MainLua.PushString("REPSboxVM v2.0 - Powered by SwitchChat v3");
        MainLua.SetGlobal("_HOST");

        MainLua.PushString(uuid);
        MainLua.PushCClosure(L_PrintOutput, 1);
        MainLua.SetGlobal("print");

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

        YieldTimer.Elapsed += YieldElapsed;

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

    public string PopOutput()
    {
        var builder = Outputs[UUID];
        var output = builder.ToString();
        builder.Clear();
        return output;
    }

    public void YieldElapsed(object source, ElapsedEventArgs e)
    {
        KillScript = true;
    }

    public void Dispose()
    {
        IsRunning = false;
        MainLua.Dispose();
        if (Outputs.ContainsKey(UUID))
            Outputs.Remove(UUID);
    }

    private static int L_PrintOutput(IntPtr state)
    {
        var L = Lua.FromIntPtr(state);

        var nargs = L.GetTop();

        var uuidIndex = Lua.UpValueIndex(1);
        var uuid = L.ToString(uuidIndex);

        if (!Outputs.ContainsKey(uuid))
            Outputs[uuid] = new();

        var output = Outputs[uuid];

        for (int i = 1; i <= nargs; i++)
            output.Append(L.ToString(i));

        output.AppendLine();

        return 0;
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
