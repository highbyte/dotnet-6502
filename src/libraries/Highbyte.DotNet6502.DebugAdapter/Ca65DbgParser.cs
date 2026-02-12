using System.Text.RegularExpressions;

namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// Simple parser for ca65 .dbg debug symbol files.
/// Parses file, line, seg, and span records to build source line to address mappings.
/// </summary>
public class Ca65DbgParser
{
    private readonly Dictionary<int, string> _files = new();
    private readonly Dictionary<int, SegmentInfo> _segments = new();
    private readonly Dictionary<int, SpanInfo> _spans = new();
    private readonly List<LineInfo> _lines = new();

    public Dictionary<string, Dictionary<int, ushort>> SourceLineToAddress { get; } = new();
    
    /// <summary>
    /// Gets the load address from the first CODE segment, or 0 if not found.
    /// </summary>
    public ushort GetLoadAddress()
    {
        // Find the first CODE segment
        foreach (var segment in _segments.Values)
        {
            if (segment.Name?.Equals("CODE", StringComparison.OrdinalIgnoreCase) == true)
            {
                return segment.Start;
            }
        }
        
        // If no CODE segment, return the first segment's start address
        return _segments.Values.FirstOrDefault()?.Start ?? 0;
    }

    public void ParseFile(string dbgFilePath)
    {
        if (!File.Exists(dbgFilePath))
            throw new FileNotFoundException($"Debug file not found: {dbgFilePath}");

        var lines = File.ReadAllLines(dbgFilePath);
        
        // First pass: parse all records
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            if (trimmed.StartsWith("file"))
                ParseFileRecord(trimmed);
            else if (trimmed.StartsWith("seg"))
                ParseSegmentRecord(trimmed);
            else if (trimmed.StartsWith("span"))
                ParseSpanRecord(trimmed);
            else if (trimmed.StartsWith("line"))
                ParseLineRecord(trimmed);
        }

        // Second pass: build source line to address mapping
        BuildAddressMap();
    }

    private void ParseFileRecord(string record)
    {
        // file id=0,name="program.asm",size=245,mtime=0x5F8C9A4E,mod=0
        var values = ParseKeyValuePairs(record);
        if (values.TryGetValue("id", out var id) && values.TryGetValue("name", out var name))
        {
            _files[int.Parse(id)] = name.Trim('"');
        }
    }

    private void ParseSegmentRecord(string record)
    {
        // seg id=0,name="CODE",start=0x00c000,size=0x000F,addrsize=absolute,type=rw
        var values = ParseKeyValuePairs(record);
        if (values.TryGetValue("id", out var id) && values.TryGetValue("start", out var start))
        {
            var name = values.TryGetValue("name", out var n) ? n.Trim('"') : null;
            _segments[int.Parse(id)] = new SegmentInfo
            {
                Id = int.Parse(id),
                Name = name,
                Start = ParseHexValue(start)
            };
        }
    }

    private void ParseSpanRecord(string record)
    {
        // span id=1,seg=0,start=0,size=2,type=1
        var values = ParseKeyValuePairs(record);
        if (values.TryGetValue("id", out var id) && 
            values.TryGetValue("seg", out var seg) && 
            values.TryGetValue("start", out var start))
        {
            _spans[int.Parse(id)] = new SpanInfo
            {
                Id = int.Parse(id),
                SegmentId = int.Parse(seg),
                Start = int.Parse(start)
            };
        }
    }

    private void ParseLineRecord(string record)
    {
        // line id=0,file=0,line=10,span=1
        // Multi-span lines use '+' separator: span=474+473+472+...
        // We use the first span for address mapping.
        var values = ParseKeyValuePairs(record);
        if (values.TryGetValue("file", out var file) &&
            values.TryGetValue("line", out var line) &&
            values.TryGetValue("span", out var span))
        {
            // Take first span if multiple are specified (e.g. "474+473+472")
            var firstSpan = span.Split('+')[0];
            _lines.Add(new LineInfo
            {
                FileId = int.Parse(file),
                LineNumber = int.Parse(line),
                SpanId = int.Parse(firstSpan)
            });
        }
    }

    private void BuildAddressMap()
    {
        foreach (var lineInfo in _lines)
        {
            // Get the source file name
            if (!_files.TryGetValue(lineInfo.FileId, out var fileName))
                continue;

            // Get the span
            if (!_spans.TryGetValue(lineInfo.SpanId, out var span))
                continue;

            // Get the segment
            if (!_segments.TryGetValue(span.SegmentId, out var segment))
                continue;

            // Calculate the actual address
            var address = (ushort)(segment.Start + span.Start);

            // Add to mapping
            if (!SourceLineToAddress.ContainsKey(fileName))
                SourceLineToAddress[fileName] = new Dictionary<int, ushort>();

            SourceLineToAddress[fileName][lineInfo.LineNumber] = address;
        }
    }

    private Dictionary<string, string> ParseKeyValuePairs(string record)
    {
        var result = new Dictionary<string, string>();
        
        // Split on first whitespace to separate keyword from values
        var parts = record.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return result;

        // Parse key=value pairs
        var pairs = parts[1].Split(',');
        foreach (var pair in pairs)
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2)
            {
                result[kv[0].Trim()] = kv[1].Trim();
            }
        }

        return result;
    }

    private ushort ParseHexValue(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToUInt16(value.Substring(2), 16);
        return Convert.ToUInt16(value);
    }

    private class SegmentInfo
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public ushort Start { get; set; }
    }

    private class SpanInfo
    {
        public int Id { get; set; }
        public int SegmentId { get; set; }
        public int Start { get; set; }
    }

    private class LineInfo
    {
        public int FileId { get; set; }
        public int LineNumber { get; set; }
        public int SpanId { get; set; }
    }
}
