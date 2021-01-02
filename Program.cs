using System;
using System.IO;
using CommandLine;
using DSharpPlus;
using DSharpPlus.CommandsNext;

namespace TheEpicAudioStreamer
{
    class Program
    {
        /// <summary>
        /// Define command line options for the program.
        /// </summary>
        public class Options
        {
            [Option('t', "tokenfile", Default = "bottoken.txt", HelpText = "Path to a text file that contains the Discord bot token.")]
            public string Token { get; set; }
        }

        static void Main(string[] args)
        {
            // Print welcome message.
            Console.WriteLine("#####################################################\n" +
                "##              TheEpicAudioStreamer               ##\n" +
                "##               by Daniel Sonntag                 ##\n" +
                "##                    v0.1.0                       ##\n" +
                "##    A Discord bot that streams audio from an     ##\n" +
                "##         local device to a voice cahnnel.        ##\n" +
                "#####################################################\n");

            // Parse bot token
            string BotToken = "";
            CommandLine.Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o =>
            {
                // Read bot token file and exit if file not found.
                try
                {
                    BotToken = File.ReadAllText(o.Token);
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine("ERROR: Bot token file not found. Exiting...");
                    return;
                }
            });
            if (BotToken == "")
            {
                Console.WriteLine("ERROR: Empty bot token. Exiting...");
                return;
            }

            // Create Discord configuration
            DiscordConfiguration config = new DiscordConfiguration()
            {
                Token = BotToken,
                TokenType = TokenType.Bot,
                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Information
            };

            // Create Discord commands configuration
            CommandsNextConfiguration cmdsConfig = new CommandsNextConfiguration()
            {
                StringPrefixes = new[] { "!" }
            };

            // When all options and configurations are parsed, create a new bot object and run it.
            AudioBot bot = new AudioBot(config, cmdsConfig);
            bot.RunBot().GetAwaiter().GetResult();
        }
    }
}