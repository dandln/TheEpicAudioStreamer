# Changelog
### v0.7.0 - 2024-04-14
* Made TEASConsole prettier. This mainly affects the config creator, which is now more user-friendly
* Added option to supply a custom activity to the bot
* Removed mentions of username discriminators as they are no longer in use on Discord
* Minor improvements to the code and logging
* Updated dependencies

### v0.6.2 - 2023-03-05
* Updated dependencies to address breaking Discord API change coming into effect on 15/03/2023

### v0.6.1 - 2023-02-19
* HOTFIX: Removed bot token validation in config manager, as it failed to validate tokens created after a recent change in token format by Discord

### v0.6.0 - 2023-02-18
* Implemented new configuration format, allowing more customisation and automatisation features
    * Bot now requires a Guild ID to connect to and register Slash Commands in
    * Added option to automatically connect to a given voice channel on startup
    * Added option to define multiple admin users and/or roles that can issue Slash Commands to the bot
    * Implemented interactive assistant to create configuration files
* Bot now always listens to commands issued by the owner of the application
* Slash Commands are now registered on a Guild-basis instead of globally
* `-t` CLI argument now only accepts a full token, not a file path
* Other changes to CLI arguments (see Readme)
* Internal code changes and cleanups
* Updated dependencies

### v0.5.2 - 2022-04-03
* Implemented Serilog to facilitate more streamlined logging of both bot and app events.
* Updated dependencies (fixes an OAuth error in D#+).

### v0.5.0 - 2021-11-30
* Restructured project architecture:
    * Core bot functionality is now provided through a class library.
    * Console application acts as a wrapper for the library.
* Migrated the bot to Slash Commands.
* Removed `-p` command line option as it is no longer needed.
* Removed `refresh` command.
* Changed target framework to .NET 6.0.
* Updated dependencies.

### v0.4.1 - 2021-04-16
* Added simple version check on startup.
* Updated dependencies.

### v0.4.0 - 2021-02-24
* Added `-a` command line option to specify a user that the bot should accept commands from, in addition to server managers.
* Updated dependencies.

### v0.3.0 - 2021-02-06
* Added `-d` command line option to pass a friendly device name to the application for use to skip manual selection.
* Added `-v` command line option to display debug messages from DSharpPlus.
* Updated dependencies.

### v0.2.1 - 2021-01-15
* Added option to pass the bot token directly via command line argument.
* Added option to change the command prefix via command line argument.
* Fixed potential memory leak.
* Updated dependencies.

### v0.1.0 - 2021-01-02
* Initial release.