using Microsoft.Extensions.Configuration;
using System.IO;
using System.Threading.Tasks;

namespace REPSboxVM;

internal class Program
{
    public static ChatBot ChatBot { get; private set; }
    public static IConfiguration Configuration { get; private set; }

    private static void Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false);
        Configuration = builder.Build();

        ChatBot = new(Configuration);

        RunAsync().Wait();
    }

    private static async Task RunAsync()
    {
        await ChatBot.RunAsync();
    }
}

