using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetChat2
{
    /* Flags are as follows:
 * Upper field are Type-Specific arguments
 * Lower Field are Types
 * 0000 0000 Text, ASCII
 * 0001 0000 Text, UTF-8
 * 0010 0000 Text, UTF-16 (Unicode)
 * 0100 0000 Text, UTF-32
 * 1xxx 0000 Text, without Username
 * 0000 0001 Data, FCT 4 Format
 * 0001 0001 Data, Raw
 * 
 * 0000 0010 Client Command, connect
 * 0001 0010 Client Command, disconnect
 * 0010 0010 Client Command, Unused
 * 0100 0010 Client Command, Unused
 * 1000 0010 Client Command, extended
 * 
 * 0000 0100 Server Response, ready
 * 0001 0100 Server Response, connect success
 * 0010 0100 Server Response, invalid command
 * 0100 0100 Server Response, connect error: Invalid Username
 * 1000 0100 Server Response, extended 
 * 
 * 0000 1000 Advanced Server Command, Request to Server
 * 0001 1000 Advanced Server Command, Response from Server
 * 0010 1000 Advanced Server Command, Request to Client
 * 0100 1000 Advanced Server Command, Response from Client
 * 1000 1000 Advanced Server Command, Error
 * 
 * Extended fields can contain new, text-commands that are up to 255 bytes long
 */
    #region Flag Enums
    [Flags]
    public enum TextFlags : byte
    {
        Base = 0b00000000,
        Ascii = 0b00000000,
        Utf8 = 0b00010000,
        Unicode = 0b00100000,
        Utf32 = 0b01000000,
        NoUserName = 0b10000000
    }
    [Flags]
    public enum DataFlags : byte
    {
        Base = 0b00000001, 
        Fct4Data = 0b00000001, 
        Raw = 0b00010001
    }
    [Flags]
    public enum ClientFlags : byte
    {
        Base = 0b00000010,
        Connect = 0b00000010,
        Disconnect = 0b00010010,
        Extended = 0b10000010
    }

    [Flags]
    public enum ServerFlags : byte
    {
        Base = 0b00000100,
        Ready = 0b00000100,
        ConnectSuccess = 0b00010100,
        InvalidCommand = 0b00100100,
        InvalidUserName = 0b01000100,
        Extended = 0b10000100
    }

    [Flags]
    public enum AdvancedFlags : byte
    {
        Base = 0b00001000, // Request to server
        ServerRequest = 0b00001000, // Request to server
        ServerResponse = 0b00011000, // Response from Server
        ClientRequest = 0b00101000,
        ClientResponse = 0b01001000,
        Error = 0b10001000
    }

    #endregion

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

        public static string GetText(byte[] data, TextFlags flags)
        {
            switch (flags)
            {
                default:
                case TextFlags.Ascii:
                    Console.WriteLine("Encoding 0");
                    return Encoding.ASCII.GetString(data);
                    
                case TextFlags.Utf8:
                    Console.WriteLine("Encoding 1");
                    return Encoding.UTF8.GetString(data);
                case TextFlags.Unicode:
                    Console.WriteLine("Encoding 2");
                    return Encoding .Unicode.GetString(data);
                case TextFlags.Utf32:
                    Console.WriteLine("Encoding 3");
                    return Encoding.UTF32.GetString(data);
            }
        }

        public static byte[] GetTextBytes(string text, TextFlags flags)
        {
            switch (flags)
            {
                default:
                case TextFlags.Ascii:
                    Console.WriteLine("Encoding 0");
                    return Encoding.ASCII.GetBytes(text);
                case TextFlags.Utf8:
                    Console.WriteLine("Encoding 1");
                    return Encoding.UTF8.GetBytes(text);
                case TextFlags.Unicode:
                    Console.WriteLine("Encoding 2");
                    return Encoding.Unicode.GetBytes(text);
                case TextFlags.Utf32:
                    Console.WriteLine("Encoding 3");
                    return Encoding.UTF32.GetBytes(text);
            }
        }
        
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
