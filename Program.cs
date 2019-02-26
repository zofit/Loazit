using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace Loazit
{
    internal static class Program
    {
        private enum ItemType : long
        {
            Page = 5,
            External = 11,
            Toc = 0,
            PageAnchor = 3,
        }

        private enum ItemLocation : long
        {
            Pages = 3,
            MainDatabase = 1,
        }

        private const string ContainerXmlTemplate =
@"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<container version=""1.0"" xmlns=""urn:oasis:names:tc:opendocument:xmlns:container"">
  <rootfiles>
    <rootfile full-path=""OEBPS/{0}"" media-type=""application/oebps-package+xml""/>
  </rootfiles>
</container>
";

        private static void Main(string[] args)
        {
            ParseCommandLine(args, out var bookFile, out var outputFile, out var appId, out var bookKeyFile);

            var workingDirectory = Utils.CreateTemporaryDirectory();

            var contentDirectory = Path.Combine(workingDirectory, "OEBPS");
            Directory.CreateDirectory(contentDirectory);

            ExtractMainDatabase(bookFile, contentDirectory);

            byte[] privateKey = null;
            if (null != appId)
            {
                privateKey = GeneratePrivateKey(bookKeyFile, appId);
            }

            var manifest = ProcessIndexDatabase(contentDirectory, privateKey);
            Cleanup(contentDirectory, manifest);

            CreateEpub(workingDirectory, outputFile);

            Directory.Delete(workingDirectory, true);
        }

        private static void ParseCommandLine(IList<string> args,
            out string bookFile,
            out string outputFile,
            out string appId,
            out string bookKeyFile)
        {
            bookFile = args[0];

            outputFile = args[1];

            appId = null;
            if (args.Count > 2)
            {
                appId = args[2];
            }

            bookKeyFile = null;
            if (args.Count > 3)
            {
                bookKeyFile = args[3];
            }
            else if (args.Count > 2)
            {
                bookKeyFile = bookFile + ".enc";
            }
        }

        private static void ExtractMainDatabase(string mainDatabaseFile, string outputDirectory)
        {
            using (var mainDatabase = new Database(mainDatabaseFile, true))
            {
                foreach (var itemsTable in mainDatabase.ReadTable("tItems"))
                {
                    var itemId = (string) itemsTable["itemid"];
                    switch (itemId)
                    {
                        case "itemlist":
                            continue;

                        default:
                            var path = Path.Combine(outputDirectory, itemId);
                            File.WriteAllBytes(path, (byte[]) itemsTable["data"]);
                            break;
                    }
                }
            }
        }

        private static byte[] GeneratePrivateKey(string bookKeyFile, string appId)
        {
            var bookKey = File.ReadAllText(bookKeyFile, Encoding.UTF8);
            return Crypto.GeneratePrivateKey(bookKey, appId.ToUpperInvariant());
        }

        private static ISet<string> ProcessIndexDatabase(string workingDirectory, [CanBeNull] byte[] privateKey)
        {
            var processedFiles = new HashSet<string>();

            IList<IDictionary<string, object>> items;
            IDictionary<long, byte[]> pages;
            using (var indexDatabase = new Database(Path.Combine(workingDirectory, "index.db"), true))
            {
                items = indexDatabase.ReadTable("items");
                pages = indexDatabase
                    .ReadTable("text_data")
                    .ToDictionary(
                        row => (long) row["text_data_key"],
                        row => (byte[]) row["data"]);
            }

            foreach (var item in items)
            {
                var href = item["href"] as string;

                switch ((ItemType) (long) item["item_type"])
                {
                    case ItemType.Page:
                        if ((ItemLocation) (long) item["data_location"] != ItemLocation.Pages)
                        {
                            throw new NotSupportedException();
                        }

                        var page = pages[(long) item["data_key"]];

                        var path = CreateFullPath(workingDirectory, href);

                        ExtractPage(path, privateKey, page);

                        processedFiles.Add(path);

                        break;

                    case ItemType.External:
                        if ((ItemLocation) (long) item["data_location"] != ItemLocation.MainDatabase)
                        {
                            throw new NotSupportedException();
                        }

                        var newPath = CreateFullPath(workingDirectory, href);

                        var originalPath = Path.Combine(workingDirectory, (string) item["item_id"]);

                        MoveExternalFile(originalPath, newPath);

                        processedFiles.Add(newPath);

                        break;

                    case ItemType.Toc:
                    case ItemType.PageAnchor:
                        continue;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return processedFiles;
        }

        private static string CreateFullPath(string workingDirectory, string href)
        {
            Debug.Assert(href != null, $"{nameof(href)} != null");
            return Path.GetFullPath(Path.Combine(workingDirectory, href));
        }

        private static void ExtractPage(string path, [CanBeNull] byte[] privateKey, byte[] page)
        {
            if (privateKey != null)
            {
                page = Crypto.Decrypt(privateKey, page);
            }

            CreateAncestors(path);

            File.WriteAllBytes(path, page);
        }

        private static void MoveExternalFile(string originalPath, string newPath)
        {
            CreateAncestors(newPath);

            File.Move(originalPath, newPath);
        }

        private static void CreateAncestors(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void Cleanup(string workingDirectory, ICollection<string> manifest)
        {
            //
            // First, delete all files that are not in the manifest.
            //

            var forDeletion = Directory
                .EnumerateFiles(workingDirectory, "*", SearchOption.AllDirectories)
                .Where(file => !manifest.Contains(file))
                .ToList();

            foreach (var file in forDeletion)
            {
                File.Delete(file);
            }

            //
            // Now go over all directories and try to delete each one.
            // Only the non-empty directories will remain.
            //

            var directories = Directory.GetDirectories(workingDirectory, "*", SearchOption.AllDirectories);
            foreach (var directory in directories)
            {
                try
                {
                    Directory.Delete(directory);
                }
                catch (IOException)
                {
                    continue;
                }
            }
        }

        private static void CreateEpub(string workingDirectory, string outputFilename)
        {
            File.WriteAllText(
                Path.Combine(workingDirectory, "mimetype"),
                "application/epub+zip",
                Encoding.ASCII);

            var contentFile = Directory
                .EnumerateFiles(Path.Combine(workingDirectory, "OEBPS"), "*.opf", SearchOption.TopDirectoryOnly)
                .Single();

            var containerXml = string.Format(ContainerXmlTemplate, Path.GetFileName(contentFile));

            var metadataDirectory = Path.Combine(workingDirectory, "META-INF");
            Directory.CreateDirectory(metadataDirectory);
            File.WriteAllText(Path.Combine(metadataDirectory, "container.xml"), containerXml, Encoding.UTF8);

            System.IO.Compression.ZipFile.CreateFromDirectory(
                workingDirectory,
                outputFilename,
                CompressionLevel.Optimal,
                false);
        }
    }
}
