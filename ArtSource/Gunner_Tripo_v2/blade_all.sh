#!/bin/bash
# Full re-author: bladed shouldered aim pose -> Custom clips -> place rifle -> bake Generic clips into Unity. usage: blade_all.sh '<author json>' '<place json fields>'
A=C:/Users/hongj/CapstoneProject; M=$A/Assets/Members/HJH/05.Data/Character/GunnerV2; T=$A/ArtSource/Gunner_Tripo_v2; B="/c/Program Files/Blender Foundation/Blender 4.5/blender.exe"
set -e
"$B" -b -P $T/author_rifle.py -- "$M/Mixamo" "$M/Custom" "$1" "$T/custom_blade/work.blend" 2>&1 | grep -E "VERIFY|rror|Trace"
$T/place.sh "$2"
J=""; for c in AimIdle AimRun CarryIdle CarryRun Attack PassiveShot Dash WeaponSkill Ultimate; do J="$J C_$c=$M/Custom/GunnerV2_C_$c.fbx"; done
"$B" -b -P $T/bake_generic.py -- "$M/Model/GunnerV2.fbx" "$M/Generic" $J 2>&1 | grep -E "rror|Trace|BAKED|done" | tail -12
echo ALLDONE
