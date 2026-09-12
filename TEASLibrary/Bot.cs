using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using DSharpPlus.Voice;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Serilog;
using System.Buffers;
using System.ComponentModel;

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
        /// The recorder instance for the audio device
        /// </summary>
        public WasapiRecorder? Recorder { get; private set; }

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
        public VoiceConnection? CurrentConnection { get; set; } = null;

        /// <summary>
        /// Stores the current DSharppPlus Audio Writer
        /// </summary>
        public AudioWriter? CurrentAudioWriter { get; private set; } = null;

        /// <summary>
        /// The event handler for when audio data is available from the capture device
        /// </summary>
        private CaptureDataAvailableHandler? AudioHandler;

        /// <summary>
        /// Constructs a new Bot object with the given parameters
        /// </summary>
        /// <param name="botConfig">The TEAS configuration object to be used by the bot</param>
        /// <param name="audioDevice">An optionally pre-defined audio device to be used for streaming</param>
        /// <param name="verbose">Define whether debug log messages should be displayed. Defaults to false</param>
        public Bot(ConfigManager botConfig, MMDevice? audioDevice = null, bool verbose = false)
        {
            BotConfig = botConfig;
            AudioDevice = audioDevice;

            // Create Discord configuration
            DiscordClientBuilder builder = DiscordClientBuilder.CreateDefault(BotConfig.BotToken, DiscordIntents.AllUnprivileged);
            builder.ConfigureLogging(loggingBuilder => loggingBuilder.AddSerilog());

            // Indicate the use of DSharpPlus.Voice
            builder.UseVoice();

            builder.ConfigureServices(services => services.AddSingleton<Bot>(this).BuildServiceProvider());

            builder.UseCommands((IServiceProvider serviceProdivder, CommandsExtension cmdExtension) =>
            {
                cmdExtension.AddCommands<SlashCommands>(ulong.Parse(BotConfig.GuildID));
                cmdExtension.CommandExecuted += async (s, e) =>
                {
                    s.Client.Logger.LogDebug("Successfully executed {CommandName}, issued by {User}", e.Context.Command.Name, e.Context.Member!.Username);
                };
                cmdExtension.CommandErrored += async (s, e) =>
                {
                    s.Client.Logger.LogError("{CommandName} threw the following exception: {ExceptionType} - {ExceptionMessage}", e.Context.Command.Name, e.Exception.GetType(), e.Exception.Message);
                };

                SlashCommandProcessor slashCommandProcessor = new(new());
                cmdExtension.AddProcessor(slashCommandProcessor);

            }, new CommandsConfiguration()
            {
                RegisterDefaultCommandProcessors = false
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
            CurrentAudioWriter = CurrentConnection.CreateAudioWriter(AudioFormat.S16LE48KHzStereoPCM);

            // If audio device is set, begin streaming
            if (AudioDevice != null)
            {
                // Initialise audio device recorder, event handler, and start recording
                Recorder = Utils.InitializeAudioRecorder(AudioDevice);
                AudioHandler = new CaptureDataAvailableHandler((b, f, p, q) => SlashCommands.AudioDataAvilableEventHander(b, CurrentAudioWriter));
                Recorder!.DataAvailable += AudioHandler;
                Recorder!.StartRecording();

                Discord.Logger.LogInformation("Bot connected to default channel {0} and started streaming", DefaultChannel!.Name);
            }
            else
                Discord.Logger.LogInformation("Bot connected to default channel {0}", DefaultChannel!.Name);
        }

        internal class SlashCommands
        {
            /// <summary>
            /// Bot object passed on to the commands
            /// </summary>
            public Bot BotInstance { private get; set; }

            public SlashCommands(Bot botInstance) { BotInstance = botInstance; }

            [Command("join")]
            [Description("Join the current voice channel")]
            public async Task Join(SlashCommandContext ctx)
            {
                if (!await Utils.CheckCommandFeasibilityAsync(ctx, BotInstance, checkPermissions:true, checkBotNotConnected:true, checkUserConnected:true))
                    return;

                // Connect to voice channel
                DiscordChannel? channel = await ctx.Member!.VoiceState.GetChannelAsync();
                if (channel is not null)
                {
                    BotInstance.CurrentConnection = await channel.ConnectAsync();

                    // Get audio writer
                    BotInstance.CurrentAudioWriter = BotInstance.CurrentConnection.CreateAudioWriter(AudioFormat.S16LE48KHzStereoPCM);

                    if (channel.Parent is not null)
                    {
                        await ctx.RespondAsync(Utils.GenerateEmbed(DiscordColor.Green, $"Bot connected to **{channel.Name}** in {channel.Parent.Name}"));
                        ctx.Client.Logger.LogInformation($"Bot connected to {channel.Name} in {channel.Parent.Name}");
                    }
                    else
                    {
                        await ctx.RespondAsync(Utils.GenerateEmbed(DiscordColor.Green, $"Bot connected to **{channel.Name}**"));
                        ctx.Client.Logger.LogInformation($"Bot connected to **{channel.Name}**");
                    }
                }
                else
                {
                    await ctx.RespondAsync(Utils.GenerateEmbed(DiscordColor.Red, "An error ocurred connecting the bot to a voice channel."));
                    ctx.Client.Logger.LogError("Could not connect the bot to a voice channel, because the channel of the issuing member returned null.");
                }
            }

            [Command("start")]
            [Description("Start streaming. Bot needs to be connected to a voice channel")]
            public async Task Start(SlashCommandContext ctx)
            {
                if (!await Utils.CheckCommandFeasibilityAsync(ctx, BotInstance, checkPermissions: true, checkBotConnected:true, checkDeviceSelected:true, checkBotNotStreaming:true))
                    return;

                // Initialise audio device recorder, event handler, and start recording
                BotInstance.Recorder = Utils.InitializeAudioRecorder(BotInstance.AudioDevice);
                BotInstance.AudioHandler = new CaptureDataAvailableHandler((b, f, p, q) => AudioDataAvilableEventHander(b, BotInstance.CurrentAudioWriter));
                BotInstance.Recorder!.DataAvailable += BotInstance.AudioHandler;
                BotInstance.Recorder!.StartRecording();

                await ctx.RespondAsync(Utils.GenerateEmbed(DiscordColor.Green, $"Capturing and streaming from device **{BotInstance.AudioDevice!.FriendlyName}**"));
                ctx.Client.Logger.LogInformation($"Capturing and streaming from device {BotInstance.AudioDevice!.FriendlyName}");
            }

            [Command("joinst")]
            [Description("Join the current voice channel and immediately start streaming")]
            public async Task Joinst(SlashCommandContext ctx)
            {
                if (!await Utils.CheckCommandFeasibilityAsync(ctx, BotInstance, checkPermissions: true, checkUserConnected: true, checkDeviceSelected: true, checkBotNotConnected:true, checkBotNotStreaming:true))
                    return;

                // Connect to voice channel
                DiscordChannel? channel = await ctx.Member!.VoiceState.GetChannelAsync();
                if (channel is not null)
                {
                    BotInstance.CurrentConnection = await channel.ConnectAsync();

                    // Get audio writer
                    BotInstance.CurrentAudioWriter = BotInstance.CurrentConnection.CreateAudioWriter(AudioFormat.S16LE48KHzStereoPCM);

                    // Initialise audio device recorder, event handler, and start recording
                    BotInstance.Recorder = Utils.InitializeAudioRecorder(BotInstance.AudioDevice);
                    BotInstance.AudioHandler = new CaptureDataAvailableHandler((b, f, p, q) => AudioDataAvilableEventHander(b, BotInstance.CurrentAudioWriter));
                    BotInstance.Recorder!.DataAvailable += BotInstance.AudioHandler;
                    BotInstance.Recorder!.StartRecording();

                    if (channel.Parent is null)
                    {
                        await ctx.RespondAsync(Utils.GenerateEmbed(DiscordColor.Green, $"Connected to **{channel.Name}** and streaming from device **{BotInstance.AudioDevice!.FriendlyName}**"));
                        ctx.Client.Logger.LogInformation($"Connected to {channel.Name} and streaming from device {BotInstance.AudioDevice!.FriendlyName}");
                    }
                    else
                    {
                        await ctx.RespondAsync(Utils.GenerateEmbed(DiscordColor.Green, $"Connected to **{channel.Name}** in **{channel.Parent.Name}** and streaming from device **{BotInstance.AudioDevice!.FriendlyName}**"));
                        ctx.Client.Logger.LogInformation($"Connected to {channel.Name} in {channel.Parent.Name} and streaming from device {BotInstance.AudioDevice!.FriendlyName}");
                    }
                }
            }

            [Command("stop")]
            [Description("Stop streaming")]
            public async Task Stop(SlashCommandContext ctx)
            {
                if (!await Utils.CheckCommandFeasibilityAsync(ctx, BotInstance, checkPermissions: true, checkBotConnected: true, checkBotStreaming: true))
                    return;
                
                // Stop capturing and dispose recorder
                BotInstance.Recorder!.StopRecording();
                BotInstance.Recorder.Dispose();

                // Signal silence to voice connection
                BotInstance.CurrentAudioWriter!.SignalSilence();

                await ctx.RespondAsync(Utils.GenerateEmbed(DiscordColor.Green, "Stopped streaming"));
                ctx.Client.Logger.LogInformation("Stopped streaming");
            }

            [Command("leave")]
            [Description("Stop streaming and disconnect from the current voice channel")]
            public async Task Leave(SlashCommandContext ctx)
            {
                if (!await Utils.CheckCommandFeasibilityAsync(ctx, BotInstance, checkPermissions: true, checkBotConnected: true))
                    return;

                // Stop capturing
                if (BotInstance.Recorder != null && BotInstance.Recorder.CaptureState != CaptureState.Stopped)
                {
                    BotInstance.Recorder.StopRecording();
                    // Unsubscribe from event
                    BotInstance.Recorder.DataAvailable -= BotInstance.AudioHandler;
                    BotInstance.AudioHandler = null;
                    // Dispose of the recorder object
                    BotInstance.Recorder.Dispose();
                }

                // Disconnect
                await BotInstance.CurrentConnection!.DisconnectAsync();
                BotInstance.CurrentConnection = null;
                BotInstance.CurrentAudioWriter = null;
                await ctx.RespondAsync(Utils.GenerateEmbed(DiscordColor.Green, "Disconnected"));
                ctx.Client.Logger.LogInformation("Disconnected");
            }

            /// <summary>
            /// Handles captured audio from a the recording device by writing it into the voice channel AudioWriter
            /// </summary>
            /// <param name="writer">The AudioWriter instance</param>
            internal static void AudioDataAvilableEventHander(ReadOnlySpan<byte> b, AudioWriter writer)
            {
                if (b.Length > 0)
                    writer.Write(b);
            }
        }
    }
}