using System;
using System.Collections.Generic;

namespace NetChat2
{
    public sealed class AdvancedCommands
    {

        public static byte[] GetUsernameGuidTable(Guid[] guids, string[] userNames)
        {
            if (userNames.Length != guids.Length)
                throw new ArgumentException("Array Size mismatch.");
            var buffer = new List<byte>();
            for (int i = 0; i < userNames.Length; i++)
            {
                byte[] textBytes = ChatBase.GetTextBytes(userNames[i], TextFlags.Unicode);
                buffer.AddRange(BitConverter.GetBytes(Convert.ToUInt16(textBytes.Length)));
                buffer.AddRange(textBytes);
                buffer.AddRange(guids[i].ToByteArray());
            }
            return buffer.ToArray();
        }
    }
}
