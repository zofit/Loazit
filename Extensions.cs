using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Loazit
{
    internal static class Extensions
    {
        public static string ToHex(this ICollection<byte> bytes)
        {
            var stringBuilder = new StringBuilder(bytes.Count * 2);
            foreach (var element in bytes)
            {
                stringBuilder.AppendFormat("{0:x2}", element);
            }

            return stringBuilder.ToString();
        }

        public static byte[] Read(this Stream stream)
        {
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.GetBuffer();
            }
        }
    }
}
