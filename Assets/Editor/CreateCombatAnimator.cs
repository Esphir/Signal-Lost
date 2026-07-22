// Editor tool that generates the player combat animator controller.
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class CreateCombatAnimator
{
    private const string OutputPath = "Assets/Animations/Controllers/PlayerCombat.controller";

    [MenuItem("Tools/Signal Lost/Create Combat Animator Controller")]
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OutputPath) != null)
            AssetDatabase.DeleteAsset(OutputPath);

        AnimationClip idle      = FindClip("HumanM@Idle01");
        AnimationClip runFwd    = FindClip("HumanM@Run01_Forward");
        AnimationClip combatIdle = FindClip("HumanM@CombatIdle1H01");
        AnimationClip attackR   = FindClip("HumanM@Attack1H01_R");
        AnimationClip attackL   = FindClip("HumanM@Attack1H01_L");

        var controller = AnimatorController.CreateAnimatorControllerAtPath(OutputPath);

        controller.AddParameter(new AnimatorControllerParameter {
            name = "Speed", type = AnimatorControllerParameterType.Float, defaultFloat = 0f });
        controller.AddParameter(new AnimatorControllerParameter {
            name = "AttackR", type = AnimatorControllerParameterType.Trigger });
        controller.AddParameter(new AnimatorControllerParameter {
            name = "AttackL", type = AnimatorControllerParameterType.Trigger });
        controller.AddParameter(new AnimatorControllerParameter {
            name = "HeavyAttack", type = AnimatorControllerParameterType.Trigger });
        controller.AddParameter(new AnimatorControllerParameter {
            name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

        AnimatorStateMachine smBase = controller.layers[0].stateMachine;

        var blendTree = new BlendTree {
            name = "Blend Tree", blendType = BlendTreeType.Simple1D,
            blendParameter = "Speed", useAutomaticThresholds = true
        };
        AssetDatabase.AddObjectToAsset(blendTree, controller);
        blendTree.AddChild(idle,   0f);
        blendTree.AddChild(runFwd, 1f);

        var stateLocomotion = smBase.AddState("Locomotion", new Vector3(250, 0));
        stateLocomotion.motion = blendTree;
        stateLocomotion.iKOnFeet = true;
        smBase.defaultState = stateLocomotion;

        controller.AddLayer(new AnimatorControllerLayer {
            name            = "Upper Body",
            defaultWeight   = 1f,
            blendingMode    = AnimatorLayerBlendingMode.Override,
            avatarMask      = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                "Assets/Kevin Iglesias/Human Animations/Models/Avatar Masks/Human Body Upper Mask.mask"),
            stateMachine    = new AnimatorStateMachine()
        });

        var layers = controller.layers;
        AnimatorStateMachine smUpper = layers[1].stateMachine;
        AssetDatabase.AddObjectToAsset(smUpper, controller);
        controller.layers = layers;

        var stateCombatIdle = smUpper.AddState("CombatIdle",  new Vector3(250,    0));
        var stateAttackR    = smUpper.AddState("Attack_R",    new Vector3(600, -120));
        var stateAttackL    = smUpper.AddState("Attack_L",    new Vector3(600,  120));
        var stateHeavy      = smUpper.AddState("HeavyAttack", new Vector3(600,  260));

        stateCombatIdle.motion = combatIdle;
        stateAttackR.motion    = attackR;
        stateAttackL.motion    = attackL;
        stateHeavy.motion      = attackR;
        stateHeavy.speedParameterActive = true;
        stateHeavy.speedParameter       = "AttackSpeed";

        smUpper.defaultState = stateCombatIdle;

        Trigger(stateCombatIdle, stateAttackR, "AttackR",     0.05f);
        Trigger(stateCombatIdle, stateAttackL, "AttackL",     0.05f);
        Trigger(stateCombatIdle, stateHeavy,   "HeavyAttack", 0.05f);

        var t = stateAttackR.AddTransition(stateAttackL);
        t.AddCondition(AnimatorConditionMode.If, 0, "AttackL");
        t.hasExitTime = true; t.exitTime = 0.55f; t.duration = 0.05f;

        t = stateAttackL.AddTransition(stateAttackR);
        t.AddCondition(AnimatorConditionMode.If, 0, "AttackR");
        t.hasExitTime = true; t.exitTime = 0.55f; t.duration = 0.05f;

        Exit(stateAttackR, stateCombatIdle, 0.15f);
        Exit(stateAttackL, stateCombatIdle, 0.15f);
        Exit(stateHeavy,   stateCombatIdle, 0.15f);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Signal Lost] PlayerCombat.controller created at {OutputPath}");
        Selection.activeObject = controller;
    }

    static void Trigger(AnimatorState from, AnimatorState to, string param, float fade)
    {
        var t = from.AddTransition(to);
        t.AddCondition(AnimatorConditionMode.If, 0, param);
        t.hasExitTime = false;
        t.duration    = fade;
    }

    static void Exit(AnimatorState from, AnimatorState to, float fade)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime    = 1f;
        t.duration    = fade;
    }

    static AnimationClip FindClip(string clipName)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && clip.name == clipName)
                    return clip;
        }
        Debug.LogWarning($"[Signal Lost] Clip not found: '{clipName}' — assign it manually.");
        return null;
    }
}
