﻿#region usings

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Serialization;
using System.Security.Principal;

#endregion

namespace EpoSql.Util
{
    public static class Dumper
    {
        /// <summary>
        /// Dumps the specified value to the <see cref="TextWriter"/> using the
        /// specified <paramref name="name"/>.
        /// </summary>
        /// <param name="value">
        /// The value to dump to the <paramref name="writer"/>.
        /// </param>
        /// <param name="name">
        /// The name of the <paramref name="value"/> being dumped.
        /// </param>
        /// <param name="writer">
        /// The <see cref="TextWriter"/> to dump the <paramref name="value"/> to.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <para><paramref name="name"/> is <c>null</c> or empty.</para>
        /// <para>- or -</para>
        /// <para><paramref name="writer"/> is <c>null</c>.</para>
        /// </exception>
        public static void Dump(object value, string name, TextWriter writer)
        {
            if (StringEx.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException("name");
            if (writer == null)
                throw new ArgumentNullException("writer");

            var idGenerator = new ObjectIDGenerator();
            InternalDump(0, name, value, writer, idGenerator, true);
        }

        private static void InternalDump(int indentationLevel, string name, object value, TextWriter writer,
            ObjectIDGenerator idGenerator, bool recursiveDump)
        {
            var indentation = new string(' ', indentationLevel*3);

            if (value == null)
            {
                writer.WriteLine("{0}{1} = <null>", indentation, name);
                return;
            }

            var type = value.GetType();

            // figure out if this is an object that has already been dumped, or is currently being dumped
            var keyRef = string.Empty;
            var keyPrefix = string.Empty;
            if (!type.IsValueType)
            {
                bool firstTime;
                var key = idGenerator.GetId(value, out firstTime);
                if (!firstTime)
                    keyRef = string.Format(CultureInfo.InvariantCulture, " (see #{0})", key);
                else
                {
                    keyPrefix = string.Format(CultureInfo.InvariantCulture, "#{0}: ", key);
                }
            }

            // work out how a simple dump of the value should be done
            var isString = value is string;
            var typeName = value.GetType().FullName;
            var formattedValue = value.ToString();

            var exception = value as Exception;
            if (exception != null)
            {
                formattedValue = exception.GetType().Name + ": " + exception.Message;
            }

            if (formattedValue == typeName)
                formattedValue = string.Empty;
            else
            {
                // escape tabs and line feeds
                formattedValue = formattedValue.Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");

                // chop at 80 characters
                var length = formattedValue.Length;
                if (length > 80)
                    formattedValue = formattedValue.Substring(0, 80);
                if (isString)
                    formattedValue = string.Format(CultureInfo.InvariantCulture, "\"{0}\"", formattedValue);
                if (length > 80)
                    formattedValue += " (+" + (length - 80) + " chars)";
                formattedValue = " = " + formattedValue;
            }

            writer.WriteLine("{0}{1}{2}{3} [{4}]{5}", indentation, keyPrefix, name, formattedValue, value.GetType(),
                keyRef);

            // Avoid dumping objects we've already dumped, or is already in the process of dumping
            if (keyRef.Length > 0)
                return;

            // don't dump strings, we already got at around 80 characters of those dumped
            if (isString)
                return;

            // don't dump value-types in the System namespace
            if (type.IsValueType && type.FullName == "System." + type.Name)
                return;

            // Avoid certain types that will result in endless recursion
            if (type.FullName == "System.Reflection." + type.Name)
                return;

            if (value is SecurityIdentifier)
                return;

            if (!recursiveDump)
                return;

            var properties =
                (from property in
                    type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    where property.GetIndexParameters().Length == 0
                          && property.CanRead
                    select property).ToArray();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).ToArray();

            if (properties.Length == 0 && fields.Length == 0)
                return;

            writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}{{", indentation));
            if (properties.Length > 0)
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}   properties {{", indentation));
                foreach (var pi in properties)
                {
                    try
                    {
                        var propertyValue = pi.GetValue(value, null);
                        InternalDump(indentationLevel + 2, pi.Name, propertyValue, writer, idGenerator, true);
                    }
                    catch (TargetInvocationException ex)
                    {
                        InternalDump(indentationLevel + 2, pi.Name, ex, writer, idGenerator, false);
                    }
                    catch (ArgumentException ex)
                    {
                        InternalDump(indentationLevel + 2, pi.Name, ex, writer, idGenerator, false);
                    }
                    catch (RemotingException ex)
                    {
                        InternalDump(indentationLevel + 2, pi.Name, ex, writer, idGenerator, false);
                    }
                }
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}   }}", indentation));
            }
            if (fields.Length > 0)
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}   fields {{", indentation));
                foreach (var field in fields)
                {
                    try
                    {
                        var fieldValue = field.GetValue(value);
                        InternalDump(indentationLevel + 2, field.Name, fieldValue, writer, idGenerator, true);
                    }
                    catch (TargetInvocationException ex)
                    {
                        InternalDump(indentationLevel + 2, field.Name, ex, writer, idGenerator, false);
                    }
                }
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}   }}", indentation));
            }
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}}}", indentation));
        }
    }
}