using System.Threading.Tasks;
using DSharpPlus.VoiceNext;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace TheEpicAudioStreamer
{
    [RequireUserPermissions(DSharpPlus.Permissions.ManageGuild)] // Give only users who have the permission to manage the server the right to execute commands.
    public class BotCommands : BaseCommandModule
    {
        public MMDevice AudioDevice { private get; set; }
        public WasapiLoopbackCapture Capture { private get; set; }

        [Command("join")]
        [Description("Joins the current voice channel.")]
        public async Task Join(CommandContext ctx)
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

            // Open transmit stream and add event handlers for available audio from capture device and recording completion.
            var stream = connection.GetTransmitSink();
            Capture.DataAvailable += async (s, e) =>
            {
                // Convert IEEE Floating Point data from the capture to 16bit PCM.
                var pcmBuffer = Helpers.ToPCM16(e.Buffer, e.BytesRecorded, Capture.WaveFormat);

                // Write into Transmit Sink if audio data exists.
                if (pcmBuffer.Length > 0)
                {
                    await stream.WriteAsync(pcmBuffer);
                }
            };
            Capture.RecordingStopped += async (s, e) =>
            {
                if(e.Exception != null)
                    await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Red, $"An error occured while capturing audio: '{e.Exception.Message}'"));
            };

            if (channel.Parent != null)
                await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Green, $"Bot connected to '{channel.Name}' in '{channel.Parent.Name}'."));
            else
                await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Green, $"Bot connected to '{channel.Name}'."));
        }

        [Command("start")]
        [Description("Starts streaming. Needs to be connected to a voice channel first.")]
        public async Task Start(CommandContext ctx)
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

        [Command("joinst")]
        [Description("Joins the current voice channel and immediately starts streaming.")]
        public async Task Joinst(CommandContext ctx)
        {
            await Join(ctx);
            await Start(ctx);
        }

        [Command("stop")]
        [Description("Stops streaming.")]
        public async Task Stop(CommandContext ctx)
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

        [Command("refresh")]
        [Description("Restarts streaming. Useful if streamed audio lags behind due to connection issues.")]
        public async Task Refresh(CommandContext ctx)
        {
            var stopCtx = ctx.CommandsNext.CreateFakeContext(ctx.Member, ctx.Channel, "stop", ctx.Prefix, ctx.CommandsNext.FindCommand("stop", out _));
            await ctx.CommandsNext.ExecuteCommandAsync(stopCtx);

            var startCtx = ctx.CommandsNext.CreateFakeContext(ctx.Member, ctx.Channel, "start", ctx.Prefix, ctx.CommandsNext.FindCommand("start", out _));
            await ctx.CommandsNext.ExecuteCommandAsync(startCtx);
        }

        [Command("leave")]
        [Description("Stops streaming and disconnects from the current voice channel.")]
        public async Task Leave(CommandContext ctx)
        {
            // Check whether the bot is connected.
            var connection = ctx.Client.GetVoiceNext().GetConnection(ctx.Guild);
            if (connection == null)
            {
                await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Red, "Bot is not connected to a voice channel."));
                return;
            }

            // Stop capturing and disconnect.
            if (Capture.CaptureState != CaptureState.Stopped)
                Capture.StopRecording();
            connection.Disconnect();
            await ctx.RespondAsync(embed: Helpers.GenerateEmbed(DiscordColor.Green, "Disconnected."));
        }
    }
}
