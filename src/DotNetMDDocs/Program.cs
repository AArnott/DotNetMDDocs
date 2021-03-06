﻿// <copyright file="Program.cs" company="Chris Crutchfield">
// Copyright (C) 2017  Chris Crutchfield
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see &lt;http://www.gnu.org/licenses/&gt;.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DotNetDocs;
using DotNetMDDocs.Extensions;
using McMaster.Extensions.CommandLineUtils;

namespace DotNetMDDocs
{
    internal class Program
    {
        [Required]
        [Option(Description = "Path to the Xml documentation generated by MSBuild.")]
        public string AssemblyPath { get; set; }

        [Option(Description = "Path to the documents folder.")]
        public string DocumentPath { get; set; } = "docs";

        public static async Task<int> Main(string[] args)
        {
            var @return = await CommandLineApplication.ExecuteAsync<Program>(args);

#if DEBUG
            if (!Console.IsInputRedirected)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
#endif

            return @return;
        }

        private async Task OnExecuteAsync()
        {
            var dllPath = this.AssemblyPath;
            var xmlPath = Path.Combine(Path.GetDirectoryName(this.AssemblyPath), $"{Path.GetFileNameWithoutExtension(this.AssemblyPath)}.xml");

            var assemblyDocumentation = AssemblyDocumentation.Parse(dllPath, xmlPath);

            var docs = Directory.CreateDirectory(this.DocumentPath);

            foreach (var typeDocumentation in assemblyDocumentation.Types)
            {
                Console.WriteLine($"Generating docs for {typeDocumentation.FullName}...");

                var rootDir = Directory.CreateDirectory(Path.Combine(docs.FullName, Path.Combine(typeDocumentation.Namespace.Split('.'))));
                var typeDir = new DirectoryInfo(Path.Combine(rootDir.FullName, typeDocumentation.GetSafeName()));

                if (typeDir.Exists)
                {
                    typeDir.Delete(true);
                }

                typeDir.Create();

                var typeDocBuilder = new TypeDocBuilder(typeDocumentation, assemblyDocumentation, docs.Name);
                using (var stream = File.CreateText(Path.Combine(rootDir.FullName, $"{typeDocumentation.GetSafeName()}.md")))
                {
                    try
                    {
                        await stream.WriteAsync(typeDocBuilder.Generate());

                        await stream.FlushAsync();
                    }
                    catch (System.IO.IOException)
                    {
                        // Failure to write.
                    }
                }

                // Constructors
                var constructorsTask = this.GenerateDocsAsync<MethodDocBuilder>(typeDocumentation.ConstructorDocumentations, typeDocumentation, assemblyDocumentation, typeDir, "Constructors");

                // Properties
                var propertiesTask = this.GenerateDocsAsync<PropertyDocBuilder>(typeDocumentation.PropertyDocumentations, typeDocumentation, assemblyDocumentation, typeDir, "Properties");

                // Methods
                var methodsTask = this.GenerateDocsAsync<MethodDocBuilder>(typeDocumentation.MethodDocumentations, typeDocumentation, assemblyDocumentation, typeDir, "Methods");

                // Fields
                var fieldsTask = this.GenerateDocsAsync<FieldDocBuilder>(typeDocumentation.FieldDocumentations, typeDocumentation, assemblyDocumentation, typeDir, "Fields");

                // Allow all the tasks to execute in parallel.
                await Task.WhenAll(constructorsTask, propertiesTask, methodsTask, fieldsTask);
            }
        }

        private async Task GenerateDocsAsync<TBuilder>(IEnumerable<DocumentationBase> documentations, TypeDocumentation typeDocumentation, AssemblyDocumentation assemblyDocumentation, DirectoryInfo typeDir, string dirName)
            where TBuilder : DocBuilder
        {
            var docDir = Directory.CreateDirectory(Path.Combine(typeDir.FullName, dirName));
            foreach (var documentation in documentations)
            {
                try
                {
                    var docBuilder = (TBuilder)Activator.CreateInstance(typeof(TBuilder), documentation, typeDocumentation, assemblyDocumentation);
                    using (var stream = File.CreateText(Path.Combine(docDir.FullName, $"{documentation.GetSafeName()}.md")))
                    {
                        await stream.WriteAsync(docBuilder.Generate());

                        await stream.FlushAsync();
                    }
                }
                catch (System.IO.IOException)
                {
                    // Failure to write.
                }
            }
        }
    }
}
