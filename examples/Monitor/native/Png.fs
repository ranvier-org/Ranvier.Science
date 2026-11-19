// A copy of AstroShowcase/Png.fs, under the namespace of this page.
module SequentialMonitor.Png

open System
open System.Buffers.Binary
open System.IO
open System.IO.Compression

module PngEncoder =
    let private crcTable =
        Array.init 256 (fun n ->
            let mutable c = uint32 n
            for _ = 0 to 7 do
                if (c &&& 1u) <> 0u then c <- 0xEDB88320u ^^^ (c >>> 1)
                else c <- c >>> 1
            c)

    let private writeChunk (out: Stream) (chunkType: string) (data: byte[]) =
        let hdr = Array.zeroCreate<byte> 8
        BinaryPrimitives.WriteUInt32BigEndian(hdr.AsSpan(0, 4), uint32 data.Length)
        hdr[4] <- byte chunkType[0]
        hdr[5] <- byte chunkType[1]
        hdr[6] <- byte chunkType[2]
        hdr[7] <- byte chunkType[3]
        out.Write(hdr, 0, 8)
        if data.Length > 0 then
            out.Write(data, 0, data.Length)

        let mutable crc = 0xFFFF_FFFFu
        for i = 4 to 7 do
            crc <- crcTable[int ((crc ^^^ uint32 hdr[i]) &&& 0xFFu)] ^^^ (crc >>> 8)
        for i = 0 to data.Length - 1 do
            crc <- crcTable[int ((crc ^^^ uint32 data[i]) &&& 0xFFu)] ^^^ (crc >>> 8)

        let crcBytes = Array.zeroCreate<byte> 4
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes.AsSpan(0, 4), crc ^^^ 0xFFFF_FFFFu)
        out.Write(crcBytes, 0, 4)

    let saveRgba8 (path: string) (width: int) (height: int) (rgba: byte[]) =
        let dir = Path.GetDirectoryName path
        if not (String.IsNullOrEmpty dir) then
            Directory.CreateDirectory dir |> ignore

        use fs = File.Create path
        fs.Write([| 137uy; 80uy; 78uy; 71uy; 13uy; 10uy; 26uy; 10uy |], 0, 8)

        let ihdr = Array.zeroCreate<byte> 13
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), uint32 width)
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), uint32 height)
        ihdr[8] <- 8uy
        ihdr[9] <- 6uy
        ihdr[10] <- 0uy
        ihdr[11] <- 0uy
        ihdr[12] <- 0uy
        writeChunk fs "IHDR" ihdr

        use idatMs = new MemoryStream()
        do
            use zlib = new ZLibStream(idatMs, CompressionLevel.Optimal, leaveOpen = true)
            let rowStride = width * 4
            let scanline = Array.zeroCreate<byte> (1 + rowStride)
            scanline[0] <- 0uy
            for y = 0 to height - 1 do
                Buffer.BlockCopy(rgba, y * rowStride, scanline, 1, rowStride)
                for x = 0 to width - 1 do
                    scanline[4 + x * 4] <- 255uy
                zlib.Write(scanline, 0, scanline.Length)

        writeChunk fs "IDAT" (idatMs.ToArray())
        writeChunk fs "IEND" Array.empty<byte>
