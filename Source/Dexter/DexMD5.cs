using System.Text;

namespace OpenVikings.Dexter
{
    internal sealed class DexMD5
    {
        private uint _a;
        private uint _b;
        private uint _c;
        private uint _d;

        private uint _countLow;
        private uint _countHigh;

        private readonly byte[] _buffer;
        private readonly byte[] _digest;

        private readonly char[] _hexString;
        private bool _finalized;

        internal DexMD5()
        {
            _buffer = new byte[64];
            _digest = new byte[16];
            _hexString = new char[33];
            Init();
        }

        internal DexMD5(string text)
        {
            _buffer = new byte[64];
            _digest = new byte[16];
            _hexString = new char[33];
            Init();

            if (text != null)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(text);
                Update(bytes, bytes.Length);
                FinalizeHash();
            }
        }

        internal void Init()
        {
            // Matches the 64-bit stores seen in the decompile:
            // *(undefined8*)this = 0xefcdab8967452301;
            // *(undefined8*)(this+8) = 0x1032547698badcfe;
            _a = 0x67452301u;
            _b = 0xEFCDAB89u;
            _c = 0x98BADCFEu;
            _d = 0x10325476u;

            _countLow = 0;
            _countHigh = 0;

            _finalized = false;
            _hexString[0] = '\0';
        }

        internal void Update(byte[] input, int length)
        {
            if (_finalized)
            {
                return;
            }

            if (input == null || length <= 0)
            {
                return;
            }

            if ((uint)length > (uint)input.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            uint oldLow = _countLow;
            uint index = (oldLow >> 3) & 0x3Fu;

            unchecked
            {
                _countLow = oldLow + ((uint)length << 3);
                _countHigh = _countHigh + ((uint)length >> 29) + ((_countLow < oldLow) ? 1u : 0u);
            }

            uint partLen = 64u - index;
            if ((uint)length >= partLen)
            {
                Buffer.BlockCopy(input, 0, _buffer, (int)index, (int)partLen);
                Transform(_buffer);

                uint uVar1 = (index ^ 0x7Fu);
                uint uVar2 = partLen;

                if (uVar1 < (uint)length)
                {
                    do
                    {
                        uVar2 = uVar1;
                        TransformBlock(input, (int)(uVar2 - 0x3Fu));
                        uVar1 = uVar2 + 0x40u;
                    } while (uVar2 + 0x40u < (uint)length);

                    uVar2++;
                }

                int i = (int)uVar2;
                int remaining = length - i;
                if (remaining != 0)
                {
                    Buffer.BlockCopy(input, i, _buffer, 0, remaining);
                }

                return;
            }

            // param_2 < uVar3
            Buffer.BlockCopy(input, 0, _buffer, (int)index, length);
        }

        internal void FinalizeHash()
        {
            if (_finalized)
            {
                return;
            }

            byte[] padding = new byte[64];
            padding[0] = 0x80;

            uint localCountLow = _countLow;
            uint localCountHigh = _countHigh;

            uint index = (localCountLow >> 3) & 0x3Fu;

            // ((uint)(0x37 < index) * 0x40 - index) + 0x38
            uint padLen = (((index > 0x37u) ? 1u : 0u) * 0x40u - index) + 0x38u;

            Update(padding, (int)padLen);

            byte[] lengthBytes = new byte[8];
            WriteUInt32LE(lengthBytes, 0, localCountLow);
            WriteUInt32LE(lengthBytes, 4, localCountHigh);
            Update(lengthBytes, 8);

            // *(undefined8 *)(this + 0x58) = *(undefined8 *)this;
            // *(undefined8 *)(this + 0x60) = *(undefined8 *)(this + 8);
            WriteUInt32LE(_digest, 0, _a);
            WriteUInt32LE(_digest, 4, _b);
            WriteUInt32LE(_digest, 8, _c);
            WriteUInt32LE(_digest, 12, _d);

            // DexterMemory::MemorySet(this + 0x18,'\0',1);
            _buffer[0] = 0;

            _finalized = true;
        }

        internal string GetDigestString()
        {
            if (!_finalized)
            {
                FinalizeHash();
            }

            // CXX uses DexterString::StringPrint at offsets 0x68,0x6a,... (2 chars each)
            for (int i = 0; i < 16; i++)
            {
                byte v = _digest[i];
                _hexString[i * 2] = ToHexLower((v >> 4) & 0x0F);
                _hexString[i * 2 + 1] = ToHexLower(v & 0x0F);
            }

            _hexString[32] = '\0';
            return new string(_hexString, 0, 32);
        }

        // ------------------------------------------------------------
        // Transform helpers
        // ------------------------------------------------------------

        private void TransformBlock(byte[] input, int offset)
        {
            byte[] block = new byte[64];
            System.Buffer.BlockCopy(input, offset, block, 0, 64);
            Transform(block);
        }

        // FULL 64-step inline Transform, 1:1 from DexMD5.cxx
        private void Transform(byte[] block)
        {
            uint aOld = _a;
            uint uVar2 = _b;
            uint uVar3 = _c;
            uint uVar4 = _d;

            uint[] x = new uint[16];
            for (int i = 0; i < 16; i++)
            {
                x[i] = ReadUInt32LE(block, i * 4);
            }

            uint uVar5;
            uint uVar7;
            uint uVar8;
            uint uVar9;
            uint uVar10;

            unchecked
            {
                uVar5 = (uVar3 & uVar2) + aOld + (~uVar2 & uVar4) + x[0] + 0xd76aa478u;
                uVar5 = (uVar5 * 0x80u | uVar5 >> 0x19) + uVar2;
                uVar7 = (uVar5 & uVar2) + uVar4 + x[1] + (~uVar5 & uVar3) + 0xe8c7b756u;
                uVar7 = (uVar7 * 0x1000u | uVar7 >> 0x14) + uVar5;
                uVar8 = (~uVar7 & uVar2) + (uVar7 & uVar5) + uVar3 + x[2] + 0x242070dbu;
                uVar9 = (uVar8 * 0x20000u | uVar8 >> 0xf) + uVar7;
                uVar8 = (uVar9 & uVar7) + uVar2 + x[3] + (~uVar9 & uVar5) + 0xc1bdceeeu;
                uVar8 = (uVar8 * 0x400000u | uVar8 >> 10) + uVar9;
                uVar5 = uVar5 + x[4] + (uVar8 & uVar9) + (~uVar8 & uVar7) + 0xf57c0fafu;
                uVar5 = (uVar5 * 0x80u | uVar5 >> 0x19) + uVar8;
                uVar7 = uVar7 + x[5] + (uVar5 & uVar8) + (~uVar5 & uVar9) + 0x4787c62au;
                uVar7 = (uVar7 * 0x1000u | uVar7 >> 0x14) + uVar5;
                uVar9 = uVar9 + x[6] + (uVar7 & uVar5) + (~uVar7 & uVar8) + 0xa8304613u;
                uVar9 = (uVar9 * 0x20000u | uVar9 >> 0xf) + uVar7;
                uVar10 = uVar8 + x[7] + (uVar9 & uVar7) + (~uVar9 & uVar5) + 0xfd469501u;
                uVar8 = (uVar10 * 0x400000u | uVar10 >> 10) + uVar9;
                uVar5 = uVar5 + x[8] + (uVar8 & uVar9) + (~uVar8 & uVar7) + 0x698098d8u;
                uVar5 = (uVar5 * 0x80u | uVar5 >> 0x19) + uVar8;
                uVar7 = uVar7 + x[9] + (uVar5 & uVar8) + (~uVar5 & uVar9) + 0x8b44f7afu;
                uVar7 = (uVar7 * 0x1000u | uVar7 >> 0x14) + uVar5;
                uVar9 = uVar9 + x[10] + (uVar7 & uVar5) + (~uVar7 & uVar8) + 0xffff5bb1u;
                uVar9 = (uVar9 * 0x20000u | uVar9 >> 0xf) + uVar7;
                uVar10 = uVar8 + x[11] + (uVar9 & uVar7) + (~uVar9 & uVar5) + 0x895cd7beu;
                uVar8 = (uVar10 * 0x400000u | uVar10 >> 10) + uVar9;
                uVar5 = uVar5 + x[12] + (uVar8 & uVar9) + (~uVar8 & uVar7) + 0x6b901122u;
                uVar5 = (uVar5 * 0x80u | uVar5 >> 0x19) + uVar8;
                uVar7 = uVar7 + x[13] + (uVar5 & uVar8) + (~uVar5 & uVar9) + 0xfd987193u;
                uVar7 = (uVar7 * 0x1000u | uVar7 >> 0x14) + uVar5;
                uVar9 = uVar9 + x[14] + (uVar7 & uVar5) + (~uVar7 & uVar8) + 0xa679438eu;
                uVar9 = (uVar9 * 0x20000u | uVar9 >> 0xf) + uVar7;
                uVar10 = uVar8 + x[15] + (uVar9 & uVar7) + (~uVar9 & uVar5) + 0x49b40821u;
                uVar8 = (uVar10 * 0x400000u | uVar10 >> 10) + uVar9;

                uVar5 = (uVar7 & ~uVar8) + uVar5 + x[1] + (uVar9 & uVar8) + 0xf61e2562u;
                uVar5 = (uVar5 * 0x20u | uVar5 >> 0x1b) + uVar8;
                uVar7 = (uVar9 & ~uVar5) + uVar7 + x[6] + (uVar8 & uVar5) + 0xc040b340u;
                uVar7 = (uVar7 * 0x200u | uVar7 >> 0x17) + uVar5;
                uVar9 = (uVar8 & ~uVar7) + uVar9 + x[11] + (uVar5 & uVar7) + 0x265e5a51u;
                uVar9 = (uVar9 * 0x4000u | uVar9 >> 0x12) + uVar7;
                uVar10 = (uVar5 & ~uVar9) + uVar8 + x[0] + (uVar7 & uVar9) + 0xe9b6c7aau;
                uVar8 = (uVar10 * 0x100000u | uVar10 >> 0xc) + uVar9;
                uVar5 = (uVar7 & ~uVar8) + uVar5 + x[5] + (uVar9 & uVar8) + 0xd62f105du;
                uVar5 = (uVar5 * 0x20u | uVar5 >> 0x1b) + uVar8;
                uVar7 = (uVar9 & ~uVar5) + uVar7 + x[10] + (uVar8 & uVar5) + 0x2441453u;
                uVar7 = (uVar7 * 0x200u | uVar7 >> 0x17) + uVar5;
                uVar9 = (uVar8 & ~uVar7) + uVar9 + x[15] + (uVar5 & uVar7) + 0xd8a1e681u;
                uVar9 = (uVar9 * 0x4000u | uVar9 >> 0x12) + uVar7;
                uVar10 = (uVar5 & ~uVar9) + uVar8 + x[4] + (uVar7 & uVar9) + 0xe7d3fbc8u;
                uVar8 = (uVar10 * 0x100000u | uVar10 >> 0xc) + uVar9;
                uVar5 = (uVar7 & ~uVar8) + uVar5 + x[9] + (uVar9 & uVar8) + 0x21e1cde6u;
                uVar5 = (uVar5 * 0x20u | uVar5 >> 0x1b) + uVar8;
                uVar7 = (uVar9 & ~uVar5) + uVar7 + x[14] + (uVar8 & uVar5) + 0xc33707d6u;
                uVar7 = (uVar7 * 0x200u | uVar7 >> 0x17) + uVar5;
                uVar9 = (uVar8 & ~uVar7) + uVar9 + x[3] + (uVar5 & uVar7) + 0xf4d50d87u;
                uVar9 = (uVar9 * 0x4000u | uVar9 >> 0x12) + uVar7;
                uVar10 = (uVar5 & ~uVar9) + uVar8 + x[8] + (uVar7 & uVar9) + 0x455a14edu;
                uVar8 = (uVar10 * 0x100000u | uVar10 >> 0xc) + uVar9;
                uVar5 = (uVar7 & ~uVar8) + uVar5 + x[13] + (uVar9 & uVar8) + 0xa9e3e905u;
                uVar5 = (uVar5 * 0x20u | uVar5 >> 0x1b) + uVar8;
                uVar7 = (uVar9 & ~uVar5) + uVar7 + x[2] + (uVar8 & uVar5) + 0xfcefa3f8u;
                uVar7 = (uVar7 * 0x200u | uVar7 >> 0x17) + uVar5;
                uVar9 = (uVar8 & ~uVar7) + uVar9 + x[7] + (uVar5 & uVar7) + 0x676f02d9u;
                uVar9 = (uVar9 * 0x4000u | uVar9 >> 0x12) + uVar7;
                uVar10 = (uVar5 & ~uVar9) + uVar8 + x[12] + (uVar7 & uVar9) + 0x8d2a4c8au;
                uVar8 = (uVar10 * 0x100000u | uVar10 >> 0xc) + uVar9;

                uVar5 = uVar5 + x[5] + (uVar7 ^ uVar9 ^ uVar8) + 0xfffa3942u;
                uVar5 = (uVar5 * 0x10u | uVar5 >> 0x1c) + uVar8;
                uVar7 = uVar7 + x[8] + (uVar5 ^ uVar9 ^ uVar8) + 0x8771f681u;
                uVar7 = (uVar7 * 0x800u | uVar7 >> 0x15) + uVar5;
                uVar9 = uVar9 + x[11] + (uVar7 ^ uVar5 ^ uVar8) + 0x6d9d6122u;
                uVar9 = (uVar9 * 0x10000u | uVar9 >> 0x10) + uVar7;
                uVar8 = uVar8 + x[14] + (uVar9 ^ uVar7 ^ uVar5) + 0xfde5380cu;
                uVar8 = (uVar8 * 0x800000u | uVar8 >> 9) + uVar9;
                uVar5 = uVar5 + x[1] + (uVar8 ^ uVar9 ^ uVar7) + 0xa4beea44u;
                uVar5 = (uVar5 * 0x10u | uVar5 >> 0x1c) + uVar8;
                uVar7 = uVar7 + x[4] + (uVar5 ^ uVar9 ^ uVar8) + 0x4bdecfa9u;
                uVar7 = (uVar7 * 0x800u | uVar7 >> 0x15) + uVar5;
                uVar9 = uVar9 + x[7] + (uVar7 ^ uVar5 ^ uVar8) + 0xf6bb4b60u;
                uVar9 = (uVar9 * 0x10000u | uVar9 >> 0x10) + uVar7;
                uVar8 = uVar8 + x[10] + (uVar9 ^ uVar7 ^ uVar5) + 0xbebfbc70u;
                uVar8 = (uVar8 * 0x800000u | uVar8 >> 9) + uVar9;
                uVar5 = uVar5 + x[13] + (uVar8 ^ uVar9 ^ uVar7) + 0x289b7ec6u;
                uVar5 = (uVar5 * 0x10u | uVar5 >> 0x1c) + uVar8;
                uVar7 = uVar7 + x[0] + (uVar5 ^ uVar9 ^ uVar8) + 0xeaa127fau;
                uVar7 = (uVar7 * 0x800u | uVar7 >> 0x15) + uVar5;
                uVar9 = uVar9 + x[3] + (uVar7 ^ uVar5 ^ uVar8) + 0xd4ef3085u;
                uVar9 = (uVar9 * 0x10000u | uVar9 >> 0x10) + uVar7;
                uVar8 = uVar8 + x[6] + (uVar9 ^ uVar7 ^ uVar5) + 0x4881d05u;
                uVar8 = (uVar8 * 0x800000u | uVar8 >> 9) + uVar9;
                uVar5 = uVar5 + x[9] + (uVar8 ^ uVar9 ^ uVar7) + 0xd9d4d039u;
                uVar5 = (uVar5 * 0x10u | uVar5 >> 0x1c) + uVar8;
                uVar7 = uVar7 + x[12] + (uVar5 ^ uVar9 ^ uVar8) + 0xe6db99e5u;
                uVar7 = (uVar7 * 0x800u | uVar7 >> 0x15) + uVar5;
                uVar9 = uVar9 + x[15] + (uVar7 ^ uVar5 ^ uVar8) + 0x1fa27cf8u;
                uVar9 = (uVar9 * 0x10000u | uVar9 >> 0x10) + uVar7;
                uVar8 = uVar8 + x[2] + (uVar9 ^ uVar7 ^ uVar5) + 0xc4ac5665u;
                uVar8 = (uVar8 * 0x800000u | uVar8 >> 9) + uVar9;

                uVar5 = uVar5 + x[0] + ((~uVar7 | uVar9) ^ uVar8) + 0xf4292244u;
                uVar5 = (uVar5 * 0x40u | uVar5 >> 0x1a) + uVar8;
                uVar7 = uVar7 + x[7] + ((~uVar9 | uVar5) ^ uVar8) + 0x432aff97u;
                uVar7 = (uVar7 * 0x400u | uVar7 >> 0x16) + uVar5;
                uVar9 = uVar9 + x[14] + ((~uVar5 | uVar7) ^ uVar8) + 0xab9423a7u;
                uVar9 = (uVar9 * 0x8000u | uVar9 >> 0x11) + uVar7;
                uVar8 = uVar8 + x[5] + ((~uVar7 | uVar9) ^ uVar5) + 0xfc93a039u;
                uVar8 = (uVar8 * 0x200000u | uVar8 >> 0xb) + uVar9;
                uVar5 = uVar5 + x[12] + ((~uVar9 | uVar7) ^ uVar8) + 0x655b59c3u;
                uVar5 = (uVar5 * 0x40u | uVar5 >> 0x1a) + uVar8;
                uVar7 = uVar7 + x[3] + ((~uVar8 | uVar5) ^ uVar9) + 0x8f0ccc92u;
                uVar7 = (uVar7 * 0x400u | uVar7 >> 0x16) + uVar5;
                uVar9 = uVar9 + x[10] + ((~uVar5 | uVar7) ^ uVar8) + 0xffeff47du;
                uVar9 = (uVar9 * 0x8000u | uVar9 >> 0x11) + uVar7;
                uVar5 = uVar8 + x[1] + ((~uVar7 | uVar9) ^ uVar5) + 0x85845dd1u;
                uVar8 = (uVar5 * 0x200000u | uVar5 >> 0xb) + uVar9;
                uVar5 = uVar7 + x[8] + ((~uVar9 | uVar8) ^ uVar7) + 0x6fa87e4fu;
                uVar5 = (uVar5 * 0x40u | uVar5 >> 0x1a) + uVar8;
                uVar7 = uVar9 + x[15] + ((~uVar8 | uVar5) ^ uVar9) + 0xfe2ce6e0u;
                uVar7 = (uVar7 * 0x400u | uVar7 >> 0x16) + uVar5;
                uVar9 = uVar8 + x[6] + ((~uVar5 | uVar7) ^ uVar8) + 0xa3014314u;
                uVar9 = (uVar9 * 0x8000u | uVar9 >> 0x11) + uVar7;
                uVar5 = uVar5 + x[13] + ((~uVar7 | uVar9) ^ uVar8) + 0x4e0811a1u;
                uVar5 = (uVar5 * 0x200000u | uVar5 >> 0xb) + uVar9;
                uVar7 = uVar7 + x[4] + ((~uVar8 | uVar5) ^ uVar9) + 0xf7537e82u;
                uVar7 = (uVar7 * 0x40u | uVar7 >> 0x1a) + uVar5;
                uVar8 = uVar8 + x[11] + ((~uVar9 | uVar7) ^ uVar5) + 0xbd3af235u;
                uVar8 = (uVar8 * 0x400u | uVar8 >> 0x16) + uVar7;
                uVar9 = uVar9 + x[2] + ((~uVar5 | uVar8) ^ uVar7) + 0x2ad7d2bbu;
                uVar9 = (uVar9 * 0x8000u | uVar9 >> 0x11) + uVar8;
                uVar5 = uVar5 + x[9] + ((~uVar7 | uVar9) ^ uVar8) + 0xeb86d391u;

                _a = uVar7 + aOld;
                _b = uVar2 + uVar9 + (uVar5 * 0x200000u | uVar5 >> 0xb);
                _c = uVar9 + uVar3;
                _d = uVar8 + uVar4;
            }
        }

        private static uint ReadUInt32LE(byte[] buf, int offset)
        {
            return (uint)buf[offset]
                 | ((uint)buf[offset + 1] << 8)
                 | ((uint)buf[offset + 2] << 16)
                 | ((uint)buf[offset + 3] << 24);
        }

        private static void WriteUInt32LE(byte[] buf, int offset, uint value)
        {
            buf[offset] = (byte)value;
            buf[offset + 1] = (byte)(value >> 8);
            buf[offset + 2] = (byte)(value >> 16);
            buf[offset + 3] = (byte)(value >> 24);
        }

        private static char ToHexLower(int value)
        {
            if ((uint)value <= 9u)
            {
                return (char)('0' + value);
            }

            return (char)('a' + (value - 10));
        }
    }
}