namespace Highbyte.DotNet6502.Systems.Debugger;

/// <summary>
/// Evaluates 6502 breakpoint condition expressions against live CPU and memory state.
///
/// Supported syntax:
///   Registers:   A, X, Y, SP, PC          (case-insensitive)
///   Flags:       C, Z, N, V, I, D, B      (Carry/Zero/Negative/Overflow/IRQ/Decimal/Break, as 0/1)
///   System:      the names an <see cref="IDebugValueSource"/> exposes, such as a C64's
///                RASTER and CYCLE (see the system's documentation)
///   Memory:      [$addr]  or  [$addr + reg]
///   Literals:    $hex, 0xhex, decimal
///   Operators:   ==  !=  &lt;  &lt;=  &gt;  &gt;=
///   Logical:     &amp;&amp;  ||   (left-to-right, short-circuit)
///
/// Examples:
///   A == $FF
///   X >= 10
///   Z == 1
///   [$D020] == $01
///   [$0300 + X] &gt; $7F
///   A == $FF &amp;&amp; X == 0
///   RASTER == 100 &amp;&amp; CYCLE &gt;= 20
///
/// On any parse or evaluation error the method returns <c>true</c> so the breakpoint
/// is not accidentally suppressed.
/// </summary>
public static class BreakpointConditionEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="condition"/> against the current CPU and memory state.
    /// Returns <c>true</c> to stop at the breakpoint, <c>false</c> to skip it.
    /// Returns <c>true</c> on any parse / evaluation error (fail-safe).
    /// </summary>
    public static bool Evaluate(string condition, CPU cpu, Memory memory)
        => Evaluate(condition, cpu, memory, debugValues: null);

    /// <summary>
    /// As <see cref="Evaluate(string, CPU, Memory)"/>, with the names of
    /// <paramref name="debugValues"/> (when given) available as operands.
    /// </summary>
    public static bool Evaluate(string condition, CPU cpu, Memory memory, IDebugValueSource? debugValues)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return true;

        try
        {
            var evaluator = new Evaluator(condition.Trim(), cpu, memory, debugValues);
            return evaluator.ParseOrExpr();
        }
        catch
        {
            return true;  // fail-safe: always stop on error
        }
    }

    /// <summary>
    /// Checks that <paramref name="condition"/> parses and evaluates against the current state,
    /// for a debugger to reject a mistyped condition up front instead of stopping on it at once.
    /// <paramref name="error"/> describes the first problem, or is null when the condition is fine.
    /// </summary>
    public static bool TryParse(string condition, CPU cpu, Memory memory, IDebugValueSource? debugValues, out string? error)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            error = "The condition is empty.";
            return false;
        }

        try
        {
            var evaluator = new Evaluator(condition.Trim(), cpu, memory, debugValues);
            evaluator.ParseOrExpr();
            if (!evaluator.AtEnd)
            {
                error = $"Unexpected text at position {evaluator.Position} in '{condition.Trim()}'";
                return false;
            }
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Internal recursive-descent evaluator
    // -------------------------------------------------------------------------

    private sealed class Evaluator
    {
        private readonly string _input;
        private int _pos;
        private readonly CPU _cpu;
        private readonly Memory _memory;
        private readonly IDebugValueSource? _debugValues;

        public Evaluator(string input, CPU cpu, Memory memory, IDebugValueSource? debugValues)
        {
            _input = input;
            _cpu = cpu;
            _memory = memory;
            _debugValues = debugValues;
        }

        public int Position => _pos;

        public bool AtEnd
        {
            get
            {
                SkipWhitespace();
                return _pos >= _input.Length;
            }
        }

        // or-expr = and-expr ( '||' and-expr )*
        public bool ParseOrExpr()
        {
            var result = ParseAndExpr();
            while (TryConsumeToken("||"))
            {
                var right = ParseAndExpr();
                result = result || right;   // short-circuit handled by evaluation order
            }
            return result;
        }

        // and-expr = comparison ( '&&' comparison )*
        private bool ParseAndExpr()
        {
            var result = ParseComparison();
            while (TryConsumeToken("&&"))
            {
                var right = ParseComparison();
                result = result && right;
            }
            return result;
        }

        // comparison = operand op operand
        private bool ParseComparison()
        {
            var lhs = ParseOperand();
            SkipWhitespace();
            var op = ParseOperator();
            var rhs = ParseOperand();

            return op switch
            {
                "==" => lhs == rhs,
                "!=" => lhs != rhs,
                "<"  => lhs < rhs,
                "<=" => lhs <= rhs,
                ">"  => lhs > rhs,
                ">=" => lhs >= rhs,
                _    => throw new FormatException($"Unknown operator '{op}'")
            };
        }

        // operand = '[' address ('+' reg)? ']'  |  identifier  |  number
        private long ParseOperand()
        {
            SkipWhitespace();

            if (Peek() == '[')
                return ParseMemoryRef();

            if (IsIdentStart(Peek()))
                return ParseIdentifier();

            return ParseNumber();
        }

        // [$addr]  or  [$addr + reg]
        private long ParseMemoryRef()
        {
            Consume('[');
            SkipWhitespace();
            var addr = ParseNumber();   // e.g. $D020 or 0xD020 or 53280
            SkipWhitespace();

            if (Peek() == '+')
            {
                Consume('+');
                SkipWhitespace();
                var reg = ParseIdentifier();
                addr += reg;
            }

            SkipWhitespace();
            Consume(']');

            var effectiveAddr = (ushort)(addr & 0xFFFF);
            return _memory[effectiveAddr];
        }

        // Register or flag name
        private long ParseIdentifier()
        {
            SkipWhitespace();
            int start = _pos;
            while (_pos < _input.Length && (char.IsLetterOrDigit(_input[_pos]) || _input[_pos] == '_'))
                _pos++;
            var name = _input.Substring(start, _pos - start).ToUpperInvariant();

            return name switch
            {
                // 16-bit register — widened to long
                "PC" => _cpu.PC,

                // 8-bit registers — widened to long (unsigned)
                "A"  => _cpu.A,
                "X"  => _cpu.X,
                "Y"  => _cpu.Y,
                "SP" => _cpu.SP,

                // Status flags — 0 or 1
                "C"  => _cpu.ProcessorStatus.Carry           ? 1 : 0,
                "Z"  => _cpu.ProcessorStatus.Zero            ? 1 : 0,
                "N"  => _cpu.ProcessorStatus.Negative        ? 1 : 0,
                "V"  => _cpu.ProcessorStatus.Overflow        ? 1 : 0,
                "I"  => _cpu.ProcessorStatus.InterruptDisable ? 1 : 0,
                "D"  => _cpu.ProcessorStatus.Decimal         ? 1 : 0,
                "B"  => _cpu.ProcessorStatus.Break           ? 1 : 0,

                _    => LookupDebugValue(name)
            };
        }

        // A name the system exposes (such as a C64's RASTER): the fallback for an unknown identifier.
        private long LookupDebugValue(string name)
        {
            if (_debugValues != null && _debugValues.TryGetDebugValue(name, out var value))
                return value;
            throw new FormatException($"Unknown register, flag or value '{name}'");
        }

        // Numeric literal: $hex, 0xhex, or decimal
        private long ParseNumber()
        {
            SkipWhitespace();

            if (_pos < _input.Length && _input[_pos] == '$')
            {
                _pos++; // consume '$'
                return ReadHexDigits();
            }

            if (_pos + 1 < _input.Length
                && _input[_pos] == '0'
                && (_input[_pos + 1] == 'x' || _input[_pos + 1] == 'X'))
            {
                _pos += 2; // consume '0x'
                return ReadHexDigits();
            }

            // Decimal
            int start = _pos;
            while (_pos < _input.Length && char.IsDigit(_input[_pos]))
                _pos++;
            if (_pos == start)
                throw new FormatException($"Expected number at position {_pos} in '{_input}'");

            return long.Parse(_input.Substring(start, _pos - start));
        }

        private uint ReadHexDigits()
        {
            int start = _pos;
            while (_pos < _input.Length && IsHexDigit(_input[_pos]))
                _pos++;
            if (_pos == start)
                throw new FormatException($"Expected hex digits at position {_pos} in '{_input}'");
            return Convert.ToUInt32(_input.Substring(start, _pos - start), 16);
        }

        private string ParseOperator()
        {
            SkipWhitespace();

            if (_pos + 1 < _input.Length)
            {
                var two = _input.Substring(_pos, 2);
                if (two is "==" or "!=" or "<=" or ">=")
                {
                    _pos += 2;
                    return two;
                }
            }

            if (_pos < _input.Length && (_input[_pos] == '<' || _input[_pos] == '>'))
            {
                return _input[_pos++].ToString();
            }

            throw new FormatException($"Expected comparison operator at position {_pos} in '{_input}'");
        }

        // Returns true and advances past token if it matches next non-whitespace content
        private bool TryConsumeToken(string token)
        {
            SkipWhitespace();
            if (_pos + token.Length <= _input.Length
                && _input.Substring(_pos, token.Length) == token)
            {
                _pos += token.Length;
                return true;
            }
            return false;
        }

        private void Consume(char c)
        {
            if (_pos >= _input.Length || _input[_pos] != c)
                throw new FormatException($"Expected '{c}' at position {_pos} in '{_input}'");
            _pos++;
        }

        private char Peek() => _pos < _input.Length ? _input[_pos] : '\0';

        private void SkipWhitespace()
        {
            while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
                _pos++;
        }

        private static bool IsIdentStart(char c) => char.IsLetter(c);
        private static bool IsHexDigit(char c) =>
            (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
    }
}
