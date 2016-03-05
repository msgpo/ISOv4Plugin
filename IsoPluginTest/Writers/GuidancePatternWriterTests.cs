﻿using System.IO;
using System.Runtime.Serialization.Formatters;
using AgGateway.ADAPT.ApplicationDataModel.ADM;
using AgGateway.ADAPT.IsoPlugin.Writers;
using Newtonsoft.Json;
using NUnit.Framework;

namespace IsoPluginTest.Writers
{
    [TestFixture]
    public class GuidancePatternWriterTests
    {
        [TearDown]
        public void Cleanup()
        {
            var folderLocation = TestContext.CurrentContext.WorkDirectory + @"\TASKDATA";
            if (Directory.Exists(folderLocation))
                Directory.Delete(folderLocation, true);
        }

        [Test]
        public void ShouldWriteAllTypesOfPatterns()
        {
            // Setup
            var taskWriter = new TaskDocumentWriter();
            var adaptDocument = TestHelpers.LoadApplicationModel(@"TestData\Guidance\AllPatterns.json");

            // Act
            using (taskWriter)
            {
                taskWriter.Write(TestContext.CurrentContext.WorkDirectory, adaptDocument);
            }

            // Verify
            Assert.AreEqual(TestHelpers.LoadFromFile(@"TestData\Guidance\AllPatternsOutput.xml"),
                TestHelpers.LoadFromFile(TestContext.CurrentContext.WorkDirectory + @"\TASKDATA\PFD00000.XML"));
        }
    }
}
