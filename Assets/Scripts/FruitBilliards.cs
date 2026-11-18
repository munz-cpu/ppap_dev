using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public sealed class FruitBilliards : MonoBehaviour
{
    private const float MinimumFieldHalfWidth = 6.2f;
    private const float MaxPull = 2.15f;
    private const float ShotSpeedPerUnit = 9f;
    private const float AimDistancePerPull = 3.35f;
    private const float RailThickness = 0.22f;
    private const float StopSpeed = 0.10f;
    private const float CastSeparation = 0.01f;
    private const float PenExitDuration = 0.38f;
    private const int MaxAimBounces = 4;

    [SerializeField] private Camera playCamera;
    [SerializeField] private RectTransform field;
    [SerializeField] private BoxCollider2D topRail;
    [SerializeField] private BoxCollider2D bottomRail;
    [SerializeField] private BoxCollider2D leftRail;
    [SerializeField] private BoxCollider2D rightRail;
    [SerializeField] private BoxCollider2D pen;
    [SerializeField] private BoxCollider2D replacementPen;
    [SerializeField] private BilliardFruit[] fruits;
    [FormerlySerializedAs("skewerSound")]
    [SerializeField] private AudioSource impactSound;
    [FormerlySerializedAs("appleSkewerClip")]
    [SerializeField] private AudioClip appleBounceClip;
    [FormerlySerializedAs("pineappleSkewerClip")]
    [SerializeField] private AudioClip pineappleBounceClip;
    [SerializeField] private AudioClip applePenClip;
    [FormerlySerializedAs("pineappleClip")]
    [SerializeField] private AudioClip pineapplePenClip;
    [SerializeField] private AudioClip penEntranceClip;
    [SerializeField] private AudioClip appleEntranceClip;
    [SerializeField] private AudioClip pineappleEntranceClip;
    [SerializeField] private BoxCollider2D[] walls;
    [SerializeField] private AimTrajectory aimTrajectory;
    [SerializeField] private TextMeshProUGUI scoreLabel;
    [SerializeField] private TextMeshProUGUI triesLabel;
    [SerializeField] private Button retryButton;
    [SerializeField] private GameObject stageMenu;
    [SerializeField] private GameObject firstStageButton;
    [SerializeField] private TrickShotStageCatalog stageCatalog;
    [SerializeField, Min(1)] private int triesPerRound = 5;

    [Header("Transition easing")]
    [SerializeField] private AnimationCurve penEntranceEase = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2.9f), new Keyframe(0.72f, 1.035f, 0f, 0f), new Keyframe(1f, 1f));
    [SerializeField] private AnimationCurve penExitEase = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.28f, 0.08f, 0.66f, 0.66f), new Keyframe(1f, 1f, 1.8f, 0f));
    [SerializeField] private AnimationCurve fruitEntranceEase = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2.9f), new Keyframe(0.72f, 1.045f, 0f, 0f), new Keyframe(1f, 1f));

    private readonly RaycastHit2D[] aimHits = new RaycastHit2D[12];
    private readonly Collider2D[] predictedBrokenWalls = new Collider2D[MaxAimBounces + 1];
    private readonly Vector2[] aimPath = new Vector2[MaxAimBounces + 2];
    private readonly ContactFilter2D aimFilter = new ContactFilter2D { useTriggers = false };
    private BilliardFruit aimedFruit;
    private Mouse activeMouse;
    private TouchControl activeTouch;
    private int activeTouchId;
    private Vector2 grabPoint;
    private Vector2 lastAimVelocity;
    private int skeweredCount;
    private int remainingTries;
    private Coroutine roundTransition;
    private float originalCameraSize;
    private PenPose applePenPose;
    private PenPose pineapplePenPose;
    private int selectedStage;
    private Rect playableBounds;
    private float fieldAspect;

    private readonly struct PenPose
    {
        public readonly Vector2 Position;
        public readonly Quaternion Rotation;
        public readonly Vector2 Axis;
        public readonly Vector2 Tip;

        public PenPose(Vector2 tip, float angle, BoxCollider2D collider)
        {
            Rotation = Quaternion.Euler(0f, 0f, angle);
            Axis = Rotation * Vector3.right;
            Vector2 halfShaft = Axis * (collider.size.x * 0.5f);
            Tip = tip;
            Position = tip + halfShaft - (Vector2)(Rotation * collider.offset);
        }
    }

    private PenPose ActivePenPose => skeweredCount == 0 ? applePenPose : pineapplePenPose;

    private void Awake()
    {
        if (playCamera == null || field == null || pen == null || replacementPen == null || scoreLabel == null ||
            aimTrajectory == null || triesLabel == null || retryButton == null ||
            topRail == null || bottomRail == null || leftRail == null || rightRail == null ||
            stageMenu == null || stageCatalog == null || stageCatalog.Count == 0 ||
            fruits.Length != 2 || fruits[0] == null || fruits[1] == null)
        {
            Debug.LogError("Fruit Billiards is missing its scene references.", this);
            enabled = false;
            return;
        }

        originalCameraSize = playCamera.orthographicSize;
        foreach (BilliardFruit fruit in fruits)
            fruit.Initialize(this);
    }

    public void SelectStage(int index)
    {
        if (index < 0 || index >= stageCatalog.Count)
            return;

        selectedStage = index;
        stageMenu.SetActive(false);
        gameObject.SetActive(true);
    }

    public void OpenStageSelection()
    {
        gameObject.SetActive(false);
        stageMenu.SetActive(true);
        if (EventSystem.current != null && firstStageButton != null)
            EventSystem.current.SetSelectedGameObject(firstStageButton);
    }

    private void OnEnable()
    {
        fieldAspect = 0f;
        ResizeField();
        roundTransition = StartCoroutine(StartRound());
    }

    private void Update()
    {
        if (ResizeField())
        {
            ClearAim();
            if (roundTransition == null)
                fruits[skeweredCount].KeepInField(playableBounds);
            Physics2D.SyncTransforms();
        }

        if (roundTransition != null)
            return;

        if (remainingTries == 0)
        {
            if (!retryButton.interactable && ActiveFruitIsStill())
                UpdateTries(true);
            return;
        }

        if (aimedFruit != null)
            UpdateAim();
        else if (ActiveFruitIsStill())
            TryBeginAim();
    }

    private bool ActiveFruitIsStill() => fruits[skeweredCount].Body.linearVelocity.sqrMagnitude <= StopSpeed * StopSpeed;

    private bool ResizeField()
    {
        float aspect = playCamera.aspect;
        if (aspect <= 0f || Mathf.Approximately(aspect, fieldAspect))
            return false;

        fieldAspect = aspect;
        playCamera.orthographicSize = Mathf.Max(originalCameraSize, MinimumFieldHalfWidth / aspect);
        float halfHeight = playCamera.orthographicSize;
        float halfWidth = halfHeight * aspect;
        field.sizeDelta = new Vector2(halfWidth * 2f, halfHeight * 2f);

        float railCenterX = halfWidth - RailThickness * 0.5f;
        float railCenterY = halfHeight - RailThickness * 0.5f;
        Vector2 horizontalSize = new Vector2(halfWidth * 2f, RailThickness);
        Vector2 verticalSize = new Vector2(RailThickness, halfHeight * 2f);
        ResizeRail(topRail, Vector2.up * railCenterY, horizontalSize);
        ResizeRail(bottomRail, Vector2.down * railCenterY, horizontalSize);
        ResizeRail(leftRail, Vector2.left * railCenterX, verticalSize);
        ResizeRail(rightRail, Vector2.right * railCenterX, verticalSize);

        Vector2 center = field.position;
        float innerWidth = halfWidth - RailThickness;
        float innerHeight = halfHeight - RailThickness;
        playableBounds = Rect.MinMaxRect(center.x - innerWidth, center.y - innerHeight,
            center.x + innerWidth, center.y + innerHeight);
        return true;
    }

    private static void ResizeRail(BoxCollider2D rail, Vector2 center, Vector2 size)
    {
        var rect = (RectTransform)rail.transform;
        rect.anchoredPosition = center;
        rect.sizeDelta = size;
        rail.size = size;
    }

    internal void HandleFruitContact(BilliardFruit fruit, Collider2D hit, ContactPoint2D contact, Vector2 incomingVelocity)
    {
        if (roundTransition != null || fruit != fruits[skeweredCount])
            return;

        bool isApple = skeweredCount == 0;
        if (!HitsPenTip(hit, contact.point, contact.normal, incomingVelocity))
        {
            PlayImpact(isApple ? appleBounceClip : pineappleBounceClip);
            if (hit.TryGetComponent<StageWallVisual>(out var wall))
                wall.BreakOnImpact();
            return;
        }

        ClearAim();
        Vector2 outward = Vector2.Dot(contact.normal, ActivePenPose.Axis) < 0f ? contact.normal : -contact.normal;
        Vector2 stoppedAt = contact.point + outward * fruit.Circle.radius;
        fruit.StopAtPenTip(stoppedAt);

        PlayImpact(isApple ? applePenClip : pineapplePenClip, interrupt: true);
        roundTransition = StartCoroutine(CompleteSkewer(fruit, stoppedAt));
    }

    private void PlayImpact(AudioClip clip, bool interrupt = false)
    {
        if (impactSound == null || clip == null)
            return;

        if (interrupt)
            impactSound.Stop();
        impactSound.PlayOneShot(clip);
    }

    private IEnumerator CompleteSkewer(BilliardFruit fruit, Vector2 stoppedAt)
    {
        skeweredCount++;
        UpdateScore();
        if (skeweredCount == fruits.Length)
        {
            yield return new WaitForSeconds(1.5f);
            roundTransition = null;
            OpenStageSelection();
            yield break;
        }
        else
        {
            float hold = applePenClip != null && impactSound != null && impactSound.isPlaying
                ? Mathf.Max(0.30f, applePenClip.length - PenExitDuration)
                : 0.30f;
            yield return new WaitForSeconds(hold);
            yield return SwapPens(fruit, stoppedAt);
        }

        roundTransition = null;
    }

    private IEnumerator SwapPens(BilliardFruit apple, Vector2 stoppedAt)
    {
        pen.enabled = false;
        PenPose oldPen = applePenPose;
        float fruitExtent = Vector2.Distance(stoppedAt, oldPen.Position) + apple.Circle.radius;
        Vector2 slide = OffscreenOffset(oldPen.Position, Mathf.Max(pen.size.x * 0.5f, fruitExtent));
        yield return Animate(PenExitDuration, penExitEase, progress =>
        {
            Vector2 offset = slide * progress;
            pen.transform.position = oldPen.Position + offset;
            apple.transform.position = stoppedAt + offset;
        });

        pen.gameObject.SetActive(false);
        apple.gameObject.SetActive(false);
        while (impactSound != null && impactSound.isPlaying)
            yield return null;

        ShowWalls(stageCatalog[selectedStage].pineapple);
        yield return PresentPen(replacementPen, pineapplePenPose, fruits[1], pineappleEntranceClip);
    }

    private IEnumerator PresentPen(BoxCollider2D target, PenPose pose, BilliardFruit fruit, AudioClip fruitEntranceClip)
    {
        Vector2 offscreen = OffscreenOffset(pose.Position, target.size.magnitude * 0.5f);
        target.transform.SetPositionAndRotation(pose.Position + offscreen, pose.Rotation);
        target.gameObject.SetActive(true);
        PlayImpact(penEntranceClip, interrupt: true);
        yield return Animate(0.55f, penEntranceEase, progress =>
            target.transform.position = pose.Position + offscreen * (1f - progress));

        while (penEntranceClip != null && impactSound != null && impactSound.isPlaying)
            yield return null;

        Vector2 spawn = fruit.StartPosition;
        Vector2 fruitOffscreen = spawn + OffscreenOffset(spawn, ((RectTransform)fruit.transform).rect.height);
        fruit.Body.simulated = false;
        fruit.transform.position = fruitOffscreen;
        fruit.gameObject.SetActive(true);
        PlayImpact(fruitEntranceClip);
        yield return Animate(0.43f, fruitEntranceEase, progress =>
            fruit.transform.position = Vector2.LerpUnclamped(fruitOffscreen, spawn, progress));

        target.enabled = true;
        fruit.Body.position = spawn;
        fruit.Body.rotation = fruit.transform.eulerAngles.z;
        fruit.Body.simulated = true;
        Physics2D.SyncTransforms();
    }

    private Vector2 OffscreenOffset(Vector2 position, float extent)
    {
        float left = position.x - playableBounds.xMin;
        float right = playableBounds.xMax - position.x;
        float bottom = position.y - playableBounds.yMin;
        float top = playableBounds.yMax - position.y;
        float nearestX = Mathf.Min(left, right);
        float nearestY = Mathf.Min(bottom, top);
        float padding = extent + 0.35f;
        if (nearestX <= nearestY)
            return (left <= right ? Vector2.left : Vector2.right) * (nearestX + padding);
        return (bottom <= top ? Vector2.down : Vector2.up) * (nearestY + padding);
    }

    private static IEnumerator Animate(float duration, AnimationCurve easing, Action<float> apply)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            apply(easing.Evaluate(elapsed / duration));
            yield return null;
        }
        apply(1f);
    }

    private bool HitsPenTip(Collider2D hit, Vector2 point, Vector2 normal, Vector2 incomingVelocity)
    {
        PenPose pose = ActivePenPose;
        return hit == (skeweredCount == 0 ? pen : replacementPen) &&
            Mathf.Abs(Vector2.Dot(point - pose.Tip, pose.Axis)) <= 0.08f &&
            Mathf.Abs(Vector2.Dot(normal, pose.Axis)) >= 0.7f && Vector2.Dot(incomingVelocity, pose.Axis) >= 0.3f;
    }

    private IEnumerator StartRound()
    {
        TrickShotStageCatalog.Stage stage = stageCatalog[selectedStage];
        ClearAim();
        pen.enabled = false;
        pen.gameObject.SetActive(false);
        replacementPen.enabled = false;
        replacementPen.gameObject.SetActive(false);
        foreach (BilliardFruit fruit in fruits)
        {
            fruit.gameObject.SetActive(false);
            fruit.ResetShot(stage.fruitSpawn);
        }

        skeweredCount = 0;
        remainingTries = triesPerRound;
        UpdateScore();
        UpdateTries(false);
        applePenPose = new PenPose(stage.apple.penTip, stage.apple.penAngle, pen);
        pineapplePenPose = new PenPose(stage.pineapple.penTip, stage.pineapple.penAngle, replacementPen);
        ShowWalls(stage.apple);
        Physics2D.SyncTransforms();
        yield return PresentPen(pen, applePenPose, fruits[0], appleEntranceClip);
        roundTransition = null;
    }

    private void UpdateScore() => scoreLabel.text = skeweredCount == fruits.Length ? "PPAP!" : $"{skeweredCount} / {fruits.Length}";

    private void UpdateTries(bool canRetry)
    {
        retryButton.interactable = canRetry;
        triesLabel.text = canRetry ? "Tries 0   Retry" : $"Tries {remainingTries}";
    }

    public void RetryRound()
    {
        if (roundTransition == null && remainingTries == 0 && ActiveFruitIsStill())
            roundTransition = StartCoroutine(StartRound());
    }

    private void ShowWalls(TrickShotStageCatalog.Shot shot)
    {
        for (int i = 0; i < walls.Length; i++)
        {
            BoxCollider2D wall = walls[i];
            if (i >= shot.walls.Length)
            {
                wall.gameObject.SetActive(false);
                continue;
            }

            TrickShotStageCatalog.Wall placement = shot.walls[i];
            var rect = (RectTransform)wall.transform;
            rect.SetPositionAndRotation(placement.center, Quaternion.identity);
            rect.sizeDelta = placement.size;
            wall.size = placement.size;
            wall.GetComponent<StageWallVisual>().ResetWall(wall, placement.breakable);
            wall.gameObject.SetActive(true);
        }
    }

    private void TryBeginAim()
    {
        if (Touchscreen.current is Touchscreen screen)
        {
            foreach (TouchControl touch in screen.touches)
            {
                if (touch.press.wasPressedThisFrame && touch.press.isPressed && SelectFruit(touch.position.ReadValue()))
                {
                    activeTouch = touch;
                    activeTouchId = touch.touchId.ReadValue();
                    return;
                }
            }
        }

        if (Mouse.current is Mouse mouse && mouse.leftButton.wasPressedThisFrame && mouse.leftButton.isPressed &&
            SelectFruit(mouse.position.ReadValue()))
            activeMouse = mouse;
    }

    private bool SelectFruit(Vector2 screenPosition)
    {
        BilliardFruit fruit = fruits[skeweredCount];
        Vector2 point = ToWorld(screenPosition);
        if (Vector2.Distance(point, fruit.Body.position) > fruit.Circle.radius + 0.16f)
            return false;

        grabPoint = point;
        aimedFruit = fruit;
        return true;
    }

    private void UpdateAim()
    {
        Vector2 screenPosition;
        bool released;
        if (activeTouch != null)
        {
            TouchPhase phase = activeTouch.phase.ReadValue();
            if (!activeTouch.device.added || activeTouch.touchId.ReadValue() != activeTouchId ||
                phase == TouchPhase.Canceled || phase == TouchPhase.None)
            {
                ClearAim();
                return;
            }

            screenPosition = activeTouch.position.ReadValue();
            released = phase == TouchPhase.Ended;
        }
        else
        {
            if (activeMouse == null || !activeMouse.added)
            {
                ClearAim();
                return;
            }

            screenPosition = activeMouse.position.ReadValue();
            released = !activeMouse.leftButton.isPressed;
        }

        ResolveAim(ToWorld(screenPosition), released);
    }

    private void ResolveAim(Vector2 pointer, bool released)
    {
        Vector2 pull = Vector2.ClampMagnitude(grabPoint - pointer, MaxPull);
        Vector2 shotVelocity = pull.sqrMagnitude > 0.01f ? pull * ShotSpeedPerUnit : Vector2.zero;
        if (released)
        {
            if (remainingTries > 0 && shotVelocity != Vector2.zero)
            {
                aimedFruit.Shoot(shotVelocity);
                remainingTries--;
                UpdateTries(false);
            }
            ClearAim();
            return;
        }

        if (shotVelocity != lastAimVelocity)
        {
            DrawAim(shotVelocity);
            lastAimVelocity = shotVelocity;
        }
    }

    private void DrawAim(Vector2 velocity)
    {
        Vector2 origin = aimedFruit.Body.position;
        aimPath[0] = origin;
        int pointCount = 1;
        int predictedBrokenCount = 0;
        float damping = Mathf.Max(aimedFruit.Body.linearDamping, 0.05f);
        float radius = aimedFruit.Circle.radius;
        float distanceLeft = velocity.magnitude / ShotSpeedPerUnit * AimDistancePerPull;

        for (int bounce = 0; bounce <= MaxAimBounces; bounce++)
        {
            float speed = velocity.magnitude;
            if (speed <= StopSpeed || distanceLeft <= CastSeparation)
                break;

            Vector2 direction = velocity / speed;
            float range = Mathf.Min(distanceLeft, (speed - StopSpeed) / damping);
            RaycastHit2D hit = FindAimHit(origin, radius, direction, range, predictedBrokenCount);
            if (hit.collider == null)
            {
                aimPath[pointCount++] = origin + direction * range;
                break;
            }

            aimPath[pointCount++] = hit.centroid;
            distanceLeft -= hit.distance + CastSeparation;
            velocity = direction * (speed - hit.distance * damping);
            if (hit.rigidbody != null || HitsPenTip(hit.collider, hit.point, hit.normal, velocity))
                break;

            if (hit.collider.TryGetComponent<StageWallVisual>(out var wall) && wall.CanBreak)
                predictedBrokenWalls[predictedBrokenCount++] = hit.collider;

            float bounceStrength = hit.collider.sharedMaterial != null ? hit.collider.sharedMaterial.bounciness : 0f;
            velocity -= (1f + bounceStrength) * Vector2.Dot(velocity, hit.normal) * hit.normal;
            origin = hit.centroid + hit.normal * CastSeparation;
        }

        aimTrajectory.SetPath(aimPath, pointCount);
    }

    private RaycastHit2D FindAimHit(Vector2 origin, float radius, Vector2 direction, float distance, int predictedBrokenCount)
    {
        int count = Physics2D.CircleCast(origin, radius, direction, aimFilter, aimHits, distance);
        RaycastHit2D nearest = default;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = aimHits[i];
            if (hit.collider == aimedFruit.Circle || Vector2.Dot(direction, hit.normal) >= -0.001f ||
                Array.IndexOf(predictedBrokenWalls, hit.collider, 0, predictedBrokenCount) >= 0)
                continue;

            if (nearest.collider == null || hit.distance < nearest.distance)
                nearest = hit;
        }

        return nearest;
    }

    private Vector2 ToWorld(Vector2 point) => playCamera.ScreenToWorldPoint(new Vector3(point.x, point.y, -playCamera.transform.position.z));

    private void ClearAim()
    {
        aimedFruit = null;
        activeTouch = null;
        activeMouse = null;
        lastAimVelocity = Vector2.zero;
        if (aimTrajectory != null)
            aimTrajectory.SetPath(null, 0);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            ClearAim();
    }

    private void OnDisable()
    {
        ClearAim();
        if (roundTransition != null)
        {
            StopCoroutine(roundTransition);
            roundTransition = null;
        }
        if (playCamera != null && originalCameraSize > 0f)
            playCamera.orthographicSize = originalCameraSize;
    }

}
