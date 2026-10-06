DeepVain book inventory (worn leather + aged parchment). Reference size 640 x 360, all positions are top-left based (Unity: anchor top-left, Pos X = x, Pos Y = -y).
Import every PNG: Sprite (2D and UI), Single, Pixels Per Unit 1, Point (no filter), Compression None. Book_Base.png is RGB, 640x360.

9-SLICE BORDERS (Sprite Editor): Popup_Frame 4, Panel_Inset 3, Button_Brass 4, Button_Leather 4, Button_Brass_Hover 4, Button_Leather_Hover 4. Divider and Popup_Arrow: none.

STATIC (already in Book_Base.png): desk, cover, pages, binding, header lines + diamonds, hotkey strap, gold plate (with coin), bookmark.
TEXT (TextMeshPro): 'EQUIPMENT' centred at x=166,y=32 ; 'BACKPACK' centred at x=474,y=32 ; 'HOTKEYS' left at x=348,y=272 ; gold amount right-aligned ending x=438,y=327 ; slots '11 / 24' right-aligned ending x=606,y=327.

EQUIPMENT PAPERDOLL (36x36 slots, Slot_Empty + Glyph_* inside, 30x30 icon centred at +3,+3):
  helmet 148,60   talisman L 28,120   talisman R 268,120
  chest 28,164    sword 268,164
  pants 28,208    shield 268,208
  boots 148,260   gloves 268,252
FIGURE: Front_Idle_Sword_01..08 (36x52) shown at 108x156 (x3) at x=112,y=117 ; 8 frames, 150 ms each.
Defence row: text x=48 (vertical centre y=314), icon at (32, 309), value right-aligned ending x=304. Attack row: same, 14 px lower (y=328).
BOOKMARK: Book/Ribbon.png (19x48) at x=574,y=16, a separate Image ABOVE the grid slots.

BACKPACK GRID: 7 columns x 5 rows, first slot at x=342,y=54, pitch 38 (slot 36 + 2). 24 slots unlocked, the rest use Slot_Locked.
COUNT TEXT: bottom-right of the slot (x+33, y+31 baseline), white with dark outline, bold.
HOTKEYS: 4 slots at x=402+50*k, y=270 (k=0..3) ; KeyChip 12x12 at (slot x-3, slot y-4) with the key letter Q E R F.

POPUP (inspect): Popup_Frame sliced, width 196, height = 64 + 14*stats + 70. Placed under the selected slot: x = clamp(slot x - 70, 330, 422), y = slot y + 46. Popup_Arrow (13x9) at (slot x + 12, popup y - 8).
  inside: icon slot (Slot_Empty) at +10,+12 ; name bold at +52,+15 ; type +52,+27 ; rarity +52,+38 ; Divider at +10,+54 (width 176) ;
  stats rows from y+64 step 14 (icon at +10, label at +26, value right-aligned at +186) ; Divider ; description 3 lines (width 168) ; 3 buttons at y = height-28, each (width-28)/3 wide, 17 high, gap 4, first = Button_Brass.
Text colours: ink #46281A, dim #8A6440, title #4A2814, rarity: Common #6A5238, Uncommon #2A8A40, Rare #2A5AB8, Epic #8A3AB0, Legendary #B07008.
