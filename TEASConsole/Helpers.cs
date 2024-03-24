using NAudio.CoreAudioApi;
using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using Serilog;
using TEASLibrary;
using System.Diagnostics;
using System.CodeDom;
using System.Threading.Channels;
using System.Linq;
using static TEASLibrary.ConfigManager;

namespace TEASConsole
{
    /// <summary>
    /// Provides static helper functions for TEASConsole
    /// </summary>
    internal static class Helpers
    {
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
                using HttpClient client = new();
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
        /// Prompts the user to select an audio playback device in the command console, or prepare a
        /// pre-selected one.
        /// </summary>
        /// <param name="deviceName">Optional: The friendly device name pre-selected by the user</param>
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

                if (audioDevice != null)
                    return audioDevice;

                // A device name was given, but it is invalid
                Log.Warning("\"{0}\" was pre-defined as an audio device, but it either does not exist or is unavailable.", deviceName);
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

        /// <summary>
        /// Runs an interactive config creator in the console. Config properties can be pre-populated through reading an existing
        /// config file and/or overwritten by arguments.
        /// </summary>
        /// <param name="configFilePath">The filepath at which the config will be saved</param>
        /// <param name="preSetGuildId">Optionally pre-set a Guild ID to be used in the config</param>
        /// <param name="preSetBotToken">Optionally pre-set a bot token to be used in the config</param>
        /// <param name="preSetAudioDeviceName">Optionally pre-set an audio device name to be used in the config</param>
        /// <param name="preSetChannelId">Optionally pre-set a Discord channel ID to be used in the config</param>
        /// <param name="preSetAdminUsers">Optionally pre-set a comma-seperated list of admin users to be used in the config</param>
        /// <param name="preSetAdminRoles">Optionally pre-set a comma-separated list of admin roles to be used in the config</param>
        /// <returns>The ConfigManager object of the created config, or null if the config was invalid</returns>
        public static ConfigManager? RunInteractiveConfig(string configFilePath,
                                                    string preSetGuildId = "",
                                                    string preSetBotToken = "",
                                                    string preSetAudioDeviceName = "",
                                                    string preSetChannelId = "",
                                                    string preSetAdminUsers = "",
                                                    string preSetAdminRoles = "")
        {
            ConfigManager createdConfig;

            // Create config object either through parsing existing config file or by using the pre set parameters
            if (File.Exists(configFilePath))
            {
                createdConfig = new ConfigManager(configFilePath);

                // Override config file parameters if set through arguments
                if (!string.IsNullOrWhiteSpace(preSetGuildId))
                    createdConfig.GuildID = preSetGuildId;
                if (!string.IsNullOrWhiteSpace(preSetBotToken))
                    createdConfig.BotToken = preSetBotToken;
                if (!string.IsNullOrWhiteSpace(preSetAudioDeviceName))
                    createdConfig.DefaultDeviceFriendlyName = preSetAudioDeviceName;
                if (!string.IsNullOrWhiteSpace(preSetChannelId))
                    createdConfig.DefaultChannelID = preSetChannelId;
                if (!string.IsNullOrWhiteSpace(preSetAdminUsers))
                    createdConfig.AdminUsers = preSetAdminUsers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                if (!string.IsNullOrWhiteSpace(preSetAdminRoles))
                    createdConfig.AdminRoles = preSetAdminRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }
            else
                createdConfig = new ConfigManager(preSetGuildId, preSetBotToken, preSetAudioDeviceName, preSetChannelId, preSetAdminUsers, preSetAdminRoles);

            Console.WriteLine(
                "------------------------------------------------------------\n" +
                "Welcome to the interactive TEAS configuration file creator!\n" +
                "If an existing file was parsed or options given through CLI\n" +
                "arguments, defaults will be set accordingly.\n" +
                "------------------------------------------------------------"
                );

            // GET GUILD ID
            Console.WriteLine(
                "STEP 1: DISCORD GUILD ID\n" +
                "TEASConsole will make itself available to a specific Guild\n" +
                "(i.e. a Discord Server). Right click on the desired server\n" +
                "in Discord (make sure developer mode is enabled), then paste\n" +
                "the ID below."
                );
            if (!string.IsNullOrWhiteSpace(createdConfig.GuildID))
                Console.WriteLine("Leave blank for default: " + createdConfig.GuildID);
            Console.Write("> ");
            string usrInptGuildId = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(usrInptGuildId))
                createdConfig.GuildID = usrInptGuildId;
            Console.WriteLine("------------------------------------------------------------");

            // GET BOT TOKEN
            Console.WriteLine(
                "STEP 2: DISCORD BOT TOKEN\n" +
                "TEASConsole needs to connect to a Discord application.\n" +
                "Copy the bot token from the developer portal and paste it below.\n" +
                "See the TEASConsole Readme for more information on how to get this."
                );
            if (File.Exists("bottoken.txt"))
                Console.WriteLine(
                    "A \"bottoken.txt\" file was found in your TEASConsole folder!\n" +
                    "Leave the input blank to use this token:\n" +
                    createdConfig.BotToken
                    );
            else if (!string.IsNullOrWhiteSpace(createdConfig.BotToken))
                Console.WriteLine("Leave blank for default: " + createdConfig.BotToken);
            Console.Write("> ");
            string usrInptBotToken = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(usrInptBotToken))
                createdConfig.BotToken = usrInptBotToken;
            Console.WriteLine("------------------------------------------------------------");

            // GET AUDIO DEVICE
            Console.WriteLine(
                "STEP 3: DEFAULT AUDIO DEVICE (optional)\n" +
                "Type the ID or name of an audio device listed below to\n" +
                "automatically use with every TEAS session."
                );
            if (!string.IsNullOrWhiteSpace(createdConfig.DefaultDeviceFriendlyName))
                Console.WriteLine("Leave blank for default: " + createdConfig.DefaultDeviceFriendlyName);
            else
                Console.WriteLine("Leave blank to skip. You will then be asked\n" +
                    "to select a device at every startup.");
            List<MMDevice> tempDevicesList = new DeviceManager().OutputDevicesList;
            foreach (MMDevice device in tempDevicesList)
            {
                Console.WriteLine($"[{tempDevicesList.IndexOf(device)}] {device.DeviceFriendlyName}");
            }
            Console.Write("> ");
            string usrInptAudioDevice = Console.ReadLine();
            if (int.TryParse(usrInptAudioDevice, out int n))
            {
                try { createdConfig.DefaultDeviceFriendlyName = tempDevicesList[n].DeviceFriendlyName; }
                catch (ArgumentOutOfRangeException) { Console.WriteLine("Device ID out of range. Continuing without default device..."); }
            }
            else if (!string.IsNullOrWhiteSpace(usrInptAudioDevice))
                createdConfig.DefaultDeviceFriendlyName = usrInptAudioDevice;
            Console.WriteLine("------------------------------------------------------------");

            // GET CHANNEL ID
            Console.WriteLine(
                "STEP 4: DEFAULT CHANNEL ID (optional)\n" +
                "You can define a Discord voice channel that the bot\n" +
                "should automatically connect to on startup. Right click on the\n" +
                "channel (make sure dev mode is enabled) and paste the ID below."
                );
            if (!string.IsNullOrWhiteSpace(createdConfig.DefaultChannelID))
                Console.WriteLine("Leave blank for default: " + createdConfig.DefaultChannelID);
            else
                Console.WriteLine("Leave blank to skip.");
            Console.Write("> ");
            string usrInptChannel = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(usrInptChannel))
            {
                try { Int64.Parse(usrInptChannel); createdConfig.DefaultChannelID = usrInptChannel; }
                catch (Exception) { Console.WriteLine("Channel ID is invalid. Continuing without default channel..."); }
            }
            Console.WriteLine("------------------------------------------------------------");

            // GET ADMIN USERS
            Console.WriteLine(
                "STEP 5: ADMIN USERS (optional)\n" +
                "You can enter a comma-separated list of admin users below.\n" +
                "These will be able to issue commands to the bot."
                );
            if (createdConfig.AdminUsers.Count > 0)
                Console.WriteLine("Leave blank for default: " + string.Join(',', createdConfig.AdminUsers.ToArray()));
            else
                Console.WriteLine("Leave blank to skip.");
            Console.Write("> ");
            string usrInptAdminUsers = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(usrInptAdminUsers))
                createdConfig.AdminUsers = usrInptAdminUsers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            Console.WriteLine("------------------------------------------------------------");

            // GET ADMIN ROLES
            Console.WriteLine(
                "STEP 6: ADMIN ROLES (optional)\n" +
                "You can enter a comma-separated list of admin roles below.\n" +
                "Users with these roles are able to issue commands to the bot."
                );
            if (createdConfig.AdminRoles.Count > 0)
                Console.WriteLine("Leave blank for default: " + string.Join(',', createdConfig.AdminRoles.ToArray()));
            else
                Console.WriteLine("Leave blank to skip.");
            Console.Write("> ");
            string usrInptAdminRoles = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(usrInptAdminRoles))
                createdConfig.AdminRoles = usrInptAdminRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            Console.WriteLine("------------------------------------------------------------");

            // VALIDATE CONFIG
            Console.Write("That's everything! Validating config... ");
            try {
                createdConfig.Validate();
            }
            catch (ConfigValidationFailedException ex)
            {
                Console.WriteLine("Config validation failed! " + ex.Message);
                return null;
            }

            // SAVE CONFIG AND RETURN
            Console.WriteLine("Success!");
            Console.WriteLine("Enter a file name or full path at which the config\n" +
                "will be saved, or leave empty for default: " + configFilePath);
            Console.Write("> ");
            string usrInptFilePath = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(usrInptFilePath))
                configFilePath = usrInptFilePath;
            try
            {
                createdConfig.Write(configFilePath);
                Console.WriteLine("Config file saved. Returning to TEASConsole...");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occured while saving the config file: " + ex.Message + "\n" +
                    "The generated configuration will be used for this session, but not saved.\n" +
                    "Returning to TEASConsole...");
                Console.WriteLine();
            }

            return createdConfig;
        }
    }
}
