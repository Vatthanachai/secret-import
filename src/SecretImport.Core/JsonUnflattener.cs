using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SecretImport.Core;

/// <summary>
/// Turns flat configuration keys back into a JSON document for export (ADR-0015). Nested output splits keys on <c>:</c>,
/// groups them case-insensitively (keeping the first spelling seen), and turns children named <c>0..n-1</c> into arrays.
/// Values are always JSON strings. Error messages name keys but never values.
/// </summary>
internal sealed class JsonUnflattener
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        IndentSize = 2,
        // One fixed newline, so the output is the same on every OS.
        NewLine = "\n",
        // The file is never embedded in HTML/JS, so keep non-ASCII and '+', '<', '&' readable (like SecretsStore).
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <param name="values">The secrets in store order.</param>
    /// <param name="flat">True writes every key as stored; false nests them.</param>
    /// <returns>The document text, ending with a newline.</returns>
    /// <exception cref="ImportException"><see cref="ImportErrorKind.InvalidInput"/> when a key cannot be nested (it has both a value and child keys).</exception>
    public string ToJson(IEnumerable<KeyValuePair<string, string>> values, bool flat)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, WriterOptions))
        {
            if (flat)
            {
                writer.WriteStartObject();
                foreach ((string key, string value) in values)
                {
                    writer.WriteString(key, value);
                }

                writer.WriteEndObject();
            }
            else
            {
                // The root stays an object even when its keys look like indexes: import requires an object root.
                WriteObject(writer, BuildTree(values));
            }
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }

    private static Node BuildTree(IEnumerable<KeyValuePair<string, string>> values)
    {
        Node root = new(string.Empty, string.Empty);
        foreach ((string key, string value) in values)
        {
            string[] segments = key.Split(':');
            Node node = root;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                node = node.GetOrAdd(segments[i], key);
                if (node.Value is not null)
                {
                    throw Conflict(node.FirstKey, key);
                }
            }

            node = node.GetOrAdd(segments[^1], key);
            if (node.Value is not null)
            {
                throw new ImportException(
                    ImportErrorKind.InvalidInput,
                    $"Cannot export nested JSON: '{node.FirstKey}' and '{key}' are the same key (keys are case-insensitive). Export flat keys (--flat) instead.");
            }

            if (node.Children.Count > 0)
            {
                throw Conflict(key, node.FirstKey);
            }

            node.Value = value;
        }

        return root;
    }

    private static ImportException Conflict(string valueKey, string childKey) =>
        new(ImportErrorKind.InvalidInput, $"Cannot export nested JSON: '{valueKey}' has a value, but '{childKey}' needs it to be an object. Export flat keys (--flat) instead.");

    private static void WriteNode(Utf8JsonWriter writer, Node node)
    {
        if (node.Value is not null)
        {
            writer.WriteStringValue(node.Value);
        }
        else if (AsArray(node) is { } items)
        {
            writer.WriteStartArray();
            foreach (Node item in items)
            {
                WriteNode(writer, item);
            }

            writer.WriteEndArray();
        }
        else
        {
            WriteObject(writer, node);
        }
    }

    private static void WriteObject(Utf8JsonWriter writer, Node node)
    {
        writer.WriteStartObject();
        foreach (Node child in node.Children)
        {
            writer.WritePropertyName(child.Name);
            WriteNode(writer, child);
        }

        writer.WriteEndObject();
    }

    /// <summary>The children in index order when they are named exactly <c>0..n-1</c>; otherwise null.</summary>
    private static Node[]? AsArray(Node node)
    {
        Node[] items = new Node[node.Children.Count];
        for (int i = 0; i < items.Length; i++)
        {
            if (!node.ByName.TryGetValue(i.ToString(CultureInfo.InvariantCulture), out Node? item))
            {
                return null;
            }

            items[i] = item;
        }

        return items;
    }

    /// <param name="Name">The first spelling seen for this segment.</param>
    /// <param name="FirstKey">The first full key that reached this node, used in error messages.</param>
    private sealed record Node(string Name, string FirstKey)
    {
        public string? Value { get; set; }

        public List<Node> Children { get; } = [];

        public Dictionary<string, Node> ByName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Node GetOrAdd(string name, string key)
        {
            if (!ByName.TryGetValue(name, out Node? child))
            {
                child = new Node(name, key);
                ByName.Add(name, child);
                Children.Add(child);
            }

            return child;
        }
    }
}