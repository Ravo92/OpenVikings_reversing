namespace OpenVikings.NXBasics
{
    internal static class XBTools
    {
        // --------------------------------------------------------------------
        // Constants / Enums
        // --------------------------------------------------------------------

        internal enum TEncryptMode : int
        {
            None = 0,
            Mode1 = 1
        }

        internal enum TPackAlgorithm : uint
        {
            None = 0x6E6F6E65u, // 'none'
            Rle6 = 0x726C6536u, // 'rle6' (16-bit)
            Rle8 = 0x726C6538u  // 'rle8' (8-bit)
        }

        internal enum TStorableId : uint
        {
        }

        // --------------------------------------------------------------------
        // Token support (minimal; original is a 0x10-stride array terminated by null/empty name)
        // --------------------------------------------------------------------

        internal readonly struct SToken
        {
            internal readonly string? Name;
            internal readonly uint Token;

            internal SToken(string? name, uint token)
            {
                Name = name;
                Token = token;
            }
        }

        // --------------------------------------------------------------------
        // Character helpers
        // --------------------------------------------------------------------

        // NXBasics::XB_Character_IsLetterOrNumber(char)
        internal static bool XB_Character_IsLetterOrNumber(char value)
        {
            return Dexter.DexterString.IsAlphaNum(value);
        }

        // NXBasics::XB_Character_IsUseable(char)
        internal static bool XB_Character_IsUseable(char value)
        {
            bool alphaNum = Dexter.DexterString.IsAlphaNum(value);
            return alphaNum || value == '_';
        }

        // NXBasics::XB_Character_IsLetter(char)
        internal static bool XB_Character_IsLetter(char value)
        {
            return Dexter.DexterString.IsAlpha(value);
        }

        // NXBasics::XB_Character_IsWhiteSpace(char)
        internal static bool XB_Character_IsWhiteSpace(char value)
        {
            return value == ' ';
        }

        // NXBasics::XB_Character_IsNumber(char)
        internal static bool XB_Character_IsNumber(char value)
        {
            return Dexter.DexterString.IsDigit(value);
        }

        // --------------------------------------------------------------------
        // Binary convert (bitwise reinterpret, sensible for "T32u")
        // --------------------------------------------------------------------

        // NXBasics::XB_BinaryConvert_FloatToT32u(float)
        internal static uint XB_BinaryConvert_FloatToT32u(float value)
        {
            return BitConverter.SingleToUInt32Bits(value);
        }

        // NXBasics::XB_BinaryConvert_T32uToFloat(unsigned int)
        internal static float XB_BinaryConvert_T32uToFloat(uint value)
        {
            return BitConverter.UInt32BitsToSingle(value);
        }

        // --------------------------------------------------------------------
        // Encrypt / Decrypt (managed byte[]; matches decompile arithmetic)
        // --------------------------------------------------------------------

        // NXBasics::XB_Encrypt_Memory(void*, unsigned int, NXBasics::TEncryptMode)
        internal static void XB_Encrypt_Memory(byte[] buffer, int offset, int length, TEncryptMode mode)
        {
            if (buffer == null)
            {
                return;
            }

            if (length <= 0 || mode != TEncryptMode.Mode1)
            {
                return;
            }

            if (offset < 0 || length < 0 || offset > buffer.Length - length)
            {
                throw new ArgumentOutOfRangeException();
            }

            byte b = 0x47;
            byte c1 = (byte)'~';
            byte c2 = (byte)'~';

            int i = 0;

            if (length > 1)
            {
                int evenLen = length & ~1;
                while (i < evenLen)
                {
                    int idx0 = offset + i;
                    int idx1 = idx0 + 1;

                    byte in0 = buffer[idx0];
                    byte in1 = buffer[idx1];

                    buffer[idx0] = unchecked((byte)((in0 ^ b) + 1));

                    // C precedence: a ^ (c1 + b)
                    byte key1 = unchecked((byte)(c1 + b));
                    buffer[idx1] = unchecked((byte)((in1 ^ key1) + 1));

                    // b = c1 + b + c2 + '!'
                    b = unchecked((byte)(c1 + b + c2 + (byte)'!'));
                    // c1 = c2 + 'B'
                    c1 = unchecked((byte)(c2 + (byte)'B'));
                    // c2 = c2 + 'B'
                    c2 = unchecked((byte)(c2 + (byte)'B'));

                    i += 2;
                }
            }

            if ((length & 1) != 0)
            {
                int idx = offset + i;
                buffer[idx] = unchecked((byte)((buffer[idx] ^ b) + 1));
            }
        }

        // NXBasics::XB_Decrypt_Memory(void*, unsigned int, NXBasics::TEncryptMode)
        internal static void XB_Decrypt_Memory(byte[] buffer, int offset, int length, TEncryptMode mode)
        {
            if (buffer == null)
            {
                return;
            }

            if (length <= 0 || mode != TEncryptMode.Mode1)
            {
                return;
            }

            if (offset < 0 || length < 0 || offset > buffer.Length - length)
            {
                throw new ArgumentOutOfRangeException();
            }

            byte b = 0x47;
            byte c = (byte)'~';

            int i = 0;

            if (length > 1)
            {
                int evenLen = length & ~1;
                while (i < evenLen)
                {
                    int idx0 = offset + i;
                    int idx1 = idx0 + 1;

                    // decrypt: out = (in - 1) ^ key
                    buffer[idx0] = unchecked((byte)(((buffer[idx0] - 1) ^ b)));

                    byte key1 = unchecked((byte)(c + b));
                    buffer[idx1] = unchecked((byte)(((buffer[idx1] - 1) ^ key1)));

                    // b = c + b + c + '!'
                    b = unchecked((byte)(c + b + c + (byte)'!'));
                    // c = c + 'B'
                    c = unchecked((byte)(c + (byte)'B'));

                    i += 2;
                }
            }

            if ((length & 1) != 0)
            {
                int idx = offset + i;
                buffer[idx] = unchecked((byte)(((buffer[idx] - 1) ^ b)));
            }
        }

        // --------------------------------------------------------------------
        // String duplicate (managed)
        // --------------------------------------------------------------------

        // NXBasics::XB_DuplicateString(char const*)
        internal static string? XB_DuplicateString(string? value)
        {
            if (value == null)
            {
                return null;
            }

            if (value.Length == 0)
            {
                return string.Empty;
            }

            return new string(value.ToCharArray());
        }

        // --------------------------------------------------------------------
        // Checksum (best-effort faithful to decompile; operates on bytes)
        // --------------------------------------------------------------------

        // NXBasics::XB_GetMemoryChecksum(void const*, unsigned int)
        internal static uint XB_GetMemoryChecksum(byte[]? data, int offset, int length)
        {
            if (data == null)
            {
                return 0x6515D4F1u;
            }

            if (length <= 0)
            {
                return 0x6515D4F1u;
            }

            if (offset < 0 || length < 0 || offset > data.Length - length)
            {
                throw new ArgumentOutOfRangeException();
            }

            uint acc = 0x6515D4F1u;
            uint seed = 0x6FD318ABu;

            uint mix16 = 0;
            uint phase = 0; // cycles 0..3 (derived from decompile intent)

            int i = 0;
            int evenLen = length & ~1;

            while (i < evenLen)
            {
                byte b0 = data[offset + i];
                byte b1 = data[offset + i + 1];

                uint w0 = (uint)b0 | (mix16 << 8);
                uint x0 = w0 ^ seed;

                uint x0ForAcc;
                if (phase == 3)
                {
                    seed = unchecked(x0 + 0x63FE53BAu);
                    x0ForAcc = x0;
                    phase = 1;
                }
                else
                {
                    x0ForAcc = 0;
                    phase++;
                }

                mix16 = (w0 << 8) | b1;

                uint x1 = mix16 ^ seed;
                if (phase == 4)
                {
                    seed = unchecked(x1 + 0x63FE53BAu);
                    x0ForAcc = x1;
                    phase = 0;
                }

                acc ^= (x0ForAcc ^ 0u);

                i += 2;
            }

            if ((length & 1) != 0)
            {
                byte last = data[offset + evenLen];
                uint v = (mix16 << 8) | last;

                uint add = 0;
                if (phase == 4)
                {
                    add = seed ^ v;
                }

                acc ^= add;
                mix16 = 0;
            }

            return acc ^ mix16;
        }

        // --------------------------------------------------------------------
        // Pack header helpers
        // Header (16 bytes, little-endian on disk):
        // 0x00 magic 'Xpcb' (0x5870636b)
        // 0x04 algo  'rle8'/'rle6'/'none'
        // 0x08 unpacked size
        // 0x0C packed size (including header)
        // --------------------------------------------------------------------

        private const uint PackMagicXpcb = 0x5870636Bu;

        private static void WriteUInt32LE(byte[] dst, int offset, uint value)
        {
            dst[offset + 0] = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
            dst[offset + 2] = (byte)(value >> 16);
            dst[offset + 3] = (byte)(value >> 24);
        }

        private static uint ReadUInt32LE(byte[] src, int offset)
        {
            return (uint)src[offset + 0]
                   | ((uint)src[offset + 1] << 8)
                   | ((uint)src[offset + 2] << 16)
                   | ((uint)src[offset + 3] << 24);
        }

        private static ushort ReadUInt16LE(byte[] src, int offset)
        {
            return (ushort)(src[offset + 0] | (src[offset + 1] << 8));
        }

        private static void WriteUInt16LE(byte[] dst, int offset, ushort value)
        {
            dst[offset + 0] = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
        }

        // NXBasics::XB_Pack_GetPackRatio(void const*)
        internal static uint XB_Pack_GetPackRatio(byte[]? packedBlock, int offset)
        {
            if (packedBlock == null)
            {
                return 0xFFFFFFFFu;
            }

            if (offset < 0 || offset > packedBlock.Length - 16)
            {
                return 0xFFFFFFFFu;
            }

            uint magic = ReadUInt32LE(packedBlock, offset + 0);
            if (magic != PackMagicXpcb)
            {
                return 0xFFFFFFFFu;
            }

            uint unpacked = ReadUInt32LE(packedBlock, offset + 8);
            uint packed = ReadUInt32LE(packedBlock, offset + 12);

            if (unpacked == 0)
            {
                return 0xFFFFFFFFu;
            }

            return (uint)((packed * 100u) / unpacked);
        }

        // NXBasics::XB_Pack_GetSizeOfPackedBlockInFile(NXBasics::CFile&)
        internal static uint XB_Pack_GetSizeOfPackedBlockInFile(CFile file)
        {
            if (file == null)
            {
                return 0;
            }

            int pos = file.GetPosition();

            byte[] header = new byte[16];
            int read = file.Read(header, 16);

            file.SeekToPosition(pos);

            if (read < 16)
            {
                return 0;
            }

            return ReadUInt32LE(header, 12);
        }

        // NXBasics::XB_Pack_Pack(NXBasics::TPackAlgorithm, void const*, unsigned int, void*, unsigned int)
        internal static int XB_Pack_Pack(TPackAlgorithm algo, byte[] src, int srcOffset, int srcLength, byte[] dst, int dstOffset, int dstCapacity)
        {
            if (src == null || dst == null)
            {
                return 0;
            }

            if (srcLength < 0 || dstCapacity < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (srcOffset < 0 || srcOffset > src.Length - srcLength)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (dstOffset < 0 || dstOffset > dst.Length - dstCapacity)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (dstCapacity < 16)
            {
                return 0;
            }

            if (algo == TPackAlgorithm.Rle8)
            {
                return XB_Pack_Simple8BitRLEPack(src, srcOffset, srcLength, dst, dstOffset, dstCapacity);
            }

            if (algo == TPackAlgorithm.Rle6)
            {
                return XB_Pack_Simple16BitRLEPack(src, srcOffset, srcLength, dst, dstOffset, dstCapacity);
            }

            if (algo == TPackAlgorithm.None)
            {
                return XB_Pack_SimpleNonePack(src, srcOffset, srcLength, dst, dstOffset, dstCapacity);
            }

            return 0;
        }

        // NXBasics::XB_Pack_UnPack(void const*, void*, unsigned int)
        internal static bool XB_Pack_UnPack(byte[] packed, int packedOffset, int packedLength, byte[] dst, int dstOffset, int dstCapacity)
        {
            if (packed == null || dst == null)
            {
                return false;
            }

            if (packedLength < 16)
            {
                return false;
            }

            if (packedOffset < 0 || packedOffset > packed.Length - packedLength)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (dstOffset < 0 || dstCapacity < 0 || dstOffset > dst.Length - dstCapacity)
            {
                throw new ArgumentOutOfRangeException();
            }

            uint magic = ReadUInt32LE(packed, packedOffset + 0);
            if (magic != PackMagicXpcb)
            {
                return false;
            }

            uint algo = ReadUInt32LE(packed, packedOffset + 4);
            uint unpackedSize = ReadUInt32LE(packed, packedOffset + 8);
            uint packedSize = ReadUInt32LE(packed, packedOffset + 12);

            if (packedSize == 16u)
            {
                return false;
            }

            if (unpackedSize > (uint)dstCapacity)
            {
                return false;
            }

            if (packedSize > (uint)packedLength)
            {
                return false;
            }

            if (algo == (uint)TPackAlgorithm.Rle8)
            {
                return XB_Pack_Simple8BitRLEUnPack(packed, packedOffset, (int)packedSize, dst, dstOffset, dstCapacity);
            }

            if (algo == (uint)TPackAlgorithm.Rle6)
            {
                return XB_Pack_Simple16BitRLEUnPack(packed, packedOffset, (int)packedSize, dst, dstOffset, dstCapacity);
            }

            if (algo == (uint)TPackAlgorithm.None)
            {
                return XB_Pack_SimpleNoneUnPack(packed, packedOffset, (int)packedSize, dst, dstOffset, dstCapacity);
            }

            return false;
        }

        // NXBasics::XB_Pack_SimpleNonePack(void const*, unsigned int, void*, unsigned int)
        internal static int XB_Pack_SimpleNonePack(byte[] src, int srcOffset, int srcLength, byte[] dst, int dstOffset, int dstCapacity)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(srcLength);

            int total = checked(srcLength + 16);
            if (total > dstCapacity)
            {
                return 0;
            }

            WriteUInt32LE(dst, dstOffset + 0, PackMagicXpcb);
            WriteUInt32LE(dst, dstOffset + 4, (uint)TPackAlgorithm.None);
            WriteUInt32LE(dst, dstOffset + 8, unchecked((uint)srcLength));
            WriteUInt32LE(dst, dstOffset + 12, unchecked((uint)total));

            Buffer.BlockCopy(src, srcOffset, dst, dstOffset + 16, srcLength);
            return total;
        }

        // NXBasics::XB_Pack_SimpleNoneUnPack(void const*, void*, unsigned int)
        internal static bool XB_Pack_SimpleNoneUnPack(byte[] packed, int packedOffset, int packedSize, byte[] dst, int dstOffset, int dstCapacity)
        {
            if (packedSize < 16)
            {
                return false;
            }

            uint magic = ReadUInt32LE(packed, packedOffset + 0);
            uint algo = ReadUInt32LE(packed, packedOffset + 4);

            if (magic != PackMagicXpcb || algo != (uint)TPackAlgorithm.None)
            {
                return false;
            }

            uint unpackedSize = ReadUInt32LE(packed, packedOffset + 8);
            if (unpackedSize > (uint)dstCapacity)
            {
                return false;
            }

            Buffer.BlockCopy(packed, packedOffset + 16, dst, dstOffset, (int)unpackedSize);
            return true;
        }

        // --------------------------------------------------------------------
        // 8-bit RLE (simple, faithful format)
        // --------------------------------------------------------------------

        // NXBasics::XB_Pack_Simple8BitRLEPack(void const*, unsigned int, void*, unsigned int)
        internal static int XB_Pack_Simple8BitRLEPack(byte[] src, int srcOffset, int srcLength, byte[] dst, int dstOffset, int dstCapacity)
        {
            WriteUInt32LE(dst, dstOffset + 0, PackMagicXpcb);
            WriteUInt32LE(dst, dstOffset + 4, (uint)TPackAlgorithm.Rle8);
            WriteUInt32LE(dst, dstOffset + 8, unchecked((uint)srcLength));
            WriteUInt32LE(dst, dstOffset + 12, 16u);

            int outPos = dstOffset + 16;
            int outEnd = dstOffset + dstCapacity;

            int i = 0;
            while (i < srcLength)
            {
                byte v = src[srcOffset + i];

                int run = 1;
                while (i + run < srcLength && run < 0x7F && src[srcOffset + i + run] == v)
                {
                    run++;
                }

                if (run >= 3)
                {
                    if (outPos + 2 > outEnd)
                    {
                        return 0;
                    }

                    dst[outPos++] = unchecked((byte)(run | 0x80));
                    dst[outPos++] = v;
                    i += run;
                    continue;
                }

                int litStart = i;
                int litLen = 0;

                while (i < srcLength && litLen < 0x7F)
                {
                    byte cur = src[srcOffset + i];

                    int r = 1;
                    while (i + r < srcLength && r < 3 && src[srcOffset + i + r] == cur)
                    {
                        r++;
                    }

                    if (r >= 3)
                    {
                        break;
                    }

                    i++;
                    litLen++;
                }

                if (litLen == 0)
                {
                    litLen = 1;
                    i = litStart + 1;
                }

                if (outPos + 1 + litLen > outEnd)
                {
                    return 0;
                }

                dst[outPos++] = (byte)litLen;
                Buffer.BlockCopy(src, srcOffset + litStart, dst, outPos, litLen);
                outPos += litLen;
            }

            int packedSize = outPos - dstOffset;
            WriteUInt32LE(dst, dstOffset + 12, unchecked((uint)packedSize));
            return packedSize;
        }

        // NXBasics::XB_Pack_Simple8BitRLEUnPack(void const*, void*, unsigned int)
        internal static bool XB_Pack_Simple8BitRLEUnPack(byte[] packed, int packedOffset, int packedSize, byte[] dst, int dstOffset, int dstCapacity)
        {
            if (packedSize < 16)
            {
                return false;
            }

            uint magic = ReadUInt32LE(packed, packedOffset + 0);
            if (magic != PackMagicXpcb)
            {
                return false;
            }

            uint algo = ReadUInt32LE(packed, packedOffset + 4);
            if (algo != (uint)TPackAlgorithm.Rle8)
            {
                return false;
            }

            uint unpackedSize = ReadUInt32LE(packed, packedOffset + 8);
            uint declaredPackedSize = ReadUInt32LE(packed, packedOffset + 12);

            if (declaredPackedSize == 16u || declaredPackedSize > (uint)packedSize)
            {
                return false;
            }

            if (unpackedSize > (uint)dstCapacity)
            {
                return false;
            }

            int inPos = packedOffset + 16;
            int inEnd = packedOffset + (int)declaredPackedSize;

            int outPos = dstOffset;
            int outEnd = dstOffset + (int)unpackedSize;

            while (inPos < inEnd && outPos < outEnd)
            {
                byte control = packed[inPos++];
                int count = control & 0x7F;

                if ((control & 0x80) != 0)
                {
                    if (inPos >= inEnd)
                    {
                        return false;
                    }

                    byte value = packed[inPos++];

                    if (outPos + count > outEnd)
                    {
                        return false;
                    }

                    for (int k = 0; k < count; k++)
                    {
                        dst[outPos + k] = value;
                    }

                    outPos += count;
                }
                else
                {
                    if (inPos + count > inEnd)
                    {
                        return false;
                    }

                    if (outPos + count > outEnd)
                    {
                        return false;
                    }

                    Buffer.BlockCopy(packed, inPos, dst, outPos, count);
                    inPos += count;
                    outPos += count;
                }
            }

            return outPos == outEnd;
        }

        // --------------------------------------------------------------------
        // 16-bit RLE (input/output are byte buffers; payload is little-endian ushort)
        // --------------------------------------------------------------------

        // NXBasics::XB_Pack_Simple16BitRLEPack(void const*, unsigned int, void*, unsigned int)
        internal static int XB_Pack_Simple16BitRLEPack(byte[] src, int srcOffset, int srcLength, byte[] dst, int dstOffset, int dstCapacity)
        {
            if ((srcLength & 1) != 0)
            {
                return 0;
            }

            WriteUInt32LE(dst, dstOffset + 0, PackMagicXpcb);
            WriteUInt32LE(dst, dstOffset + 4, (uint)TPackAlgorithm.Rle6);
            WriteUInt32LE(dst, dstOffset + 8, unchecked((uint)srcLength));
            WriteUInt32LE(dst, dstOffset + 12, 16u);

            int outPos = dstOffset + 16;
            int outEnd = dstOffset + dstCapacity;

            int wordCount = srcLength / 2;

            int i = 0;
            while (i < wordCount)
            {
                ushort v = ReadUInt16LE(src, srcOffset + i * 2);

                int run = 1;
                while (i + run < wordCount && run < 0x7F && ReadUInt16LE(src, srcOffset + (i + run) * 2) == v)
                {
                    run++;
                }

                if (run >= 3)
                {
                    if (outPos + 1 + 2 > outEnd)
                    {
                        return 0;
                    }

                    dst[outPos++] = unchecked((byte)(run | 0x80));
                    WriteUInt16LE(dst, outPos, v);
                    outPos += 2;
                    i += run;
                    continue;
                }

                int litStart = i;
                int litLen = 0;

                while (i < wordCount && litLen < 0x7F)
                {
                    ushort cur = ReadUInt16LE(src, srcOffset + i * 2);

                    int r = 1;
                    while (i + r < wordCount && r < 3 && ReadUInt16LE(src, srcOffset + (i + r) * 2) == cur)
                    {
                        r++;
                    }

                    if (r >= 3)
                    {
                        break;
                    }

                    i++;
                    litLen++;
                }

                if (litLen == 0)
                {
                    litLen = 1;
                    i = litStart + 1;
                }

                int bytesToCopy = litLen * 2;

                if (outPos + 1 + bytesToCopy > outEnd)
                {
                    return 0;
                }

                dst[outPos++] = (byte)litLen;

                int srcBytes = srcOffset + litStart * 2;
                Buffer.BlockCopy(src, srcBytes, dst, outPos, bytesToCopy);
                outPos += bytesToCopy;
            }

            int packedSize = outPos - dstOffset;
            WriteUInt32LE(dst, dstOffset + 12, unchecked((uint)packedSize));
            return packedSize;
        }

        // NXBasics::XB_Pack_Simple16BitRLEUnPack(void const*, void*, unsigned int)
        internal static bool XB_Pack_Simple16BitRLEUnPack(byte[] packed, int packedOffset, int packedSize, byte[] dst, int dstOffset, int dstCapacity)
        {
            if (packedSize < 16)
            {
                return false;
            }

            uint magic = ReadUInt32LE(packed, packedOffset + 0);
            if (magic != PackMagicXpcb)
            {
                return false;
            }

            uint algo = ReadUInt32LE(packed, packedOffset + 4);
            if (algo != (uint)TPackAlgorithm.Rle6)
            {
                return false;
            }

            uint unpackedBytes = ReadUInt32LE(packed, packedOffset + 8);
            uint declaredPackedSize = ReadUInt32LE(packed, packedOffset + 12);

            if ((unpackedBytes & 1) != 0)
            {
                return false;
            }

            if (declaredPackedSize == 16u || declaredPackedSize > (uint)packedSize)
            {
                return false;
            }

            if (unpackedBytes > (uint)dstCapacity)
            {
                return false;
            }

            int inPos = packedOffset + 16;
            int inEnd = packedOffset + (int)declaredPackedSize;

            int outPos = dstOffset;
            int outEnd = dstOffset + (int)unpackedBytes;

            while (inPos < inEnd && outPos < outEnd)
            {
                byte control = packed[inPos++];
                int count = control & 0x7F;

                if ((control & 0x80) != 0)
                {
                    if (inPos + 2 > inEnd)
                    {
                        return false;
                    }

                    ushort value = ReadUInt16LE(packed, inPos);
                    inPos += 2;

                    int bytes = count * 2;
                    if (outPos + bytes > outEnd)
                    {
                        return false;
                    }

                    for (int k = 0; k < count; k++)
                    {
                        WriteUInt16LE(dst, outPos + k * 2, value);
                    }

                    outPos += bytes;
                }
                else
                {
                    int bytes = count * 2;

                    if (inPos + bytes > inEnd)
                    {
                        return false;
                    }

                    if (outPos + bytes > outEnd)
                    {
                        return false;
                    }

                    Buffer.BlockCopy(packed, inPos, dst, outPos, bytes);
                    inPos += bytes;
                    outPos += bytes;
                }
            }

            return outPos == outEnd;
        }

        // --------------------------------------------------------------------
        // Storable helpers
        // --------------------------------------------------------------------

        // NXBasics::XB_Storable_GetObjectIdAndVersion(NXBasics::CFile&, NXBasics::CStorable::TStorableId&, unsigned int&)
        internal static void XB_Storable_GetObjectIdAndVersion(CFile file, out TStorableId id, out uint version)
        {
            id = 0;
            version = 0;

            if (file == null)
            {
                return;
            }

            ulong size = file.GetSize();
            if (size < 8)
            {
                return;
            }

            int pos = file.GetPosition();

            uint readId = file.ReadLong();
            uint readVer = file.ReadLong();

            id = (TStorableId)readId;
            version = readVer;

            file.SeekToPosition(pos);
        }

        // NXBasics::XB_Storable_GetObjectIdAndVersion(char const*, NXBasics::CStorable::TStorableId&, unsigned int&)
        internal static void XB_Storable_GetObjectIdAndVersion(string fileName, out TStorableId id, out uint version)
        {
            using CFile file = new(fileName, true);
            XB_Storable_GetObjectIdAndVersion(file, out id, out version);
        }

        // --------------------------------------------------------------------
        // Token helpers
        // --------------------------------------------------------------------

        // NXBasics::XB_Token_CheckForDoubleUsedTokens(NXBasics::SToken const*)
        internal static bool XB_Token_CheckForDoubleUsedTokens(SToken[]? tokens)
        {
            if (tokens == null || tokens.Length == 0)
            {
                return false;
            }

            int count = 0;
            while (count < tokens.Length)
            {
                string? name = tokens[count].Name;
                if (string.IsNullOrEmpty(name))
                {
                    break;
                }

                count++;
            }

            if (count < 2)
            {
                return false;
            }

            for (int i = 0; i < count - 1; i++)
            {
                uint tok = tokens[i].Token;
                for (int j = i + 1; j < count; j++)
                {
                    if (tokens[j].Token == tok)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // NXBasics::XB_Token_FindTokenInArray(NXBasics::SToken const*, char const*)
        internal static uint XB_Token_FindTokenInArray(SToken[]? tokens, string? name)
        {
            if (tokens == null || tokens.Length == 0 || string.IsNullOrEmpty(name))
            {
                return 0;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string? n = tokens[i].Name;
                if (string.IsNullOrEmpty(n))
                {
                    return 0;
                }

                if (LowStringCompare(n, name, 999) == 0)
                {
                    return tokens[i].Token;
                }
            }

            return 0;
        }

        // NXBasics::XB_Token_GetStringFromToken(NXBasics::SToken const*, unsigned int)
        internal static string? XB_Token_GetStringFromToken(SToken[]? tokens, uint token)
        {
            if (tokens == null || tokens.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string? n = tokens[i].Name;
                if (string.IsNullOrEmpty(n))
                {
                    return null;
                }

                if (tokens[i].Token == token)
                {
                    return n;
                }
            }

            return null;
        }

        private static int LowStringCompare(string left, string right, int maxLen)
        {
            int i = 0;

            while (i < maxLen)
            {
                char a = i < left.Length ? left[i] : '\0';
                char b = i < right.Length ? right[i] : '\0';

                if (a >= 'A' && a <= 'Z') a = (char)(a + 32);
                if (b >= 'A' && b <= 'Z') b = (char)(b + 32);

                if (a == '\0' && b == '\0')
                {
                    return 0;
                }

                if (a != b)
                {
                    return a < b ? -1 : 1;
                }

                i++;
            }

            return 0;
        }

        // --------------------------------------------------------------------
        // Byte tools
        // --------------------------------------------------------------------

        // NXBasics::XB_Tool_Byte_CopyAndRemap(unsigned char const*, unsigned char*, unsigned char const*, unsigned int)
        internal static void XB_Tool_Byte_CopyAndRemap(byte[] src, int srcOffset, byte[] dst, int dstOffset, byte[] remap, int count)
        {
            if (src == null || dst == null || remap == null || count <= 0)
            {
                return;
            }

            if (srcOffset < 0 || dstOffset < 0 || count < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (srcOffset > src.Length - count || dstOffset > dst.Length - count)
            {
                throw new ArgumentOutOfRangeException();
            }

            for (int i = 0; i < count; i++)
            {
                dst[dstOffset + i] = remap[src[srcOffset + i]];
            }
        }

        // NXBasics::XB_Tool_Byte_CopyAndRemapTwice(unsigned char const*, unsigned char*, unsigned char const*, unsigned char const*, unsigned int)
        internal static void XB_Tool_Byte_CopyAndRemapTwice(byte[] src, int srcOffset, byte[] dst, int dstOffset, byte[] remapA, byte[] remapB, int count)
        {
            if (src == null || dst == null || remapA == null || remapB == null || count <= 0)
            {
                return;
            }

            if (srcOffset < 0 || dstOffset < 0 || count < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (srcOffset > src.Length - count || dstOffset > dst.Length - count)
            {
                throw new ArgumentOutOfRangeException();
            }

            for (int i = 0; i < count; i++)
            {
                dst[dstOffset + i] = remapA[remapB[src[srcOffset + i]]];
            }
        }

        // NXBasics::XB_Tool_Byte_CopyBlock(unsigned char const*, unsigned char*, int, int, int, int)
        internal static void XB_Tool_Byte_CopyBlock(byte[] src, int srcOffset, byte[] dst, int dstOffset, int widthBytes, int height, int srcStride, int dstStride)
        {
            if (src == null || dst == null)
            {
                return;
            }

            if (widthBytes <= 0 || height <= 0)
            {
                return;
            }

            int s = srcOffset;
            int d = dstOffset;

            for (int y = 0; y < height; y++)
            {
                Buffer.BlockCopy(src, s, dst, d, widthBytes);
                s += srcStride;
                d += dstStride;
            }
        }

        // NXBasics::XB_Tool_Byte_FillBlock(unsigned char*, int, int, int, unsigned char)
        internal static void XB_Tool_Byte_FillBlock(byte[] dst, int dstOffset, int widthBytes, int height, int dstStride, byte value)
        {
            if (dst == null)
            {
                return;
            }

            if (widthBytes <= 0 || height <= 0)
            {
                return;
            }

            int d = dstOffset;

            for (int y = 0; y < height; y++)
            {
                dst.AsSpan(d, widthBytes).Fill(value);
                d += dstStride;
            }
        }

        // NXBasics::XB_Tool_Byte_Remap(unsigned char*, unsigned char const*, unsigned int)
        internal static void XB_Tool_Byte_Remap(byte[] buffer, int offset, byte[] remap, int count)
        {
            if (buffer == null || remap == null || count <= 0)
            {
                return;
            }

            if (offset < 0 || count < 0 || offset > buffer.Length - count)
            {
                throw new ArgumentOutOfRangeException();
            }

            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] = remap[buffer[offset + i]];
            }
        }

        // NXBasics::XB_Tool_Byte_RemapAndCopyBlock(...)
        internal static void XB_Tool_Byte_RemapAndCopyBlock(byte[] src, int srcOffset, byte[] dst, int dstOffset, int widthBytes, int height, int srcStride, int dstStride, byte[] remap)
        {
            if (src == null || dst == null || remap == null)
            {
                return;
            }

            if (widthBytes <= 0 || height <= 0)
            {
                return;
            }

            int s = srcOffset;
            int d = dstOffset;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < widthBytes; x++)
                {
                    dst[d + x] = remap[src[s + x]];
                }

                s += srcStride;
                d += dstStride;
            }
        }

        // NXBasics::XB_Tool_Byte_RemapBlock(unsigned char*, unsigned char const*, int, int, int)
        internal static void XB_Tool_Byte_RemapBlock(byte[] buffer, int offset, byte[] remap, int widthBytes, int height, int strideDelta)
        {
            if (buffer == null || remap == null)
            {
                return;
            }

            if (widthBytes <= 0 || height <= 0)
            {
                return;
            }

            int row = offset;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < widthBytes; x++)
                {
                    buffer[row + x] = remap[buffer[row + x]];
                }

                row += widthBytes + strideDelta;
            }
        }

        // NXBasics::XB_Tool_Byte_RemapTwiceAndCopyBlock(...)
        internal static void XB_Tool_Byte_RemapTwiceAndCopyBlock(byte[] src, int srcOffset, byte[] dst, int dstOffset, int widthBytes, int height, int srcStride, int dstStride, byte[] remapA, byte[] remapB)
        {
            if (src == null || dst == null || remapA == null || remapB == null)
            {
                return;
            }

            if (widthBytes <= 0 || height <= 0)
            {
                return;
            }

            int s = srcOffset;
            int d = dstOffset;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < widthBytes; x++)
                {
                    dst[d + x] = remapA[remapB[src[s + x]]];
                }

                s += srcStride;
                d += dstStride;
            }
        }

        // --------------------------------------------------------------------
        // Byte->Long / Byte->Word tools
        // --------------------------------------------------------------------

        internal static void XB_Tool_ByteToLong_CopyBlock(byte[] src, int srcOffset, uint[] dst, int dstOffset, int width, int height, int srcStride, int dstStride, uint[] table)
        {
            if (src == null || dst == null || table == null)
            {
                return;
            }

            if (width <= 0 || height <= 0)
            {
                return;
            }

            int s = srcOffset;
            int d = dstOffset;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    dst[d + x] = table[src[s + x]];
                }

                s += srcStride;
                d += dstStride;
            }
        }

        internal static void XB_Tool_ByteToLong_RemapAndCopyBlock(byte[] src, int srcOffset, uint[] dst, int dstOffset, int width, int height, int srcStride, int dstStride, byte[] remap, uint[] table)
        {
            if (src == null || dst == null || remap == null || table == null)
            {
                return;
            }

            if (width <= 0 || height <= 0)
            {
                return;
            }

            int s = srcOffset;
            int d = dstOffset;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    dst[d + x] = table[remap[src[s + x]]];
                }

                s += srcStride;
                d += dstStride;
            }
        }

        internal static void XB_Tool_ByteToLong_RemapTwiceAndCopyBlock(byte[] src, int srcOffset, uint[] dst, int dstOffset, int width, int height, int srcStride, int dstStride, byte[] remapA, byte[] remapB, uint[] table)
        {
            if (src == null || dst == null || remapA == null || remapB == null || table == null)
            {
                return;
            }

            if (width <= 0 || height <= 0)
            {
                return;
            }

            int s = srcOffset;
            int d = dstOffset;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    dst[d + x] = table[remapB[remapA[src[s + x]]]];
                }

                s += srcStride;
                d += dstStride;
            }
        }

        internal static void XB_Tool_ByteToWord_CopyBlock(byte[] src, int srcOffset, ushort[] dst, int dstOffset, int width, int height, int srcStride, int dstStride, ushort[] table)
        {
            if (src == null || dst == null || table == null)
            {
                return;
            }

            if (width <= 0 || height <= 0)
            {
                return;
            }

            int s = srcOffset;
            int d = dstOffset;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    dst[d + x] = table[src[s + x]];
                }

                s += srcStride;
                d += dstStride;
            }
        }

        internal static void XB_Tool_ByteToWord_RemapAndCopyBlock(byte[] src, int srcOffset, ushort[] dst, int dstOffset, int width, int height, int srcStride, int dstStride, byte[] remap, ushort[] table)
        {
            if (src == null || dst == null || remap == null || table == null)
            {
                return;
            }

            if (width <= 0 || height <= 0)
            {
                return;
            }

            int s = srcOffset;
            int d = dstOffset;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    dst[d + x] = table[remap[src[s + x]]];
                }

                s += srcStride;
                d += dstStride;
            }
        }

        internal static void XB_Tool_ByteToWord_RemapTwiceAndCopyBlock(byte[] src, int srcOffset, ushort[] dst, int dstOffset, int width, int height, int srcStride, int dstStride, byte[] remapA, byte[] remapB, ushort[] table)
        {
            if (src == null || dst == null || remapA == null || remapB == null || table == null)
            {
                return;
            }

            if (width <= 0 || height <= 0)
            {
                return;
            }

            int s = srcOffset;
            int d = dstOffset;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    dst[d + x] = table[remapB[remapA[src[s + x]]]];
                }

                s += srcStride;
                d += dstStride;
            }
        }

        // --------------------------------------------------------------------
        // Find memory
        // --------------------------------------------------------------------

        // NXBasics::XB_Tool_FindMemoryInMemory(unsigned char const*, unsigned int, unsigned char const*, unsigned int)
        internal static uint XB_Tool_FindMemoryInMemory(byte[]? haystack, int hayOffset, int hayLength, byte[]? needle, int needleOffset, int needleLength)
        {
            if (haystack == null || needle == null)
            {
                return 0xFFFFFFFFu;
            }

            if (needleLength < 0 || hayLength < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (needleLength == 0)
            {
                return 0;
            }

            if (hayOffset < 0 || needleOffset < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (hayOffset > haystack.Length - hayLength || needleOffset > needle.Length - needleLength)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (needleLength > hayLength)
            {
                return 0xFFFFFFFFu;
            }

            int max = hayLength - needleLength;
            for (int i = 0; i <= max; i++)
            {
                bool match = true;
                for (int j = 0; j < needleLength; j++)
                {
                    if (haystack[hayOffset + i + j] != needle[needleOffset + j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return unchecked((uint)i);
                }
            }

            return 0xFFFFFFFFu;
        }

        // --------------------------------------------------------------------
        // Long / Word block tools
        // --------------------------------------------------------------------

        // NXBasics::XB_Tool_Long_CopyBlock(unsigned int const*, unsigned int*, int, int, int, int)
        internal static void XB_Tool_Long_CopyBlock(uint[] src, int srcOffset, uint[] dst, int dstOffset, int width, int height, int srcStride, int dstStride)
        {
            if (src == null || dst == null)
            {
                return;
            }

            if (width <= 0 || height <= 0)
            {
                return;
            }

            int s = srcOffset;
            int d = dstOffset;

            for (int y = 0; y < height; y++)
            {
                Array.Copy(src, s, dst, d, width);
                s += srcStride;
                d += dstStride;
            }
        }

        // NXBasics::XB_Tool_Long_FillBlock(unsigned int*, int, int, int, unsigned int)
        internal static void XB_Tool_Long_FillBlock(uint[] dst, int dstOffset, int width, int height, int dstStride, uint value)
        {
            if (dst == null)
            {
                return;
            }

            if (width <= 0 || height <= 0)
            {
                return;
            }

            int d = dstOffset;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    dst[d + x] = value;
                }

                d += dstStride;
            }
        }

        // NXBasics::XB_Tool_Word_CopyBlock(unsigned short const*, unsigned short*, int, int, int, int)
        internal static void XB_Tool_Word_CopyBlock(ushort[] src, int srcOffset, ushort[] dst, int dstOffset, int width, int height, int srcStride, int dstStride)
        {
            if (src == null || dst == null)
            {
                return;
            }

            if (width <= 0 || height <= 0)
            {
                return;
            }

            int s = srcOffset;
            int d = dstOffset;

            for (int y = 0; y < height; y++)
            {
                Array.Copy(src, s, dst, d, width);
                s += srcStride;
                d += dstStride;
            }
        }

        // NXBasics::XB_Tool_Word_FillBlock(unsigned short*, int, int, int, unsigned short)
        internal static void XB_Tool_Word_FillBlock(ushort[] dst, int dstOffset, int width, int height, int dstStride, ushort value)
        {
            if (dst == null)
            {
                return;
            }

            if (width <= 0 || height <= 0)
            {
                return;
            }

            int d = dstOffset;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    dst[d + x] = value;
                }

                d += dstStride;
            }
        }

        // NXBasics::XB_Tool_ZeroMemory(void*, unsigned int)
        internal static void XB_Tool_ZeroMemory(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
            {
                return;
            }

            if (length <= 0)
            {
                return;
            }

            if (offset < 0 || length < 0 || offset > buffer.Length - length)
            {
                throw new ArgumentOutOfRangeException();
            }

            Array.Fill(buffer, (byte)0, offset, length);
        }
    }
}