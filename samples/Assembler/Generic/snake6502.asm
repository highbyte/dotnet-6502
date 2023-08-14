; Modified version of the Snake game from http://skilldrick.github.io/easy6502/#snake
; to be able to 
; - Compile with ACME assembler
; - Run in the DotNet6502 emulator.

* = $c000
blankCharachter = 32  ; 32 = C64 font space
snakeCharacter = 160  ; 160 = C64 font inverted space
appleCharacter = 64   ; 64 = C64 font  @ sign
screenMem = 0x0200    ; Start address of screen memory
screenCols = 32       ; Note: Cannot change cols without modifying code
screenRows = 32       ; Note: Cannot change rows without modifying code

;Byte with status flags to communicate with emulator host. When host new frame, emulator done for frame, etc.
SCREEN_REFRESH_STATUS = 0xd000


;  ___           _        __ ___  __ ___
; / __|_ _  __ _| |_____ / /| __|/  \_  )
; \__ \ ' \/ _` | / / -_) _ \__ \ () / /
; |___/_||_\__,_|_\_\___\___/___/\__/___|

; Change direction: W A S D

appleL         = $00 ; screen location of apple, low byte
appleH         = $01 ; screen location of apple, high byte
snakeHeadL     = $10 ; screen location of snake head, low byte
snakeHeadH     = $11 ; screen location of snake head, high byte
snakeBodyStart = $12 ; start of snake body byte pairs
snakeDirection = $02 ; direction (possible values are below)
snakeLength    = $03 ; snake length, in bytes

; Directions (each using a separate bit)
movingUp      = 1
movingRight   = 2
movingDown    = 4
movingLeft    = 8

; ASCII values of keys controlling the snake
ASCII_w      = $77
ASCII_a      = $61
ASCII_s      = $73
ASCII_d      = $64

; System variables
sysRandom    = 0xd41b
sysLastKey   = 0xd031

Start:
  jsr init
  jsr loop

init:
  jsr ClearScreen
  jsr initSnake
  jsr generateApplePosition
  rts


initSnake:
  lda #movingRight  ;start direction
  sta snakeDirection

  lda #4  ;start length (2 segments)
  sta snakeLength
  
  lda #$11
  sta snakeHeadL
  
  lda #$10
  sta snakeBodyStart
  
  lda #$0f
  sta $14 ; body segment 1
  
  lda #$04
  sta snakeHeadH
  sta $13 ; body segment 1
  sta $15 ; body segment 2
  rts


generateApplePosition:
  ;load a new random byte into $00
  lda sysRandom
  sta appleL

  ;load a new random number from 2 to 5 into $01
  lda sysRandom
  and #$03 ;mask out lowest 2 bits
  clc
  ;adc #2    ;Highbyte of video memory start address (2 = 0x0200)
  adc #>screenMem    ;Highbyte of video memory start address (2 = 0x0200)
  sta appleH

  rts


loop:
;Wait for new frame (flag set by emulator host)
	jsr waitforrefresh

  jsr readKeys
  jsr checkCollision
  jsr updateSnake
  jsr drawApple
  jsr drawSnake

  ldx #5 ;wait for x frames
  jsr spinWheels

  jmp loop


readKeys:
  lda sysLastKey
  cmp #ASCII_w
  beq upKey
  cmp #ASCII_d
  beq rightKey
  cmp #ASCII_s
  beq downKey
  cmp #ASCII_a
  beq leftKey
  rts
upKey:
  lda #movingDown
  bit snakeDirection
  bne illegalMove

  lda #movingUp
  sta snakeDirection
  rts
rightKey:
  lda #movingLeft
  bit snakeDirection
  bne illegalMove

  lda #movingRight
  sta snakeDirection
  rts
downKey:
  lda #movingUp
  bit snakeDirection
  bne illegalMove

  lda #movingDown
  sta snakeDirection
  rts
leftKey:
  lda #movingRight
  bit snakeDirection
  bne illegalMove

  lda #movingLeft
  sta snakeDirection
  rts
illegalMove:
  rts


checkCollision:
  jsr checkAppleCollision
  jsr checkSnakeCollision
  rts


checkAppleCollision:
  lda appleL
  cmp snakeHeadL
  bne doneCheckingAppleCollision
  lda appleH
  cmp snakeHeadH
  bne doneCheckingAppleCollision

  ;eat apple
  inc snakeLength
  inc snakeLength ;increase length
  jsr generateApplePosition
doneCheckingAppleCollision:
  rts


checkSnakeCollision:
  ldx #2 ;start with second segment
snakeCollisionLoop:
  lda snakeHeadL,x
  cmp snakeHeadL
  bne continueCollisionLoop

maybeCollided:
  lda snakeHeadH,x
  cmp snakeHeadH
  beq didCollide

continueCollisionLoop:
  inx
  inx
  cpx snakeLength          ;got to last section with no collision
  beq didntCollide
  jmp snakeCollisionLoop

didCollide:
  jmp gameOver
didntCollide:
  rts


updateSnake:
  ldx snakeLength
  dex
  txa
updateloop:
  lda snakeHeadL,x
  sta snakeBodyStart,x
  dex
  bpl updateloop

  lda snakeDirection
  lsr
  bcs up
  lsr
  bcs right
  lsr
  bcs down
  lsr
  bcs left
up:
  lda snakeHeadL
  sec
  sbc #screenCols ;#$20
  sta snakeHeadL
  bcc upup
  rts
upup:
  dec snakeHeadH
  ;lda #$1   ; Highbyte of screen memory when we reached top (1 = highbyte of 0x01?? = (screen start x0200 - x rows/cols) 
  lda #>(screenMem-1)
  cmp snakeHeadH
  beq collision
  rts
right:
  inc snakeHeadL
  lda #(screenCols-1)  ;#$1f
  bit snakeHeadL
  beq collision
  rts
down:
  lda snakeHeadL
  clc
  adc #screenCols ;#$20
  sta snakeHeadL
  bcs downdown
  rts
downdown:
  inc snakeHeadH
  ;lda #$6       ; Highbyte of screen memory when we reached end (6 = highbyte of 0x0600 = (screen start x0200 + screen size COLS*ROWS 0x0400)) 
  lda #>(screenMem + (screenRows * screenCols))
  cmp snakeHeadH
  beq collision
  rts
left:
  dec snakeHeadL
  lda snakeHeadL
  and #(screenRows-1) ;#$1f
  cmp #(screenRows-1) ;#$1f
  beq collision
  rts
collision:
  jmp gameOver


drawApple:
  ldy #0
  lda #appleCharacter
  sta (appleL),y
  rts


drawSnake:
  ldx snakeLength
  ;lda #0
  lda #blankCharachter
  sta (snakeHeadL,x) ; erase end of tail

  ldx #0
  ;lda #1
  lda #snakeCharacter
  sta (snakeHeadL,x) ; paint head
  rts

spinWheels:
spinloop:
  jsr waitforrefresh
  dex
  bne spinloop
  rts

ClearScreen:
	lda #blankCharachter
  ldx #screenCols ;Width
.clrScrLoop:
	dex
	sta screenMem,x
	sta screenMem + $0020,x
	sta screenMem + $0040,x
	sta screenMem + $0060,x
	sta screenMem + $0080,x
	sta screenMem + $00a0,x
	sta screenMem + $00c0,x
	sta screenMem + $00e0,x
	sta screenMem + $0100,x
	sta screenMem + $0120,x
	sta screenMem + $0140,x
	sta screenMem + $0160,x
	sta screenMem + $0180,x
	sta screenMem + $01a0,x
	sta screenMem + $01c0,x
	sta screenMem + $01e0,x
	sta screenMem + $0200,x
	sta screenMem + $0220,x
	sta screenMem + $0240,x
	sta screenMem + $0260,x
	sta screenMem + $0280,x
	sta screenMem + $02a0,x
	sta screenMem + $02c0,x
	sta screenMem + $02e0,x
	sta screenMem + $0300,x
	sta screenMem + $0320,x
	sta screenMem + $0340,x
	sta screenMem + $0360,x
	sta screenMem + $0380,x
	sta screenMem + $03a0,x
	sta screenMem + $03c0,x
	sta screenMem + $03e0,x
	bne .clrScrLoop

	rts

gameOver:
  ;brk
  ldx #120 ;wait for x frames
  jsr spinWheels
  jmp Start

;-----------------

!zone waitforrefresh
waitforrefresh:
.loop
	lda SCREEN_REFRESH_STATUS
	tax ; Store copy of current screen status in X
	and #%00000001	;Bit 0 set signals it time to refresh screen
	beq .loop	;Loop if bit 0 is not set (AND results in value 0, then zero flag set, BEQ branches zero flag is set)
	lda SCREEN_REFRESH_STATUS
	and #%11111110 ;Clear bit 0.
	sta SCREEN_REFRESH_STATUS ;Update status to memory (will acknowledge that 6502 code is done waiting for the next frame)
	rts