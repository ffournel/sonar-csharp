﻿/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2015-2017 SonarSource SA
 * mailto: contact AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using Microsoft.CodeAnalysis;
using SonarAnalyzer.Helpers;
using System.Linq;
using System.Collections.Generic;
using Google.Protobuf;
using System.IO;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Xml.Linq;
using SonarAnalyzer.Protobuf;

namespace SonarAnalyzer.Rules
{
    public abstract class UtilityAnalyzerBase : SonarDiagnosticAnalyzer
    {
        internal const string ConfigurationAdditionalFile = "ProjectOutFolderPath.txt";
        internal const string IgnoreHeaderCommentsCSharp = "sonar.cs.ignoreHeaderComments";
        internal const string IgnoreHeaderCommentsVisualBasic = "sonar.vbnet.ignoreHeaderComments";

        protected readonly object parameterReadLock = new object();

        protected bool IsAnalyzerEnabled { get; set; } = false;

        protected string WorkDirectoryBasePath { get; set; }

        protected Dictionary<string, bool> IgnoreHeaderComments { get; } = new Dictionary<string, bool>
            {
                { IgnoreHeaderCommentsCSharp, false },
                { IgnoreHeaderCommentsVisualBasic, false },
            };

        protected void ReadParameters(AnalyzerOptions options, string language)
        {
            var sonarLintAdditionalFile = options.AdditionalFiles
                .FirstOrDefault(f => ParameterLoader.ConfigurationFilePathMatchesExpected(f.Path));

            var projectOutputAdditionalFile = options.AdditionalFiles
                .FirstOrDefault(f => ParameterLoader.ConfigurationFilePathMatchesExpected(f.Path, ConfigurationAdditionalFile));

            if (sonarLintAdditionalFile == null ||
                projectOutputAdditionalFile == null)
            {
                return;
            }

            lock (parameterReadLock)
            {
                var xml = XDocument.Load(sonarLintAdditionalFile.Path);
                var settings = xml.Descendants("Setting");
                ReadHeaderCommentProperties(settings);
                WorkDirectoryBasePath = File.ReadAllLines(projectOutputAdditionalFile.Path).FirstOrDefault(l => !string.IsNullOrEmpty(l));

                if (!string.IsNullOrEmpty(WorkDirectoryBasePath))
                {
                    var suffix = language == LanguageNames.CSharp
                        ? "cs"
                        : "vbnet";
                    WorkDirectoryBasePath = Path.Combine(WorkDirectoryBasePath, "output-" + suffix);
                    IsAnalyzerEnabled = true;
                }
            }
        }

        private void ReadHeaderCommentProperties(IEnumerable<XElement> settings)
        {
            ReadHeaderCommentProperties(settings, IgnoreHeaderCommentsCSharp);
            ReadHeaderCommentProperties(settings, IgnoreHeaderCommentsVisualBasic);
        }

        private void ReadHeaderCommentProperties(IEnumerable<XElement> settings, string propertyName)
        {
            string propertyStringValue = GetPropertyStringValue(settings, propertyName);
            bool propertyValue;
            if (propertyStringValue != null &&
                bool.TryParse(propertyStringValue, out propertyValue))
            {
                IgnoreHeaderComments[propertyName] = propertyValue;
            }
        }

        private static string GetPropertyStringValue(IEnumerable<XElement> settings, string propName)
        {
            return settings
                .FirstOrDefault(s => s.Element("Key")?.Value == propName)
                ?.Element("Value").Value;
        }

        internal static TextRange GetTextRange(FileLinePositionSpan lineSpan)
        {
            return new TextRange
            {
                StartLine = lineSpan.StartLinePosition.GetLineNumberToReport(),
                EndLine = lineSpan.EndLinePosition.GetLineNumberToReport(),
                StartOffset = lineSpan.StartLinePosition.Character,
                EndOffset = lineSpan.EndLinePosition.Character
            };
        }
    }

    public abstract class UtilityAnalyzerBase<TMessage> : UtilityAnalyzerBase
        where TMessage : IMessage, new()
    {
        private static readonly object fileWriteLock = new TMessage();

        protected sealed override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterCompilationAction(
                c =>
                {
                    ReadParameters(c.Options, c.Compilation.Language);

                    if (!IsAnalyzerEnabled)
                    {
                        return;
                    }

                    var messages = new List<TMessage>();

                    foreach (var tree in c.Compilation.SyntaxTrees)
                    {
                        if (!GeneratedCodeRecognizer.IsGenerated(tree))
                        {
                            messages.Add(GetMessage(tree, c.Compilation.GetSemanticModel(tree)));
                        }
                    }

                    if (!messages.Any())
                    {
                        return;
                    }

                    var pathToWrite = Path.Combine(WorkDirectoryBasePath, FileName);
                    lock (fileWriteLock)
                    {
                        // Make sure the folder exists
                        Directory.CreateDirectory(WorkDirectoryBasePath);

                        if (!File.Exists(pathToWrite))
                        {
                            using (File.Create(pathToWrite)) { }
                        }

                        using (var metricsStream = new FileStream(pathToWrite, FileMode.Append, FileAccess.Write))
                        {
                            foreach (var message in messages)
                            {
                                message.WriteDelimitedTo(metricsStream);
                            }
                        }
                    }
                });
        }

        protected abstract TMessage GetMessage(SyntaxTree syntaxTree, SemanticModel semanticModel);

        protected abstract GeneratedCodeRecognizer GeneratedCodeRecognizer { get; }

        protected abstract string FileName { get; }
    }
}
