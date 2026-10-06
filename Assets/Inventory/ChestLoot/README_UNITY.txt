LOOT-STRIPER (poppet nede til hoyre naar kista aapnes)
Testet i ekte Unity (kopi av prosjektet): bygging, 3 striper fra kista, samme ting slaas sammen (x5), og stripene forsvinner etter ca. 3 s.

1. Kopier hele ChestLoot-mappen til Assets/Inventory/  (du faar Assets/Inventory/ChestLoot/Sprites/Toast_Rare.png osv).
2. Kopier Unity/Scripts/LootToastUI.cs til Assets/Script/Inventory/ og Unity/Scripts/Editor/LootToastBuilder.cs til en Editor-mappe.
3. Erstatt Chest.cs med Unity/Chest.cs (ny: stripene dukker opp en og en, 0,25 s mellom).
4. Meny DeepVain > Loot toasts > 2 - Build in scene (kjorer 1 - Prepare sprites selv). Gjor dette i scenen med kista og spilleren.
5. Spill, aapne kista med G.

Justering: LootToastUI i Inspector - Lifetime (3 s), Fade Out Time (0,5), Gap, Max Visible (6). Chest - Loot Interval (0,25).
Fra egen kode: LootToastUI.Show(item, antall);  (gjor ingenting hvis scenen ikke har stripene)
