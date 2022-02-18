using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace TEASLibrary
{
    /// <summary>
    /// Class that runs and manages the audio streamer bot.
    /// </summary>
    public class Bot
    {
        /// <summary>
        /// The Discord client that the bot can connect to.
        /// </summary>
        private DiscordClient Discord { get; set; }

        /// <summary>
        /// The audio device that is used for capturing/streaming.
        /// </summary>
        private MMDevice? AudioDevice { get; set; }

        /// <summary>
        /// The capture instance for the audio device.
        /// </summary>
        private WasapiLoopbackCapture? Capture { get; set; }

        /// <summary>
        /// The Discord username of an eligible user that is allowed to control the bot.
        /// </summary>
        private string AdminUserName { get; set; }

        private EventHandler<WaveInEventArgs>? AudioHandler;
        private EventHandler<StoppedEventArgs>? StoppedHandler;

        /// <summary>
        /// Constructs a new Bot object with the given parameters.
        /// </summary>
        /// <param name="botToken">The Discord bot token to be used with the bot.</param>
        /// <param name="logFactory">An optional LoggerFactory object that will be passed to DSharpPlus to handle logging of events.</param>
        /// <param name="adminUserName">An optional Discord name of a user that the bot should accept commands from in addition to server managers.</param>
        /// <param name="audioDevice">An optionally pre-defined audio device to be used for streaming.</param>
        /// <param name="verbose">Define whether debug log messages should be displayed. Defaults to false.</param>
        public Bot(string botToken, ILoggerFactory? logFactory = null, string adminUserName = "", MMDevice? audioDevice = null, bool verbose = false)
        {
            ChangeAudioDevice(audioDevice);
            AdminUserName = adminUserName;

            // Create Discord configuration
            DiscordConfiguration botConfig = new()
            {
                Token = botToken,
                TokenType = TokenType.Bot
            };

            // Set log factory if parameter is not null
            if (logFactory != null)
                botConfig.LoggerFactory = logFactory;

            // Set debug log level if verbose is set
            if (verbose == true)
                botConfig.MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Debug;

            // Create client object.
            Discord = new DiscordClient(botConfig);

            // Register Slash Commands
            var slashCmds = Discord.UseSlashCommands(new SlashCommandsConfiguration
            {
                Services = new ServiceCollection().AddSingleton<Bot>(this).BuildServiceProvider()
            });
            slashCmds.RegisterCommands<SlashCommands>();

            // Register event handler for logging command errors
            slashCmds.SlashCommandErrored += async (s, e) =>
            {
                Discord.Logger.LogError("{CommandName} threw the following exception: {ExceptionType} - {ExceptionMessage}", e.Context.CommandName, e.Exception.GetType(), e.Exception.Message);
            };

            // Indicate the use of VoiceNext
            Discord.UseVoiceNext();
        }

        /// <summary>
        /// Connects the application to Discord.
        /// </summary>
        /// <returns></returns>
        public async Task Connect()
        {
            await Discord.ConnectAsync();
            await Task.Delay(-1);
        }

        /// <summary>
        /// Disconnects the application from Discord.
        /// </summary>
        /// <returns></returns>
        public async Task Disconnect()
        {
            await Discord.DisconnectAsync();
        }

        /// <summary>
        /// Updates the audio device the application is using to stream audio and creates a new
        /// capture instance if the device is not null.
        /// </summary>
        /// <param name="audioDevice">The new device to use. Can be null if no device is used/available.</param>
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

        private class SlashCommands : ApplicationCommandModule
        {

            /// <summary>
            /// Bot object passed on to the commands.
            /// </summary>
            public Bot BotInstance { private get; set; }

            [SlashCommand("join", "Joins the current voice channel.")]
            public async Task Join(InteractionContext ctx)
            {
                var vnext = ctx.Client.GetVoiceNext();
                var connection = ctx.Client.GetVoiceNext().GetConnection(ctx.Guild);

                // Perform feasibility checks
                if (!CheckPermissions(ctx, BotInstance.AdminUserName))
                    return;
                if (connection != null)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "Bot is already connected to a voice channel.")));
                    return;
                }
                var voicestate = ctx.Member?.VoiceState;
                if (voicestate?.Channel == null)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "You are not in a voice channel.")));
                    return;
                }

                // Connect to voice channel
                DiscordChannel channel = voicestate.Channel;
                connection = await vnext.ConnectAsync(channel);

                // Open transmit stream
                var stream = connection.GetTransmitSink();

                if (BotInstance.Capture != null && BotInstance.AudioDevice != null)
                {
                    // Initialise new event handlers and subscribe to events for available audio from capture device and recording completion
                    // Note: This is a little messy, but creates the ability to unsubscribe from the events to prevent a memory leak
                    BotInstance.AudioHandler = new EventHandler<WaveInEventArgs>((s, e) => AudioDataAvilableEventHander(s, e, stream, BotInstance.Capture));
                    BotInstance.Capture.DataAvailable += BotInstance.AudioHandler;
                    BotInstance.StoppedHandler = new EventHandler<StoppedEventArgs>((s, e) => AudioRecordingStoppedEventHandler(s, e, ctx));
                    BotInstance.Capture.RecordingStopped += BotInstance.StoppedHandler;
                }

                if (channel.Parent != null)
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Green, $"Bot connected to **{channel.Name}** in '{channel.Parent.Name}'.")));
                else
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Green, $"Bot connected to **{channel.Name}**.")));
            }

            [SlashCommand("start", "Starts streaming. Needs to be connected to a voice channel first.")]
            public async Task Start(InteractionContext ctx)
            {
                var connection = ctx.Client.GetVoiceNext().GetConnection(ctx.Guild);

                // Perform feasibility checks
                if (!CheckPermissions(ctx, BotInstance.AdminUserName))
                    return;
                if (connection == null)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "Bot is not connected to a voice channel.")));
                    return;
                }
                if (BotInstance.Capture == null || BotInstance.AudioDevice == null)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "No audio device is selected.")));
                    return;
                }

                // Check whether the bot is already recording, start recording if not
                if (BotInstance.Capture.CaptureState == CaptureState.Stopped)
                {
                    BotInstance.Capture.StartRecording();
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                            (Utils.GenerateEmbed(DiscordColor.Green, $"Capturing and streaming from device **{BotInstance.AudioDevice.FriendlyName}**.")));
                }
                else
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "Already capturing from device.")));
                }
            }

            [SlashCommand("joinst", "Joins the current voice channel and immediately starts streaming.")]
            public async Task Joinst(InteractionContext ctx)
            {
                var vnext = ctx.Client.GetVoiceNext();
                var connection = ctx.Client.GetVoiceNext().GetConnection(ctx.Guild);
                var voicestate = ctx.Member?.VoiceState;

                // Perform feasibility check
                if (!CheckPermissions(ctx, BotInstance.AdminUserName))
                    return;
                if (voicestate?.Channel == null)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "You are not in a voice channel.")));
                    return;
                }
                if (BotInstance.Capture == null || BotInstance.AudioDevice == null)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "No audio device is selected.")));
                    return;
                }

                if (connection == null)
                {
                    // Connect to voice channel
                    DiscordChannel channel = voicestate.Channel;
                    connection = await vnext.ConnectAsync(channel);

                    // Open transmit stream
                    var stream = connection.GetTransmitSink();

                    if (BotInstance.Capture != null && BotInstance.AudioDevice != null)
                    {
                        // Initialise new event handlers and subscribe to events for available audio from capture device and recording completion
                        // Note: This is a little messy, but creates the ability to unsubscribe from the events to prevent a memory leak
                        BotInstance.AudioHandler = new EventHandler<WaveInEventArgs>((s, e) => AudioDataAvilableEventHander(s, e, stream, BotInstance.Capture));
                        BotInstance.Capture.DataAvailable += BotInstance.AudioHandler;
                        BotInstance.StoppedHandler = new EventHandler<StoppedEventArgs>((s, e) => AudioRecordingStoppedEventHandler(s, e, ctx));
                        BotInstance.Capture.RecordingStopped += BotInstance.StoppedHandler;
                    }
                }

                // Check whether the bot is already recording, start recording if not
                if (BotInstance.Capture.CaptureState == CaptureState.Stopped)
                {
                    BotInstance.Capture.StartRecording();
                    if (voicestate.Channel.Parent == null)
                        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                            (Utils.GenerateEmbed(DiscordColor.Green, $"Connected to **{voicestate.Channel.Name}** and streaming from device **{BotInstance.AudioDevice.FriendlyName}**.")));
                    else
                        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                           (Utils.GenerateEmbed(DiscordColor.Green, $"Connected to **{voicestate.Channel.Name}** in **{voicestate.Channel.Parent.Name}** and streaming from device **{BotInstance.AudioDevice.FriendlyName}**.")));
                }
                else
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "Already capturing from device.")));
                }
            }

            [SlashCommand("stop", "Stops streaming.")]
            public async Task Stop(InteractionContext ctx)
            {
                // Perform feasibility checks
                if (!CheckPermissions(ctx, BotInstance.AdminUserName))
                    return;
                if (Utils.CheckConnectionStatus(ctx) == false)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "Bot is not connected to a voice channel.")));
                    return;
                }
                if (BotInstance.AudioDevice == null || BotInstance.Capture == null)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "No audio device is selected.")));
                    return;
                }
                if (BotInstance.Capture.CaptureState != CaptureState.Capturing)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "Bot is not streaming.")));
                    return;
                }

                // Stop capturing.
                BotInstance.Capture.StopRecording();
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                    (Utils.GenerateEmbed(DiscordColor.Green, "Stopped streaming.")));
            }

            [SlashCommand("leave", "Stops streaming and disconnects from the current voice channel.")]
            public async Task Leave(InteractionContext ctx)
            {
                // Perform feasibility checks
                var connection = ctx.Client.GetVoiceNext().GetConnection(ctx.Guild);
                if (!CheckPermissions(ctx, BotInstance.AdminUserName))
                    return;
                if (connection == null)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "Bot is not connected to a voice channel.")));
                    return;
                }

                // Stop capturing
                if (BotInstance.Capture != null && BotInstance.Capture.CaptureState != CaptureState.Stopped)
                {
                    // Stop capturing.
                    BotInstance.Capture.StopRecording();
                }
                if (BotInstance.Capture != null)
                {
                    // Unsubscribe from events to prevent memory leak
                    BotInstance.Capture.DataAvailable -= BotInstance.AudioHandler;
                    BotInstance.Capture.RecordingStopped -= BotInstance.StoppedHandler;
                    BotInstance.AudioHandler = null;
                    BotInstance.StoppedHandler = null;
                }

                // Disconnect
                connection.Disconnect();
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                    (Utils.GenerateEmbed(DiscordColor.Green, "Disconnected.")));
            }

            /// <summary>
            /// Checks whether the user has permissions to execute the command.
            /// </summary>
            /// <param name="ctx">The InteractionContext of the command.</param>
            /// <param name="adminUsername">The username of the potentially non-privileged user that is allowed to execute commands.</param>
            /// <returns>True if the user can execute the command, false if not.</returns>
            private static bool CheckPermissions(InteractionContext ctx, string adminUsername)
            {
                if (ctx.Member.PermissionsIn(ctx.Channel).HasFlag(DSharpPlus.Permissions.ManageGuild) || ctx.Member.Username + "#" + ctx.Member.Discriminator == adminUsername)
                    return true;
                else
                {
                    ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, $"Sorry {ctx.Member.Mention}, you're not the DJ today.")));
                    return false;
                }
            }

            /// <summary>
            /// Handles captured audio from a Wasapi device by converting it to PCM16 and writing it into a voice transmit sink.
            /// </summary>
            /// <param name="sink">The Discord VoiceTransmitSink instance.</param>
            /// <param name="device">The WasapiLoopbackCapture device.</param>
            private static async void AudioDataAvilableEventHander(object s, WaveInEventArgs e, VoiceTransmitSink sink, WasapiLoopbackCapture device)
            {
                // If audio data is available, convert it into PCM16 format and write it into the stream.
                if (e.Buffer.Length > 0)
                {
                    await sink.WriteAsync(Utils.AudioToPCM16(e.Buffer, e.BytesRecorded, device.WaveFormat));
                }
            }

            /// <summary>
            /// An event handler that prints potential error messages from the audio capture process to a Discord text channel.
            /// </summary>
            /// <param name="ctx">The InteractionContext.</param>
            private static async void AudioRecordingStoppedEventHandler(object s, StoppedEventArgs e, InteractionContext ctx)
            {
                if (e.Exception != null)
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, $"An error occured while capturing audio: '{e.Exception.Message}'")));
            }
        }
    }
}