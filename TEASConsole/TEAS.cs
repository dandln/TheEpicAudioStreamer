﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using CommandLine;
using Spectre.Console;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.Spectre;
using NAudio.CoreAudioApi;
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
            [Value(0, Default = "botconfig.txt", HelpText = "Path to the TEASConsole config file. " +
                "If none is found a new one will be generated, values can be pre-set through arguments. " +
                "If a valid config file is found, other CLI arguments will override values found in the file, but are not saved.")]
            public string ConfigFilePath { get; set; }

            [Option('g', "guild", Required = false, HelpText = "The ID of a Discord Guild the bot makes itself available to.")]
            public string GuildID { get; set; }

            [Option('t', "token", Required = false, HelpText = "The bot token of the Discord application TEAS will connect to.")]
            public string Token { get; set; }

            [Option('d', "device", Default = "", HelpText = "The friendly name of an audio device that will be used by TEAS.")]
            public string AudioFriendlyName { get; set; }

            [Option('c', "channel", Default = "", HelpText = "The ID of a Discord channel that the bot will connect to on startup.")]
            public string ChannelID { get; set; }

            [Option("admin-names", Default = "", HelpText = "A comma-separated list of Discord users that the bot will accept commands from.")]
            public string AdminUserNames { get; set; }

            [Option("admin-roles", Default = "", HelpText = "A comma-separated list of server role names that the bot will accept commands from.")]
            public string AdminRoles { get; set; }

            [Option("activity", Default = "", HelpText = "An activity that the bot will display (\"Playing...\")")]
            public string Activity { get; set; }

            [Option("new", Required = false, HelpText = "Launches the configuration assistant regardless of whether a valid config file was found at the given location.")]
            public bool NewConfig { get; set; }

            [Option("verbose", Required = false, HelpText = "Enables debug messages.")]
            public bool Verbose { get; set; }
        }

        static int Main(string[] args)
        {
            Version appVersion = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;

            // Print greeting
            AnsiConsole.Write(new FigletText(FigletFont.Load(Assembly.GetExecutingAssembly().GetManifestResourceStream("Resources.smslant.flf")), "TEASConsole")
                .Centered()
                .Color(Color.Purple)
                );
            AnsiConsole.Write(new Text($"v{appVersion.ToString(3)}").Centered());
            AnsiConsole.WriteLine("\n");

            // Check for updates
            if (Helpers.CheckUpdate(appVersion) == 1)
            {
                Panel updatePanel = new Panel("A new version of TEASConsole is available.\nDownload here: [link]https://github.com/dandln/TheEpicAudioStreamer/releases/[/]");
                updatePanel.Header = new PanelHeader("Update available");
                updatePanel.BorderColor(Color.Red);
                updatePanel.Expand = true;
                AnsiConsole.Write(updatePanel);
                AnsiConsole.WriteLine();
            }

            // Initialise Serilog
            var logLevelSwitch = new LoggingLevelSwitch();
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(logLevelSwitch)
                .WriteTo.Spectre()
                .CreateLogger();
            Log.Debug("TEASConsole, version {0}", appVersion.ToString(3));


            // BEGINN PARSING OF COMMAND LINE OPTIONS
            string configFile = "";
            string guildID = "";
            string botToken = "";
            string audioDeviceName = "";
            string channelID = "";
            string adminUsers = "";
            string adminRoles = "";
            string botActivity = "";
            bool newConfig = false;
            bool verbose = false;

            var parseResult = Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o =>
            {
                configFile = o.ConfigFilePath;
                guildID = o.GuildID;
                botToken = o.Token;
                audioDeviceName = o.AudioFriendlyName;
                channelID = o.ChannelID;
                adminUsers = o.AdminUserNames;
                adminRoles = o.AdminRoles;
                botActivity = o.Activity;
                newConfig = o.NewConfig;
                verbose = o.Verbose;
            });
            if (parseResult.Tag == ParserResultType.NotParsed)
                return 0;

            if (verbose)
            {
                logLevelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Debug;

                // Log options parsed from the CLI if debug logs are enabled
                string parsedConfigs = "Config parsed from CLI: ";
                parsedConfigs += $"Config({configFile}) ";
                if (!string.IsNullOrWhiteSpace(guildID))
                    parsedConfigs += $"GuildID({guildID}) ";
                if (!string.IsNullOrWhiteSpace(botToken))
                    parsedConfigs += $"BotToken({botToken}) ";
                if (!string.IsNullOrWhiteSpace(audioDeviceName))
                    parsedConfigs += $"AudioDeviceName({audioDeviceName}) ";
                if (!string.IsNullOrWhiteSpace(channelID))
                    parsedConfigs += $"ChannelID({channelID}) ";
                if (!string.IsNullOrWhiteSpace(adminUsers))
                    parsedConfigs += $"AdminUsers({adminUsers}) ";
                if (!string.IsNullOrWhiteSpace(adminRoles))
                    parsedConfigs += $"AdminRoles({adminRoles}) ";
                if (!string.IsNullOrWhiteSpace(botActivity))
                    parsedConfigs += $"BotActivity{botActivity}) ";
                if (newConfig)
                    parsedConfigs += "NewConfig(true)";
                Log.Debug(parsedConfigs);
            }

            ConfigManager config = null;
            if (newConfig)
            {
                // User wants to create a new config from scratch, ignore existing configuration and launch configuration assistant
                config = Helpers.RunInteractiveConfig(configFile, guildID, botToken, audioDeviceName, channelID, adminUsers, adminRoles);
                // Reprint greeting
                AnsiConsole.Write(new FigletText(FigletFont.Load(Assembly.GetExecutingAssembly().GetManifestResourceStream("Resources.smslant.flf")), "TEASConsole")
                    .Centered()
                    .Color(Color.Purple)
                    );
                AnsiConsole.Write(new Text($"v{appVersion.ToString(3)}").Centered());
                AnsiConsole.WriteLine("\n");
                if (config == null)
                {
                    Log.Fatal("Configuration assistant returned an invalid configuration. Please retry using valid parameters.");
                    return -1;
                }
                else
                {
                    Log.Information("A new TEAS configuration was created and will be used for this session! Remember to pass " +
                        "the file name/path to TEASConsole next time you want to use it if you have saved it in a place other" +
                        "than \"botconfig.txt\".");
                }
            }
            else
            {
                if (File.Exists(configFile))
                {
                    // Conifg file exists, parse options
                    Log.Debug("Parsing existing config file \"{0}\"", configFile);
                    try
                    {
                        config = new ConfigManager(configFile);
                        Log.Debug("Valid config file found");

                        // Config file valid, but check if one of the override options are set in the CLI, if so, apply them
                        if (!string.IsNullOrWhiteSpace(guildID))
                        {
                            Log.Information("Guild ID {0} is set in CLI arguments, overriding config file", guildID);
                            config.GuildID = guildID;
                        }
                        if (!string.IsNullOrWhiteSpace(botToken))
                        {
                            Log.Information("Bot token is set in CLI arguments, overriding config file");
                            config.BotToken = botToken;
                        }
                        if (!string.IsNullOrWhiteSpace(audioDeviceName))
                        {
                            Log.Information("Audio device name {0} is set in CLI arguments, overriding config file", audioDeviceName);
                            config.DefaultDeviceFriendlyName = audioDeviceName;
                        }
                        if (!string.IsNullOrWhiteSpace(channelID))
                        {
                            Log.Information("Channel id {0} is set in CLI arguments, overriding config file", channelID);
                            config.DefaultChannelID = channelID;
                        }
                        if (!string.IsNullOrWhiteSpace(adminUsers))
                        {
                            Log.Information("Admin users \"{0}\" are set in CLI arguments, overriding config file", adminUsers);
                            config.AdminUsers = adminUsers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                        }
                        if (!string.IsNullOrWhiteSpace(adminRoles))
                        {
                            Log.Information("Admin roles \"{0}\" are set in CLI arguments, overriding config file", adminRoles);
                            config.AdminRoles = adminRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                        }
                        if (!string.IsNullOrWhiteSpace(botActivity))
                        {
                            Log.Information("Bot activity {0} is set in CLI arguments, overriding config file", botActivity);
                            config.BotActivity = botActivity;
                        }

                        // Run one last check to see whether config is valid with the overriden options
                        try
                        {
                            config.Validate();
                        }
                        catch (ConfigManager.ConfigValidationFailedException ex)
                        {
                            Log.Fatal($"Error encountered while validating config overrides: {ex.Message} " +
                                $"Check CLI arguments.");
                            return -1;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Fatal($"Error ecountered while trying to parse existing configuration file: {ex.Message} " +
                            $"Try to fix the configuration or run '--new' to create a new one.");
                        return -1;
                    }
                }
                else
                {
                    // Config file does not exist, check if required parameters are set through CLI
                    if (!string.IsNullOrWhiteSpace(guildID) && !string.IsNullOrWhiteSpace(botToken))
                    {
                        Log.Debug("Config file \"{0}\" not found, but required options were given via CLI, creating config...", configFile);
                        try
                        {
                            config = new ConfigManager(guildID, botToken, audioDeviceName, channelID, adminUsers, adminRoles);
                            config.Write(configFile);
                        }
                        catch (ConfigManager.ConfigValidationFailedException ex)
                        {
                            Log.Fatal($"Given required configuration options are invalid: {ex.Message} " +
                                $"Check CLI arguments.");
                            return -1;
                        }
                        catch (Exception ex)
                        {
                            Log.Fatal($"An error occured when trying to save the new configuration: {ex.Message}");
                            return -1;
                        }
                    }
                    else
                    {
                        // Config file was not found, and required options were not set via CLI, creating a new config in the configuration assistant
                        if (File.Exists("bottoken.txt"))
                        {
                            // An older bottoken file was found, probably the user has updated from an old TEASConsole version
                            Log.Warning("Config file not found, but an old bottoken.txt file exists. " +
                                "Have you recently updated from an earlier TEASConsole version? " +
                                "Check https://github.com/dandln/TheEpicAudioStreamer#user-content-migrating-from-v052-or-earlier for more information. " +
                                "Launching configuration assistant...");
                            botToken = File.ReadAllLines("bottoken.txt")[0];
                        }
                        else
                        {
                            Log.Warning("Config file was not found, and required options were not set via command line. Launching configuration assistant...");
                        }

                        config = Helpers.RunInteractiveConfig(configFile, guildID, botToken, audioDeviceName, channelID, adminUsers, adminRoles);
                        if (config == null)
                        {
                            Log.Fatal("Configuration assistant returned an invalid configuration. Please retry using valid parameters.");
                            return -1;
                        }
                        else
                        {
                            Log.Information("A new TEAS configuration was created and will be used for this session! Remember to pass " +
                                "the file name/path to TEASConsole next time you want to use it if you have saved it in a place other " +
                                "than \"botconfig.txt\".");
                        }
                    }
                }
            }
            // END PARSING OF COMMAND LINE OPTIONS

            // Get an audio device from the user
            MMDevice AudioDevice;
            AudioDevice = Helpers.SelectDevice(config.DefaultDeviceFriendlyName);
            Log.Information("Chosen audio device is {0}", AudioDevice.DeviceFriendlyName);

            if (!string.IsNullOrWhiteSpace(config.DefaultChannelID))
                Log.Debug("Automatically connecting to channel ID {0}", config.DefaultChannelID);
            if (!string.IsNullOrWhiteSpace(config.BotActivity))
                Log.Debug("Activity will be set to \"Playing {0}\"", config.BotActivity);
            if (config.AdminUsers.Count != 0)
                Log.Information("Admin users: {0}", string.Join(',', config.AdminUsers.ToArray()));
            if (config.AdminRoles.Count != 0)
                Log.Information("Admin roles: {0}", string.Join(',', config.AdminUsers.ToArray()));

            // When all options and configurations are parsed, create a new bot object and run it.
            Bot bot = new(config, AudioDevice, verbose);

            if (!string.IsNullOrWhiteSpace(config.BotActivity))
                bot.Connect(config.BotActivity).GetAwaiter().GetResult();
            else
                bot.Connect().GetAwaiter().GetResult();

            return 0;
        }
    }
}