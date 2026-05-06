// This file is subject to the terms and conditions defined
// in file 'LICENSE', which is part of this source code package.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.IsolatedStorage;
using ProtoBuf;

namespace DepotDownloader
{
    [ProtoContract]
    class AccountSettingsStore
    {
        // Member 1 was a Dictionary<string, byte[]> for SentryData.

        [ProtoMember(2, IsRequired = false)]
        public ConcurrentDictionary<string, int> ContentServerPenalty { get; private set; }

        // Member 3 was a Dictionary<string, string> for LoginKeys.

        [ProtoMember(4, IsRequired = false)]
        public Dictionary<string, string> LoginTokens { get; private set; }

        [ProtoMember(5, IsRequired = false)]
        public Dictionary<string, string> GuardData { get; private set; }

        string FileName;

        AccountSettingsStore()
        {
            ContentServerPenalty = new ConcurrentDictionary<string, int>();
            LoginTokens = new(StringComparer.OrdinalIgnoreCase);
            GuardData = new(StringComparer.OrdinalIgnoreCase);
        }

        static bool Loaded
        {
            get { return Instance != null; }
        }

        public static AccountSettingsStore Instance;
        static readonly IsolatedStorageFile IsolatedStorage = IsolatedStorageFile.GetUserStoreForAssembly();

        /// <summary>
        /// When set, LoadFromFile and Save use this directory instead of IsolatedStorage.
        /// </summary>
        public static string CustomStorePath;

        static Stream OpenRead(string filename)
        {
            if (CustomStorePath != null)
            {
                var path = Path.Combine(CustomStorePath, filename);
                return File.Exists(path) ? File.OpenRead(path) : null;
            }
            return IsolatedStorage.FileExists(filename)
                ? IsolatedStorage.OpenFile(filename, FileMode.Open, FileAccess.Read)
                : null;
        }

        static Stream OpenWrite(string filename)
        {
            if (CustomStorePath != null)
            {
                Directory.CreateDirectory(CustomStorePath);
                return File.Create(Path.Combine(CustomStorePath, filename));
            }
            return IsolatedStorage.OpenFile(filename, FileMode.Create, FileAccess.Write);
        }

        public static void LoadFromFile(string filename)
        {
            if (Loaded)
                throw new Exception("Config already loaded");

            var fs = OpenRead(filename);
            if (fs != null)
            {
                try
                {
                    using var ds = new DeflateStream(fs, CompressionMode.Decompress);
                    Instance = Serializer.Deserialize<AccountSettingsStore>(ds);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to load account settings: {0}", ex.Message);
                    fs.Dispose();
                    Instance = new AccountSettingsStore();
                }
            }
            else
            {
                Instance = new AccountSettingsStore();
            }

            Instance.FileName = filename;
        }

        public static void Save()
        {
            if (!Loaded)
                throw new Exception("Saved config before loading");

            try
            {
                using var fs = OpenWrite(Instance.FileName);
                using var ds = new DeflateStream(fs, CompressionMode.Compress);
                Serializer.Serialize(ds, Instance);
            }
            catch (IOException ex)
            {
                Console.WriteLine("Failed to save account settings: {0}", ex.Message);
            }
        }
    }
}
