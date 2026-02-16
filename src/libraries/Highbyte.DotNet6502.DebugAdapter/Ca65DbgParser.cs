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
    /// Like <see cref="SourceLineToAddress"/> but excludes macro-expansion lines (type=2).
    /// Used for after-line address decorations so macro body definition lines don't get addresses.
    /// The full <see cref="SourceLineToAddress"/> is still needed for PC→source reverse lookup
    /// so stepping into a macro still shows source, not disassembly.
    /// </summary>
    public Dictionary<string, Dictionary<int, ushort>> NonMacroSourceLineToAddress { get; } = new();

    /// <summary>
    /// Reverse map: 6502 address → (source file name, 1-based line number).
    /// Covers ALL lines including macro expansion lines (type=2).
    /// Used for PC→source reverse lookup so that every invocation of a macro
    /// (each of which lives at a distinct address) resolves to the correct source line.
    /// Unlike <see cref="SourceLineToAddress"/> (line→address, last-write-wins), this
    /// map is keyed by the unique address so multiple macro invocations never collide.
    /// </summary>
    public Dictionary<ushort, (string FileName, int LineNumber)> AddressToSource { get; } = new();

    /// <summary>
    /// All symbols from the .dbg file. Key is symbol name.
    /// Type "lab" = code/data label with memory address; "equ" = symbolic constant.
    /// </summary>
    public Dictionary<string, SymbolInfo> Symbols { get; } = new();
    
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
            else if (trimmed.StartsWith("sym"))
                ParseSymRecord(trimmed);
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
        // line id=3,file=0,line=98,type=2,count=1,span=157  (type=2 = macro expansion)
        // Multi-span lines use '+' separator: span=474+473+472+...
        //
        // For macros called from N sites, ca65 generates ONE line record per macro-body line
        // with ALL N invocations' spans in a single '+'-delimited list:
        //   line id=181,file=0,line=111,type=2,span=199+37+20
        //   → span 199 = first call-site, span 37 = second, span 20 = third
        //
        // We store all span IDs so BuildAddressMap can add every invocation's address
        // to AddressToSource.  For forward lookup (breakpoints, decorations) we use only
        // the primary (first) span — that sets the breakpoint at the first invocation.
        var values = ParseKeyValuePairs(record);
        if (values.TryGetValue("file", out var file) &&
            values.TryGetValue("line", out var line) &&
            values.TryGetValue("span", out var span))
        {
            // type=2 means this line was generated from a macro expansion.
            // The line points back into the macro body definition, not the call site.
            // We skip these so decorations appear at call sites, not inside macro bodies.
            var isMacroExpansion = values.TryGetValue("type", out var lineType) && lineType == "2";

            var spanIds = span.Split('+').Select(int.Parse).ToArray();
            _lines.Add(new LineInfo
            {
                FileId = int.Parse(file),
                LineNumber = int.Parse(line),
                SpanId = spanIds[0],          // primary span for forward lookup
                AllSpanIds = spanIds,          // all spans for reverse lookup
                IsMacroExpansion = isMacroExpansion
            });
        }
    }

    private void ParseSymRecord(string record)
    {
        // sym id=28,name="vblank_irq",addrsize=absolute,scope=0,def=46,ref=145+145,val=0xC01A,seg=0,type=lab
        // sym id=37,name="XSHIFT",addrsize=zeropage,scope=0,def=21,ref=137+215+93,val=0x5,type=equ
        var values = ParseKeyValuePairs(record);
        if (values.TryGetValue("name", out var name) &&
            values.TryGetValue("val", out var val) &&
            values.TryGetValue("type", out var type))
        {
            var addrSize = values.TryGetValue("addrsize", out var a) ? a : "";
            Symbols[name.Trim('"')] = new SymbolInfo
            {
                Value = ParseHexValue(val),
                Type = type,
                AddrSize = addrSize
            };
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

            // Always populate the full map (used for forward lookup: line→address, e.g. setting breakpoints)
            if (!SourceLineToAddress.ContainsKey(fileName))
                SourceLineToAddress[fileName] = new Dictionary<int, ushort>();

            SourceLineToAddress[fileName][lineInfo.LineNumber] = address;

            // Populate the reverse map (address→source) for O(1) PC→source lookup.
            // Keyed by the unique 6502 address, so multiple macro invocations at
            // different addresses all get their own entry — no last-write-wins problem.
            //
            // Also add every *additional* span from the same line record: when a macro
            // is called N times, ca65 puts all N invocation spans in a single line record
            // (e.g. span=199+37+20).  We must add each one so the debugger can resolve
            // the PC for any invocation back to the correct source line.
            //
            // Use TryAdd (first-write-wins): when two line records share the same spans
            // (e.g. the .macro header line and the first body instruction both get assigned
            // the same span because the header generates no code), the first record in the
            // .dbg file is the actual instruction and should win; the header record comes
            // later with a higher id and must NOT overwrite the instruction mapping.
            AddressToSource.TryAdd(address, (fileName, lineInfo.LineNumber));
            foreach (var extraSpanId in lineInfo.AllSpanIds)
            {
                if (extraSpanId == lineInfo.SpanId) continue; // already handled above
                if (!_spans.TryGetValue(extraSpanId, out var extraSpan)) continue;
                if (!_segments.TryGetValue(extraSpan.SegmentId, out var extraSeg)) continue;
                var extraAddress = (ushort)(extraSeg.Start + extraSpan.Start);
                AddressToSource.TryAdd(extraAddress, (fileName, lineInfo.LineNumber));
            }

            // Also populate the non-macro map (used for after-line address decorations).
            // Macro expansion lines (type=2) are excluded so the decoration only appears
            // at the call site, not inside macro body definition lines.
            if (!lineInfo.IsMacroExpansion)
            {
                if (!NonMacroSourceLineToAddress.ContainsKey(fileName))
                    NonMacroSourceLineToAddress[fileName] = new Dictionary<int, ushort>();
                NonMacroSourceLineToAddress[fileName][lineInfo.LineNumber] = address;
            }
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
        /// <summary>Primary (first) span — used for forward lookup (breakpoints, decorations).</summary>
        public int SpanId { get; set; }
        /// <summary>
        /// All spans, including the primary.  When a macro is called N times, ca65 stores
        /// all N invocation spans in one '+'-delimited field, so this array has N entries.
        /// Used so <see cref="Ca65DbgParser.AddressToSource"/> gets an entry for every
        /// invocation address, not just the first.
        /// </summary>
        public int[] AllSpanIds { get; set; } = Array.Empty<int>();
        /// <summary>True when type=2 in the .dbg file (macro expansion body line).</summary>
        public bool IsMacroExpansion { get; set; }
    }
}

public class SymbolInfo
{
    /// <summary>The symbol's value (address for labels, constant for equ).</summary>
    public ushort Value { get; set; }
    /// <summary>"lab" for code/data labels, "equ" for symbolic constants.</summary>
    public string Type { get; set; } = "";
    /// <summary>"absolute", "zeropage", etc.</summary>
    public string AddrSize { get; set; } = "";
}
