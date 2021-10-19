using System;
using System.Collections.Generic;
using System.Linq;

namespace NetChat2
{
    public sealed class AdvancedCommands
    {
        public sealed class Server
        {
            public static void GetClientUUIDs(CommandObject command)
            {
                var retDictionary = new Dictionary<Guid, string>();
                foreach (var client in NetChat2.Server.Clients)
                    retDictionary.Add(client.Key, client.Value.UserName);

                var sendCommand = new CommandObject(
                    NetChat2.Server.Guid,
                    CommandTypes.Server,
                    BaseCommand.SERVER_ADVANCEDCOMMANDRESULT,
                    ("COMMAND", command.Data["COMMAND"]),
                    ("RESULT", ChatBase.JsonSerializer.Serialize(retDictionary))
                    );

                NetChat2.Server.Clients[command.SenderGuid].Write(sendCommand);
            }


            public static void Whisper(CommandObject command)
            {
                var commandArgs = ChatBase.JsonSerializer.Deserialize<string[]>(command.Data["ARGS"]);
                var lastUserIndex = Array.FindLastIndex(commandArgs, (string x) => x.Trim().StartsWith('@'));
                var selectedUsers = commandArgs[..(lastUserIndex + 1)].ToArray();
                for (int i = 0; i < selectedUsers.Length; i++)
                    selectedUsers[i] = selectedUsers[i][1..];

                var text = string.Join("", commandArgs[(lastUserIndex + 1)..]).TrimStart();

                // TODO: support for selecting a specific users if two users share the same name
                var serverClients = NetChat2.Server.Clients.Where(
                    x => selectedUsers.Any(
                        y => x.Value.UserName == y
                        ) || x.Key == command.SenderGuid
                    ).ToArray();

                // Self and Target at minimum. Not distinct yet, because you can target yourself.
                if (serverClients.Length < 2)
                {
                    var errorCommand = new CommandObject(
                        NetChat2.Server.Guid,
                        CommandTypes.Server,
                        BaseCommand.SERVER_INVALID_COMMAND,
                        ("REASON", Errors.Reasons.INVALID_COMMAND_ARGS.ToString())
                        );
                    NetChat2.Server.Clients[command.SenderGuid].Write(errorCommand);
                    return;
                }

                serverClients = serverClients.Distinct().ToArray();

                var sendCommand = new CommandObject(
                    NetChat2.Server.Guid,
                    CommandTypes.Server,
                    BaseCommand.SERVER_ADVANCEDCOMMANDRESULT,
                    ("COMMAND", command.Data["COMMAND"]),
                    ("COMMANDSENDERGUID", command.SenderGuid.ToString()),
                    ("USERNAME", NetChat2.Server.Clients[command.SenderGuid].UserName),
                    ("TEXT", text)
                    );

                foreach (var client in serverClients)
                    client.Value.Write(sendCommand);
            }
        }

        public sealed class Client
        {
            public sealed class Preparation
            {

            }

            public sealed class Processing
            {

                public static string FormatUserUUIDs(CommandObject command)
                {
                    string tableString = "User-IDs of connected Users:";
                    var clients = ChatBase.JsonSerializer.Deserialize<Dictionary<Guid, string>>(command.Data[""]);
                    foreach (var client in clients)
                        tableString += client.Key + "\t" + client.Value + "\n";
                    return tableString;
                }

                public static string FormatWhisper(CommandObject command) => $"(whisper) <{command.Data["USERNAME"]}> {command.Data["TEXT"]}\n";
            }
        }
    }
}
