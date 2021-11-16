using System;
using System.IO;
using CommandLine;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using NAudio.CoreAudioApi;

namespace TheEpicAudioStreamer
{
    class Program
    {
        /// <summary>
        /// Define command line options for the program.
        /// </summary>
        public class Options
        {
            [Option('t', "token", Default = "bottoken.txt", HelpText = "The Discord bot token or a path to a text file that contains the Discord bot token.")]
            public string Token { get; set; }

            [Option('p', "prefix", Default = "!", HelpText = "The command prefix used for the bot to recognise commands.")]
            public string Prefix { get; set; }

            [Option('d', "device", Default = "", HelpText = "Preselect an audio device by its friendly name.")]
            public string PreSeDeviceName { get; set; }

            [Option('a', "admin", Default = "", HelpText = "Specify a Discord user that the bot should accept commands from, in addition to server owners. Format: <Username>#<Discriminator>")]
            public string AdminUserName { get; set; }

            [Option('v', "verbose", Required = false, HelpText = "Enables debug messages from DSharpPlus.")]
            public bool Verbose { get; set; }
        }

        static void Main(string[] args)
        {
            // Print welcome message.
            Console.WriteLine(
                "-------------------------------------------\n" +
                " TheEpicAudioStreamer                      \n" +
                "------------v" + System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString() + "--by @TheEpicSnowWolf-- \n");

            // Check for updates.
            if (Helpers.UpdateAvailable() == true)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("A newer version of this application is available. Please download it from the Releases section on GitHub:\nhttps://github.com/TheEpicSnowWolf/TheEpicAudioStreamer/releases/\n");
                Console.ResetColor();
            }

            // Parse command line options
            string BotToken = "";
            string Prefix = "";
            string AudioDeviceName = "";
            string AdminUserName = "";
            bool Verbose = false;
            CommandLine.Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o =>
            {
                // Check if token file exists or validate passed bot token instead.
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
                        Console.WriteLine("ERROR: Bot token file not found and no valid token given. Exiting...");
                        return;
                    }
                }

                // Parse given prefix.
                Prefix = o.Prefix;

                // Parse given audio device.
                AudioDeviceName = o.PreSeDeviceName;

                // Parse admin username.
                AdminUserName = o.AdminUserName;

                // Parse verbose option.
                Verbose = o.Verbose;
            });

            if (BotToken == "")
            {
                Console.WriteLine("ERROR: Empty bot token. Exiting...");
                return;
            }

            if (AdminUserName != "")
                Console.WriteLine($"The bot will accept commands from user {AdminUserName}.");

            // Get an audio device from the user
            MMDevice AudioDevice;
            AudioDevice = Helpers.SelectDevice(AudioDeviceName);
            while (AudioDevice == null)
            {
                Console.WriteLine("No valid audio device selected. Try again.\n");
                AudioDevice = Helpers.SelectDevice(AudioDeviceName);
            }

            // Create Discord configuration
            DiscordConfiguration config = new DiscordConfiguration()
            {
                Token = BotToken,
                TokenType = TokenType.Bot
            };

            if (Verbose)
                config.MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Debug;
            else
                config.MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Information;

            // Create Discord commands configuration
            CommandsNextConfiguration cmdsConfig = new CommandsNextConfiguration()
            {
                StringPrefixes = new[] { Prefix }
            };

            // When all options and configurations are parsed, create a new bot object and run it.
            AudioBot bot = new AudioBot(config, cmdsConfig, AudioDevice, AdminUserName);
            bot.RunBot().GetAwaiter().GetResult();
        }
    }
}