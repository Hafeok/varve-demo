// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using Varve.Rdf;

namespace VarveDemo.Importer;

/// <summary>
/// Terms written as SPARQL Update's <c>QuadData</c> accepts them: N-Triples
/// IRIs, blank node labels and literals, escaped by SPARQL 1.1's string and
/// IRI rules.
/// </summary>
public static class NTriplesTerms
{
    private static ReadOnlySpan<byte> XsdString => "http://www.w3.org/2001/XMLSchema#string"u8;

    /// <summary>Writes <paramref name="term"/>; a blank node's label is prefixed with <paramref name="blankPrefix"/>.</summary>
    public static void Write(StringBuilder output, in RdfTermView term, string blankPrefix)
    {
        ArgumentNullException.ThrowIfNull(output);
        switch (term.Kind)
        {
            case RdfTermKind.Iri:
                Iri(output, Encoding.UTF8.GetString(term.Lexical));
                break;
            case RdfTermKind.BlankNode:
                ReadOnlySpan<byte> label = term.Lexical;
                if (label.StartsWith("_:"u8))
                {
                    label = label[2..];
                }

                output.Append("_:").Append(blankPrefix);
                foreach (byte b in label)
                {
                    output.Append(char.IsAsciiLetterOrDigit((char)b) ? (char)b : '_');
                }

                break;
            case RdfTermKind.Literal:
                if (term.Direction != TextDirection.None)
                {
                    throw new InvalidDataException("A literal with a base direction, which SPARQL 1.1 cannot write.");
                }

                Literal(output, Encoding.UTF8.GetString(term.Lexical));
                if (term.HasLanguage)
                {
                    output.Append('@').Append(Encoding.UTF8.GetString(term.Language));
                }
                else if (term.HasDatatype && !term.Datatype.SequenceEqual(XsdString))
                {
                    output.Append("^^");
                    Iri(output, Encoding.UTF8.GetString(term.Datatype));
                }

                break;
            default:
                throw new InvalidDataException("A term of kind " + term.Kind.ToString() + ", which a catalog leaf does not produce.");
        }
    }

    /// <summary>An IRI in angle brackets, with the characters SPARQL forbids escaped as <c>\u</c>.</summary>
    public static void Iri(StringBuilder output, string iri)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(iri);
        output.Append('<');
        foreach (char c in iri)
        {
            if (c <= ' ' || c is '<' or '>' or '"' or '{' or '}' or '|' or '^' or '`' or '\\')
            {
                output.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            }
            else
            {
                output.Append(c);
            }
        }

        output.Append('>');
    }

    /// <summary>A string in double quotes, escaped.</summary>
    public static void Literal(StringBuilder output, string text)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(text);
        output.Append('"');
        foreach (char c in text)
        {
            switch (c)
            {
                case '"': output.Append("\\\""); break;
                case '\\': output.Append("\\\\"); break;
                case '\n': output.Append("\\n"); break;
                case '\r': output.Append("\\r"); break;
                case '\t': output.Append("\\t"); break;
                case '\b': output.Append("\\b"); break;
                case '\f': output.Append("\\f"); break;
                default:
                    if (c < ' ')
                    {
                        output.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        output.Append(c);
                    }

                    break;
            }
        }

        output.Append('"');
    }
}
