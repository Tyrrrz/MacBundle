using System.IO;

namespace MacBundle.Utils.Extensions;

internal static class StreamExtensions
{
    extension(Stream stream)
    {
        public byte[] ReadAllBytes()
        {
            if (stream is MemoryStream memoryStream)
                return memoryStream.ToArray();

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);

            return buffer.ToArray();
        }
    }
}
