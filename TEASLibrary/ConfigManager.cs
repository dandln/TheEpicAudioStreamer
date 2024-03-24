namespace TEASLibrary
{
    /// <summary>
    /// Class for handling TEAS bot configuration files
    /// </summary>
    public class ConfigManager
    {
        /// <summary>
        /// The ID of the Discord Guild that the bot should make itself available in
        /// </summary>
        public string GuildID { get; set; }

        /// <summary>
        /// The Bot Token of the Discord application that the bot should connect to
        /// </summary>
        public string BotToken { get; set; }

        /// <summary>
        /// An audio DeviceFriendlyName that should automatically be used on startup
        /// </summary>
        public string DefaultDeviceFriendlyName { get; set; }

        /// <summary>
        /// A Discord channel ID that the bot should automatically connect to on startup
        /// </summary>
        public string DefaultChannelID { get; set; }

        /// <summary>
        /// A list of admin users in the form 'Username#Discriminator' that the bot should accept commands from
        /// </summary>
        public List<string> AdminUsers { get; set; }

        /// <summary>
        /// A list of server roles that the bot should accept commands from
        /// </summary>
        public List<string> AdminRoles { get; set; }

        /// <summary>
        /// Initialises a new configuration based on a configuration file
        /// </summary>
        /// <param name="configFilePath">The path to an existing configuration file</param>
        public ConfigManager(string configFilePath)
        {
            GuildID = "";
            BotToken = "";
            DefaultDeviceFriendlyName = "";
            DefaultChannelID = "";
            AdminUsers = new List<string>();
            AdminRoles = new List<string>();
            Parse(configFilePath);
        }

        /// <summary>
        /// Initialises a new configuration based on given arguments. Does not validate after object creation.
        /// </summary>
        /// <param name="guildID">The ID of the Discord Guild that the bot should make itself available in</param>
        /// <param name="botToken">The Bot Token of the Discord application that the bot should connect to</param>
        /// <param name="defaultDeviceFriendlyName">An audio device ID that should automatically be used on startup</param>
        /// <param name="defaultChannelID">A Discord channel ID that the bot should automatically connect to on startup</param>
        /// <param name="adminUsers">A comma-separated string of usernames that the bot should accept commands from</param>
        /// <param name="adminRoles">A comma-separated string of server roles that the bot should accept commands from</param>
        public ConfigManager(string guildID = "",
                            string botToken = "",
                            string defaultDeviceFriendlyName = "",
                            string defaultChannelID = "",
                            string adminUsers = "",
                            string adminRoles = "")
        {
            GuildID = guildID;
            BotToken = botToken;
            DefaultDeviceFriendlyName = defaultDeviceFriendlyName;
            DefaultChannelID = defaultChannelID;
            AdminUsers = adminUsers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            AdminRoles = adminRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        /// <summary>
        /// <para>Parses a config file into the ConfigManager's attribute properties, and validats the resulting configuration afterwards.
        /// A valid TEASConsole configuration file has the following structure (values in [] are placeholders, * marks an optional property):</para>
        /// 
        /// <para>GuildID=[Discord Guild ID where the bot is used]<br />
        /// BotToken=[Application bot token to connect to]<br />
        /// DefaultDevice=[A FriendlyName of an audio device the bot should use]*<br />
        /// DefaultChannel=[Channel ID of a Discord channel that the bot connects to on startup]*<br />
        /// AdminUsers=[Comma-separated list of Discord users that the bot accepts commands from]*<br />
        /// AdminRoles=[Comma-separated list of server role names that the bot accepts commands from]*
        /// </para>
        /// </summary>
        /// <param name="configFilePath">The path to the config file</param>
        public void Parse(string configFilePath)
        {
            foreach (string optionLine in System.IO.File.ReadAllLines(configFilePath))
            {
                string[] option = optionLine.Split('=');
                switch (option[0])
                {
                    case "GuildID":
                        GuildID = option[1];
                        break;
                    case "BotToken":
                        BotToken = option[1];
                        break;
                    case "DefaultDevice":
                        DefaultDeviceFriendlyName= option[1];
                        break;
                    case "DefaultChannel":
                        DefaultChannelID = option[1];
                        break;
                    case "AdminUsers":
                        AdminUsers = option[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                        break;
                    case "AdminRoles":
                        AdminRoles = option[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                        break;
                }
            }
            Validate();
        }

        /// <summary>
        /// Validates the configuration and writes it to the specified file path. Overwrites existing files.
        /// </summary>
        /// <param name="configFilePath">The path to the (new) configuration file</param>
        public void Write(string configFilePath)
        {
            Validate();
            string[] options =
            {
                string.Join('=', "GuildID", GuildID),
                string.Join('=', "BotToken", BotToken),
                string.Join('=', "DefaultDevice", DefaultDeviceFriendlyName),
                string.Join('=', "DefaultChannel", DefaultChannelID),
                string.Join('=', "AdminUsers", string.Join(',', AdminUsers.ToArray())),
                string.Join('=', "AdminRoles", string.Join(',', AdminRoles.ToArray()))
            };
            File.WriteAllLines(configFilePath, options);
        }

        /// <summary>
        /// Performs basic validation on the config object, to check whether required fields are set and valid data types.
        /// Does not check whether Guild/Channel IDs or the bot token is actually available/accessible in Discord.
        /// </summary>
        /// <exception cref="ConfigValidationFailedException">Exception thrown if validation fails</exception>
        public void Validate()
        {
            // Validate Guild ID
            if (string.IsNullOrWhiteSpace(GuildID))
                throw new ConfigValidationFailedException("The Guild ID is empty.");
            else
            {
                try { Int64.Parse(GuildID); }
                catch (Exception) { throw new ConfigValidationFailedException("The Guild ID is invalid."); }
            }

            // Validate Bot Token
            if (string.IsNullOrWhiteSpace(BotToken))
                throw new ConfigValidationFailedException("The bot token is empty.");

            // Validate channel ID if set
            if (!string.IsNullOrWhiteSpace(DefaultChannelID))
            {
                try { Int64.Parse(DefaultChannelID); }
                catch (Exception) { throw new ConfigValidationFailedException("The default channel ID is invalid."); }
            }
        }

        /// <summary>
        /// Exception for errors in validating a configuration object
        /// </summary>
        public class ConfigValidationFailedException : Exception { public ConfigValidationFailedException(string message) : base(message) { } }
    }
}
