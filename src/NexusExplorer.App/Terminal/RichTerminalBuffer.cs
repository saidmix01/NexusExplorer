using System.Text.RegularExpressions;

namespace NexusExplorer.App.Terminal;

/// <summary>
/// A fixed-grid terminal buffer that preserves ANSI color/style attributes per cell.
/// Models a real VT100 terminal screen with scrollback.
/// </summary>
public sealed partial class RichTerminalBuffer
{
    private int _cols;
    private int _rows;
    private TerminalCell[][] _screen;
    private readonly List<TerminalCell[]> _scrollback = [];
    private const int MaxScrollback = 5000;

    private int _cursorRow;
    private int _cursorCol;
    private int _savedRow;
    private int _savedCol;

    // Current SGR state applied to new characters.
    private byte _currentFg = 255; // default
    private byte _currentBg = 255; // default
    private CellAttributes _currentAttrs = CellAttributes.None;

    public int Columns => _cols;
    public int Rows => _rows;
    public int CursorRow => _cursorRow;
    public int CursorCol => _cursorCol;
    public int ScrollbackCount => _scrollback.Count;
    public bool IsDirty { get; private set; } = true;

    [GeneratedRegex(@"\x1b\[([0-9;?]*)([A-Za-z@])")]
    private static partial Regex CsiRegex();

    [GeneratedRegex(@"\x1b\][^\x07\x1b]*(?:\x07|\x1b\\)")]
    private static partial Regex OscRegex();

    public RichTerminalBuffer(int columns = 120, int rows = 30)
    {
        _cols = Math.Max(1, columns);
        _rows = Math.Max(1, rows);
        _screen = CreateScreen(_rows, _cols);
    }

    public void MarkClean() => IsDirty = false;

    public TerminalCell[] GetScreenRow(int row)
    {
        if (row >= 0 && row < _rows)
            return _screen[row];
        return CreateRow(_cols);
    }

    public TerminalCell[] GetScrollbackRow(int index)
    {
        if (index >= 0 && index < _scrollback.Count)
            return _scrollback[index];
        return CreateRow(_cols);
    }

    public void Resize(int columns, int rows)
    {
        columns = Math.Max(1, columns);
        rows = Math.Max(1, rows);
        if (columns == _cols && rows == _rows) return;

        var newScreen = CreateScreen(rows, columns);
        for (var r = 0; r < Math.Min(rows, _rows); r++)
            for (var c = 0; c < Math.Min(columns, _cols); c++)
                newScreen[r][c] = _screen[r][c];

        _screen = newScreen;
        _cols = columns;
        _rows = rows;
        _cursorRow = Math.Clamp(_cursorRow, 0, _rows - 1);
        _cursorCol = Math.Clamp(_cursorCol, 0, _cols - 1);
        IsDirty = true;
    }

    public void Clear()
    {
        _scrollback.Clear();
        _screen = CreateScreen(_rows, _cols);
        _cursorRow = 0;
        _cursorCol = 0;
        _currentFg = 255;
        _currentBg = 255;
        _currentAttrs = CellAttributes.None;
        IsDirty = true;
    }

    /// <summary>
    /// Fired when an OSC 9;9 sequence reports a new working directory.
    /// </summary>
    public event Action<string>? WorkingDirectoryChanged;

    [GeneratedRegex(@"\x1b\]9;9;([^\x07\x1b]*?)(?:\x07|\x1b\\)")]
    private static partial Regex OscCwdRegex();

    public void Write(string rawText)
    {
        // Extract OSC 9;9 (CWD notification) before stripping OSC
        foreach (System.Text.RegularExpressions.Match match in OscCwdRegex().Matches(rawText))
        {
            var path = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(path))
                WorkingDirectoryChanged?.Invoke(path);
        }

        var text = OscRegex().Replace(rawText, string.Empty);
        var i = 0;

        while (i < text.Length)
        {
            var ch = text[i];

            if (ch == '\x1b')
            {
                if (i + 1 < text.Length && text[i + 1] == '[')
                {
                    var match = CsiRegex().Match(text, i);
                    if (match.Success && match.Index == i)
                    {
                        ProcessCsi(match.Groups[1].Value, match.Groups[2].Value[0]);
                        i += match.Length;
                        continue;
                    }
                    i += 2;
                    continue;
                }

                if (i + 1 < text.Length)
                {
                    switch (text[i + 1])
                    {
                        case '7': _savedRow = _cursorRow; _savedCol = _cursorCol; break;
                        case '8': _cursorRow = _savedRow; _cursorCol = _savedCol; break;
                        case 'M': ReverseIndex(); break;
                    }
                    i += 2;
                    continue;
                }
                i++;
                continue;
            }

            switch (ch)
            {
                case '\r': _cursorCol = 0; break;
                case '\n': LineFeed(); break;
                case '\b': if (_cursorCol > 0) _cursorCol--; break;
                case '\t':
                    var next = ((_cursorCol / 8) + 1) * 8;
                    _cursorCol = Math.Min(next, _cols - 1);
                    break;
                case '\x07': case '\0': break;
                default:
                    if (ch >= ' ') PutChar(ch);
                    break;
            }
            i++;
        }

        IsDirty = true;
    }

    /// <summary>
    /// Gets plain text content for a row range (for copy/selection).
    /// </summary>
    public string GetPlainText(int startRow, int startCol, int endRow, int endCol, bool isScrollback, int scrollOffset)
    {
        var sb = new System.Text.StringBuilder();
        var totalRows = _scrollback.Count + _rows;

        for (var absRow = startRow + scrollOffset; absRow <= endRow + scrollOffset; absRow++)
        {
            TerminalCell[] row;
            if (absRow < _scrollback.Count)
                row = _scrollback[absRow];
            else
            {
                var screenRow = absRow - _scrollback.Count;
                if (screenRow < 0 || screenRow >= _rows) continue;
                row = _screen[screenRow];
            }

            var fromCol = (absRow == startRow + scrollOffset) ? startCol : 0;
            var toCol = (absRow == endRow + scrollOffset) ? endCol : _cols - 1;
            toCol = Math.Min(toCol, row.Length - 1);

            for (var c = fromCol; c <= toCol; c++)
                sb.Append(row[c].Character == '\0' ? ' ' : row[c].Character);

            if (absRow < endRow + scrollOffset)
                sb.AppendLine();
        }

        // Trim trailing spaces per line
        return sb.ToString().TrimEnd();
    }

    private void PutChar(char c)
    {
        if (_cursorCol >= _cols)
        {
            _cursorCol = 0;
            LineFeed();
        }

        _screen[_cursorRow][_cursorCol] = new TerminalCell
        {
            Character = c,
            ForegroundIndex = _currentFg,
            BackgroundIndex = _currentBg,
            Attributes = _currentAttrs
        };
        _cursorCol++;
    }

    private void LineFeed()
    {
        if (_cursorRow < _rows - 1)
            _cursorRow++;
        else
            ScrollUp();
    }

    private void ReverseIndex()
    {
        if (_cursorRow > 0) _cursorRow--;
    }

    private void ScrollUp()
    {
        _scrollback.Add(_screen[0]);
        while (_scrollback.Count > MaxScrollback)
            _scrollback.RemoveAt(0);

        for (var r = 0; r < _rows - 1; r++)
            _screen[r] = _screen[r + 1];
        _screen[_rows - 1] = CreateRow(_cols);
    }

    private void ProcessCsi(string parameters, char command)
    {
        var args = ParseArgs(parameters);

        switch (command)
        {
            case 'A': _cursorRow = Math.Max(0, _cursorRow - GetArg(args, 0, 1)); break;
            case 'B': _cursorRow = Math.Min(_rows - 1, _cursorRow + GetArg(args, 0, 1)); break;
            case 'C': _cursorCol = Math.Min(_cols - 1, _cursorCol + GetArg(args, 0, 1)); break;
            case 'D': _cursorCol = Math.Max(0, _cursorCol - GetArg(args, 0, 1)); break;
            case 'E': _cursorRow = Math.Min(_rows - 1, _cursorRow + GetArg(args, 0, 1)); _cursorCol = 0; break;
            case 'F': _cursorRow = Math.Max(0, _cursorRow - GetArg(args, 0, 1)); _cursorCol = 0; break;
            case 'G': _cursorCol = Math.Clamp(GetArg(args, 0, 1) - 1, 0, _cols - 1); break;
            case 'H': case 'f':
                _cursorRow = Math.Clamp(GetArg(args, 0, 1) - 1, 0, _rows - 1);
                _cursorCol = Math.Clamp(GetArg(args, 1, 1) - 1, 0, _cols - 1);
                break;
            case 'd': _cursorRow = Math.Clamp(GetArg(args, 0, 1) - 1, 0, _rows - 1); break;
            case 'J': EraseInDisplay(GetArg(args, 0, 0)); break;
            case 'K': EraseInLine(GetArg(args, 0, 0)); break;
            case 'P': DeleteChars(GetArg(args, 0, 1)); break;
            case '@': InsertChars(GetArg(args, 0, 1)); break;
            case 'X': EraseChars(GetArg(args, 0, 1)); break;
            case 'm': ProcessSgr(args); break;
            case 's': _savedRow = _cursorRow; _savedCol = _cursorCol; break;
            case 'u': _cursorRow = _savedRow; _cursorCol = _savedCol; break;
            case 'h': case 'l': case 'r': case 'n': case 'c': case 't': break;
        }
    }

    private void ProcessSgr(int[] args)
    {
        if (args.Length == 0)
        {
            ResetSgr();
            return;
        }

        for (var i = 0; i < args.Length; i++)
        {
            var code = args[i];
            switch (code)
            {
                case 0: ResetSgr(); break;
                case 1: _currentAttrs |= CellAttributes.Bold; break;
                case 2: _currentAttrs |= CellAttributes.Dim; break;
                case 3: _currentAttrs |= CellAttributes.Italic; break;
                case 4: _currentAttrs |= CellAttributes.Underline; break;
                case 7: _currentAttrs |= CellAttributes.Inverse; break;
                case 22: _currentAttrs &= ~(CellAttributes.Bold | CellAttributes.Dim); break;
                case 23: _currentAttrs &= ~CellAttributes.Italic; break;
                case 24: _currentAttrs &= ~CellAttributes.Underline; break;
                case 27: _currentAttrs &= ~CellAttributes.Inverse; break;
                case >= 30 and <= 37: _currentFg = (byte)(code - 30); break;
                case 38:
                    // Extended color: 38;5;n or 38;2;r;g;b — skip for now, use default
                    if (i + 1 < args.Length && args[i + 1] == 5 && i + 2 < args.Length)
                    {
                        var idx = args[i + 2];
                        _currentFg = idx < 16 ? (byte)idx : (byte)255;
                        i += 2;
                    }
                    else if (i + 1 < args.Length && args[i + 1] == 2 && i + 4 < args.Length)
                    {
                        i += 4; // skip r,g,b — use default
                        _currentFg = 255;
                    }
                    break;
                case 39: _currentFg = 255; break;
                case >= 40 and <= 47: _currentBg = (byte)(code - 40); break;
                case 48:
                    if (i + 1 < args.Length && args[i + 1] == 5 && i + 2 < args.Length)
                    {
                        var idx = args[i + 2];
                        _currentBg = idx < 16 ? (byte)idx : (byte)255;
                        i += 2;
                    }
                    else if (i + 1 < args.Length && args[i + 1] == 2 && i + 4 < args.Length)
                    {
                        i += 4;
                        _currentBg = 255;
                    }
                    break;
                case 49: _currentBg = 255; break;
                case >= 90 and <= 97: _currentFg = (byte)(code - 90 + 8); break;
                case >= 100 and <= 107: _currentBg = (byte)(code - 100 + 8); break;
            }
        }
    }

    private void ResetSgr()
    {
        _currentFg = 255;
        _currentBg = 255;
        _currentAttrs = CellAttributes.None;
    }

    private void EraseInDisplay(int mode)
    {
        switch (mode)
        {
            case 0:
                EraseInLine(0);
                for (var r = _cursorRow + 1; r < _rows; r++) ClearRow(r);
                break;
            case 1:
                for (var r = 0; r < _cursorRow; r++) ClearRow(r);
                EraseInLine(1);
                break;
            case 2: case 3:
                for (var r = 0; r < _rows; r++) ClearRow(r);
                break;
        }
    }

    private void EraseInLine(int mode)
    {
        var row = _screen[_cursorRow];
        switch (mode)
        {
            case 0: for (var c = _cursorCol; c < _cols; c++) row[c] = TerminalCell.Empty; break;
            case 1: for (var c = 0; c <= _cursorCol && c < _cols; c++) row[c] = TerminalCell.Empty; break;
            case 2: for (var c = 0; c < _cols; c++) row[c] = TerminalCell.Empty; break;
        }
    }

    private void DeleteChars(int count)
    {
        var row = _screen[_cursorRow];
        count = Math.Min(count, _cols - _cursorCol);
        for (var c = _cursorCol; c < _cols; c++)
            row[c] = (c + count < _cols) ? row[c + count] : TerminalCell.Empty;
    }

    private void InsertChars(int count)
    {
        var row = _screen[_cursorRow];
        count = Math.Min(count, _cols - _cursorCol);
        for (var c = _cols - 1; c >= _cursorCol + count; c--)
            row[c] = row[c - count];
        for (var c = _cursorCol; c < _cursorCol + count && c < _cols; c++)
            row[c] = TerminalCell.Empty;
    }

    private void EraseChars(int count)
    {
        var row = _screen[_cursorRow];
        for (var c = _cursorCol; c < Math.Min(_cursorCol + count, _cols); c++)
            row[c] = TerminalCell.Empty;
    }

    private void ClearRow(int r)
    {
        for (var c = 0; c < _cols; c++)
            _screen[r][c] = TerminalCell.Empty;
    }

    private static TerminalCell[][] CreateScreen(int rows, int cols)
    {
        var screen = new TerminalCell[rows][];
        for (var r = 0; r < rows; r++)
            screen[r] = CreateRow(cols);
        return screen;
    }

    private static TerminalCell[] CreateRow(int cols)
    {
        var row = new TerminalCell[cols];
        for (var c = 0; c < cols; c++)
            row[c] = TerminalCell.Empty;
        return row;
    }

    private static int[] ParseArgs(string parameters)
    {
        var clean = parameters.TrimStart('?');
        if (string.IsNullOrEmpty(clean)) return [];
        return clean.Split(';', StringSplitOptions.None)
            .Select(s => int.TryParse(s, out var v) ? v : 0)
            .ToArray();
    }

    private static int GetArg(int[] args, int index, int defaultValue)
    {
        if (index >= args.Length) return defaultValue;
        return args[index] > 0 ? args[index] : defaultValue;
    }
}
