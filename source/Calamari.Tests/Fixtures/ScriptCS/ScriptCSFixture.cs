﻿using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.ScriptCS
{
    [TestFixture]
    public class ScriptCSFixture : CalamariFixture
    {
        [Test, RequiresDotNet45]
        public void ShouldPrintEncodedVariable()
        {
            var output = RunScript(GetFixtureResouce("Scripts", "PrintEncodedVariable.csx"));

            output.AssertZero();
            output.AssertOutput("##octopus[setVariable name='RG9ua2V5' value='S29uZw==']");
        }

        [Test, RequiresDotNet45]
        public void ShouldCreateArtifact()
        {
            var output = RunScript(GetFixtureResouce("Scripts", "CreateArtifact.csx"));

            output.AssertZero();
            output.AssertOutput("##octopus[createArtifact");
            output.AssertOutput("name='bXlGaWxlLnR4dA==' length='MTAw']");
        }

        [Test, RequiresDotNet45]
        public void ShouldCallHello()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set("Name", "Paul");
            variables.Set("Variable2", "DEF");
            variables.Set("Variable3", "GHI");
            variables.Set("Foo_bar", "Hello");
            variables.Set("Host", "Never");
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = RunScript(GetFixtureResouce("Scripts", "Hello.csx"), variablesFile);

                output.AssertZero();
                output.AssertOutput("Hello Paul");
            }
        }

        private CalamariResult RunScript(string scriptName, string variables = null)
        {
            var argBuilder = new ArgumentBuilder()
                .Action("run-script")
                .Argument("script", scriptName);
            if (!string.IsNullOrWhiteSpace(variables))
            {
                argBuilder = argBuilder.Argument("variables", variables);
            }
            return Invoke2(argBuilder);
        }
    }
}