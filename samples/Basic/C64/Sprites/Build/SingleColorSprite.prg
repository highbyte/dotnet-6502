
  Ç(147) : CLEAR SCREEN P  53280,1 : CHANGE THE BORDER COLOR TO WHITE   53281,6 : CHANGE THE BACKGROUND COLOR TO BLUE µ  CHANGE TEXT COLOR TO ÙELLOW WITH CHR$(14) á  Ç(158) : CHANGE TEXT COLOR TO YELLOW ð  "":  ""  	  "            ***************            " P	  "            * SPRITE DEMO *            " 	  "            ***************            "  ´	  POKE 53269,128+64 :REM ENABLE SPRITE 6 AND 7 Þ	  S²0 ¤ 7 : SET SPRITE PTRS TO 12288 ï	   2040ªS,192 õ	"   
*  53293,2 : SET SPRITE 6 COLOR TO RED M
+  53294,5 : SET SPRITE 7 COLOR TO GREEN |
-  53271,128 : SET SPRITE 7 DOUBLE HEIGHT  ª
/  53277,128 : SET SPRITE 7 DOUBLE WIDTH  Í
2  N²0 ¤ 62 : LOAD SPRITE DATA Õ
<  Q å
F 12288ªN, Q ë
P  S  53269,128ª64 : ENABLE SPRITE 6 AND 7 >U  Z²1 ¤ 200 : MOVE SPRITES AROUND [  53260, ((Z¬2))ª80 ¯ 255 : SET SPRITE 6 HORIZONTAL POSITION À[  53261, ((Z­2)ª40) ¯ 255 : SET SPRITE 6 VERTICAL POSITION ó\  53262, Z : SET SPRITE 7 HORIZONTAL POSITION $]  53263, Z : SET SPRITE 7 VERTICAL POSITION *b  3c  85 Ed  255,255,255 Sn  0,126,0 cx  1,129,128 p  2,0,64 ~  12,0,48   8,0,16    19,197,200 ©ª  16,0,8 ¹´  160,195,5 É¾  160,195,5 ØÈ  160,24,5 çÒ  160,24,5 öÜ  160,24,5 æ  16,126,8 ð  17,60,136 $ú  8,129,16 3 8,126,16 @ 4,0,32 M 2,0,64 ]" 1,129,128 o, 255,255,255  SHOW SPRITE 7 AT TOP/LEFT ¬ 53262, 24:  53263, 50   