;------------------------------------------------------------
;Data
;------------------------------------------------------------
.rodata	;Read-only data section

SCROLLER_TEXT_COLOR:
	;.byte $0b,$0b,$0b,$0b,$0c,$0c,$0c,$0c,$0f,$0f,$01,$01,$0f,$0f,$0c,$0c,$0c,$0c,$0c,$0c,$0c
	;.byte $02,$02,$02,$02,$04,$04,$04,$04,$0a,$0a,$0a,$0a,$07,$07,$07,$07,$0a,$0a,$0a,$0a,$04,$04,$04,$04,$02,$02,$02,$02
	.byte $02,$02,$02,$02,$0a,$0a,$0a,$0a,$07,$07,$07,$07,$0a,$0a,$0a,$0a,$02,$02,$02,$02
	.byte $06,$06,$06,$06,$0e,$0e,$0e,$0e,$01,$01,$01,$01,$0e,$0e,$0e,$0e,$06,$06,$06,$06
	.byte $05,$05,$05,$05,$0d,$0d,$0d,$0d,$01,$01,$01,$01,$0d,$0d,$0d,$0d,$05,$05,$05,$05
	.byte $ff ;End of color indicator (cannot be 0 which is black)

SCROLL_TEXT:
	;scrcode "                                                                                "
	scrcode "                                        "
	scrcode "highbyte, in 2026, proudly presents... a dotnet 6502 cpu emulator!    "
	scrcode "the raster bars and smooth scroller are written in 6502 machine code for c64.   "
	;scrcode "hold space to flash border color.   "
	scrcode "greetings to all my demo-scene friends from back in the late 80s & early 90s in the groups them and virtual!"
	;scrcode "                                                                                "
	scrcode "                                        "
	.byte 0 ;End of text indicator

rasterLength = 9

rasterColorTable:
;.byte 09,08,12,13,01,13,12,08,09
;.byte ColorDarkGrey, ColorBlue, ColorLightBlue, ColorLightGrey, ColorWhite, ColorLightGrey, ColorLightBlue, ColorBlue, ColorDarkGrey
;.byte ColorDarkGrey, ColorGreen, ColorLightGreen, ColorLightGrey, ColorWhite, ColorLightGrey, ColorLightGreen, ColorGreen, ColorDarkGrey
;.byte ColorDarkGrey, ColorRed, ColorLightRed, ColorLightGrey, ColorWhite, ColorLightGrey, ColorLightRed, ColorRed, ColorDarkGrey
.byte ColorBrown, ColorRed, ColorLightRed, ColorOrange, ColorYellow, ColorOrange, ColorLightRed, ColorRed, ColorBrown

rasterDelayTable:
.byte 08,08,09,08,12,08,08,08,09

rasterSinusTable:
.byte 140,143,145,148,151,154,156,159,162,164,167,169,172,175,177,180,182,185,187,190
.byte 192,194,197,199,201,204,206,208,210,212,214,216,218,220,222,224,225,227,229,230
.byte 232,233,235,236,237,238,240,241,242,243,244,245,245,246,247,247,248,248,249,249
.byte 250,250,250,250,250,250,250,250,249,249,249,248,248,247,247,246,245,244,243,242
.byte 241,240,239,238,237,235,234,232,231,229,228,226,224,223,221,219,217,215,213,211
.byte 209,207,205,202,200,198,196,193,191,188,186,183,181,178,176,173,171,168,166,163
.byte 160,158,155,152,149,147,144,141,139,136,133,131,128,125,122,120,117,114,112,109
.byte 107,104,102,99,97,94,92,89,87,84,82,80,78,75,73,71,69,67,65,63
.byte 61,59,57,56,54,52,51,49,48,46,45,43,42,41,40,39,38,37,36,35
.byte 34,33,33,32,32,31,31,31,30,30,30,30,30,30,30,30,31,31,32,32
.byte 33,33,34,35,35,36,37,38,39,40,42,43,44,45,47,48,50,51,53,55
.byte 56,58,60,62,64,66,68,70,72,74,76,79,81,83,86,88,90,93,95,98
.byte 100,103,105,108,111,113,116,118,121,124,126,129,132,135,137,140

PREVIOUS_RASTER_LINE:
	.byte 0
