using Microsoft.Extensions.Configuration;
using SwitchChatNet;
using SwitchChatNet.Models;
using SwitchChatNet.Models.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace REPSboxVM;

internal class ChatBot
{
    public readonly Dictionary<string, char> TypeColors = new()
    {
        ["string"] = 'c',
        ["number"] = 'd',
        ["boolean"] = '9',
        ["table"] = 'f',
        ["nil"] = '7',
        ["function"] = 'e',
        ["out"] = 'a',
        ["info"] = 'a',
    };

    public readonly Client Client;
    public readonly Dictionary<string, Runtime> Runtimes = new();

    private readonly string _token;
    private readonly IConfiguration _configuration;
    public ChatBot(IConfiguration configuration)
    {
        _configuration = configuration;
        _token = configuration["ChatboxToken"];

        Client = new Client(_token)
        {
            DefaultFormattingMode = SwitchChatNet.Enums.FormattingMode.Format,
            DefaultName = "&9Lua",
        };

        Client.OnReady += OnReady;
        Client.OnChatboxCommand += OnCommand;
        Client.OnLeave += OnLeave;
    }

    public async Task RunAsync()
    {
        await Client.RunAsync();
    }

    private void OnReady(object sender, EventArgs e)
    {
        Console.WriteLine("Connected to Chatbox. Owner is {0}", Client.Owner);
    }

    private void OnCommand(object sender, ChatboxCommand ev)
    {
        var cmd = ev.Command.ToLower();
        var replCommands = _configuration.GetSection("ReplCommands").Get<string[]>();
        var vmCommands = _configuration.GetSection("VmCommands").Get<string[]>();

        var doFunny = _configuration.GetValue("EnableTheFunny", false);

        if (doFunny)
        {
            var singleArg = string.Join(' ', ev.Args);
            var fullComand = cmd + singleArg;
            if (fullComand.StartsWith('='))
            {
                ev.Args = fullComand[1..].Split(' ');
                _ = OnReplCommand(ev);
                return;
            }
        }

        if (replCommands.Contains(cmd))
        {
            _ = OnReplCommand(ev);
        }
        else if (vmCommands.Contains(cmd))
        {
            _ = OnVmCommand(ev);
        }
    }

    private Runtime GetOrCreateRuntime(string uuid)
    {
        if (Runtimes.TryGetValue(uuid, out Runtime runtime))
            return runtime;

        runtime = new(uuid);
        runtime.Run(uuid);

        Runtimes[uuid] = runtime;
        return runtime;
    }

    private void DisposeRuntime(string uuid)
    {
        if (Runtimes.TryGetValue(uuid, out Runtime runtime))
        {
            runtime.Dispose();
            Runtimes.Remove(uuid);
        }
    }

    private static JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private async Task OnReplCommand(ChatboxCommand ev)
    {
        var script = string.Join(" ", ev.Args);

        Console.WriteLine("{0}: {1}", ev.User.Name, script);

        var runtime = GetOrCreateRuntime(ev.User.UUID);

        runtime.YieldTimer.Start();
        try
        {
            runtime.Run(script);
        }
        catch (Exception ex)
        {
            await Client.TellAsync(ev.User.Name, string.Format("Runtime died: {0}", ex.Message));
            runtime.PopOutput();
            return;
        }
        finally
        {
            if (runtime.YieldTimer.Enabled)
                runtime.YieldTimer.Stop();
        }

        var rawOutput = runtime.PopOutput();

        var output = JsonSerializer.Deserialize<List<ReplOutput>>(rawOutput, JsonOptions);
        var message = new StringBuilder();
        message.AppendFormat("&7{0}&f\n", script.TrimStart());

        for (int i = 0; i < output.Count; i++)
        {
            var op = output[i];

            if (op.Error)
            {
                await Client.TellAsync(ev.User.Name, $"&c{op.Value}", "&cLua Error");
                return;
            }

            char color;
            if (!TypeColors.TryGetValue(op.Type, out color))
                color = '7';

            message.AppendFormat("&f[&7{0}&f][&{1}{2}&f] {3}\n", i + 1, color, op.Type, op.Value ?? "nil");
        }

        await Client.TellAsync(ev.User.Name, message.ToString());
    }

    private async Task OnVmCommand(ChatboxCommand ev)
    {
        var command = ev.Args.FirstOrDefault() ?? "help";
        command = command.ToLower();

        if (command == "help")
        {
            await Client.TellAsync(ev.User.Name, "Commands: restart, stop, help");
        }
        else if (command is "restart" or "stop")
        {
            await Client.TellAsync(ev.User.Name, "Lua state stopped!");
            DisposeRuntime(ev.User.UUID);
        }
        else
        {
            await Client.TellAsync(ev.User.Name, "Command not found! Run \"help\" for help.");
        }
    }

    private void OnLeave(object sender, Leave e)
    {
        DisposeRuntime(e.User.UUID);
    }
}
