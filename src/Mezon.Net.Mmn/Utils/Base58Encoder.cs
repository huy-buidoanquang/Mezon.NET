using System;
using System.Security.Cryptography;
using System.Text;

namespace Mezon.Net.Mmn.Utils
{
    internal static class Base58Encoder
    {
        private const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

        public static string Encode(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty)
            {
                return string.Empty;
            }

            var leadingZeros = 0;
            while (leadingZeros < data.Length && data[leadingZeros] == 0)
            {
                leadingZeros++;
            }

            var input = data.ToArray();
            var output = new char[input.Length * 2];
            var outputLength = 0;

            var start = leadingZeros;
            while (start < input.Length)
            {
                var remainder = 0;
                for (var i = start; i < input.Length; i++)
                {
                    var digit = input[i];
                    var accumulator = remainder * 256 + digit;
                    input[i] = (byte)(accumulator / 58);
                    remainder = accumulator % 58;
                }

                output[outputLength++] = Alphabet[remainder];
                if (input[start] == 0)
                {
                    start++;
                }
            }

            var result = new StringBuilder(leadingZeros + outputLength);
            result.Append('1', leadingZeros);
            for (var i = outputLength - 1; i >= 0; i--)
            {
                result.Append(output[i]);
            }

            return result.ToString();
        }

        public static string AddressFromUserId(string userId)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(userId));
            return Encode(hash);
        }
    }
}
