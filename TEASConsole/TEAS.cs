using System;
using System.IO;
using System.Collections.Generic;
using CommandLine;
using NAudio.CoreAudioApi;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using TEASLibrary;
using System.Net.Http;
using System.Text.RegularExpressions;

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
            if (CheckUpdate(appVersion) == 1)
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
            AudioDevice = SelectDevice(AudioDeviceName);
            while (AudioDevice == null)
            {
                Console.WriteLine("No valid audio device selected. Try again.\n");
                AudioDevice = SelectDevice(AudioDeviceName);
            }
            Log.Information("Chosen audio device is {0}", AudioDevice.DeviceFriendlyName);

            if (AdminUserName != "")
                Log.Information("The bot will accept commands from user {0}", AdminUserName);

            // When all options and configurations are parsed, create a new bot object and run it.
            var logFactory = new LoggerFactory().AddSerilog();
            Bot bot = new(BotToken, logFactory, AdminUserName, AudioDevice, Verbose);
            bot.Connect().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Checks whether an update for TEASConsole is available by looking at the latest release on GitHub
        /// </summary>
        /// <param name="appVersion">The application version to compare to the newest version</param>
        /// <returns>1 if a newer version is available, 0 if no newer version is available, -1 if update check resulted in an error</returns>
        public static int CheckUpdate(Version appVersion)
        {
            string latestReleaseJson = "";
            try
            {
                // Download latest release information from GitHub
                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "TEASConsole Update Checker");
                latestReleaseJson = client.GetStringAsync("https://api.github.com/repos/dandln/theepicaudiostreamer/releases/latest").Result;
            }
            catch (Exception)
            {
                return -1;  // Update check failed
            }

            // Search for latest version
            var match = new Regex(@"""tag_name"":""v(.+?)""").Match(latestReleaseJson);

            // Version number not found
            if (match.Success == false)
                return -1;

            string latestVersion = match.Groups[1].Value;

            // Compare latest version to application
            var comparison = appVersion.CompareTo(new Version(latestVersion));
            if (comparison < 0)
                return 1;   // Newer version is available
            else
                return 0;   // No newer version available
        }

        /// <summary>
        /// Prompts the user to select an audio playback device in the command console
        /// </summary>
        /// <param name="deviceName">Optional: The friendly device name already preselected by the user</param>
        /// <returns>The audio device, or null if no valid device was selected</returns>
        public static MMDevice SelectDevice(string deviceName = "")
        {
            DeviceManager devMgr = new();
            List<MMDevice> devicesList = devMgr.OutputDevicesList;
            MMDevice audioDevice;
            int userDeviceID;

            // If a preselected device name is given, use this device
            if (deviceName != "")
            {
                audioDevice = devMgr.FindOutputDeviceByDeviceFriendlyName(deviceName);

                if(audioDevice != null)
                {
                    Log.Information("The device \"{0}\" is being used in this session as per command line argument", audioDevice.DeviceFriendlyName);
                    return audioDevice;
                }

                // A device name was given, but it is invalid
                Log.Warning("\"{0}\" was given as a device name via command line argument, but it either does not exist or is unavailable", deviceName);
            }

            // Prompt user to select a readable device ID
            Console.WriteLine("\nPlease type the ID of the audio device you want to stream from:");
            foreach (MMDevice device in devicesList)
            {
                Console.WriteLine($"[{devicesList.IndexOf(device)}] {device.DeviceFriendlyName} - {device.FriendlyName}");
            }
            Console.Write("> ");
            string userInput = Console.ReadLine();

            // Parse user input and get audio device
            try
            {
                userDeviceID = int.Parse(userInput);
                audioDevice = devicesList[userDeviceID];
            }
            catch (FormatException)
            {
                Console.WriteLine("Incorrect input. Integer ID expected.");
                return null;
            }
            catch (ArgumentOutOfRangeException)
            {
                Console.WriteLine("Not a valid device ID.");
                return null;
            }

            Console.WriteLine();
            return audioDevice;
        }
    }
}