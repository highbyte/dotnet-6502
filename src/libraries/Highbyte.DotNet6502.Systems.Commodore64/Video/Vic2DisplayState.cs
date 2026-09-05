namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// The VIC-II's vertical state as it stands for one raster line: whether the line is drawn in
/// display state (character/bitmap rows) or idle state, where in the video matrix the row starts,
/// which line of the row it is, and whether the vertical border covers it.
/// </summary>
/// <param name="DisplayState">True when the VIC-II is in display state for this line: a bad line
/// has switched it on and the current row is not finished. False is idle state, in which the
/// graphics sequencer shows the byte at the end of the VIC-II bank in black over the background
/// colour, without touching the video matrix or colour RAM.</param>
/// <param name="VerticalBorder">True while the vertical border flip-flop is set: the border colour
/// covers the whole line, graphics and sprites included.</param>
/// <param name="VideoCounterBase">VC at the start of the line: the video matrix offset of the row
/// being drawn (0, 40, 80 ...). Ten bits, as on the chip.</param>
/// <param name="RowCounter">RC: which of the row's eight lines this is, 0 to 7.</param>
public readonly record struct Vic2LineDisplayState(bool DisplayState, bool VerticalBorder, ushort VideoCounterBase, byte RowCounter);
