using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace RobotArena.WebGL.Editor
{
    public static class WebGLPackageBudget
    {
        public const long DefaultLimitBytes = 80_000_000;

        public static WebGLPackageBudgetResult Measure(string archivePath, long limitBytes)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
            {
                throw new ArgumentException("An archive path is required.", nameof(archivePath));
            }

            if (limitBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limitBytes), "The package limit must be positive.");
            }

            List<string> errors = new List<string>();
            long uncompressedBytes = 0;
            int rootIndexCount = 0;
            bool hasBuildEntry = false;
            HashSet<string> entryNames = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(archivePath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string entryName = entry.FullName.Replace('\\', '/');
                        uncompressedBytes += entry.Length;

                        if (!entryNames.Add(entryName))
                        {
                            errors.Add("archive contains duplicate entry: " + entryName);
                        }

                        if (entryName == "index.html")
                        {
                            rootIndexCount++;
                        }
                        else if (!entryName.StartsWith("Build/", StringComparison.Ordinal))
                        {
                            errors.Add("all non-index entries must be under Build/: " + entryName);
                        }
                        else
                        {
                            hasBuildEntry = true;
                        }

                        if (ContainsWhitespaceOrNonAscii(entryName))
                        {
                            errors.Add("entry contains spaces or non-ASCII characters: " + entryName);
                        }
                    }
                }
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException)
            {
                errors.Add("archive could not be read: " + exception.Message);
            }

            if (rootIndexCount != 1)
            {
                errors.Add("archive must contain exactly one index.html at the archive root");
            }

            if (!hasBuildEntry)
            {
                errors.Add("archive must contain at least one entry under Build/");
            }

            bool structureIsValid = errors.Count == 0;
            bool isWithinBudget = uncompressedBytes <= limitBytes;
            if (!isWithinBudget)
            {
                errors.Add(
                    "uncompressed archive size " + uncompressedBytes
                    + " exceeds the limit " + limitBytes + " bytes");
            }

            return new WebGLPackageBudgetResult(uncompressedBytes, limitBytes, structureIsValid, errors);
        }

        private static bool ContainsWhitespaceOrNonAscii(string value)
        {
            foreach (char character in value)
            {
                if (character > 127 || char.IsWhiteSpace(character))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class WebGLPackageBudgetResult
    {
        internal WebGLPackageBudgetResult(
            long uncompressedBytes,
            long limitBytes,
            bool structureIsValid,
            IReadOnlyList<string> errors)
        {
            UncompressedBytes = uncompressedBytes;
            LimitBytes = limitBytes;
            IsValid = structureIsValid;
            Errors = errors;
        }

        public long UncompressedBytes { get; }

        public long LimitBytes { get; }

        public IReadOnlyList<string> Errors { get; }

        public bool IsWithinBudget => UncompressedBytes <= LimitBytes;

        public bool IsValid { get; }

        public bool IsPassing => IsValid && IsWithinBudget;
    }
}
