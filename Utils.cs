using System;
using System.IO;

namespace Loazit
{
    internal static class Utils
    {
        public static string CreateTemporaryDirectory()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        public static string CreateRelativePath(string root, string path)
        {
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                root += Path.DirectorySeparatorChar;
            }

            return new Uri(root).MakeRelativeUri(new Uri(path)).ToString();
        }
    }
}
