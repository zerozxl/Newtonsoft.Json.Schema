using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema.Infrastructure;

namespace Newtonsoft.Json.Schema.V4.Infrastructure
{
    internal class JSchema4Discovery
    {
        private readonly KnownSchemaState _state;
        private readonly Stack<SchemaPath> _pathStack;
        private readonly List<KnownSchema> _knownSchemas;

        public List<KnownSchema> KnownSchemas
        {
            get { return _knownSchemas; }
        }

        public JSchema4Discovery()
            : this(new List<KnownSchema>(), KnownSchemaState.External)
        {
        }

        public JSchema4Discovery(List<KnownSchema> knownSchemas, KnownSchemaState state)
        {
            _state = state;
            _pathStack = new Stack<SchemaPath>();
            _knownSchemas = knownSchemas ?? new List<KnownSchema>();
        }

        public void Discover(JSchema4 schema, Uri uri)
        {
            Uri resolvedUri = uri ?? schema.Id ?? new Uri(string.Empty, UriKind.RelativeOrAbsolute);

            _pathStack.Push(new SchemaPath(resolvedUri, string.Empty));

            DiscoverInternal(schema, "#");

            _pathStack.Pop();
        }

        private void DiscoverInternal(JSchema4 schema, string latestPath)
        {
            if (schema.Reference != null)
                return;

            if (_knownSchemas.Any(s => s.Schema == schema))
                return;

            string currentPath;
            if (schema.Id == null)
            {
                Uri currentScopeId = _pathStack.First().Id;
                currentPath = string.Join("/", _pathStack.Where(p => p.Id == currentScopeId && !string.IsNullOrEmpty(p.Path)).Select(p => p.Path));

                if (!string.IsNullOrEmpty(currentPath))
                    currentPath += "/";

                currentPath += latestPath;
            }
            else
            {
                latestPath = "#";
                currentPath = "#";
            }

            Uri newScopeId;
            Uri schemaKnownId = SchemaDiscovery.ResolveSchemaIdAndScopeId(_pathStack.First().Id, schema.Id, currentPath, out newScopeId);

            _knownSchemas.Add(new KnownSchema(schemaKnownId, schema, _state));

            _pathStack.Push(new SchemaPath(newScopeId, latestPath));

            // discover should happen in the same order as writer except extension data (e.g. definitions)
            if (schema._extensionData != null)
            {
                foreach (KeyValuePair<string, JToken> valuePair in schema._extensionData)
                {
                    DiscoverTokenSchemas(valuePair.Key, valuePair.Value);
                }
            }

            DiscoverSchema(Constants.PropertyNames.AdditionalProperties, schema.AdditionalProperties);
            DiscoverSchema(Constants.PropertyNames.AdditionalItems, schema.AdditionalItems);
            DiscoverDictionarySchemas(Constants.PropertyNames.Properties, schema._properties);
            DiscoverDictionarySchemas(Constants.PropertyNames.PatternProperties, schema._patternProperties);
            DiscoverArraySchemas(Constants.PropertyNames.Items, schema._items);
            DiscoverArraySchemas(Constants.PropertyNames.AllOf, schema._allOf);
            DiscoverArraySchemas(Constants.PropertyNames.AnyOf, schema._anyOf);
            DiscoverArraySchemas(Constants.PropertyNames.OneOf, schema._oneOf);
            DiscoverSchema(Constants.PropertyNames.Not, schema.Not);

            _pathStack.Pop();
        }

        private void DiscoverTokenSchemas(string name, JToken token)
        {
            if (token is JObject)
            {
                JObject o = (JObject)token;

                JSchemaAnnotation annotation = token.Annotation<JSchemaAnnotation>();
                if (annotation != null)
                {
                    DiscoverInternal(annotation.Schema, name);
                }
                else
                {
                    foreach (KeyValuePair<string, JToken> valuePair in o)
                    {
                        DiscoverTokenSchemas(name + "/" + valuePair.Key, valuePair.Value);
                    }
                }
            }
            else if (token is JArray || token is JConstructor)
            {
                IList<JToken> l = (IList<JToken>)token;

                for (int i = 0; i < l.Count; i++)
                {
                    DiscoverTokenSchemas(name + "/" + i.ToString(CultureInfo.InvariantCulture), l[i]);
                }
            }
        }

        private void DiscoverDictionarySchemas(string name, IDictionary<string, JSchema4> schemas)
        {
            if (schemas != null)
            {
                foreach (KeyValuePair<string, JSchema4> valuePair in schemas)
                {
                    DiscoverInternal(valuePair.Value, name + "/" + valuePair.Key);
                }
            }
        }

        private void DiscoverArraySchemas(string name, IList<JSchema4> schemas)
        {
            if (schemas != null)
            {
                for (int i = 0; i < schemas.Count; i++)
                {
                    DiscoverInternal(schemas[i], name + "/" + i.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        private void DiscoverSchema(string name, JSchema4 schema)
        {
            if (schema != null)
                DiscoverInternal(schema, name);
        }
    }
}