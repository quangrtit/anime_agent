using AnimeAssistant.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AnimeAssistant.Presentation
{
    [DisallowMultipleComponent]
    public sealed class GreyboxSummonController : MonoBehaviour
    {
        private const float DoorOpeningDuration = 0.85f;
        private const float AvatarExitDuration = 2.0f;
        private const float AvatarReturnDuration = 2.0f;
        private const float AvatarEnterDuration = 1.35f;
        private const float DoorClosingDuration = 0.75f;
        private const float PortalLightIntensity = 1.8f;

        [Header("Greybox references")]
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private Transform doorPivot;
        [SerializeField] private Transform secondaryDoorPivot;
        [SerializeField] private Transform handle;
        [SerializeField] private Transform avatar;
        [SerializeField] private Light portalLight;
        [SerializeField] private Collider doorClickZone;

        [Header("Blocking positions")]
        [SerializeField] private Vector3 avatarHiddenPosition = new Vector3(1.7f, 1.2f, 0.55f);
        [SerializeField] private Vector3 avatarThresholdPosition = new Vector3(1.2f, 1.2f, -0.4f);
        [SerializeField] private Vector3 avatarActivePosition = new Vector3(-1.15f, 1.2f, -0.75f);

        private readonly Quaternion doorClosed = Quaternion.identity;
        private readonly Quaternion doorOpen = Quaternion.Euler(0f, -105f, 0f);
        private readonly Quaternion secondaryDoorOpen = Quaternion.Euler(0f, 105f, 0f);
        private readonly Quaternion handleRest = Quaternion.identity;
        private readonly Quaternion handleTurned = Quaternion.Euler(0f, 0f, -42f);

        private SummonOrchestrator orchestrator;
        private Vector3 activeMotionOffset;
        private Quaternion activeMotionRotation = Quaternion.identity;
        private Vector3 avatarBaseScale = Vector3.one;
        private float activeMotionScale = 1f;
        private Vector3 returnStartPosition;
        private Quaternion returnStartRotation = Quaternion.identity;
        private GameObject doorArt;
        private GameObject recallButton;
        private Transform recallButtonCap;
        private Collider recallButtonCollider;
        private Material recallButtonMaterial;
        private Material recallButtonBaseMaterial;
        private Material recallButtonRimMaterial;
        private Material recallButtonArcMaterial;
        private float lastUserDoorClickTime = float.NegativeInfinity;
        private bool completedFirstCycle;
        private string lastEvent = "Ready — click the portal door or press Space";

        public SummonState State => orchestrator?.State ?? SummonState.DoorClosed;
        public bool PendingReturn => orchestrator != null && orchestrator.PendingReturn;
        public float ElapsedInState => orchestrator?.ElapsedInState ?? 0f;
        public Vector3 AvatarActivePosition => avatarActivePosition;
        public bool IsDoorVisible => doorArt != null && doorArt.activeInHierarchy;
        public bool IsRecallButtonVisible => recallButton != null && recallButton.activeInHierarchy;
        public bool IsAvatarVisible => HasVisibleAvatarRenderer();

        /// <summary>
        /// Reanchors the serialized choreography after the Windows work area and
        /// actual render aspect are known. Door-relative points move together,
        /// while the outside resting point is selected independently on screen.
        /// </summary>
        public void ConfigureDesktopLayout(Vector3 hiddenPosition, Vector3 thresholdPosition,
            Vector3 activePosition)
        {
            avatarHiddenPosition = hiddenPosition;
            avatarThresholdPosition = thresholdPosition;
            avatarActivePosition = activePosition;
            if (avatar != null && activeMotionScale == 1f)
            {
                avatarBaseScale = avatar.localScale;
            }
            if (orchestrator != null && orchestrator.State == SummonState.DoorClosed)
            {
                ApplyClosedPose();
            }
        }

        public void Configure(
            Camera camera,
            Transform configuredDoorPivot,
            Transform configuredHandle,
            Transform configuredAvatar,
            Light configuredPortalLight,
            Collider configuredDoorClickZone,
            Vector3 hiddenPosition,
            Vector3 thresholdPosition,
            Vector3 activePosition)
        {
            interactionCamera = camera;
            doorPivot = configuredDoorPivot;
            handle = configuredHandle;
            avatar = configuredAvatar;
            portalLight = configuredPortalLight;
            doorClickZone = configuredDoorClickZone;
            avatarHiddenPosition = hiddenPosition;
            avatarThresholdPosition = thresholdPosition;
            avatarActivePosition = activePosition;
        }

        public void ConfigureSecondaryDoor(Transform configuredSecondaryDoorPivot)
        {
            secondaryDoorPivot = configuredSecondaryDoorPivot;
        }

        public void SetActiveMotionOffset(Vector3 offset)
        {
            activeMotionOffset = Vector3.ClampMagnitude(offset, 4.5f);
        }

        public void SetActiveMotionYaw(float yawDegrees)
        {
            activeMotionRotation = Quaternion.Euler(0f, Mathf.Clamp(yawDegrees, -180f, 180f), 0f);
        }

        /// <summary>
        /// Extra scale multiplier for scripted moments (shrinking onto a desktop
        /// icon). 1 restores the composed size.
        /// </summary>
        public void SetActiveMotionScale(float multiplier)
        {
            activeMotionScale = Mathf.Clamp(multiplier, 0.02f, 1f);
        }

        private void Awake()
        {
            CreateRecallButton();
            orchestrator = new SummonOrchestrator();
            orchestrator.Transitioned += OnTransitioned;
            ApplyClosedPose();
            SetFrameRateFor(SummonState.DoorClosed);
        }

        private void OnDestroy()
        {
            if (orchestrator != null)
            {
                orchestrator.Transitioned -= OnTransitioned;
            }

            if (recallButtonMaterial != null)
            {
                Destroy(recallButtonMaterial);
            }
            if (recallButtonBaseMaterial != null)
            {
                Destroy(recallButtonBaseMaterial);
            }
            if (recallButtonRimMaterial != null)
            {
                Destroy(recallButtonRimMaterial);
            }
            if (recallButtonArcMaterial != null)
            {
                Destroy(recallButtonArcMaterial);
            }
        }

        private void Update()
        {
            ReadInput();
            AdvanceSimulation(Time.unscaledDeltaTime);
        }

        public void AdvanceSimulation(float deltaSeconds)
        {
            orchestrator.Tick(deltaSeconds);
            UpdatePresentation();
        }

        public void SimulateDoorClick()
        {
            if (orchestrator.OnDoorClicked())
            {
                DesktopAudioController.Instance?.PlayButtonPress();
            }
        }

        public void HandleUserDoorClick()
        {
            // A transparent layered window can expose the same physical click to
            // both Win32 polling and Unity input during a style transition.
            if (Time.unscaledTime - lastUserDoorClickTime < 0.25f)
            {
                return;
            }

            // Runtime clicks during travel are ignored instead of being queued.
            // This keeps one physical click from summoning and immediately
            // recalling the avatar as the interactive bounds change shape.
            if (State == SummonState.DoorOpening || State == SummonState.AvatarExiting ||
                (State == SummonState.AvatarActive && ElapsedInState < 0.75f))
            {
                return;
            }

            lastUserDoorClickTime = Time.unscaledTime;
            SimulateDoorClick();
        }

        private void ReadInput()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                HandleUserDoorClick();
                return;
            }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // Poll the native mouse in WindowsDesktopOverlay so the first click
            // cannot be lost while the transparent window style is changing.
            return;
#else
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame || interactionCamera == null)
            {
                return;
            }

            var ray = interactionCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out var hit, 100f) &&
                (hit.collider == doorClickZone || hit.collider == recallButtonCollider ||
                 hit.transform.IsChildOf(doorClickZone.transform) ||
                 (recallButton != null && hit.transform.IsChildOf(recallButton.transform))))
            {
                HandleUserDoorClick();
            }
#endif
        }

        private void UpdatePresentation()
        {
            var elapsed = orchestrator.ElapsedInState;
            switch (orchestrator.State)
            {
                case SummonState.DoorClosed:
                    ApplyClosedPose();
                    break;

                case SummonState.DoorOpening:
                {
                    SetAvatarPresentation(false);
                    var t = Mathf.Clamp01(elapsed / DoorOpeningDuration);
                    var eased = Smooth(t);
                    handle.localRotation = Quaternion.Slerp(handleRest, handleTurned, Smooth(Mathf.Clamp01(t * 3f)));
                    doorPivot.localRotation = Quaternion.Slerp(doorClosed, doorOpen, eased);
                    if (secondaryDoorPivot != null)
                    {
                        secondaryDoorPivot.localRotation = Quaternion.Slerp(doorClosed, secondaryDoorOpen, eased);
                    }
                    portalLight.intensity = Mathf.Lerp(0f, PortalLightIntensity, eased);
                    if (elapsed >= DoorOpeningDuration)
                    {
                        orchestrator.Handle(SummonEvent.DoorPassageClear);
                    }
                    break;
                }

                case SummonState.AvatarExiting:
                {
                    SetAvatarPresentation(true);
                    ApplyOpenDoorPose();
                    var normalized = Mathf.Clamp01(elapsed / AvatarExitDuration);
                    const float thresholdPortion = 0.58f;
                    if (normalized < thresholdPortion)
                    {
                        var rawT = normalized / thresholdPortion;
                        var passageT = Smooth(rawT);
                        avatar.position = Vector3.Lerp(avatarHiddenPosition, avatarThresholdPosition, passageT) +
                                          Vector3.up * SkipHop(rawT, 2f, 0.018f);
                        avatar.rotation = MovementFacing(avatarHiddenPosition, avatarThresholdPosition);
                    }
                    else
                    {
                        var rawT = (normalized - thresholdPortion) / (1f - thresholdPortion);
                        var approachT = Smooth(rawT);
                        var passageFacing = MovementFacing(avatarHiddenPosition, avatarThresholdPosition);
                        var travelFacing = MovementFacing(avatarThresholdPosition, avatarActivePosition);
                        avatar.position = Vector3.Lerp(avatarThresholdPosition, avatarActivePosition, approachT) +
                                          Vector3.up * SkipHop(rawT, 2f, 0.022f);
                        avatar.rotation = rawT < 0.72f
                            ? Quaternion.Slerp(passageFacing, travelFacing, Smooth(Mathf.Clamp01(rawT / 0.72f)))
                            : Quaternion.Slerp(travelFacing, Quaternion.identity,
                                Smooth(Mathf.Clamp01((rawT - 0.72f) / 0.28f)));
                    }
                    if (elapsed >= AvatarExitDuration)
                    {
                        orchestrator.Handle(SummonEvent.AvatarExitCompleted);
                    }
                    break;
                }

                case SummonState.AvatarActive:
                    SetAvatarPresentation(true);
                    avatar.position = avatarActivePosition + activeMotionOffset;
                    avatar.rotation = activeMotionRotation;
                    avatar.localScale = avatarBaseScale * activeMotionScale;
                    UpdateDoorCollapse(elapsed);
                    break;

                case SummonState.AvatarReturning:
                {
                    SetAvatarPresentation(true);
                    ApplyOpenDoorPose();
                    const float turnPortion = 0.22f;
                    var normalized = Mathf.Clamp01(elapsed / AvatarReturnDuration);
                    var outwardFacing = returnStartRotation;
                    var returnFacing = MovementFacing(returnStartPosition, avatarThresholdPosition);
                    if (normalized < turnPortion)
                    {
                        var turnT = Smooth(normalized / turnPortion);
                        avatar.position = returnStartPosition + Vector3.up * (Mathf.Sin(turnT * Mathf.PI) * 0.035f);
                        avatar.rotation = Quaternion.Slerp(outwardFacing, returnFacing, turnT);
                    }
                    else
                    {
                        var rawT = (normalized - turnPortion) / (1f - turnPortion);
                        var travelT = Smooth(rawT);
                        var curved = Vector3.Lerp(returnStartPosition, avatarThresholdPosition, travelT);
                        curved.z -= Mathf.Sin(travelT * Mathf.PI) * 0.14f;
                        avatar.position = curved + Vector3.up * SkipHop(rawT, 3f, 0.02f);
                        avatar.rotation = returnFacing;
                    }
                    if (elapsed >= AvatarReturnDuration)
                    {
                        orchestrator.Handle(SummonEvent.AvatarReadyToEnter);
                    }
                    break;
                }

                case SummonState.AvatarEntering:
                {
                    SetAvatarPresentation(true);
                    ApplyOpenDoorPose();
                    var rawT = Mathf.Clamp01(elapsed / AvatarEnterDuration);
                    var t = Smooth(rawT);
                    var returnFacing = MovementFacing(avatarActivePosition, avatarThresholdPosition);
                    var inwardFacing = MovementFacing(avatarThresholdPosition, avatarHiddenPosition);
                    avatar.position = Vector3.Lerp(avatarThresholdPosition, avatarHiddenPosition, t) +
                                      Vector3.up * SkipHop(rawT, 2f, 0.016f);
                    avatar.rotation = Quaternion.Slerp(returnFacing, inwardFacing, Smooth(Mathf.Clamp01(rawT * 1.8f)));
                    if (elapsed >= AvatarEnterDuration)
                    {
                        orchestrator.Handle(SummonEvent.AvatarFullyOccluded);
                    }
                    break;
                }

                case SummonState.DoorClosing:
                {
                    SetAvatarPresentation(false);
                    avatar.position = avatarHiddenPosition;
                    var t = Smooth(Mathf.Clamp01(elapsed / DoorClosingDuration));
                    doorPivot.localRotation = Quaternion.Slerp(doorOpen, doorClosed, t);
                    if (secondaryDoorPivot != null)
                    {
                        secondaryDoorPivot.localRotation = Quaternion.Slerp(secondaryDoorOpen, doorClosed, t);
                    }
                    handle.localRotation = Quaternion.Slerp(handleTurned, handleRest, t);
                    portalLight.intensity = Mathf.Lerp(PortalLightIntensity, 0f, t);
                    if (elapsed >= DoorClosingDuration)
                    {
                        orchestrator.Handle(SummonEvent.DoorCloseCompleted);
                    }
                    break;
                }

                case SummonState.Faulted:
                case SummonState.Suspended:
                    ApplyClosedPose();
                    break;
            }
        }

        private void ApplyClosedPose()
        {
            SetDoorPresentation(!completedFirstCycle);
            if (completedFirstCycle)
            {
                UpdateRecallButtonAnimation();
            }
            SetAvatarPresentation(false);
            activeMotionOffset = Vector3.zero;
            activeMotionRotation = Quaternion.identity;
            activeMotionScale = 1f;
            if (avatar != null)
            {
                avatar.localScale = avatarBaseScale;
            }
            doorPivot.localRotation = doorClosed;
            if (secondaryDoorPivot != null)
            {
                secondaryDoorPivot.localRotation = doorClosed;
            }
            handle.localRotation = handleRest;
            avatar.position = avatarHiddenPosition;
            portalLight.intensity = 0f;
        }

        private void SetAvatarPresentation(bool visible)
        {
            if (avatar == null)
            {
                return;
            }

            foreach (var renderer in avatar.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = visible;
            }
        }

        private bool HasVisibleAvatarRenderer()
        {
            if (avatar == null)
            {
                return false;
            }

            foreach (var renderer in avatar.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }
            return false;
        }

        private void ApplyOpenDoorPose()
        {
            SetDoorPresentation(true);
            doorPivot.localRotation = doorOpen;
            if (secondaryDoorPivot != null)
            {
                secondaryDoorPivot.localRotation = secondaryDoorOpen;
            }
            handle.localRotation = handleTurned;
            portalLight.intensity = PortalLightIntensity;
        }

        private void UpdateDoorCollapse(float elapsed)
        {
            var collapseDuration = DesktopExperienceConfig.Current.doorCollapseSeconds;
            if (elapsed < collapseDuration)
            {
                SetDoorPresentation(true);
                var t = Smooth(Mathf.Clamp01(elapsed / collapseDuration));
                doorPivot.localRotation = Quaternion.Slerp(doorOpen, doorClosed, t);
                if (secondaryDoorPivot != null)
                {
                    secondaryDoorPivot.localRotation = Quaternion.Slerp(secondaryDoorOpen, doorClosed, t);
                }
                handle.localRotation = Quaternion.Slerp(handleTurned, handleRest, t);
                portalLight.intensity = Mathf.Lerp(PortalLightIntensity, 0f, t);
                return;
            }

            doorPivot.localRotation = doorClosed;
            if (secondaryDoorPivot != null)
            {
                secondaryDoorPivot.localRotation = doorClosed;
            }
            handle.localRotation = handleRest;
            portalLight.intensity = 0f;
            SetDoorPresentation(false);
            UpdateRecallButtonAnimation();
        }

        private void SetDoorPresentation(bool showDoor)
        {
            if (doorArt != null && doorArt.activeSelf != showDoor)
            {
                doorArt.SetActive(showDoor);
            }
            if (doorClickZone != null && doorClickZone.gameObject.activeSelf != showDoor)
            {
                doorClickZone.gameObject.SetActive(showDoor);
            }
            if (recallButton != null && recallButton.activeSelf == showDoor)
            {
                recallButton.SetActive(!showDoor);
            }
        }

        private void CreateRecallButton()
        {
            doorArt = GameObject.Find("CastleDoorArt");
            if (doorClickZone == null || recallButton != null)
            {
                return;
            }

            recallButton = new GameObject("DoorRecallButton");
            recallButton.transform.SetParent(doorClickZone.transform.parent, false);
            recallButton.transform.localPosition = new Vector3(1.75f, 0.34f, -0.54f);
            recallButton.transform.localRotation = Quaternion.identity;
            var size = DesktopExperienceConfig.Current.recallButtonScale;
            recallButton.transform.localScale = Vector3.one;

            recallButtonBaseMaterial = CreateButtonMaterial("Recall Base",
                new Color(0.035f, 0.045f, 0.065f, 1f), 0.88f, 0.42f, false);
            recallButtonRimMaterial = CreateButtonMaterial("Recall Rim",
                new Color(0.34f, 0.38f, 0.44f, 1f), 0.95f, 0.72f, false);
            recallButtonMaterial = CreateButtonMaterial("Recall Red Switch",
                new Color(0.82f, 0.018f, 0.025f, 1f), 0.42f, 0.83f, true);
            recallButtonArcMaterial = CreateButtonMaterial("Recall Electric Arc",
                new Color(1f, 0.40f, 0.025f, 1f), 0f, 0.25f, true);

            CreateRecallPrimitive("Elliptical Base", PrimitiveType.Cylinder, recallButton.transform,
                new Vector3(0f, 0f, 0.055f), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(size * 1.52f, size * 0.30f, size * 1.03f), recallButtonBaseMaterial, false);
            CreateRecallPrimitive("Metal Bezel", PrimitiveType.Cylinder, recallButton.transform,
                new Vector3(0f, 0f, -0.005f), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(size * 1.27f, size * 0.25f, size * 0.82f), recallButtonRimMaterial, false);
            var cap = CreateRecallPrimitive("Red Emergency Switch", PrimitiveType.Cylinder, recallButton.transform,
                new Vector3(0f, 0f, -0.085f), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(size * 1.02f, size * 0.29f, size * 0.61f), recallButtonMaterial, true);
            recallButtonCap = cap.transform;
            recallButtonCollider = cap.GetComponent<Collider>();

            var highlight = CreateRecallPrimitive("Switch Highlight", PrimitiveType.Sphere, recallButton.transform,
                new Vector3(-size * 0.25f, size * 0.15f, -size * 0.205f), Quaternion.identity,
                new Vector3(size * 0.24f, size * 0.10f, size * 0.045f), recallButtonArcMaterial, false);
            highlight.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            CreateElectricArc("Electric Arc Left", size, -1f, 0.12f);
            CreateElectricArc("Electric Arc Right", size, 1f, -0.08f);
            CreateElectricArc("Electric Arc Top", size, 0.1f, 0.62f);

            recallButton.SetActive(false);
        }

        private GameObject CreateRecallPrimitive(string objectName, PrimitiveType primitive,
            Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale,
            Material material, bool keepCollider)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;
            var renderer = part.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
            if (!keepCollider && part.TryGetComponent<Collider>(out var partCollider))
            {
                Destroy(partCollider);
            }
            return part;
        }

        private void CreateElectricArc(string arcName, float size, float side, float vertical)
        {
            var arcObject = new GameObject(arcName);
            arcObject.transform.SetParent(recallButton.transform, false);
            var arc = arcObject.AddComponent<LineRenderer>();
            arc.useWorldSpace = false;
            arc.loop = false;
            arc.positionCount = 5;
            arc.widthMultiplier = size * 0.055f;
            arc.numCapVertices = 2;
            arc.sharedMaterial = recallButtonArcMaterial;
            var outerX = side * size * (vertical > 0.5f ? 0.45f : 1.65f);
            var outerY = size * vertical;
            arc.SetPositions(new[]
            {
                new Vector3(side * size * 0.98f, size * vertical * 0.42f, -size * 0.25f),
                new Vector3(side * size * 1.18f, outerY + size * 0.10f, -size * 0.24f),
                new Vector3(side * size * 1.30f, outerY - size * 0.04f, -size * 0.23f),
                new Vector3(outerX, outerY + size * 0.15f, -size * 0.22f),
                new Vector3(side * size * (vertical > 0.5f ? 0.58f : 1.82f),
                    outerY + size * 0.22f, -size * 0.21f)
            });
        }

        private static Material CreateButtonMaterial(string materialName, Color color,
            float metallic, float smoothness, bool emissive)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader) { name = materialName };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            material.color = color;
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.2f);
            }
            return material;
        }

        private void UpdateRecallButtonAnimation()
        {
            if (recallButton == null || !recallButton.activeSelf)
            {
                return;
            }

            AlignRecallButtonToCamera();
            var pulse = 1f + Mathf.Sin(Time.unscaledTime * 4.2f) * 0.045f;
            recallButton.transform.localScale = new Vector3(pulse, pulse, 1f);
            if (recallButtonCap != null)
            {
                var press = Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 2.1f)) * 0.008f;
                var capPosition = recallButtonCap.localPosition;
                capPosition.z = -0.085f + press;
                recallButtonCap.localPosition = capPosition;
            }
            if (recallButtonArcMaterial != null)
            {
                var flicker = 1.55f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9.5f)) * 1.35f;
                var arcColor = new Color(1f, 0.32f, 0.015f, 1f) * flicker;
                if (recallButtonArcMaterial.HasProperty("_EmissionColor"))
                {
                    recallButtonArcMaterial.SetColor("_EmissionColor", arcColor);
                }
            }
        }

        private void AlignRecallButtonToCamera()
        {
            if (interactionCamera == null)
            {
                return;
            }

            // The visible face of the procedural switch is local -Z. Keep that
            // face aimed at the viewer independently of the rotated door parent.
            var awayFromCamera = recallButton.transform.position - interactionCamera.transform.position;
            if (awayFromCamera.sqrMagnitude > 0.0001f)
            {
                recallButton.transform.rotation = Quaternion.LookRotation(
                    awayFromCamera.normalized, interactionCamera.transform.up);
            }
        }

        private void OnTransitioned(SummonTransition transition)
        {
            lastEvent = $"{transition.From} → {transition.To} ({transition.Reason})";
            if (transition.To == SummonState.DoorOpening)
            {
                SetDoorPresentation(true);
            }
            if (transition.To == SummonState.AvatarReturning)
            {
                SetDoorPresentation(true);
                returnStartPosition = avatar.position;
                returnStartRotation = avatar.rotation;
                activeMotionOffset = Vector3.zero;
                activeMotionRotation = Quaternion.identity;
            }
            if (transition.To == SummonState.DoorClosed && transition.From == SummonState.DoorClosing)
            {
                completedFirstCycle = true;
                SetDoorPresentation(false);
            }
            Debug.Log($"[SummonCycle] {lastEvent}; door={IsDoorVisible}; recallButton={IsRecallButtonVisible}");
            SetFrameRateFor(transition.To);
        }

        private static void SetFrameRateFor(SummonState state)
        {
            Application.targetFrameRate = state == SummonState.DoorClosed ? 15 : 60;
        }

        private static float Smooth(float value)
        {
            return value * value * (3f - (2f * value));
        }

        private static Quaternion MovementFacing(Vector3 from, Vector3 to)
        {
            var direction = to - from;
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f
                ? Quaternion.identity
                : Quaternion.LookRotation(-direction.normalized, Vector3.up);
        }

        private static float SkipHop(float normalized, float hopCount, float height)
        {
            var arc = Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI * hopCount));
            return arc * arc * height;
        }

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var panel = new Rect(18f, 18f, 420f, 88f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(32f, 28f, 390f, 24f), "ANIME DESKTOP ASSISTANT — M1 GREYBOX");
            GUI.Label(new Rect(32f, 50f, 390f, 22f),
                $"State: {State}  |  Pending: {PendingReturn}  |  Timeout: {orchestrator.TimeoutRemaining:0.0}s");
            GUI.Label(new Rect(32f, 70f, 390f, 22f), lastEvent);
#endif
        }
    }
}
