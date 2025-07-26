# Basic commands to manipulate CIA #2 Port A ($DD00) ATN, CLOCK, DATA output lines.
? peek(56576)

rem set atn low
poke 56576,peek(56576)or8

rem set atn released
poke 56576,peek(56576)and247



? peek(56576)

rem set clk low
poke 56576,peek(56576)or16

rem set clk released
poke 56576,peek(56576)and239


# Basic commands for testing file IO
open 15,8,15,"save:test.prg"
open 1,8,1
print#1,"hello"
close 1

rem byte sent by: open 15,8,15,"i"
rem ATN low
rem Byte received: $28 (40)  -> command: Listen ($20) device 8
rem Byte received: $FF (255) -> command: Open ($F0) channel 15
rem ATN high
rem Byte received: $49 (73) -> data: "i" (Initialize) - EOI
rem ATN low
rem Byte received: $3F (63) -> command: Unlisten all devices
rem ATN high
open 15,8,15,"i"


rem ATN low
rem Byte received: $28 (40) -> command: Listen ($20) device 8
rem Byte received: $F0 (240)-> command: Open ($F0) channel 0
rem ATN high
rem Byte received: $24 (36) -> data: "$" (Directory) - EOI
rem ATN low
rem Byte received: $3F (63) -> command: Unlisten all devices
rem Byte received: $48 (72) -> command: Talk device 8
rem Byte received: $60 (96) -> command: Reopen channel 0
rem ATN high
rem Drive is now Talker, C64 is listener. Drive sends program bytes.
rem $5F (95) ATN low -> command: Unlink all devices
rem $28 (40) ATN low -> command: Listen device 8
rem $E0 (224) ATN low -> command: Close channel 0
rem $3F (63) ATN low -> command: Unlisten all devices
load "$",8

# Interesting locations in Kernal IEC bus code

Addr | Comment
---------------
ED0C | LISTN: Start of Send listen command
ed62 | LISTN: Start send 8 bits
edac | LISTN: End send byte

EDB9 | SECND: Start of Send secondary address command
edbe | SECND (SCATN): Release ATN.
EDC6 | SECND: End Send secondary address 

f3ed | After call to SECND
f3fc | Load next Data byte
f3fe | Call subroutine to send data byte (CIOUT)

eddd | CIOUT: Start of send data byte

f654 | CUNLSN: Start of send Unlisten command. Calls UNLSN.

edfe | UNLSN: Start of "real" send Unlisten command.
              Sets EOI flag?

ed40 | ISOUR: "send last character" (will set EOI flag)
               Release DATA.
               If DATA is high (means DATA input line is set which is reversed to 	       actual state.) THEN DEVICE ERROR!
ed49 | ISOUR: Set Clock high to trigger "Ready to Send" phase.
ed4c | ISOUR: Check EOI flag, if last character.
ed50 | ISOUR: Do the EOI.  Wait for Data to go High.
ed55 | ISOUR: Data is now High: Wait for Data to go Low (must be set by listener if it doesn't get Clock set low within 200 Microseconds. 
              If this isn't handled by the listener it will be in a forever loop here...


edd9 | TKATN1 | After Talk and Channel command sent. Set Data low. Set Clock High. Waiting for Clock Low
ee13 | ACPTR | Input byte from serial bus start. Set Clock high. Wait for Clock High.
ee20 | ACPTR | Done waiting for clock high.
ee2a | ACPTR | Sets Data High
ee37 | ACPTR | Done waiting for timer
ee3c | ACPTR | Done waiting for clock low.

ee56 | ACPTR | Start reading 8 bits
ee63 | ACPTR | Wait for reading 1 bit
ee65 | ACPTR | Bit ready to read
ee72 | ACPTR | Done waiting for Clock to go low 
ee76 | ACPTR | All 8 bits received.


ee44 | ACP00B | EOI (Timeout) detected. Last byte has been sent. Release ATN, Set Clock High, Set Data High

f504 | | Back after byte received or timeout. Status in $90, value $42.
