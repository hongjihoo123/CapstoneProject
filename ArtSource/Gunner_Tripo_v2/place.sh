#!/bin/bash
# usage: place.sh '<extra json fields, e.g. "off_right":0.035>'   (re-places the rifle on the Gunner v2 model, with the feedback style pass)
A=C:/Users/hongj/CapstoneProject; M=$A/Assets/Members/HJH/05.Data/Character/GunnerV2; T=$A/ArtSource/Gunner_Tripo_v2
"/c/Program Files/Blender Foundation/Blender 4.5/blender.exe" -b -P $T/gun_place.py -- "$M/Mixamo/GunnerV2_TPose.fbx" "${AIM:-$M/Custom/GunnerV2_C_AimIdle.fbx}" "${OUTFBX:-$M/Model/GunnerV2.fbx}" "$T/rifle/rifle_clean.glb" "{\"muzzle\":1,\"keep\":1,$1,\"style\":[\"$T/backup_before_rifle/GunnerV2_BaseColor.jpg\",\"$M/Textures/GunnerV2_BaseColor.jpg\"]}" 2>&1 | grep -E "hand gap|exported|rror|Trace"
W=$(dirname "${OUTFBX:-$M/Model/GunnerV2.fbx}")/GunnerV2_work.blend; [ -f "$W" ] && mv -f "$W" $T/rifle/ ; rm -f $M/Model/GunnerV2_work.blend.meta
