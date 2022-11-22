/*
 * Transmitting info over the data tubes can take a while.
 *
 * Factors out of control affecting speed:
 * connection speed
 * geography
 * hardware
 *
 * You can control the size of data to send
 * Compression can make data slimmer
 *
 * A Quick Note about MessagePack
 *
When I posted the last section on Reddit, a user commented that serializing the Packet information to MessagePack would have been more efficient than using JSON.  
If you don't know what that is, it's essentially a compressed version of JSON (and it's fast too).  
So if your sending JSON like information over a network, you might want to take a look into using MessagePack instead.

 */

using System.Diagnostics;
using System.IO.Compression; // GZipStream and DeflateStream
// These streams use DEFLATE algorithm to compress data to lossless format, implemented with zlib library

// DeflateStream only compresses
// GZipStream compresses and adds some extra information (CRC) so you can save data in .gz file

// Use any compression in your application from NuGet or dif algo. These two are built in and easy

namespace Compression
{
    class CompressionExample
    {
        // Tiny Helper to get number in MegaBytes
        public static float ComputeSizeInMB(long size)
        {
            return (float)size / 1024f / 1024f;
        }

        public static void Main(string[] args)
        {
            // Our testing file
            string fileToCompress = "image.bmp";
            byte[] uncompressedBytes = File.ReadAllBytes(fileToCompress);

            // benchmarking
            Stopwatch timer = new Stopwatch();

            // Display info
            long uncompressedFileSize = uncompressedBytes.LongLength;
            Console.WriteLine("{0} uncompressed is {1:0.0000} MB large", fileToCompress,
                ComputeSizeInMB(uncompressedFileSize));

            // Compress it using Deflate (optimal)
            using (MemoryStream compressedStream = new MemoryStream())
            {
                // init
                DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionLevel.Optimal, true);

                // Run the compression
                timer.Start();
                deflateStream.Write(uncompressedBytes, 0, uncompressedBytes.Length);
                deflateStream.Close();
                timer.Stop();

                // Print some info
                long compressedFileSize = compressedStream.Length;
                Console.WriteLine("Compressed using DeflateStream (Optimal): {0:0.0000} MB [{1:0.00}%] in {2}ms",
                    ComputeSizeInMB(compressedFileSize),
                    100f * (float)compressedFileSize / (float)uncompressedFileSize,
                    timer.ElapsedMilliseconds);

                // cleanup
                timer.Reset();
            }
            // Compress using fast
            using (MemoryStream compressedStream = new MemoryStream())
            {
                // init
                DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionLevel.Fastest, true);

                // Run the compression
                timer.Start();
                deflateStream.Write(uncompressedBytes, 0, uncompressedBytes.Length);
                deflateStream.Close();
                timer.Stop();

                // Print some info
                long compressedFileSize = compressedStream.Length;
                Console.WriteLine("Compressed using DeflateStream (Fast): {0:0.0000} MB [{1:0.00}%] in {2}ms",
                    ComputeSizeInMB(compressedFileSize),
                    100f * (float)compressedFileSize / (float)uncompressedFileSize,
                    timer.ElapsedMilliseconds);

                // cleanup
                timer.Reset();
            }

            // Compress it using GZip (save it)
            string savedArchive = fileToCompress + ".gz";
            using (MemoryStream compressedStream = new MemoryStream())
            {
                // init
                GZipStream gzipStream = new GZipStream(compressedStream, CompressionMode.Compress, true);

                // Run the compression
                timer.Start();
                gzipStream.Write(uncompressedBytes, 0, uncompressedBytes.Length);
                gzipStream.Close();
                timer.Stop();

                // Print some info
                long compressedFileSize = compressedStream.Length;
                Console.WriteLine("Compressed using GZipStream: {0:0.0000} MB [{1:0.00}%] in {2}ms",
                    ComputeSizeInMB(compressedFileSize),
                    100f * (float)compressedFileSize / (float)uncompressedFileSize,
                    timer.ElapsedMilliseconds);

                // Save it
                using (FileStream saveStream = new FileStream(savedArchive, FileMode.Create))
                {
                    compressedStream.Position = 0;
                    compressedStream.CopyTo(saveStream);
                }

                // cleanup
                timer.Reset();
            }
        }
    }
}

