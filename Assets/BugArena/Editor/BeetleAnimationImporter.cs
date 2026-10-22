using System;
using UnityEditor;
using UnityEngine;

public sealed class BeetleAnimationImporter : AssetPostprocessor
{
    bool IsBeetle => assetPath == "Assets/Resources/BugArenaArt/JadeBeetle.fbx" || assetPath == "Assets/Resources/BugArenaArt/EmberBeetle.fbx";
    void OnPreprocessTexture()
    {
        if (!assetPath.EndsWith("/JadePalette.png") && !assetPath.EndsWith("/CoralPalette.png")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true; importer.alphaIsTransparency = false;
        importer.mipmapEnabled = false; importer.isReadable = true;
        importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
    }
    void OnPreprocessModel()
    {
        if (!IsBeetle) return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.optimizeGameObjects = false;
        importer.removeConstantScaleCurves = true;
        importer.animationRotationError = .1f;
        importer.animationPositionError = .1f;
        importer.animationScaleError = .1f;
        importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
    }
    void OnPreprocessAnimation()
    {
        if (!IsBeetle) return;
        var importer = (ModelImporter)assetImporter;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.name = clip.takeName.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0 ? "Walk" : "Idle";
            clip.loopTime = true;
            clip.loopPose = false;
            clip.wrapMode = WrapMode.Loop;
            clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
        }
        importer.clipAnimations = clips;
    }
}
