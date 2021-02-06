using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Converters;
using DSharpPlus.CommandsNext.Entities;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace TheEpicAudioStreamer
{
    class AudioBot
    {
        private DiscordClient Discord { get; set; }
        private MMDevice AudioDevice { get; set; }

        /// <summary>
        /// Creates an instance of the audio stream bot.
        /// </summary>
        /// <param name="config">A valid DiscordConfiguration object, including the bot token and other settings.</param>
        /// <param name="cmdsConfig">A valid CommandsNextConfiguration object, including prefix and other settings, but NO services.</param>
        /// <param name="audioDevice">The audio device to use for this session.</param>
        public AudioBot(DiscordConfiguration config, CommandsNextConfiguration cmdsConfig, MMDevice audioDevice)
        {
            // Assign audio device.
            AudioDevice = audioDevice;

            // Create client object.
            Discord = new DiscordClient(config);

            // Create and configure LoopbackCapture object.
            var capture = new WasapiLoopbackCapture(AudioDevice);

            // Create services, include the AudioDevice and LoopbackCapture object and add services to commands configuration.
            var services = new ServiceCollection()
                .AddSingleton<MMDevice>(AudioDevice)
                .AddSingleton<WasapiLoopbackCapture>(capture)
                .BuildServiceProvider();
            cmdsConfig.Services = services;

            // Register commands
            var commands = Discord.UseCommandsNext(cmdsConfig);
            commands.RegisterCommands<BotCommands>();
            commands.SetHelpFormatter<AudioStreamerHelpFormatter>();

            // Indicate the use of VoiceNext
            Discord.UseVoiceNext();
        }

        /// <summary>
        /// Run the Discord music bot.
        /// </summary>
        public async Task RunBot()
        {
            // Run the bot.
            await Discord.ConnectAsync();
            await Task.Delay(-1);
        }
    }

    // Formatter for the !help command.
    public class AudioStreamerHelpFormatter : BaseHelpFormatter
    {
        // Create Discord embed.
        protected DiscordEmbedBuilder _embed;
        private readonly string cmdPrefix;

        /// <summary>
        /// Creates an help formatter object and configures basic layout options for the embed.
        /// </summary>
        /// <param name="ctx">The command context of the help command.</param>
        public AudioStreamerHelpFormatter(CommandContext ctx) : base(ctx)
        {
            _embed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.CornflowerBlue,
                Title = "TheEpicAudioStreamer",
                Footer = new DiscordEmbedBuilder.EmbedFooter()
                {
                    Text = "ver 0.2.1"
                }
            };
            cmdPrefix = ctx.Prefix;
        }

        /// <summary>
        /// Called to get a detailed description of a command.
        /// </summary>
        public override BaseHelpFormatter WithCommand(Command command)
        {
            _embed.Description += "`" + cmdPrefix + command.Name + "` - " + command.Description + "\n";

            return this;
        }

        /// <summary>
        /// Called when there are multiple subcommands to a certain command, or the general help is called.
        /// </summary>
        public override BaseHelpFormatter WithSubcommands(IEnumerable<Command> cmds)
        {
            foreach (var cmd in cmds)
            {
                _embed.Description += "`" + cmdPrefix + cmd.Name + "` - " + cmd.Description + "\n";
            }

            return this;
        }

        /// <summary>
        /// Return the embed to be displayed in the channel as response to the help command.
        /// </summary>
        /// <returns>Return the embed to be displayed</returns>
        public override CommandHelpMessage Build()
        {
            return new CommandHelpMessage(embed: _embed);
        }
    }
}
