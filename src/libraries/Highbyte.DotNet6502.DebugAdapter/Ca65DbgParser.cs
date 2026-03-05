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
    /// <summary>
    /// All addresses that map to a given source line, keyed by (file, lineNumber).
    /// Used for source-line stepping: the debugger keeps executing while PC stays in this set.
    /// Built alongside <see cref="AddressToSource"/> in <see cref="BuildAddressMap"/>.
    /// </summary>
    public Dictionary<(string FileName, int LineNumber), HashSet<ushort>> SourceLineAddresses { get; } = new();

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

    /// <summary>
    /// Resolves any relative source-file paths in this parser's output maps to absolute
    /// paths using <paramref name="baseDirectory"/> as the root.  Absolute paths are
    /// left unchanged.
    ///
    /// Call this on a secondary parser (with its own .dbg directory) before calling
    /// <see cref="MergeFrom"/> so that relative paths from the secondary .dbg are not
    /// accidentally resolved relative to the primary .dbg's directory.
    /// </summary>
    public void ResolveRelativePaths(string baseDirectory)
    {
        // SourceLineToAddress and NonMacroSourceLineToAddress are keyed by filename
        ResolveKeysInDictionary(SourceLineToAddress, baseDirectory);
        ResolveKeysInDictionary(NonMacroSourceLineToAddress, baseDirectory);

        // AddressToSource values contain the filename
        foreach (var addr in AddressToSource.Keys.ToList())
        {
            var (fileName, lineNum) = AddressToSource[addr];
            var resolved = ResolveFileName(fileName, baseDirectory);
            if (!string.Equals(resolved, fileName, StringComparison.Ordinal))
                AddressToSource[addr] = (resolved, lineNum);
        }
    }

    private static void ResolveKeysInDictionary(
        Dictionary<string, Dictionary<int, ushort>> dict, string baseDirectory)
    {
        foreach (var key in dict.Keys.ToList())
        {
            var resolved = ResolveFileName(key, baseDirectory);
            if (!string.Equals(resolved, key, StringComparison.Ordinal))
            {
                dict[resolved] = dict[key];
                dict.Remove(key);
            }
        }
    }

    private static string ResolveFileName(string fileName, string baseDirectory)
    {
        if (Path.IsPathRooted(fileName))
            return fileName;
        return Path.GetFullPath(Path.Combine(baseDirectory, fileName));
    }

    /// <summary>
    /// Merges source-map data from another parsed .dbg file into this one.
    /// Call this after both parsers have had <see cref="ParseFile"/> called on them.
    /// Used when multiple .dbg files cover different address ranges
    /// (e.g., a user program's .dbg + C64 Kernal/Basic ROM .dbg files).
    ///
    /// Internal ID namespaces (file/span/segment/line IDs) are local to each .dbg
    /// file, so only the resolved output maps are merged.
    /// Primary parser entries win on collision (TryAdd / existing-key-wins).
    /// </summary>
    public void MergeFrom(Ca65DbgParser other)
    {
        // SourceLineToAddress — breakpoint lookup (line → address)
        foreach (var (file, lineMap) in other.SourceLineToAddress)
        {
            if (!SourceLineToAddress.TryGetValue(file, out var existing))
            {
                existing = new Dictionary<int, ushort>();
                SourceLineToAddress[file] = existing;
            }
            foreach (var (line, addr) in lineMap)
                existing[line] = addr; // last-write-wins within a file, same as ParseFile
        }

        // NonMacroSourceLineToAddress — editor address decorations
        foreach (var (file, lineMap) in other.NonMacroSourceLineToAddress)
        {
            if (!NonMacroSourceLineToAddress.TryGetValue(file, out var existing))
            {
                existing = new Dictionary<int, ushort>();
                NonMacroSourceLineToAddress[file] = existing;
            }
            foreach (var (line, addr) in lineMap)
                existing[line] = addr;
        }

        // AddressToSource — PC→source reverse lookup; primary parser takes precedence
        foreach (var (addr, source) in other.AddressToSource)
            AddressToSource.TryAdd(addr, source);

        // SourceLineAddresses — source-line stepping lookup
        foreach (var (key, addrs) in other.SourceLineAddresses)
        {
            if (!SourceLineAddresses.TryGetValue(key, out var existing))
            {
                existing = new HashSet<ushort>();
                SourceLineAddresses[key] = existing;
            }
            existing.UnionWith(addrs);
        }

        // Symbols — label/constant table; primary parser takes precedence
        foreach (var (name, sym) in other.Symbols)
            Symbols.TryAdd(name, sym);
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
            var spanInfo = new SpanInfo
            {
                Id = int.Parse(id),
                SegmentId = int.Parse(seg),
                Start = int.Parse(start)
            };
            if (values.TryGetValue("size", out var size))
                spanInfo.Size = int.Parse(size);
            _spans[spanInfo.Id] = spanInfo;
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
            string? segName = null;
            if (values.TryGetValue("seg", out var segId) && int.TryParse(segId, out var segIdInt))
            {
                if (_segments.TryGetValue(segIdInt, out var segInfo))
                    segName = segInfo.Name;
            }
            Symbols[name.Trim('"')] = new SymbolInfo
            {
                Value = ParseHexValue(val),
                Type = type,
                AddrSize = addrSize,
                SegmentName = segName
            };
        }
    }

    private void BuildAddressMap()
    {
        // === Pass 1: Process non-macro (type!=2) lines first ===
        // This lets invocation-site records claim addresses in AddressToSource
        // before macro body records.  Also record "container ranges" for macro
        // invocations — non-macro spans whose size > 3 bytes, meaning they cover
        // multiple instructions (i.e., a macro expansion).
        //
        // The longest 6502 instruction is 3 bytes, so any span with size > 3
        // necessarily contains multiple instructions and is a container.
        var invocationRanges = new List<(ushort Start, ushort End, string FileName, int LineNumber)>();

        foreach (var lineInfo in _lines.Where(l => !l.IsMacroExpansion))
        {
            if (!_files.TryGetValue(lineInfo.FileId, out var fileName))
                continue;
            if (!_spans.TryGetValue(lineInfo.SpanId, out var span))
                continue;
            if (!_segments.TryGetValue(span.SegmentId, out var segment))
                continue;

            var address = (ushort)(segment.Start + span.Start);

            // Forward lookup: line → address (breakpoints, etc.)
            if (!SourceLineToAddress.ContainsKey(fileName))
                SourceLineToAddress[fileName] = new Dictionary<int, ushort>();
            SourceLineToAddress[fileName][lineInfo.LineNumber] = address;

            // Reverse lookup: address → source.
            // Use TryAdd (first-write-wins): when two non-macro line records share
            // the same span (e.g. .macro header + first body instruction), the first
            // record in the .dbg file is the actual instruction and should win.
            AddressToSource.TryAdd(address, (fileName, lineInfo.LineNumber));

            // Extra spans (multi-invocation macros: each span = different call site)
            foreach (var extraSpanId in lineInfo.AllSpanIds)
            {
                if (extraSpanId == lineInfo.SpanId) continue;
                if (!_spans.TryGetValue(extraSpanId, out var extraSpan)) continue;
                if (!_segments.TryGetValue(extraSpan.SegmentId, out var extraSeg)) continue;
                var extraAddress = (ushort)(extraSeg.Start + extraSpan.Start);
                AddressToSource.TryAdd(extraAddress, (fileName, lineInfo.LineNumber));
            }

            // Detect macro invocation container spans.
            // A span with size > 3 covers multiple instructions → macro expansion.
            if (span.Size > 3)
            {
                var rangeStart = (ushort)(segment.Start + span.Start);
                var rangeEnd = (ushort)(segment.Start + span.Start + span.Size);
                invocationRanges.Add((rangeStart, rangeEnd, fileName, lineInfo.LineNumber));
            }

            // Non-macro address decorations (excludes type=2 and multi-span lines)
            if (lineInfo.AllSpanIds.Length <= 1)
            {
                if (!NonMacroSourceLineToAddress.ContainsKey(fileName))
                    NonMacroSourceLineToAddress[fileName] = new Dictionary<int, ushort>();
                NonMacroSourceLineToAddress[fileName][lineInfo.LineNumber] = address;
            }
        }

        // Sort invocation ranges innermost-first (smallest size first) so that
        // nested macro expansions are remapped to their immediate invocation,
        // not an outer one.
        invocationRanges.Sort((a, b) => (a.End - a.Start).CompareTo(b.End - b.Start));

        // === Pass 2: Process type=2 (macro expansion) lines ===
        // If an address falls within an invocation container range, override its
        // AddressToSource mapping to the invocation line.  This ensures the debugger
        // resolves the PC to the macro call site, not the body definition.
        foreach (var lineInfo in _lines.Where(l => l.IsMacroExpansion))
        {
            if (!_files.TryGetValue(lineInfo.FileId, out var fileName))
                continue;

            // Process all spans (primary + extra invocation spans)
            foreach (var spanId in lineInfo.AllSpanIds)
            {
                if (!_spans.TryGetValue(spanId, out var span)) continue;
                if (!_segments.TryGetValue(span.SegmentId, out var segment)) continue;

                var address = (ushort)(segment.Start + span.Start);

                // Forward lookup for body lines (used when setting breakpoints in macro file)
                if (!SourceLineToAddress.ContainsKey(fileName))
                    SourceLineToAddress[fileName] = new Dictionary<int, ushort>();
                SourceLineToAddress[fileName][lineInfo.LineNumber] = address;

                // Check if this address falls within a macro invocation container range.
                // If so, override AddressToSource to point to the invocation line.
                var invocation = invocationRanges
                    .FirstOrDefault(r => address >= r.Start && address < r.End);

                if (invocation != default)
                {
                    // Remap to the invocation line — this overwrites any previous
                    // body-line mapping (or is a no-op if the invocation already claimed it).
                    AddressToSource[address] = (invocation.FileName, invocation.LineNumber);
                }
                else
                {
                    // Not within any container — normal type=2 handling.
                    AddressToSource.TryAdd(address, (fileName, lineInfo.LineNumber));
                }
            }
        }

        // === Build SourceLineAddresses from the final AddressToSource ===
        // This ensures consistency: all addresses mapping to the same (file, line)
        // are grouped together, so source-line stepping works correctly across
        // macro invocations.
        foreach (var (addr, (fn, ln)) in AddressToSource)
        {
            AddToSourceLineAddresses(fn, ln, addr);
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

    private void AddToSourceLineAddresses(string fileName, int lineNumber, ushort address)
    {
        var key = (fileName, lineNumber);
        if (!SourceLineAddresses.TryGetValue(key, out var set))
        {
            set = new HashSet<ushort>();
            SourceLineAddresses[key] = set;
        }
        set.Add(address);
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
        /// <summary>Size in bytes. Used to detect macro invocation container spans (size > 3).</summary>
        public int Size { get; set; }
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
    /// <summary>Segment name from .dbg file (e.g. "CODE", "DATA", "RODATA"). Null if not available.</summary>
    public string? SegmentName { get; set; }
}
