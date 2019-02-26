using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;

namespace Loazit
{
    internal static class Crypto
    {
        private const string Salt = "k4sdfr35ed.f%e344@fs3${5dse8k4}sdf*%4fxz^c12@xa34rqw1r41@f4";

        // ReSharper disable once InconsistentNaming
        private static readonly byte[] IV =
            {0x72, 0x20, 0x40, 0x12, 0x1a, 0x03, 0xe9, 0x22, 0xd9, 0xc1, 0x1b, 0x22, 0x00, 0x8d, 0x15, 04};

        public static byte[] GeneratePrivateKey(string publicKey, string extraSid)
        {
            string publicKeyHash;
            using (var md5 = MD5.Create())
            {
                publicKeyHash = md5.ComputeHash(Encoding.UTF8.GetBytes(publicKey)).ToHex();
            }

            var privateKeyMaterial = publicKeyHash + extraSid + Salt;
            var privateKeyMaterialBytes = Encoding.UTF8.GetBytes(privateKeyMaterial);

            using (var md5 = MD5.Create())
            {
                // Sic.
                var keyString = md5.ComputeHash(privateKeyMaterialBytes).ToHex();
                return Encoding.UTF8.GetBytes(keyString);
            }
        }

        public static byte[] Decrypt(byte[] privateKey, byte[] data)
        {
            using (var inputStream = new MemoryStream(data, false))
            using (var outputStream = new  MemoryStream())
            {
                Decrypt(privateKey, inputStream, outputStream);

                return outputStream.GetBuffer();
            }
        }

        public static void Decrypt(byte[] privateKey, Stream inputStream, Stream outputStream)
        {
            using (var aes = Aes.Create())
            {
                Debug.Assert(aes != null, $"{nameof(aes)} != null");

                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = privateKey;
                aes.IV = IV;

                using (var decryptor = aes.CreateDecryptor())
                using (var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read))
                {
                    cryptoStream.CopyTo(outputStream);
                }
            }
        }
    }
}
