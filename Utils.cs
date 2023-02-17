using System;
using System.IO;
using System.Security.Cryptography;

namespace NeosVRMImporter
{
    public static class Utils
    {
        public static string GenerateMD5(string filepath)
        {
            // from https://github.com/dfgHiatus/AssetImportAPI/blob/main/AssetImportAPI/Utils.cs

            using var hasher = MD5.Create();
            using var stream = File.OpenRead(filepath);
            var hash = hasher.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "");
        }
    }
}
