﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.FileSystem;

namespace Calamari.Deployment.Conventions
{
    public class ConfigurationTransformsConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IConfigurationTransformer configurationTransformer;

        public ConfigurationTransformsConvention(ICalamariFileSystem fileSystem, IConfigurationTransformer configurationTransformer)
        {
            this.fileSystem = fileSystem;
            this.configurationTransformer = configurationTransformer;
        }

        public void Install(RunningDeployment deployment)
        {

            var transformDefinitions = GetTransformDefinitions(deployment.Variables.Get(SpecialVariables.Package.AdditionalXmlConfigurationTransforms));

            var sourceExtensions = new HashSet<string>(
                  transformDefinitions
                    .Where(transform => transform.Advanced)
                    .Select(transform => "*" + Path.GetExtension(transform.SourcePattern))
                    .Distinct()
                );

            if (deployment.Variables.GetFlag(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles))
            {
                sourceExtensions.Add("*.config");
                transformDefinitions.Add(new XmlConfigTransformDefinition("Release"));

                var environment = deployment.Variables.Get(SpecialVariables.Environment.Name);
                if (!string.IsNullOrWhiteSpace(environment))
                {
                    transformDefinitions.Add(new XmlConfigTransformDefinition(environment));
                }
            }

            var transformsRun = new HashSet<string>();
            foreach (var configFile in fileSystem.EnumerateFilesRecursively(deployment.CurrentDirectory, sourceExtensions.ToArray()))
            {
                ApplyTransformations(configFile, transformDefinitions, transformsRun);
            }

            deployment.Variables.SetStrings(SpecialVariables.AppliedXmlConfigTransforms, transformsRun, "|");
        }

        void ApplyTransformations(string sourceFile, IEnumerable<XmlConfigTransformDefinition> transformations, HashSet<string> alreadyRun)
        {
            foreach (var transformation in transformations)
            {
                if ((transformation.Wildcard && !sourceFile.EndsWith(transformation.SourcePattern, StringComparison.InvariantCultureIgnoreCase)))
                    continue;
                try
                {
                    ApplyTransformations(sourceFile, transformation, alreadyRun);
                }
                catch (Exception)
                {
                    Log.ErrorFormat("Could not transform the file '{0}' using the {1}pattern '{2}'.", sourceFile, transformation.Wildcard ? "wildcard " : "", transformation.TransformPattern);
                    throw;
                }
            }
        }

        void ApplyTransformations(string sourceFile, XmlConfigTransformDefinition transformation, HashSet<string> alreadyRun)
        {
            foreach (var transformFile in DetermineTransformFileNames(sourceFile, transformation))
            {
                var sourceFileName = (transformation?.SourcePattern?.Contains("\\") ?? false)
                    ? fileSystem.GetRelativePath(transformFile, sourceFile).TrimStart('.','\\')
                    : Path.GetFileName(sourceFile);

                if (transformation.Advanced && !transformation.Wildcard && !string.Equals(transformation.SourcePattern, sourceFileName, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                if (!fileSystem.FileExists(transformFile))
                    continue;

                if (string.Equals(sourceFile, transformFile, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                if (alreadyRun.Contains(transformFile))
                    continue;

                Log.Info("Transforming '{0}' using '{1}'.", sourceFile, transformFile);
                configurationTransformer.PerformTransform(sourceFile, transformFile, sourceFile);
                alreadyRun.Add(transformFile);
            }
        }

        private static List<XmlConfigTransformDefinition> GetTransformDefinitions(string transforms)
        {
            if (string.IsNullOrWhiteSpace(transforms))
                return new List<XmlConfigTransformDefinition>();

            return transforms
                .Split(',', '\r', '\n')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => new XmlConfigTransformDefinition(s))
                .ToList();
        }

        private IEnumerable<string> DetermineTransformFileNames(string sourceFile, XmlConfigTransformDefinition transformation)
        {
            var defaultTransformFileName = DetermineTransformFileName(sourceFile, transformation, true);
            var transformFileName = DetermineTransformFileName(sourceFile, transformation, false);

            var relativeTransformPath = fileSystem.GetRelativePath(sourceFile, transformFileName);
            var transformPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile), Path.GetDirectoryName(relativeTransformPath)));

            // The reason we use fileSystem.EnumerateFiles here is to get the actual file-names from the physical file-system.
            // This prevents any issues with mis-matched casing in transform specifications.
            return fileSystem.EnumerateFiles(transformPath,
               Path.GetFileName(Path.GetFileName(defaultTransformFileName)),
               Path.GetFileName(Path.GetFileName(transformFileName))
            );
        }

        private static string DetermineTransformFileName(string sourceFile, XmlConfigTransformDefinition transformation, bool defaultExtension)
        {
            var tp = transformation.TransformPattern;
            if (defaultExtension && !tp.EndsWith(".config"))
                tp += ".config";

            if (transformation.Advanced && transformation.Wildcard)
            {
                var sourcePatternWithoutPrefix = transformation.SourcePattern;
                if (transformation.SourcePattern.StartsWith("."))
                {
                    sourcePatternWithoutPrefix = transformation.SourcePattern.Remove(0, 1);
                }
                    
                var baseFileName = sourceFile.Replace(sourcePatternWithoutPrefix, "");
                return Path.ChangeExtension(baseFileName, tp);
            }

            if (transformation.Advanced && !transformation.Wildcard)
            {
                var transformDirectory = GetTransformationFileDirectory(sourceFile, transformation);
                return Path.Combine(transformDirectory, tp);
            }

            return Path.ChangeExtension(sourceFile, tp);
        }

        static string GetTransformationFileDirectory(string sourceFile, XmlConfigTransformDefinition transformation)
        {
            var sourceDirectory = Path.GetDirectoryName(sourceFile) ?? string.Empty;
            if (!transformation.SourcePattern.Contains("\\"))
                return sourceDirectory;

            var sourcePattern = transformation.SourcePattern;
            var sourcePatternPath = sourcePattern.Substring(0, sourcePattern.LastIndexOf("\\", StringComparison.Ordinal));
            return sourceDirectory.Replace(sourcePatternPath, string.Empty);
        }
    }
}