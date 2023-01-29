using System;
using System.IO;
using CommandLine;
using NAudio.CoreAudioApi;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using TEASLibrary;

namespace TEASConsole
{
    class TEASConsole
    {
        /// <summary>
        /// Define command line options
        /// </summary>
        public class Options
        {
            [Option('t', "token", Default = "bottoken.txt", HelpText = "The Discord bot token or a path to a text file that contains the Discord bot token")]
            public string Token { get; set; }

            [Option('d', "device", Default = "", HelpText = "Preselect an audio device by its friendly name")]
            public string PreSeDeviceName { get; set; }

            [Option('a', "admin", Default = "", HelpText = "Specify a Discord user that the bot should accept commands from, in addition to server owners. Format: <Username>#<Discriminator>")]
            public string AdminUserName { get; set; }

            [Option('v', "verbose", Required = false, HelpText = "Enables debug messages")]
            public bool Verbose { get; set; }
        }

        static void Main(string[] args)
        {
            // Initialise Serilog
            var logLevelSwitch = new LoggingLevelSwitch();
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(logLevelSwitch)
                .WriteTo.Console()
                .CreateLogger();

            Version appVersion = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
            Log.Information("Welcome to TEASConsole, version {0}", appVersion.ToString(3));

            // Check for updates
            if (Helpers.CheckUpdate(appVersion) == 1)
                Log.Warning("A newer version of TEASConsole is available. Please download it from the Releases section on GitHub: https://github.com/dandln/TheEpicAudioStreamer/releases/");

            // Parse command line options
            string BotToken = "";
            string AudioDeviceName = "";
            string AdminUserName = "";
            bool Verbose = false;
            CommandLine.Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o =>
            {
                // Check if token file exists or validate passed bot token instead
                try
                {
                    BotToken = File.ReadAllText(o.Token);
                }
                catch (FileNotFoundException)
                {
                    if (o.Token.Length == 59 && o.Token.Contains('.'))
                    {
                        BotToken = o.Token;
                    }
                    else
                    {
                        Log.Error("Bot token file not found and no valid token given");
                        return;
                    }
                }

                // Parse options
                AudioDeviceName = o.PreSeDeviceName;
                AdminUserName = o.AdminUserName;
                Verbose = o.Verbose;
            });

            if (Verbose)
                logLevelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Debug;
            
            if (BotToken == "")
            {
                Log.Fatal("Empty bot token. Exiting...");
                return;
            }

            // Get an audio device from the user
            MMDevice AudioDevice;
            AudioDevice = Helpers.SelectDevice(AudioDeviceName);
            while (AudioDevice == null)
            {
                Console.WriteLine("No valid audio device selected. Try again.\n");
                AudioDevice = Helpers.SelectDevice(AudioDeviceName);
            }
            Log.Information("Chosen audio device is {0}", AudioDevice.DeviceFriendlyName);

            if (AdminUserName != "")
                Log.Information("The bot will accept commands from user {0}", AdminUserName);

            // When all options and configurations are parsed, create a new bot object and run it.
            var logFactory = new LoggerFactory().AddSerilog();
            Bot bot = new(BotToken, logFactory, AdminUserName, AudioDevice, Verbose);
            bot.Connect().GetAwaiter().GetResult();
        }
    }
}