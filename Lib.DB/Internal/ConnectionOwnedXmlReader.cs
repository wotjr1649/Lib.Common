#nullable enable

using System.Xml;
using Microsoft.Data.SqlClient;

namespace Lib.DB.Internal
{
    /// <summary>
    /// SqlConnection/SqlCommand 수명과 결합된 XmlReader 래퍼.
    /// Dispose 시 내부 리소스를 안전하게 해제한다.
    /// </summary>
    internal sealed class ConnectionOwnedXmlReader : XmlReader
    {
        private SqlConnection? _connection;
        private SqlCommand? _command;
        private XmlReader? _reader;

        public ConnectionOwnedXmlReader(SqlConnection connection, SqlCommand command, XmlReader reader)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        private XmlReader Reader => _reader ?? throw new ObjectDisposedException(nameof(ConnectionOwnedXmlReader));

        public override int AttributeCount => Reader.AttributeCount;
        public override string BaseURI => Reader.BaseURI;
        public override int Depth => Reader.Depth;
        public override bool EOF => Reader.EOF;
        public override bool HasValue => Reader.HasValue;
        public override bool IsEmptyElement => Reader.IsEmptyElement;
        public override string LocalName => Reader.LocalName;
        public override string NamespaceURI => Reader.NamespaceURI;
        public override XmlNameTable NameTable => Reader.NameTable;
        public override XmlNodeType NodeType => Reader.NodeType;
        public override string Prefix => Reader.Prefix;
        public override ReadState ReadState => Reader.ReadState;
        public override string Value => Reader.Value;

        public override string GetAttribute(int i) => Reader.GetAttribute(i);
        public override string? GetAttribute(string name) => Reader.GetAttribute(name);
        public override string? GetAttribute(string name, string? namespaceURI) => Reader.GetAttribute(name, namespaceURI);

        // 🔧 핵심 수정: 반환형을 string?로 맞춰 null 반환 가능성을 정식화
        public override string? LookupNamespace(string prefix) => Reader.LookupNamespace(prefix);

        public override bool MoveToAttribute(string name) => Reader.MoveToAttribute(name);
        public override bool MoveToAttribute(string name, string? ns) => Reader.MoveToAttribute(name, ns);
        public override bool MoveToElement() => Reader.MoveToElement();
        public override bool MoveToFirstAttribute() => Reader.MoveToFirstAttribute();
        public override bool MoveToNextAttribute() => Reader.MoveToNextAttribute();
        public override bool Read() => Reader.Read();
        public override bool ReadAttributeValue() => Reader.ReadAttributeValue();
        public override void ResolveEntity() => Reader.ResolveEntity();
        public override void Close() => Reader.Close();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _reader?.Dispose(); } catch { /* ignore */ }
                try { _command?.Dispose(); } catch { /* ignore */ }
                try { _connection?.Dispose(); } catch { /* ignore */ }
            }
            _reader = null;
            _command = null;
            _connection = null;
            base.Dispose(disposing);
        }
    }
}
