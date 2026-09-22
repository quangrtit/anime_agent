using System;
using System.Collections.Generic;
using System.Linq;
using AnimeAssistant.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AnimeAssistant.Presentation
{
    /// <summary>
    /// Lightweight M2 motion layer for the authored Airi VRM. It drives visible
    /// idle/locomotion, blink, eye look and restrained ponytail follow-through while
    /// the domain state machine remains the source of lifecycle truth.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class AiriAvatarAnimator : MonoBehaviour
    {
        private readonly List<Transform> blinkParts = new List<Transform>();
        private readonly List<Transform> lookParts = new List<Transform>();
        private readonly Dictionary<Transform, Vector3> baseScales = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Vector3> basePositions = new Dictionary<Transform, Vector3>();

        private GreyboxSummonController summonController;
        private Transform leftArmPivot;
        private Transform rightArmPivot;
        private Transform leftLegPivot;
        private Transform rightLegPivot;
        private Transform ponytailPivot;
        private Transform torso;
        private Vector3 torsoBaseScale;
        private float blinkCountdown = 1.8f;
        private float blinkElapsed = -1f;

        private void Start()
        {
            summonController = FindFirstObjectByType<GreyboxSummonController>();
            var descendants = GetComponentsInChildren<Transform>(true);
            torso = Find(descendants, "Torso_Ivory");
            if (torso != null)
            {
                torsoBaseScale = torso.localScale;
            }

            leftArmPivot = CreatePivot(descendants, "AiriArmPivot_L", "Shoulder_L",
                "Shoulder_L", "UpperArm_L", "Forearm_L", "Glove_L", "Cuff_L", "Hand_L");
            rightArmPivot = CreatePivot(descendants, "AiriArmPivot_R", "Shoulder_R",
                "Shoulder_R", "UpperArm_R", "Forearm_R", "Glove_R", "Cuff_R", "Hand_R");
            leftLegPivot = CreatePivot(descendants, "AiriLegPivot_L", "Thigh_L",
                "Thigh_L", "Knee_L", "Shin_L", "BootCuff_L", "BootFoot_L", "BootGlow_L");
            rightLegPivot = CreatePivot(descendants, "AiriLegPivot_R", "Thigh_R",
                "Thigh_R", "Knee_R", "Shin_R", "BootCuff_R", "BootFoot_R", "BootGlow_R");

            var ponytailNames = descendants
                .Where(item => item.name.StartsWith("Ponytail_", StringComparison.Ordinal) ||
                               item.name == "PonytailGoldRing")
                .Select(item => item.name)
                .ToArray();
            ponytailPivot = CreatePivot(descendants, "AiriPonytailPivot", "PonytailGoldRing", ponytailNames);

            foreach (var item in descendants)
            {
                if (item.name.StartsWith("EyeWhite_", StringComparison.Ordinal) ||
                    item.name.StartsWith("EyeIris_", StringComparison.Ordinal) ||
                    item.name.StartsWith("EyePupil_", StringComparison.Ordinal) ||
                    item.name.StartsWith("EyeGoldRing_", StringComparison.Ordinal))
                {
                    blinkParts.Add(item);
                    baseScales[item] = item.localScale;
                }

                if (item.name.StartsWith("EyeIris_", StringComparison.Ordinal) ||
                    item.name.StartsWith("EyePupil_", StringComparison.Ordinal) ||
                    item.name.StartsWith("EyeGoldRing_", StringComparison.Ordinal))
                {
                    lookParts.Add(item);
                    basePositions[item] = item.localPosition;
                }
            }
        }

        private void Update()
        {
            var activeMotion = summonController != null &&
                (summonController.State == SummonState.AvatarExiting ||
                 summonController.State == SummonState.AvatarReturning ||
                 summonController.State == SummonState.AvatarEntering);

            var phase = Time.unscaledTime * (activeMotion ? 7.2f : 1.45f);
            var stride = activeMotion ? Mathf.Sin(phase) : Mathf.Sin(phase) * 0.08f;
            PoseArms(stride, activeMotion);
            PoseLegs(stride, activeMotion);
            PoseBreathing(phase);
            PosePonytail(phase, activeMotion);
            UpdateBlink(Time.unscaledDeltaTime);
            UpdateLook();
        }

        private void PoseArms(float stride, bool locomoting)
        {
            var swing = locomoting ? stride * 18f : stride * 2f;
            if (leftArmPivot != null)
            {
                leftArmPivot.localRotation = Quaternion.Euler(swing, 0f, -72f);
            }

            if (rightArmPivot != null)
            {
                rightArmPivot.localRotation = Quaternion.Euler(-swing, 0f, 72f);
            }
        }

        private void PoseLegs(float stride, bool locomoting)
        {
            var swing = locomoting ? stride * 7f : 0f;
            if (leftLegPivot != null)
            {
                leftLegPivot.localRotation = Quaternion.Euler(swing, 0f, 0f);
            }

            if (rightLegPivot != null)
            {
                rightLegPivot.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            }
        }

        private void PoseBreathing(float phase)
        {
            if (torso == null)
            {
                return;
            }

            var breath = 1f + Mathf.Sin(phase) * 0.008f;
            torso.localScale = new Vector3(torsoBaseScale.x * breath, torsoBaseScale.y, torsoBaseScale.z * breath);
        }

        private void PosePonytail(float phase, bool locomoting)
        {
            if (ponytailPivot == null)
            {
                return;
            }

            var amplitude = locomoting ? 7f : 2.2f;
            ponytailPivot.localRotation = Quaternion.Euler(Mathf.Sin(phase * 0.82f) * amplitude, 0f,
                Mathf.Sin((phase * 0.63f) + 0.8f) * amplitude * 0.55f);
        }

        private void UpdateBlink(float deltaSeconds)
        {
            blinkCountdown -= deltaSeconds;
            if (blinkElapsed < 0f && blinkCountdown <= 0f)
            {
                blinkElapsed = 0f;
                blinkCountdown = 2.6f + Mathf.Repeat(Time.unscaledTime * 0.37f, 2.1f);
            }

            var openness = 1f;
            if (blinkElapsed >= 0f)
            {
                blinkElapsed += deltaSeconds;
                var normalized = blinkElapsed / 0.16f;
                openness = 1f - Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI) * 0.94f;
                if (normalized >= 1f)
                {
                    blinkElapsed = -1f;
                    openness = 1f;
                }
            }

            foreach (var item in blinkParts)
            {
                var scale = baseScales[item];
                item.localScale = new Vector3(scale.x, scale.y * openness, scale.z);
            }
        }

        private void UpdateLook()
        {
            if (Mouse.current == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var pointer = Mouse.current.position.ReadValue();
            var offset = new Vector2(
                Mathf.Clamp((pointer.x / Screen.width) - 0.5f, -0.5f, 0.5f),
                Mathf.Clamp((pointer.y / Screen.height) - 0.5f, -0.5f, 0.5f));
            foreach (var item in lookParts)
            {
                var origin = basePositions[item];
                item.localPosition = origin + new Vector3(offset.x * 0.025f, offset.y * 0.018f, 0f);
            }
        }

        private Transform CreatePivot(Transform[] descendants, string pivotName, string anchorName, params string[] members)
        {
            var anchor = Find(descendants, anchorName);
            if (anchor == null)
            {
                return null;
            }

            var pivotObject = new GameObject(pivotName);
            var pivot = pivotObject.transform;
            pivot.SetParent(transform, true);
            pivot.position = anchor.position;
            foreach (var memberName in members)
            {
                var member = Find(descendants, memberName);
                if (member != null && member != pivot)
                {
                    member.SetParent(pivot, true);
                }
            }

            return pivot;
        }

        private static Transform Find(IEnumerable<Transform> descendants, string objectName)
        {
            return descendants.FirstOrDefault(item => item.name == objectName);
        }
    }
}
