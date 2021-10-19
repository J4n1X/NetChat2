using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json.Converters;

namespace NetChat2
{


    /* I hate writing up Protocol Specifications, but if I don't do it, I will mess everything up.
     * Anyways, here's how communication will function.
     * Every single packet sent is lead by a 4-byte long "length" field
     * After this, most data is sent encoded as JSON.
     * The only exception will be raw file data, which will follow a data client command field
     * 
     * Commands are split into two categories: Client Commands, which are commands from the client to the server,
     * and Server Commands, which are the opposite.
     */


    #region Command Classes
    public class CommandObject
    {
        public Guid SenderGuid { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public CommandTypes Type { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public BaseCommand Command { get; set; }
        public DateTime CreatedAt { get; set; }

        public Dictionary<string, string> Data { get; set; }

        public CommandObject(Guid guid, CommandTypes commandType, BaseCommand command, params ValueTuple<string, string>[] data)
        {
            SenderGuid = guid;
            Type = commandType;
            Command = command;
            CreatedAt = DateTime.Now;
            Data ??= data.ToDictionary(x => x.Item1, x => x.Item2);
        }

        [JsonConstructor]
        public CommandObject(Guid guid, CommandTypes commandType, BaseCommand command, Dictionary<string, string> data = null)
        {
            SenderGuid = guid;
            Type = commandType;
            Command = command;
            CreatedAt = DateTime.Now;
            Data ??= data;
        }
    }

    public enum CommandTypes
    {
        Client = 0,
        Server = 1
    }

    public enum BaseCommand
    {
        CLIENT_CONNECT,                          // Used to request a connection
        CLIENT_ABORT,                            // Used to abort a connection request
        CLIENT_DISCONNECT,                       // Used to announce a disconnect
        CLIENT_TEXT,                             // Used to broadcast text to all other clients
        CLIENT_ADVANCEDCOMMAND,                  // Used to order server to process Command

        SERVER_READY,                            // Used to signal client that the server is ready
        SERVER_CONNECT_SUCCESS,                  // Used to signal client that the connection was established
        SERVER_INVALID_COMMAND,                  // Used to reject client in case of invalid command
        SERVER_CONNECTION_FAILED,                // Used to reject client connection attempt because of invalid args
        SERVER_TEXT,                             // Used to broadcast a server message to all clients
        SERVER_ADVANCEDCOMMANDRESULT,            // Used to return Command result
        SERVER_CLOSING,                          // Used to broadcast to all clients that the server is stopping
    }

    public enum AdvancedClientCommand
    {
        GETUSERUUIDS,
        WHISPER,
    }

    public sealed class Errors
    {
        public enum Reasons
        {
            INVALID_USERNAME,                           // When Username is invalid
            VERSION_MISMATCH,                           // When Server and Client Versions don't match
            INVALID_COMMAND,                            // When the Command is invalid or does not exist
            INVALID_COMMAND_ARGS,                       // When the Command arguments are invalid or missing
            INVALID_COMMAND_CONTEXT,                    // When the Command is not allowed in this context
        }

        // base class for indexer
        public class ReasonTexts
        {
            private static readonly Dictionary<Reasons, string> texts = new Dictionary<Reasons, string> {
                    {Reasons.INVALID_USERNAME, "The Username specified is invalid."},
                    {Reasons.VERSION_MISMATCH, "The Versions of Client and Server do not match."},
                    {Reasons.INVALID_COMMAND, "The Command specified is invalid or does not exist."},
                    {Reasons.INVALID_COMMAND_ARGS, "The Command Arguments specified are invalid."}
                };
            public string this[Reasons r]
            {
                get
                {
                    return texts[r] ?? "An error occurred while processing the request.";
                }
            }

            public string this[string s]
            {
                get
                {
                    return texts[(Reasons)Enum.Parse(typeof(Reasons), s)];
                }
            }
        }

        private static readonly ReasonTexts texts = new ReasonTexts();
        public static ReasonTexts ReasonText { get { return texts; } }

    }

    #endregion

    public class InvalidVersionException : Exception
    {
        public Version ClientVersion { get; set; }
        public Version ServerVersion { get; set; }
        public InvalidVersionException(
            Version clientVersion, 
            Version serverVersion, 
            string message = "Client-Server version mismatch") 
            : base(message)
        {
            ClientVersion = clientVersion;
            ServerVersion = serverVersion;
        }
    }

    public class InvalidCommandException : Exception
    {
        public Version ProgramVersion { get; set; }
        public InvalidCommandException(string message = "Invalid Command received") : base(message)
        {
        }
    }

    public class Serializer
    {
        private readonly JsonSerializer serializer;
        /// <summary>
        /// Serialize the message to a JSON String.
        /// IMPORTANT: This is preferred over serializing XML
        /// </summary>
        /// <param name="toSerialize">The Object that is to be serialized</param>
        /// <returns>A JSON representation of this object</returns>
        public string Serialize<TValue>(TValue toSerialize)
        {
            using StringWriter writer = new StringWriter();
            serializer.Serialize(writer, toSerialize);
            return writer.ToString();
        }

        /// <summary>
        /// Deserializes a JSON string (represented as a byte array) into the type specified.
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="jsonStringBytes"></param>
        /// <returns></returns>
        public T Deserialize<T>(byte[] jsonStringBytes) => Deserialize<T>(Encoding.ASCII.GetString(jsonStringBytes));

        /// <summary>
        /// Deserializes a JSON string into the type specified.
        /// </summary>
        /// <typeparam name="TValue">The Type of the deserialized Object</typeparam>
        /// <param name="jsonString">The serialized Object String</param>
        /// <returns></returns>
        public T Deserialize<T>(string jsonString)
        {
            using StringReader reader = new StringReader(jsonString);
            using JsonTextReader jsonReader = new JsonTextReader(reader);
            return serializer.Deserialize<T>(jsonReader);
        }

        /// <summary>
        /// Deserializes an XML string into the type specified
        /// </summary>
        /// <typeparam name="TValue">The Type of the deserialized Object</typeparam>
        /// <param name="xmlString">The serialized Object String</param>
        /// <returns></returns>
        public TValue DeserializeXML<TValue>(string xmlString)
        {
            var deserializer = new XmlSerializer(typeof(TValue));
            using StringReader reader = new StringReader(xmlString);
            return (TValue)deserializer.Deserialize(reader);
        }

        /// <summary>
        /// Serialize the message to a XML String.
        /// IMPORTANT: It is always preferred to use JSON over XML
        /// </summary>
        /// <param name="toSerialize">The Object that is to be serialized</param>
        /// <returns>An XML representation of this object</returns>
        public string SerializeXML<T>(T toSerialize)
        {
            var serializer = new XmlSerializer(toSerialize.GetType());
            using StringWriter writer = new StringWriter();
            serializer.Serialize(writer, toSerialize);
            return serializer.ToString();
        }

        public Serializer()
        {
            serializer = new JsonSerializer();
            serializer.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
        }
    }

    abstract class ChatBase : IDisposable
    { 
        protected bool available = false;
        public static Serializer JsonSerializer = new Serializer();

        public bool Available
        {
            get { return available; }
        }

        protected int port;
        public int Port
        {
            get { return port; }
        }

        protected IPAddress localAddress;
        public IPAddress LocalAddress
        {
            get { return localAddress; }
        }

        protected volatile bool disposing = false;

        
        public void Dispose()
        {
            disposing = true;
        }

        ~ChatBase()
        {
            disposing = true;
        }
    }
}
