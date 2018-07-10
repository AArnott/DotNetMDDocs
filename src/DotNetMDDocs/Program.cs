﻿using DotNetMDDocs.Markdown;
using DotNetMDDocs.XmlDocParser;
using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace DotNetMDDocs
{
    class Program
    {
        [Required]
        [Option(Description = "Path to the Xml documentation generated by MSBuild.")]
        public string XmlPath { get; set; }

        public static async Task<int> Main(string[] args)
        {
            var @return = await CommandLineApplication.ExecuteAsync<Program>(args);

#if DEBUG
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
#endif

            return @return;
        }

        private async Task OnExecuteAsync()
        {
            var xmlpath = XmlPath;
            var dllPath = Path.Combine(Path.GetDirectoryName(XmlPath), $"{Path.GetFileNameWithoutExtension(XmlPath)}.dll");

            var document = new Document(xmlpath, dllPath);

            var docs = Directory.CreateDirectory("docs");

            foreach (var type in document.Types)
            {
                var rootDir = Directory.CreateDirectory(Path.Combine(docs.FullName, Path.Combine(type.Namespace.Split('.'))));
                var typeDir = new DirectoryInfo(Path.Combine(rootDir.FullName, type.SafeName));

                if (typeDir.Exists)
                    typeDir.Delete(true);

                typeDir.Create();

                var typeDocBuilder = new TypeDocBuilder(type, document);
                using (var stream = File.CreateText(Path.Combine(rootDir.FullName, $"{type.SafeName}.md")))
                {
                    await stream.WriteAsync(typeDocBuilder.Generate());
                }

                // Constructors
                var constructorsTask = GenerateDocsAsync<MethodDocBuilder>(type.Constructors, type, document, typeDir, "Constructors");

                // Properties
                var propertiesTask = GenerateDocsAsync<PropertyDocBuilder>(type.Properties, type, document, typeDir, "Properties");

                // Methods
                var methodsTask = GenerateDocsAsync<MethodDocBuilder>(type.Methods, type, document, typeDir, "Methods");

                // Fields
                var fieldsTask = GenerateDocsAsync<FieldDocBuilder>(type.Fields, type, document, typeDir, "Fields");

                // Allow all the tasks to execute in parallel.
                await Task.WhenAll(constructorsTask, propertiesTask, methodsTask, fieldsTask);
            }
        }

        private async Task GenerateDocsAsync<TBuilder>(IEnumerable<BaseDoc> docs, TypeDoc type, Document document, DirectoryInfo typeDir, string dirName)
            where TBuilder : DocBuilder
        {
            var docDir = Directory.CreateDirectory(Path.Combine(typeDir.FullName, dirName));
            foreach (var doc in docs)
            {
                var docBuilder = (TBuilder)Activator.CreateInstance(typeof(TBuilder), doc, type, document);
                using (var stream = File.CreateText(Path.Combine(docDir.FullName, $"{doc.SafeName}.md")))
                {
                    await stream.WriteAsync(docBuilder.Generate());
                }
            }
        }
    }
}
