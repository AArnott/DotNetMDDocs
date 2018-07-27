﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Mono.Cecil;

namespace DotNetDocs
{
    public class FieldDocumentation : DocumentationBase
    {
        protected TypeDocumentation DeclaringType { get; private set; }

        public FieldDocumentation(FieldDefinition fieldDefinition, XElement xElement, System.Reflection.Metadata.EntityHandle handle, TypeDocumentation declaringType)
            : base(fieldDefinition, xElement)
        {
            DeclaringType = declaringType;

            var declaringAssembly = declaringType.DeclaringAssembly;
            Declaration = declaringAssembly.Decompiler.DecompileAsString(handle).Trim();

            if (Declaration.Contains("="))
            {
                Declaration = $"{Declaration.Substring(0, Declaration.IndexOf('=')).Trim()};";
            }
        }
    }
}
