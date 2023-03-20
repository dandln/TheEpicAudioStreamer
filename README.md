# TheEpicAudioStreamer (TEAS)
TEAS is a Discord bot that allows you to stream audio directly from a local audio device to a voice channel. It runs through a Windows console application called TEASConsole, that accesses your local audio devices.

There is a little bit of setup needed on first time use, but running the bot afterwards is very straightforward. This Readme contains detailed instructions and documentation for set up and usage.

### System Requirements
* Windows 10 or above
* A stable internet connection

## Contents of this Readme
1. [Preparing the Bot](#user-content-preparing-the-bot)
2. [Running TEAS](#user-content-running-teas)
3. [Discord Commands](#user-content-discord-slash-commands)
4. [Command Line Usage](#user-content-command-line-usage)
5. [Migrating from v0.5.2 or earlier](#user-content-migrating-from-v052-or-earlier)

## Preparing the Bot
To run TEAS, you will need two things: A **bot token** for a Discord application and the **Guild ID**, i.e. the ID of the server you want to use the bot in. If you already have this, feel free to skip ahead to ["Running TEAS"](#user-content-running-teas), otherwise this section will run you through to steps to gather this information.

### Creating an application and retrieving the bot token
1. Go to the [Applications](https://discord.com/developers/applications) section of the Discord Developer Portal and click the button "New Application" in the top right corner.
2. Enter a name for your application, which should be the same as the one you want your bot to have, accept the ToS and click "Create".
3. You can enter a description and upload an image for your application, this will be displayed when you invite your bot to your server.
4. Go to "Bot" in the settings sidebar, click "Add Bot" and acknowledge the warning with "Yes, do it!"
5. Deselect the "Public Bot" setting, leave the rest of the options untouched.
6. Head back to the "General Information" tab and make a note of your "Application ID".
7. Replace `[APP_ID]` in the following URL with your Application ID and access it to invite your bot to your server.<br/>`https://discord.com/api/oauth2/authorize?client_id=[APP_ID]&permissions=2150648832&scope=bot%20applications.commands`<br />
This gives your bot the following permissions on your server: `Read Messages/View Channels`, `Send Messages`, `Embed Links`, `Use Slash Commands`, `Connect`, `Speak`
7. Go to the "Bot" section, click on "View Token", and copy it somewhere safe. Note that Discord will only show you this once, if you lose the token you will need to generate a new one.

### Retrieving the Guild ID
1. In Discord, go to User Settings -> Advanced and make sure that "Developer Mode" is enabled.
2. Right-click on the server you want to use the bot in, and click "Copy ID".

## Running TEAS
Now that you have both your bot token and the Guild ID, follow these steps to run TEASConsole for the first time:
1. Download and run the latest version of the executable from the ["Releases" section of this repository](https://github.com/dandln/TheEpicAudioStreamer/releases/).
2. TEASConsole will assist you in creating a configuration file that stores information about your bot and some default settings. You can even create multiple configurations if you want to use TEAS on different servers/in different scenarios. See ["Command Line Usage"](#user-content-command-line-usage) for more information. The first two entries in a configuration are required, the rest is entirely optional.
    1. Enter or paste the Guild ID of the server you want to use the bot in
    2. Enter or paste the bot token of the application you created
    3. A list of audio output devices on your system will be displayed. You can select one to automatically use in this configuration. If you skip this step, you will be asked to select an audio device on each startup. *It is recommended to use a virtual audio device solution like [VB-Cable](https://vb-audio.com/Cable/) for this.*
    4. You can enter or paste the ID of a voice channel on your server that the bot automatically connects and streams audio to on startup. If left blank, you will need to manually connect the bot to a voice channel (see ["Discord Slash Commands"](#user-content-discord-slash-commands)). To retrieve a channel ID, make sure Developer Mode is enabled in User Settings -> Advanced, then right-click on the voice channel of your choice and click "Copy ID".
    5. You can optionally enter a comma-separated list of Discord users in the format `<username>#<discriminator>` that will be able to issue Slash Commands to the bot
    6. You can optionally enter a comma-separated list of user roles. Users with those roles will be able to issue Slash Commands to the bot
    7. If your configuration is valid, TEASConsole will prompt you to enter a file name or path where your configuration will be saved. Note that by default, TEASConsole will look for a file called `botconfig.txt` upon startup, so it is recommended to leave the file name as it is unless you intend to use multiple configurations. See ["Command Line Usage"](#user-content-command-line-usage) for more information.
5. TEASConsole will now connect to your bot and is ready to receive commands in the text channels of your server as long as the application window is open or until you terminate the process in the command prompt.

You can always edit your created configuration file either through a standard text editor or in the configuration assistant directly in TEASConsole. Additionally, command line arguments are supported to temporarily override settings found in a config file. See ["Command Line Usage"](#user-content-command-line-usage) for more information.

## Discord Slash Commands
Type these slash commands in any text channel on your server. The bot will listen to commands from users who a) are owners of the application b) are server managers c) are defined as admin users in the config d) have a role that has been defined as an admin role in the config.
* `/join` - Join the voice channel the issuing user is currently in
* `/start` - Start streaming. The bot needs to be connected to a voice channel first
* `/joinst` - Join the current voice channel and immediately start streaming
* `/stop` - Stop streaming
* `/leave` - Stop streaming and disconnect from the current voice channel

## Command Line Usage
TEASConsole is used in the following way: `TEASConsole.exe [configfile] <arguments>`

* `[configfile]` defaults to `botconfig.txt` and is the file name or path to the text file containing the bot configuration. If the file is not found, the configuration assistant is launched to create a new config at this location.

The following arguments are supported to override options found in the config file. Note that these will not be saved to the existing file.
* `-g` or `--guild` - The ID of the Discord Guild the bot makes itself available in
* `-t` or `--token` - The bot token of the Discord application TEAS will connect to
* `-d` or `--device` - The friendly name of an audio device that will be used by TEAS
* `-c` or `--channel` - The ID of a Discord channel that the bot will connect to on startup
* `--admin-names` - A comma-separated list of Discord users that the bot will accept commands from
* `--admin-roles` - A comma-separated list of server role names that the bot will accept commands from

The following misc arguments are supported:
* `--new` - Launches the configuration assistant regardless of whether a valid config file was found at the given location. If one exists, default values will be set according to the content of the file.
* `--verbose` - Enables debug messages

## Migrating from v0.5.2 or earlier
Version 0.6 of TEASConsole introduced a new format for configuration files and changed the way that it registers Slash Commands on the Discord server. Because of that, a Discord Guild ID is now needed to run the bot. See ["Retrieving the Guild ID"](#user-content-retrieving-the-guild-id) for how to get this.

Upon running v0.6 or higher for the first time, an interactive assistant will guide you through the creation of a new configuration file. Your bot token from an existing `bottoken.txt` file will be used automatically. Note that passing a bot token file via the `-t` argument is no longer supported. See ["Running TEAS"](#user-content-running-teas) for more information on new features introduced by the configuration format, and ["Command Line Usage"](#user-content-command-line-usage) for more information on the changes to CLI usage.

You will likely also notice that each Slash Commands appears twice in your server, but only one of them will function. This should be resolved by simply restarting your Discord client, as TEAS will automatically clear the old commands upon first startup. 