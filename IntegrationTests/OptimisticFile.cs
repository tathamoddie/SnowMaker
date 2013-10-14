﻿using System;
using System.IO;
using NUnit.Framework;
using SnowMaker;

namespace IntegrationTests.cs
{
    [TestFixture]
    public class OptimisticFile : Scenarios<OptimisticFile.TestScope>
    {
        protected override TestScope BuildTestScope()
        {
            return new TestScope();
        }

        protected override IOptimisticDataStore BuildStore(TestScope scope)
        {
            return new FileOptimisticDataStore(scope.DirectoryPath);
        }

        public class TestScope : ITestScope
        {
            public TestScope()
            {
                var ticks = DateTime.UtcNow.Ticks;
                IdScopeName = string.Format("snowmakertest{0}", ticks);

                DirectoryPath = Path.Combine(Path.GetTempPath(), IdScopeName);
                Directory.CreateDirectory(DirectoryPath);
            }

            public string IdScopeName { get; private set; }
            public string DirectoryPath { get; private set; }

            public string ReadCurrentPersistedValue()
            {
                var filePath = Path.Combine(DirectoryPath, string.Format("{0}.txt", IdScopeName));
                using (TextReader reader = new StreamReader(filePath, System.Text.Encoding.Default))
                    return reader.ReadToEnd();
            }

            public void Dispose()
            {
                if (Directory.Exists(DirectoryPath))
                    Directory.Delete(DirectoryPath, true);
            }
        }
    }
}
