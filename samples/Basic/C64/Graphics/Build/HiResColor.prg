'( � 200: � SET UP BITMAP HIRES MODE N2 � 400: � SET DEFAULT BITMAP COLORS h< � 300: � CLEAR SCREEN �d � DRAW PIXELS IN A "CHARACTER" AND SET COLOR �i BK�0: WHITE�1: RED�2: CYAN�3: VIOL�4 �j GREEN�5: BLUE�6: YELLOW�7 	n COL�19: ROW�11: FG�WHITE: BG�RED: � 500 ;	o COL�20: ROW�11: FG�YELLOW: BG�GREEN: � 500 g	p COL�21: ROW�11: FG�VIOL: BG�BLUE: � 500 �	x COL�20: ROW�12: FG�WHITE: BG�BK: � 500 �	� COL�19: ROW�13: FG�WHITE: BG�RED: � 500 �	� COL�20: ROW�13: FG�YELLOW: BG�GREEN: � 500 
� COL�21: ROW�13: FG�VIOL: BG�BLUE: � 500 <
� � 190: � LET IT STAY ON SCREEN Z
� � SETUP BITMAP HIRES MODE u
� � SET UP HI-RES SCREEN �
� BASE�2�4096 �
� �53272,�(53272)�8:� PUT BIT MAP AT 8192 �
� �53265,�(53265)�32:� ENTER BIT MAP MODE �
� � ,� CLEAR SCREEN SUBROUTINE -1A$�"":S1��(51):S2��(52):� 51,64:� 52,63 G6�T�1�125:A$�A$��(0):� [@�51,S1:�52,S2:� s�� SET BITMAP COLORS ��� SET COLOR MAP (WHICH IS SAME AREA AS TEXT SCREEN IN TEXT MODE) ��A$�"" ��� I�1 � 37 ��A$�A$�"C" ��� ��� �(19); �� I�1 � 27 
��A$; �� =��2023,�(2022) : � BOTTOM RIGHT CHARACTER C�� t�� DRAW PIXELS IN A "CHARACTER" AND SET COLOR ��� INPUT: COL, ROW, FG, BG ��� 1. DRAW PIXELS �OFF � ROW � (40 � 8) � COL � 8 ��CH � BASE � OFF  �� CH � 0, 128 � CH � 1, 64 � CH � 2, 32 $� CH � 3, 16 4 � CH � 4, 8 D!� CH � 5, 4 T"� CH � 6, 2 d#� CH � 7, 1 �X� 2. SET COLOR FOR SAME "CHARACTER" �lOFF � ROW � 40 � COL �vCHCOL � 1024 � OFF : � ASSUME SCREEN RAM HAS NOT MOVED FROM DEFAULT �� CHCOL, (BG � (16 � FG)) ��   