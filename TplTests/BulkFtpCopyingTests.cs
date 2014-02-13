﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using NUnit.Framework;

namespace TplTests
{
    [TestFixture]
    internal class BulkFtpCopyingTests
    {
        private const string FtpHostName = "10.10.201.134";
        private const string FtpUserName = "env6ftp";
        private const string FtpPasssword = "w1nd0w5.";

        private static readonly Tuple<string, string> SourceDirectoryPair = Tuple.Create(@"LoopMonTest/SourceDir", @"\\10.10.201.134\e$\HostEnvironments\LoopMonTest\SourceDir");
        private static readonly Tuple<string, string> TargetDirectoryPair1 = Tuple.Create(@"LoopMonTest/TargetDir1", @"\\10.10.201.134\e$\HostEnvironments\LoopMonTest\TargetDir1");
        private static readonly Tuple<string, string> TargetDirectoryPair2 = Tuple.Create(@"LoopMonTest/TargetDir2", @"\\10.10.201.134\e$\HostEnvironments\LoopMonTest\TargetDir2");
        private static readonly Tuple<string, string> TargetDirectoryPair3 = Tuple.Create(@"LoopMonTest/TargetDir3", @"\\10.10.201.134\e$\HostEnvironments\LoopMonTest\TargetDir3");
        private static readonly Tuple<string, string> TargetDirectoryPair4 = Tuple.Create(@"LoopMonTest/TargetDir4", @"\\10.10.201.134\e$\HostEnvironments\LoopMonTest\TargetDir4");
        private static readonly Tuple<string, string> TargetDirectoryPair5 = Tuple.Create(@"LoopMonTest/TargetDir5", @"\\10.10.201.134\e$\HostEnvironments\LoopMonTest\TargetDir5");
        private static readonly Tuple<string, string> TargetDirectoryPair6 = Tuple.Create(@"LoopMonTest/TargetDir6", @"\\10.10.201.134\e$\HostEnvironments\LoopMonTest\TargetDir6");
        private static readonly Tuple<string, string> TargetDirectoryPair7 = Tuple.Create(@"LoopMonTest/TargetDir7", @"\\10.10.201.134\e$\HostEnvironments\LoopMonTest\TargetDir7");
        private static readonly Tuple<string, string> TargetDirectoryPair8 = Tuple.Create(@"LoopMonTest/TargetDir8", @"\\10.10.201.134\e$\HostEnvironments\LoopMonTest\TargetDir8");
        private static readonly Tuple<string, string> TargetDirectoryPair9 = Tuple.Create(@"LoopMonTest/TargetDir9", @"\\10.10.201.134\e$\HostEnvironments\LoopMonTest\TargetDir9");

        [SetUp]
        public void SetUp()
        {
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;
        }

        [Test]
        public async void CopyOneFileToOneTargetDirectory()
        {
            // Arrange
            var fileNames = new[]
                {
                    "combined_Master_F217307845EECA800B75936A37BCA697.js"
                };

            var sourceBaseUri = MakeBaseUri(SourceDirectoryPair.Item1);
            var sourceDirectoryUncPath = SourceDirectoryPair.Item2;

            var targetDirectoryPairs = new[]
                {
                    TargetDirectoryPair1
                };
            var targetBaseUris = targetDirectoryPairs.Select(x => MakeBaseUri(x.Item1)).ToList();
            var targetDirectoryUncPaths = targetDirectoryPairs.Select(x => x.Item2).ToList();

            // Act
            DeleteAllFilesInDirectories(targetDirectoryUncPaths);
            var bulkFtpCopyManager = new BulkFtpCopyManager(FtpUserName, FtpPasssword);

            MyDebug.Log("Calling bulkFtpCopyManager.CopyFiles...");
            await bulkFtpCopyManager.CopyFiles(fileNames, sourceBaseUri, targetBaseUris);
            MyDebug.Log("Returned from bulkFtpCopyManager.CopyFiles");

            // Assert
            AssertFilesHaveBeenCopiedCorrectly(fileNames, sourceDirectoryUncPath, targetDirectoryUncPaths);
        }

        [Test]
        public async void CopyOneFileToThreeTargetDirectories()
        {
            // Arrange
            var fileNames = new[]
                {
                    "combined_Master_F217307845EECA800B75936A37BCA697.js"
                };

            var sourceBaseUri = MakeBaseUri(SourceDirectoryPair.Item1);
            var sourceDirectoryUncPath = SourceDirectoryPair.Item2;

            var targetDirectoryPairs = new[]
                {
                    TargetDirectoryPair1,
                    TargetDirectoryPair2,
                    TargetDirectoryPair3
                };
            var targetBaseUris = targetDirectoryPairs.Select(x => MakeBaseUri(x.Item1)).ToList();
            var targetDirectoryUncPaths = targetDirectoryPairs.Select(x => x.Item2).ToList();

            // Act
            DeleteAllFilesInDirectories(targetDirectoryUncPaths);
            var bulkFtpCopyManager = new BulkFtpCopyManager(FtpUserName, FtpPasssword);

            MyDebug.Log("Calling bulkFtpCopyManager.CopyFiles...");
            await bulkFtpCopyManager.CopyFiles(fileNames, sourceBaseUri, targetBaseUris);
            MyDebug.Log("Returned from bulkFtpCopyManager.CopyFiles");

            // Assert
            AssertFilesHaveBeenCopiedCorrectly(fileNames, sourceDirectoryUncPath, targetDirectoryUncPaths);
        }

        [Test]
        public async void CopyTenFilesToNineTargetDirectories()
        {
            // Arrange
            var fileNames = new[]
                {
                    "combined_00BCCE928A0F2D7A35A4789355EAC889.js",
                    "combined_0E5A05E395420EE14D39190125EBAFFB.js",
                    "combined_0F44B72026533831467A6E77BEED7B8F_structureLanding.css",
                    "combined_1C14A4DC502F638EC8C2A214AD5C0721.js",
                    "combined_1E5D13856547C4A301D0502E2B0D876C.js",
                    "combined_1E3315DDA8DF021431D8E325F113FDFC.js",
                    "combined_1E81561C9F4FA424EACCF1B34508AD00.js",
                    "combined_1FE7C48D388623664D0736426B66BF99_structureCheckoutWide.css",
                    "combined_02B90A87AFD3FAC1D505F7B56CBF748E.css",
                    "combined_Master_F217307845EECA800B75936A37BCA697.js"
                };

            var sourceBaseUri = MakeBaseUri(SourceDirectoryPair.Item1);
            var sourceDirectoryUncPath = SourceDirectoryPair.Item2;

            var targetDirectoryPairs = new[]
                {
                    TargetDirectoryPair1,
                    TargetDirectoryPair2,
                    TargetDirectoryPair3,
                    TargetDirectoryPair4,
                    TargetDirectoryPair5,
                    TargetDirectoryPair6,
                    TargetDirectoryPair7,
                    TargetDirectoryPair8,
                    TargetDirectoryPair9
                };
            var targetBaseUris = targetDirectoryPairs.Select(x => MakeBaseUri(x.Item1)).ToList();
            var targetDirectoryUncPaths = targetDirectoryPairs.Select(x => x.Item2).ToList();

            // Act
            DeleteAllFilesInDirectories(targetDirectoryUncPaths);
            var bulkFtpCopyManager = new BulkFtpCopyManager(FtpUserName, FtpPasssword);

            MyDebug.Log("Calling bulkFtpCopyManager.CopyFiles...");
            await bulkFtpCopyManager.CopyFiles(fileNames, sourceBaseUri, targetBaseUris);
            MyDebug.Log("Returned from bulkFtpCopyManager.CopyFiles");

            // Assert
            AssertFilesHaveBeenCopiedCorrectly(fileNames, sourceDirectoryUncPath, targetDirectoryUncPaths);
        }

        private static void DeleteAllFilesInDirectories(IEnumerable<string> directories)
        {
            foreach (var directory in directories)
            {
                DeleteAllFilesInDirectory(directory);
            }

            WaitForFileSystemToSettleDown();
        }

        private static void DeleteAllFilesInDirectory(string directory)
        {
            var files = Directory.GetFiles(directory);
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }

        private static void AssertFilesHaveBeenCopiedCorrectly(string[] fileNames, string sourceDirectory, IEnumerable<string> targetDirectories)
        {
            WaitForFileSystemToSettleDown();

            var dictionary = new Dictionary<string, byte[]>();
            foreach (var fileName in fileNames)
            {
                var path = Path.Combine(sourceDirectory, fileName);
                var buffer = File.ReadAllBytes(path);
                dictionary[fileName] = buffer;
            }

            foreach (var targetDirectory in targetDirectories)
            {
                foreach (var fileName in fileNames)
                {
                    var path = Path.Combine(targetDirectory, fileName);
                    var targetBuffer = File.ReadAllBytes(path);
                    var sourceBuffer = dictionary[fileName];
                    Assert.That(targetBuffer, Is.EqualTo(sourceBuffer));
                }
            }
        }

        private static void WaitForFileSystemToSettleDown()
        {
            System.Threading.Thread.Sleep(5 * 1000);
        }

        private static string MakeBaseUri(string path)
        {
            return string.Format("ftp://{0}/{1}", FtpHostName, path);
        }
    }
}
