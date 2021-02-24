using System;
using System.Threading.Tasks;
using DSharpPlus.VoiceNext;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace TheEpicAudioStreamer
{
    public class BotCommands : BaseCommandModule
    {
        // Properties set by the audio bot.
        public MMDevice AudioDevice { private get; set; }
        public WasapiLoopbackCapture Capture { private get; set; }
        public string AdminUserName { private get; set; }

        // Register EventHandlers.
        private EventHandler<WaveInEventArgs> AudioHandler;
        private EventHandler<StoppedEventArgs> StoppedHandler;

        [Command("join")]
        [Description("Joins the current voice channel.")]
        public async Task Join(CommandContext ctx)
        {
            if (ctx.Member.PermissionsIn(ctx.Channel).HasFlag(DSharpPlus.Permissions.ManageGuild) || ctx.Member.Username + "#" + ctx.Member.Discriminator == AdminUserName)
            {
                // Get VoiceNext object.
                var vnext = ctx.Client.GetVoiceNext();

                // Check whether the bot is already connected.
                var connection = vnext.GetConnection(ctx.Guild);
                if (connection != null)
                {
                    await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Red, "Bot is already connected to a voice channel."));
                    return;
                }

                // Check whether user is in a voice channel.
                var voicestate = ctx.Member?.VoiceState;
                if (voicestate?.Channel == null)
                {
                    await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Red, "You are not in a voice channel."));
                    return;
                }

                // Connect.
                DiscordChannel channel = voicestate.Channel;
                connection = await vnext.ConnectAsync(channel);

                // Open transmit stream.
                var stream = connection.GetTransmitSink();

                // Subscribe to events for available audio from capture device and recording completion.
                // Note: This is a little messy, but creates the ability to unsubscribe from the events to prevent a memory leak.
                AudioHandler = new EventHandler<WaveInEventArgs>((s, e) => Helpers.AudioDataAvilableEventHander(s, e, stream, Capture));
                Capture.DataAvailable += AudioHandler;
                StoppedHandler = new EventHandler<StoppedEventArgs>((s, e) => Helpers.AudioRecordingStoppedEventHandler(s, e, ctx));
                Capture.RecordingStopped += StoppedHandler;

                if (channel.Parent != null)
                    await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Green, $"Bot connected to '{channel.Name}' in '{channel.Parent.Name}'."));
                else
                    await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Green, $"Bot connected to '{channel.Name}'."));
            }
            else
            {
                // Action not permitted from user.
                await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Red, $"Sorry {ctx.Member.Mention}, you're not the DJ today."));
            }
            
        }

        [Command("start")]
        [Description("Starts streaming. Needs to be connected to a voice channel first.")]
        public async Task Start(CommandContext ctx)
        {
            if (ctx.Member.PermissionsIn(ctx.Channel).HasFlag(DSharpPlus.Permissions.ManageGuild) || ctx.Member.Username + "#" + ctx.Member.Discriminator == AdminUserName)
            {   
                // Check whether the bot is connected.
                var connection = ctx.Client.GetVoiceNext().GetConnection(ctx.Guild);
                if (connection == null)
                {
                    await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Red, "Bot is not connected to a voice channel."));
                    return;
                }

                // Check whether the bot is already recording, start recording if not.
                if (Capture.CaptureState == CaptureState.Stopped)
                {
                    Capture.StartRecording();
                    await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Green, $"Capturing and streaming from device '{AudioDevice.FriendlyName}'."));
                }
                else
                {
                    await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Red, "Already capturing from device."));
                }
            }
            else
            {
                // Action not permitted from user.
                await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Red, $"Sorry {ctx.Member.Mention}, you're not the DJ today."));
            }
        }

        [Command("joinst")]
        [Description("Joins the current voice channel and immediately starts streaming.")]
        public async Task Joinst(CommandContext ctx)
        {
            if (ctx.Member.PermissionsIn(ctx.Channel).HasFlag(DSharpPlus.Permissions.ManageGuild) || ctx.Member.Username + "#" + ctx.Member.Discriminator == AdminUserName)
            {
                await Join(ctx);
                await Start(ctx);
            }
            else
            {
                // Action not permitted from user.
                await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Red, $"Sorry {ctx.Member.Mention}, you're not the DJ today."));
            }
        }

        [Command("stop")]
        [Description("Stops streaming.")]
        public async Task Stop(CommandContext ctx)
        {
            if (ctx.Member.PermissionsIn(ctx.Channel).HasFlag(DSharpPlus.Permissions.ManageGuild) || ctx.Member.Username + "#" + ctx.Member.Discriminator == AdminUserName)
            {
                // Check whether the bot is connected to a voice channel.
                if (Helpers.CheckConnectionStatus(ctx) == false)
                {
                    await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Red, "Bot is not connected to a voice channel."));
                    return;
                }

                // Check whether bot is streaming.
                if (Capture.CaptureState != CaptureState.Capturing)
                {
                    await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Red, "Bot is not streaming."));
                    return;
                }

                // Stop capturing.
                Capture.StopRecording();

                await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Green, "Stopped streaming."));
            }
            else
            {
                // Action not permitted from user.
                await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Red, $"Sorry {ctx.Member.Mention}, you're not the DJ today."));
            }
        }

        [Command("refresh")]
        [Description("Restarts streaming. Useful if streamed audio lags behind due to connection issues.")]
        public async Task Refresh(CommandContext ctx)
        {
            if (ctx.Member.PermissionsIn(ctx.Channel).HasFlag(DSharpPlus.Permissions.ManageGuild) || ctx.Member.Username + "#" + ctx.Member.Discriminator == AdminUserName)
            {
                var stopCtx = ctx.CommandsNext.CreateFakeContext(ctx.Member, ctx.Channel, "stop", ctx.Prefix, ctx.CommandsNext.FindCommand("stop", out _));
                await ctx.CommandsNext.ExecuteCommandAsync(stopCtx);

                var startCtx = ctx.CommandsNext.CreateFakeContext(ctx.Member, ctx.Channel, "start", ctx.Prefix, ctx.CommandsNext.FindCommand("start", out _));
                await ctx.CommandsNext.ExecuteCommandAsync(startCtx);
            }
            else
            {
                // Action not permitted from user.
                await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Red, $"Sorry {ctx.Member.Mention}, you're not the DJ today."));
            }
        }

        [Command("leave")]
        [Description("Stops streaming and disconnects from the current voice channel.")]
        public async Task Leave(CommandContext ctx)
        {
            if (ctx.Member.PermissionsIn(ctx.Channel).HasFlag(DSharpPlus.Permissions.ManageGuild) || ctx.Member.Username + "#" + ctx.Member.Discriminator == AdminUserName)
            {
                // Check whether the bot is connected.
                var connection = ctx.Client.GetVoiceNext().GetConnection(ctx.Guild);
                if (connection == null)
                {
                    await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Red, "Bot is not connected to a voice channel."));
                    return;
                }

                // Stop capturing.
                if (Capture.CaptureState != CaptureState.Stopped)
                    Capture.StopRecording();

                // Unsubscribe from events to prevent memory leak.
                Capture.DataAvailable -= AudioHandler;
                Capture.RecordingStopped -= StoppedHandler;

                // Disconnect.
                connection.Disconnect();
                await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Green, "Disconnected."));
            }
            else
            {
                // Action not permitted from user.
                await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Red, $"Sorry {ctx.Member.Mention}, you're not the DJ today."));
            }
        }
    }
}
