using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using Spectre.Console;
using Serilog;
using NAudio.CoreAudioApi;
using TEASLibrary;
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

            // If a preselected device name is given, use this device
            if (deviceName != "")
            {
                audioDevice = devMgr.FindOutputDeviceByDeviceFriendlyName(deviceName);

                if (audioDevice != null)
                    return audioDevice;

                // A device name was given, but it is invalid
                Log.Warning("\"{0}\" was pre-defined as an audio device, but it either does not exist or is unavailable.", deviceName);
            }

            // Prompt user to select a device
            List<MMDevice> tempDevicesList = new DeviceManager().OutputDevicesList;
            AnsiConsole.WriteLine("Select an audio device to stream from:");
            SelectionPrompt<string> devicePrompt = new SelectionPrompt<string>().PageSize(10).MoreChoicesText("[dim]Move down for more options[/]").HighlightStyle("turquoise2");
            foreach (MMDevice device in tempDevicesList)
                devicePrompt.AddChoice(device.DeviceFriendlyName);
            string userInput = AnsiConsole.Prompt(devicePrompt);
            audioDevice = devMgr.FindOutputDeviceByDeviceFriendlyName(userInput);
            AnsiConsole.MarkupLine($"[italic][dim]Selected {userInput}[/][/]");

            AnsiConsole.WriteLine();
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

            AnsiConsole.Write(new Rule("Welcome to the TEAS configuration creator!").RuleStyle("turquoise2").LeftJustified());
            AnsiConsole.WriteLine(
                "This will take you through the settings necessary to connect TEAS to your Discord Guild.\n" +
                "If an existing file was parsed or options were given through CLI arguments, defaults will be set accordingly.\n" +
                "Required settings will have yellow headings, optional settings green ones.\n"
                );

            // GET GUILD ID
            AnsiConsole.Write(new Rule("Step 1: Discord Guild ID").RuleStyle("yellow3_1").LeftJustified());
            AnsiConsole.MarkupLine(
                "TEAS will make itself available to a specific Guild, i.e. a Discord Server.\n" +
                "Right click on the desired Guild in Discord [italic](make sure developer mode is enabled)[/],\n" +
                "then paste the ID below."
                );
            string usrInptGuildId = AnsiConsole.Prompt(new TextPrompt<string>("> ")
                .DefaultValue(!string.IsNullOrEmpty(createdConfig.GuildID) ? createdConfig.GuildID : "")
                .DefaultValueStyle("grey")
                );
            if (!string.IsNullOrWhiteSpace(usrInptGuildId))
                createdConfig.GuildID = usrInptGuildId;
            AnsiConsole.WriteLine();

            // GET BOT TOKEN
            AnsiConsole.Write(new Rule("Step 2: Bot Token").RuleStyle("yellow3_1").LeftJustified());
            AnsiConsole.MarkupLine(
                "TEAS needs to connect to a Discord application.\n" +
                "Copy the bot token from the developer portal and paste it below.\n" +
                "See the [link=https://github.com/dandln/TheEpicAudioStreamer]TEAS Readme[/] for a guide on how to get this."
                );
            if (File.Exists("bottoken.txt"))
                AnsiConsole.MarkupLine(
                    "A [italic]\"bottoken.txt\"[/] file was found in your TEASConsole folder!\n" +
                    "[dim]Leave the input blank to use this token:\n" +
                    createdConfig.BotToken + "[/]"
                    );
            string usrInptBotToken = AnsiConsole.Prompt(new TextPrompt<string>("> ")
                .DefaultValue(!string.IsNullOrEmpty(createdConfig.BotToken) ? createdConfig.BotToken : "")
                .DefaultValueStyle("grey")
                );
            if (!string.IsNullOrWhiteSpace(usrInptBotToken))
                createdConfig.BotToken = usrInptBotToken;
            AnsiConsole.WriteLine();

            // GET AUDIO DEVICE
            AnsiConsole.Write(new Rule("Step 3: Default Audio Device (optional)").RuleStyle("lightgreen_1").LeftJustified());
            AnsiConsole.WriteLine("Select an audio device listed below to automatically use with every TEAS session.");
            if (string.IsNullOrWhiteSpace(createdConfig.DefaultDeviceFriendlyName))
                AnsiConsole.MarkupLine("[dim]If you don't select one now, you will be asked to do so at every startup.[/]");
            List<MMDevice> tempDevicesList = new DeviceManager().OutputDevicesList;

            SelectionPrompt<string> devicePrompt = new SelectionPrompt<string>().PageSize(10).MoreChoicesText("[dim]Move down for more options[/]").HighlightStyle("turquoise2");
            devicePrompt.AddChoice("Skip");
            foreach (MMDevice device in tempDevicesList)
                devicePrompt.AddChoice(device.DeviceFriendlyName);

            string usrInptAudioDevice = AnsiConsole.Prompt(devicePrompt);
            if (usrInptAudioDevice == "Skip")
            {
                createdConfig.DefaultDeviceFriendlyName = "";
                AnsiConsole.MarkupLine("[italic][dim]No device selected.[/][/]");
            }
            else
            {
                createdConfig.DefaultDeviceFriendlyName = usrInptAudioDevice;
                AnsiConsole.MarkupLine($"[italic][dim]Selected {usrInptAudioDevice}[/][/]");
            }
            AnsiConsole.WriteLine();

            // GET CHANNEL ID
            AnsiConsole.Write(new Rule("Step 4: Default Channel (optional)").RuleStyle("lightgreen_1").LeftJustified());
            AnsiConsole.MarkupLine(
                "You can define a Discord voice channel that TEAS automatically connects to on startup.\n" +
                "Right click on the channel [italic](make sure dev mode is enabled[/] and paste the ID below."
                );
            if (string.IsNullOrWhiteSpace(createdConfig.DefaultChannelID))
                AnsiConsole.MarkupLine("[dim]Leave blank to skip.[/]");
            string usrInptChannel = AnsiConsole.Prompt(new TextPrompt<string>("> ")
                .AllowEmpty()
                .DefaultValue(!string.IsNullOrEmpty(createdConfig.DefaultChannelID) ? createdConfig.DefaultChannelID : "")
                .DefaultValueStyle("grey")
                .Validate(id =>
                {
                    if (Int64.TryParse(id, out Int64 num) || id == "")
                        return ValidationResult.Success();
                    else
                        return ValidationResult.Error("[red]Channel ID can only consist of numbers.[/]");
                }
                ));
            createdConfig.DefaultChannelID = usrInptChannel;
            AnsiConsole.WriteLine();

            // GET ADMIN USERS
            AnsiConsole.Write(new Rule("Step 5: Admin Users (optional)").RuleStyle("lightgreen_1").LeftJustified());
            AnsiConsole.WriteLine(
                "You can enter a comma-separated list of admin users below.\n" +
                "These will be able to issue commands to the bot."
                );
            if (createdConfig.AdminUsers.Count == 0)
                AnsiConsole.MarkupLine("[dim]Leave blank to skip.[/]");
            string usrInptAdminUsers = AnsiConsole.Prompt(new TextPrompt<string>("> ")
                .AllowEmpty()
                .DefaultValue(createdConfig.AdminUsers.Count > 0 ? string.Join(',', createdConfig.AdminUsers.ToArray()) : "")
                .DefaultValueStyle("grey")
                );
            if (!string.IsNullOrWhiteSpace(usrInptAdminUsers))
                createdConfig.AdminUsers = usrInptAdminUsers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            AnsiConsole.WriteLine() ;

            // GET ADMIN ROLES
            AnsiConsole.Write(new Rule("Step 6: Admin Roles (optional)").RuleStyle("lightgreen_1").LeftJustified());
            AnsiConsole.WriteLine(
                "You can enter a comma-separated list of admin roles below.\n" +
                "Users with these roles are able to issue commands to the bot."
                );
            if (createdConfig.AdminRoles.Count == 0)
                AnsiConsole.MarkupLine("[dim]Leave blank to skip.[/]");
            string usrInptAdminRoles = AnsiConsole.Prompt(new TextPrompt<string>("> ")
                .AllowEmpty()
                .DefaultValue(createdConfig.AdminRoles.Count > 0 ? string.Join(',', createdConfig.AdminRoles.ToArray()) : "")
                .DefaultValueStyle("grey")
                );
            if (!string.IsNullOrWhiteSpace(usrInptAdminRoles))
                createdConfig.AdminRoles = usrInptAdminRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            AnsiConsole.WriteLine();

            // VALIDATE CONFIG
            AnsiConsole.Write(new Rule("That's all!").RuleStyle("turquoise2").LeftJustified());
            AnsiConsole.Write("Validating config... ");
            try
            {
                createdConfig.Validate();
            }
            catch (ConfigValidationFailedException ex)
            {
                AnsiConsole.MarkupLine("[red]Config validation failed![/] " + ex.Message);
                return null;
            }

            // SAVE CONFIG AND RETURN
            AnsiConsole.MarkupLine("[green]Success![/]");
            AnsiConsole.WriteLine("Enter a file name or full path at which the config will be saved.");
            AnsiConsole.MarkupLine("[dim]Leave blank for default.[/]");

            string usrInptFilePath = AnsiConsole.Prompt(new TextPrompt<string>("> ")
                .AllowEmpty()
                .DefaultValue(configFilePath)
                .DefaultValueStyle("grey")
                );
            if (!string.IsNullOrWhiteSpace(usrInptFilePath))
                configFilePath = usrInptFilePath;
            try
            {
                createdConfig.Write(configFilePath);
                AnsiConsole.MarkupLine("[green]Config file saved.[/] Returning to TEASConsole in 3 seconds...");
                AnsiConsole.WriteLine();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("[red]An error occured while saving the config file:[/] " + ex.Message + "\n" +
                    "The generated configuration will be used for this session, but will not be saved.\n" +
                    "Returning to TEASConsole in 3 seconds...");
                AnsiConsole.WriteLine();
            }

            Thread.Sleep(3000);
            AnsiConsole.Clear();

            return createdConfig;
        }
    }
}
