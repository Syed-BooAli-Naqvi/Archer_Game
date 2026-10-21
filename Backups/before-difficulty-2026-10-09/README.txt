Backup taken 2026-10-09 before the difficulty changes (power scaling, early waves, boss charge + minions).

QUICK OFF SWITCH (no code changes):
  Select Assets/Resources/DifficultySettings.asset in Unity and untick "Enabled".
  The game then plays exactly like before.

FULL REVERSE:
  Copy EVERY file under Assets/ in this folder back over the project's copy (same paths):
    Scripts/Enemy Scripts/  EnemySpawner.cs EnemyHealth.cs EnemyAI.cs BossAI.cs BossHealth.cs
    Scripts/Player Scripts/ ArcherPlayerController.cs
    Scripts/BA/             MainMenuUIHandler.cs SoundData.asset (coin sound volume)
    3D Data/Characters/Animator/ Boss.controller Enemy.controller
  then delete the added files listed in step 2 below.

(Older step list, kept for reference:)
  1. Copy the 4 files in Assets/Scripts/Enemy Scripts/ from this folder back over
     the ones in the project (EnemySpawner.cs, EnemyHealth.cs, BossAI.cs, BossHealth.cs).
     Also copy "Assets/3D Data/Characters/Animator/Boss.controller" back (only change:
     Sword Attack -> Idle transition got "Has Exit Time" so the boss stops freezing in its attack pose).
  2. Delete these files added by the change:
       Assets/Scripts/Difficulty/            (folder + .meta)
       Assets/Resources/DifficultySettings.asset (+ .meta)
       Assets/Resources/ (folder + .meta, if empty)
No scenes or prefabs were modified. Animator changes: Boss.controller (Sword Attack -> Idle gets exit time),
Enemy.controller (new Idle state on the base layer, using Animations/Idle.anim).
Visual fixes (enemy idle while attacking, steady health bars, merged coin popups, player run/bow anim in
boss fight, no warnings from hidden animators) are in those same files.

NOTE (added later the same day): the project folders were reorganised. Files in this backup use the OLD
paths (e.g. Assets/Scripts/Enemy Scripts/EnemyAI.cs is now Assets/_Game/Scripts/Enemies/EnemyAI.cs).
To restore an old file, copy it over the file with the same name in its new folder under Assets/_Game/.
A complete snapshot from just before the reorganisation is in Backups/before-reorganize-2026-10-09/.
