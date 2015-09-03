﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Orleankka
{
    static class ActorTypeCode
    {
        static readonly Dictionary<string, Type> codeMap =
                    new Dictionary<string, Type>();

        static readonly Dictionary<Type, string> typeMap =
                    new Dictionary<Type, string>();

        public static void Register(Type type)
        {
            var code = CodeOf(type);

            if (codeMap.ContainsKey(code))
            {
                var existing = codeMap[code];

                if (existing != type)
                    throw new ArgumentException(
                        $"The type {existing.FullName} has been already registered under the code {code}. " +
                        $"Use ActorTypeCode attribute to provide unique code for {type.FullName}");

                throw new ArgumentException($"The type {type} has been already registered");
            }

            codeMap.Add(code, type);
            typeMap.Add(type, code);
        }

        public static Type RegisteredType(string code)
        {
            Type type;
                
            if (!codeMap.TryGetValue(code, out type))
                throw new InvalidOperationException(
                    $"Unable to map type code '{code}' to the corresponding runtime type. " +
                     "Make sure that you've registered the assembly containing this type");

            return type;
        }

        public static string RegisteredCode(Type type)
        {
            string code;

            if (!typeMap.TryGetValue(type, out code))
                throw new InvalidOperationException(
                    $"Unable to map type '{type}' to the corresponding type code. " +
                     "Make sure that you've registered the assembly containing this type");

            return code;
        }

        public static void Reset()
        {
            codeMap.Clear();
            typeMap.Clear();
        }

        public static string CodeOf(Type type)
        {
            var att = type
                .GetCustomAttributes(typeof(ActorTypeCodeAttribute), false)
                .Cast<ActorTypeCodeAttribute>()
                .SingleOrDefault();

            return att != null ? att.Code : type.FullName;
        }
    }
}