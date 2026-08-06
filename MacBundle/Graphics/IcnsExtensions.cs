using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using PowerKit;
using PowerKit.Extensions;

namespace MacBundle.Graphics;

internal static class IcnsExtensions
{
    extension(Icon icon)
    {
        private void SaveIcnsToSeekable(Stream stream)
        {
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

            var imagesBySize = icon
                // Only square images are supported
                .Images.Where(i => i.Width == i.Height)
                // Width must be a power of two
                .Where(i => BitOperations.IsPow2(i.Width))
                .DistinctBy(i => i.Width)
                .ToDictionary(i => i.Width, i => i);

            if (!imagesBySize.Any())
            {
                throw new InvalidOperationException(
                    "No supported icon sizes are available for the ICNS output."
                );
            }

            // Magic
            stream.Write("icns"u8);

            // Length (will overwrite later)
            var lengthPortal = stream.CreatePortal();
            writer.WriteBigEndian(0u);

            foreach (var (size, image) in imagesBySize)
            {
                // Type
                var typeExponent = BitOperations.Log2((uint)size);
                var typeCode = typeExponent switch
                {
                    < 7 => "icp" + typeExponent,
                    < 10 => "ic0" + typeExponent,
                    _ => "ic" + typeExponent,
                };

                writer.Write(typeCode.ToCharArray()); // cast to array to avoid length prefix

                // Length (will overwrite later)
                var entryLengthPortal = stream.CreatePortal();
                writer.WriteBigEndian(0u);

                // Image data
                image.SavePng(stream);

                // Update length
                var entryLength = stream.Position - entryLengthPortal.Position + sizeof(uint);
                using (entryLengthPortal.Jump())
                    writer.WriteBigEndian(checked((uint)entryLength));
            }

            // Update length
            var length = stream.Position - lengthPortal.Position + sizeof(uint);
            using (lengthPortal.Jump())
                writer.WriteBigEndian(checked((uint)length));
        }

        public void SaveIcns(Stream stream)
        {
            if (!stream.CanSeek)
            {
                using var seekableStream = new MemoryWriteStream(stream);
                icon.SaveIcnsToSeekable(seekableStream);
            }
            else
            {
                icon.SaveIcnsToSeekable(stream);
            }
        }
    }
}
