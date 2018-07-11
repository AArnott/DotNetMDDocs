﻿using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DotNetMDDocs.XmlDocParser
{
    public class TypeDoc : BaseDoc
    {
        public InheritanceDoc InheritanceHierarchy { get; private set; }
        public string CodeSyntax { get; private set; }
        public string Namespace { get; private set; }
        public IEnumerable<MethodDoc> Constructors { get; private set; }
        public IEnumerable<PropertyDoc> Properties { get; private set; }
        public IEnumerable<MethodDoc> Methods { get; private set; }
        public IEnumerable<FieldDoc> Fields { get; private set; }

        public string FullName => $"{Namespace}.{Name}";

        public TypeDoc(XElement xElement, XDocument xDocument, AssemblyDefinition assembly)
            : base("T", xElement, string.Empty)
        {
            Namespace = xElement.Attribute("name").Value.Substring("T:".Length);
            Namespace = Namespace.Substring(0, Namespace.LastIndexOf('.'));

            Name = Name.Replace($"{Namespace}.", string.Empty);

            Constructors = GetConstructors(xDocument);
            Properties = GetProperties(xDocument);
            Methods = GetMethods(xDocument);
            Fields = GetFields(xDocument);

            var type = assembly.MainModule.GetType(Namespace, Name);

            InheritanceHierarchy = GetInheritanceHierarchy(type);

            CodeSyntax = GetCodeSyntax(type);
        }

        private InheritanceDoc GetInheritanceHierarchy(TypeDefinition type)
        {
            InheritanceDoc baseClass = null;
            if (type != null && type.BaseType != null)
                baseClass = GetInheritanceHierarchy(type.BaseType.Resolve());

            var @return = new InheritanceDoc
            {
                BaseClass = baseClass,
                Name = type?.Name ?? Name,
                Namespace = type?.Namespace ?? Namespace
            };

            return @return;
        }

        private string GetCodeSyntax(TypeDefinition type)
        {
            if (type == null)
                return string.Empty;

            var stringBuilder = new StringBuilder();
            
            foreach (var attribute in type.CustomAttributes)
            {
                stringBuilder.AppendLine($"[{attribute.AttributeType.Name}]");
            }

            stringBuilder.Append($"public class {type.Name}");

            return stringBuilder.ToString();
        }

        private IEnumerable<MethodDoc> GetConstructors(XDocument xDocument)
        {
            return (from m in GetMembers("M", xDocument)
                    where m.Attribute("name").Value.Contains("#ctor")
                    select new MethodDoc(m, FullName)).ToArray();
        }

        private IEnumerable<PropertyDoc> GetProperties(XDocument xDocument)
        {
            return (from m in GetMembers("P", xDocument)
                    select new PropertyDoc(m, FullName)).ToArray();
        }

        private IEnumerable<MethodDoc> GetMethods(XDocument xDocument)
        {
            return (from m in GetMembers("M", xDocument)
                    where !m.Attribute("name").Value.Contains("#ctor")
                    select new MethodDoc(m, FullName)).ToArray();
        }

        private IEnumerable<FieldDoc> GetFields(XDocument xDocument)
        {
            return (from m in GetMembers("F", xDocument)
                    select new FieldDoc(m, FullName)).ToArray();
        }

        private IEnumerable<XElement> GetMembers(string identifier, XDocument xDocument)
        {
            var members = (from e in xDocument.Root.Elements()
                           where e.Name == "members"
                           select e).Single();

            return from e in members.Elements()
                   where e.Name == "member" && e.Attribute("name").Value.StartsWith($"{identifier}:{FullName}.")
                   select e;
        }
    }
}
