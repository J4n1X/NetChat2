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
    public class BaseCommand
    {
        public Guid SenderGuid { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public CommandTypes Type { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public Commands Command { get; set; }
        public DateTime CreatedAt { get; set; }

        public Dictionary<string, string> Data { get; set; }

        public BaseCommand(Guid guid, CommandTypes commandType, Commands command, Dictionary<string, string> data = null)
        {
            SenderGuid = guid;
            Type = commandType;
            Command = command;
            CreatedAt = DateTime.Now;
            if (data != null) Data = data;
        }
    }

    public enum CommandTypes
    {
        Client = 0,
        Server = 1
    }

    public enum Commands
    {
        ClientConnect,
        ClientDisconnect,
        ClientText,

        ServerReady,
        ServerConnectSuccess,
        ServerInvalidCommand,
        ServerConnectFailInvalidUserName,
        ServerText,
        ServerClosing,
    }

    #endregion

    public class Serializer
    {
        private readonly JsonSerializer serializer;
        /// <summary>
        /// Serialize the message to a JSON String.
        /// IMPORTANT: This is preferred over serializing XML
        /// </summary>
        /// <param name="toSerialize">The Object that is to be serialized</param>
        /// <returns>A JSON representation of this object</returns>
        public string Serialize(BaseCommand toSerialize)
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
        public BaseCommand Deserialize(byte[] jsonStringBytes) => Deserialize(Encoding.ASCII.GetString(jsonStringBytes));

        /// <summary>
        /// Deserializes a JSON string into the type specified.
        /// </summary>
        /// <typeparam name="TValue">The Type of the deserialized Object</typeparam>
        /// <param name="jsonString">The serialized Object String</param>
        /// <returns></returns>
        public BaseCommand Deserialize(string jsonString)
        {
            using StringReader reader = new StringReader(jsonString);
            using JsonTextReader jsonReader = new JsonTextReader(reader);
            return serializer.Deserialize<BaseCommand>(jsonReader);
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
