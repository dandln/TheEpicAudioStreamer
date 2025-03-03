﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.VoiceNext;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Serilog;

namespace TEASLibrary
{
    /// <summary>
    /// Class that runs and manages the audio streamer bot
    /// </summary>
    public class Bot
    {
        /// <summary>
        /// The Discord client that the bot can connect to
        /// </summary>
        public DiscordClient Discord { get; private set; }

        /// <summary>
        /// The audio device that is used for capturing/streaming
        /// </summary>
        public MMDevice? AudioDevice { get; private set; }

        /// <summary>
        /// The capture instance for the audio device
        /// </summary>
        public WasapiLoopbackCapture? Capture { get; private set; }

        /// <summary>
        /// The bot configuration in use by the instance
        /// </summary>
        public ConfigManager BotConfig { get; private set; }

        /// <summary>
        /// The active Guild that the user has defined for the bot
        /// </summary>
        protected DiscordGuild Guild { get; private set; }

        /// <summary>
        /// The default voice channel that the user has defined for the bot
        /// </summary>
        protected DiscordChannel? DefaultChannel { get; private set; }

        /// <summary>
        /// Stores the current connection object from a voice channel connection
        /// </summary>
        public VoiceNextConnection? CurrentConnection { get; set; } = null;

        private EventHandler<WaveInEventArgs>? AudioHandler;

        /// <summary>
        /// Constructs a new Bot object with the given parameters
        /// </summary>
        /// <param name="botConfig">The TEAS configuration object to be used by the bot</param>
        /// <param name="audioDevice">An optionally pre-defined audio device to be used for streaming</param>
        /// <param name="verbose">Define whether debug log messages should be displayed. Defaults to false</param>
        public Bot(ConfigManager botConfig, MMDevice? audioDevice = null, bool verbose = false)
        {
            BotConfig = botConfig;
            ChangeAudioDevice(audioDevice);

            // Create Discord configuration
            DiscordClientBuilder builder = DiscordClientBuilder.CreateDefault(BotConfig.BotToken, DiscordIntents.AllUnprivileged);
            builder.ConfigureLogging(loggingBuilder => loggingBuilder.AddSerilog());

            // Set debug log level if verbose is set
            if (verbose == true)
                builder.SetLogLevel(LogLevel.Debug);

            // Indicate the use of voicenext
            builder.UseVoiceNext(new VoiceNextConfiguration());

            builder.ConfigureServices(services => services.AddSingleton<Bot>(this).BuildServiceProvider());

            builder.UseSlashCommands(cmdExtension =>
            {
                cmdExtension.RegisterCommands<SlashCommands>(ulong.Parse(BotConfig.GuildID));
                cmdExtension.SlashCommandInvoked += async (s, e) =>
                {
                    s.Client.Logger.LogInformation("{CommandName} issued by {User}", e.Context.CommandName, e.Context.Member.Username);
                };
                cmdExtension.SlashCommandExecuted += async (s, e) =>
                {
                    s.Client.Logger.LogDebug("Successfully executed {CommandName}, issued by {User}", e.Context.CommandName, e.Context.Member.Username);
                };
                cmdExtension.SlashCommandErrored += async (s, e) =>
                {
                    s.Client.Logger.LogError("{CommandName} threw the following exception: {ExceptionType} - {ExceptionMessage}", e.Context.CommandName, e.Exception.GetType(), e.Exception.Message);
                };
            });

            builder.ConfigureEventHandlers
                (
                    // Register event handler to GuildDownloadCompleted to run validation of passed guild and channel IDs and potentially connect to a voice channel
                    b => b.HandleGuildDownloadCompleted(async (s, e) =>
                    {
                        // Validate and save Guild that the user has defined for the bot
                        try
                        {
                            Guild = s.Guilds[ulong.Parse(BotConfig.GuildID)];
                        }
                        catch (KeyNotFoundException)
                        {
                            s.Logger.LogCritical("Passed Guild ID is invalid! Check bot configuration and make sure the bot is in the referenced guild.");
                            throw new KeyNotFoundException("Passed Guild ID is invalid! Check bot configuration and make sure the bot is in the referenced guild.");
                        }

                        // Validate and save default Channel if the user has defined one
                        if (!string.IsNullOrWhiteSpace(BotConfig.DefaultChannelID))
                        {
                            try
                            {
                                DefaultChannel = Guild.Channels[ulong.Parse(BotConfig.DefaultChannelID)];
                            }
                            catch (KeyNotFoundException)
                            {
                                s.Logger.LogWarning("Passed Channel ID is invalid! Check bot configuration and make sure the channel is in the defined guild. " +
                                    "Bot will not automatically connect.");
                            }
                        }

                        // If default channel is set, connect on startup
                        if (DefaultChannel is not null)
                            AutoConnectToVoice();
                    })
                );

            // Build the DiscordClient
            Discord = builder.Build();
        }

        /// <summary>
        /// Connects the application to Discord
        /// </summary>
        public async Task Connect(string ?botActivity = null)
        {
            try {
                if (!string.IsNullOrWhiteSpace(botActivity))
                    await Discord.ConnectAsync(activity: new DiscordActivity() { ActivityType = DiscordActivityType.Playing, Name = botActivity });
                else
                    await Discord.ConnectAsync();
            }
            catch (Exception ex) { Discord.Logger.LogCritical("Could not connect to bot. " + ex.Message); return; }
            // Make sure old global Slash Commands are cleared. This is to ensure commands don't appear twice when migrating from an old TEAS version.
            List<DiscordApplicationCommand> emptyCommands = new();
            await Discord.BulkOverwriteGlobalApplicationCommandsAsync(emptyCommands);

            await Task.Delay(-1);
        }

        /// <summary>
        /// Disconnects the application from Discord
        /// </summary>
        public async Task Disconnect()
        {
            await Discord.DisconnectAsync();
        }

        /// <summary>
        /// Connects to the voice channel defined in the bot configuration and starts streaming if a device has been set
        /// </summary>
        private async void AutoConnectToVoice()
        {
            CurrentConnection = await DefaultChannel!.ConnectAsync();
            var stream = CurrentConnection.GetTransmitSink();

            // If audio device and capture instance are set, begin streaming
            if (Capture != null && AudioDevice != null)
            {
                // Initialise event handler for audio captured
                AudioHandler = new EventHandler<WaveInEventArgs>((s, e) => SlashCommands.AudioDataAvilableEventHander(s, e, stream, Capture));
                Capture.DataAvailable += AudioHandler;
                Capture.StartRecording();
                Discord.Logger.LogInformation("Bot connected to default channel {0} and started streaming", DefaultChannel!.Name);
            }
            else
                Discord.Logger.LogInformation("Bot connected to default channel {0}", DefaultChannel!.Name);
        }

        /// <summary>
        /// Updates the audio device the application is using to stream audio and creates a new
        /// capture instance if the device is not null
        /// </summary>
        /// <param name="audioDevice">The new device to use, can be null if no device is used/available</param>
        public void ChangeAudioDevice(MMDevice? audioDevice)
        {
            bool restartRecording = false;
            if(Capture != null && Capture.CaptureState == CaptureState.Capturing)
            {
                if (Capture.CaptureState == CaptureState.Capturing)
                {
                    Capture.StopRecording();
                    restartRecording = true;
                }
                Capture.Dispose();
            }

            AudioDevice = audioDevice;

            if (AudioDevice != null)   
                Capture = new WasapiLoopbackCapture(audioDevice);

            if (restartRecording && Capture != null)
                Capture.StartRecording();
        }

        internal class SlashCommands : ApplicationCommandModule
        {

            /// <summary>
            /// Bot object passed on to the commands
            /// </summary>
            public Bot BotInstance { private get; set; }

            [SlashCommand("join", "Join the current voice channel")]
            public async Task Join(InteractionContext ctx)
            {
                var voicestate = ctx.Member!.VoiceState;

                if (!Utils.CheckCommandFeasibility(ctx, BotInstance, checkPermissions:true, checkBotNotConnected:true, checkUserConnected:true))
                    return;

                // Connect to voice channel
                DiscordChannel channel = voicestate.Channel!;
                BotInstance.CurrentConnection = await channel.ConnectAsync();

                // Open transmit stream
                var stream = BotInstance.CurrentConnection.GetTransmitSink();

                if (BotInstance.Capture != null && BotInstance.AudioDevice != null)
                {
                    // Initialise event handler for audio captured
                    BotInstance.AudioHandler = new EventHandler<WaveInEventArgs>((s, e) => AudioDataAvilableEventHander(s, e, stream, BotInstance.Capture));
                    BotInstance.Capture.DataAvailable += BotInstance.AudioHandler;
                }

                if (channel.Parent is not null)
                    await ctx.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Green, $"Bot connected to **{channel.Name}** in {channel.Parent.Name}")));
                else
                    await ctx.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Green, $"Bot connected to **{channel.Name}**")));
            }

            [SlashCommand("start", "Start streaming. Bot needs to be connected to a voice channel")]
            public async Task Start(InteractionContext ctx)
            {
                if (!Utils.CheckCommandFeasibility(ctx, BotInstance, checkPermissions: true, checkBotConnected:true, checkDeviceSelected:true, checkBotNotStreaming:true))
                    return;

                BotInstance.Capture!.StartRecording();
                await ctx.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Green, $"Capturing and streaming from device **{BotInstance.AudioDevice!.FriendlyName}**")));
            }

            [SlashCommand("joinst", "Join the current voice channel and immediately start streaming")]
            public async Task Joinst(InteractionContext ctx)
            {
                var voicestate = ctx.Member!.VoiceState;

                if (!Utils.CheckCommandFeasibility(ctx, BotInstance, checkPermissions: true, checkUserConnected: true, checkDeviceSelected: true, checkBotNotConnected:true, checkBotNotStreaming:true))
                    return;

                // Connect to voice channel
                DiscordChannel channel = voicestate.Channel!;
                BotInstance.CurrentConnection = await channel.ConnectAsync();

                // Open transmit stream
                var stream = BotInstance.CurrentConnection.GetTransmitSink();

                // Initialise event handler for audio captured
                BotInstance.AudioHandler = new EventHandler<WaveInEventArgs>((s, e) => AudioDataAvilableEventHander(s, e, stream, BotInstance.Capture!));
                BotInstance.Capture!.DataAvailable += BotInstance.AudioHandler;

                // Start capturing
                BotInstance.Capture.StartRecording();
                if (voicestate.Channel!.Parent is null)
                    await ctx.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Green, $"Connected to **{voicestate.Channel.Name}** and streaming from device **{BotInstance.AudioDevice!.FriendlyName}**")));
                else
                    await ctx.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Green, $"Connected to **{voicestate.Channel.Name}** in **{voicestate.Channel.Parent.Name}** and streaming from device **{BotInstance.AudioDevice!.FriendlyName}**")));
            }

            [SlashCommand("stop", "Stop streaming")]
            public async Task Stop(InteractionContext ctx)
            {
                if (!Utils.CheckCommandFeasibility(ctx, BotInstance, checkPermissions: true, checkBotConnected: true, checkBotStreaming: true))
                    return;

                // Stop capturing
                BotInstance.Capture!.StopRecording();
                await ctx.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                    (Utils.GenerateEmbed(DiscordColor.Green, "Stopped streaming")));
            }

            [SlashCommand("leave", "Stop streaming and disconnect from the current voice channel")]
            public async Task Leave(InteractionContext ctx)
            {
                if (!Utils.CheckCommandFeasibility(ctx, BotInstance, checkPermissions: true, checkBotConnected: true))
                    return;

                // Stop capturing
                if (BotInstance.Capture != null && BotInstance.Capture.CaptureState != CaptureState.Stopped)
                {
                    // Stop capturing
                    BotInstance.Capture.StopRecording();
                }
                if (BotInstance.Capture != null)
                {
                    // Unsubscribe from event
                    BotInstance.Capture.DataAvailable -= BotInstance.AudioHandler;
                    BotInstance.AudioHandler = null;
                }

                // Disconnect
                BotInstance.CurrentConnection!.Disconnect();
                BotInstance.CurrentConnection = null;
                await ctx.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                    (Utils.GenerateEmbed(DiscordColor.Green, "Disconnected")));
            }

            /// <summary>
            /// Handles captured audio from a Wasapi device by converting it to PCM16 and writing it into a voice transmit sink
            /// </summary>
            /// <param name="sink">The Discord VoiceTransmitSink instance</param>
            /// <param name="device">The WasapiLoopbackCapture device</param>
            internal static async void AudioDataAvilableEventHander(object s, WaveInEventArgs e, VoiceTransmitSink sink, WasapiLoopbackCapture device)
            {
                // If audio data is available, convert it into PCM16 format and write it into the sink.
                if (e.Buffer.Length > 0)
                {
                    await sink.WriteAsync(Utils.AudioToPCM16(e.Buffer, e.BytesRecorded, device.WaveFormat));
                }
            }
        }
    }
}