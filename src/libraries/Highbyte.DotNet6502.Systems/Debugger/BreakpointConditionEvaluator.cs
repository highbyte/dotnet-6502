namespace Highbyte.DotNet6502.Systems.Debugger;

/// <summary>
/// Evaluates 6502 breakpoint condition expressions against live CPU and memory state.
///
/// Supported syntax:
///   Registers:   A, X, Y, SP, PC          (case-insensitive)
///   Flags:       C, Z, N, V, I, D, B      (Carry/Zero/Negative/Overflow/IRQ/Decimal/Break, as 0/1)
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
    {
        if (string.IsNullOrWhiteSpace(condition))
            return true;

        try
        {
            var evaluator = new Evaluator(condition.Trim(), cpu, memory);
            return evaluator.ParseOrExpr();
        }
        catch
        {
            return true;  // fail-safe: always stop on error
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

        public Evaluator(string input, CPU cpu, Memory memory)
        {
            _input = input;
            _cpu = cpu;
            _memory = memory;
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
        private int ParseOperand()
        {
            SkipWhitespace();

            if (Peek() == '[')
                return ParseMemoryRef();

            if (IsIdentStart(Peek()))
                return ParseIdentifier();

            return ParseNumber();
        }

        // [$addr]  or  [$addr + reg]
        private int ParseMemoryRef()
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
        private int ParseIdentifier()
        {
            SkipWhitespace();
            int start = _pos;
            while (_pos < _input.Length && (char.IsLetterOrDigit(_input[_pos]) || _input[_pos] == '_'))
                _pos++;
            var name = _input.Substring(start, _pos - start).ToUpperInvariant();

            return name switch
            {
                // 16-bit register — widened to int
                "PC" => (int)_cpu.PC,

                // 8-bit registers — widened to int (unsigned)
                "A"  => (int)_cpu.A,
                "X"  => (int)_cpu.X,
                "Y"  => (int)_cpu.Y,
                "SP" => (int)_cpu.SP,

                // Status flags — 0 or 1
                "C"  => _cpu.ProcessorStatus.Carry           ? 1 : 0,
                "Z"  => _cpu.ProcessorStatus.Zero            ? 1 : 0,
                "N"  => _cpu.ProcessorStatus.Negative        ? 1 : 0,
                "V"  => _cpu.ProcessorStatus.Overflow        ? 1 : 0,
                "I"  => _cpu.ProcessorStatus.InterruptDisable ? 1 : 0,
                "D"  => _cpu.ProcessorStatus.Decimal         ? 1 : 0,
                "B"  => _cpu.ProcessorStatus.Break           ? 1 : 0,

                _    => throw new FormatException($"Unknown register or flag '{name}'")
            };
        }

        // Numeric literal: $hex, 0xhex, or decimal
        private int ParseNumber()
        {
            SkipWhitespace();

            if (_pos < _input.Length && _input[_pos] == '$')
            {
                _pos++; // consume '$'
                return (int)ReadHexDigits();
            }

            if (_pos + 1 < _input.Length
                && _input[_pos] == '0'
                && (_input[_pos + 1] == 'x' || _input[_pos + 1] == 'X'))
            {
                _pos += 2; // consume '0x'
                return (int)ReadHexDigits();
            }

            // Decimal
            int start = _pos;
            while (_pos < _input.Length && char.IsDigit(_input[_pos]))
                _pos++;
            if (_pos == start)
                throw new FormatException($"Expected number at position {_pos} in '{_input}'");

            return int.Parse(_input.Substring(start, _pos - start));
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
