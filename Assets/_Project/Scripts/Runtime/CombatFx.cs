using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace RollOfLegends
{
    /// <summary>
    /// Battle effects made in code (no effect files needed): sword slashes, a thrown spear, fireballs,
    /// earthquakes, level-up light, hit flashes and screen shake. Most effects are coroutines, so the
    /// game can wait for one ("yield return fx.Fireball(...)") or let it run alongside (StartCoroutine).
    /// Particles and glowing lines use one soft "glow" material; their color comes from the effect.
    /// </summary>
    public class CombatFx : MonoBehaviour
    {
        public static readonly Color SlashColor = new Color(0.55f, 0.9f, 1f);
        public static readonly Color ClawColor = new Color(1f, 0.3f, 0.25f);
        public static readonly Color FireColor = new Color(1f, 0.48f, 0.1f);
        public static readonly Color MagicColor = new Color(0.72f, 0.42f, 1f);
        public static readonly Color LevelColor = new Color(1f, 0.78f, 0.1f);
        public static readonly Color HasteColor = new Color(0.35f, 0.95f, 1f);
        public static readonly Color HealColor = new Color(0.4f, 1f, 0.5f);
        public static readonly Color DustColor = new Color(0.55f, 0.42f, 0.28f);
        public static readonly Color SmokeColor = new Color(0.22f, 0.2f, 0.2f, 0.8f);

        private static readonly Color HurtTint = new Color(1f, 0.25f, 0.22f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Soft glowing particle material (uses vertex colors). Used for sparks, rings, trails and slashes.")]
        [SerializeField] private Material _glowMaterial;

        [Tooltip("Plain unlit material for solid glowing shapes such as the fireball's core.")]
        [SerializeField] private Material _solidMaterial;

        [Tooltip("The spear thrown at castles (its tip must point along +Z).")]
        [SerializeField] private GameObject _spearPrefab;

        [Tooltip("The sword thrown at heroes (it spins end over end while it flies).")]
        [SerializeField] private GameObject _swordPrefab;

        [Tooltip("1 = normal screen shake, 0 = no screen shake.")]
        [SerializeField] private float _shakeStrength = 1f;

        private Material _lineMaterial;
        private float _shakeAmount;
        private float _shakeTimeLeft;
        private float _shakeDuration;
        private Vector3 _cameraOffset;
        private readonly System.Collections.Generic.HashSet<Renderer> _tinted = new System.Collections.Generic.HashSet<Renderer>();

        // ---------- Combat ----------

        /// <summary>A glowing sword swipe across a point, as seen by the camera. Tilt turns the swipe direction.</summary>
        public IEnumerator Slash(Vector3 center, Color color, float tilt = 0f, float radius = 0.95f, float duration = 0.34f)
        {
            Transform view = Camera.main.transform;
            Vector3 right = view.right;
            Vector3 up = view.up;
            center -= view.forward * 0.7f; // in front of the target, so the model doesn't hide it

            // The blade sweeps along a circle from upper-left to lower-right; its middle passes over "center".
            float from = 150f + tilt;
            float to = -10f + tilt;
            Vector3 middle = Direction(right, up, (from + to) / 2f) * radius;
            Vector3 circleCenter = center - middle * 0.55f;

            LineRenderer glow = NewLine("Slash", 20, loop: false);
            LineRenderer core = NewLine("SlashCore", 20, loop: false);
            var shape = new AnimationCurve(new Keyframe(0f, 0.05f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0.25f));
            glow.widthCurve = shape;
            core.widthCurve = shape;
            glow.widthMultiplier = 0.6f;
            core.widthMultiplier = 0.16f;

            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float head = EaseOut(Mathf.Clamp01(t / 0.45f));
                float tail = EaseIn(Mathf.Clamp01((t - 0.2f) / 0.8f));
                for (int i = 0; i < glow.positionCount; i++)
                {
                    float angle = Mathf.Lerp(from, to, Mathf.Lerp(tail, head, i / (glow.positionCount - 1f)));
                    Vector3 point = circleCenter + Direction(right, up, angle) * radius;
                    glow.SetPosition(i, point);
                    core.SetPosition(i, point);
                }
                float alpha = t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
                SetLineColor(glow, color, alpha);
                SetLineColor(core, Color.white, alpha);
                yield return null;
            }
            Destroy(glow.gameObject);
            Destroy(core.gameObject);
        }

        /// <summary>Three short red scratches: a monster's claws.</summary>
        public IEnumerator Claw(Vector3 center)
        {
            Vector3 side = Camera.main.transform.right * 0.18f;
            StartCoroutine(Slash(center - side, ClawColor, tilt: 20f, radius: 0.55f, duration: 0.28f));
            StartCoroutine(Slash(center, ClawColor, tilt: 20f, radius: 0.6f, duration: 0.28f));
            yield return Slash(center + side, ClawColor, tilt: 20f, radius: 0.55f, duration: 0.28f);
        }

        /// <summary>
        /// Makes a hero or monster react to a hit: it flashes white then red, shakes, plays its hit
        /// animation, sparks fly and the screen shakes. <paramref name="strength"/> 1 = a normal hit.
        /// </summary>
        public IEnumerator Hit(Transform target, float strength = 1f)
        {
            if (target == null) yield break;
            Animate(target, "Hit_A", "HitReact", "HitRecieve", "Hit_B");
            Sparks(TowardCamera(target.position + Vector3.up * 0.2f, 0.5f), new Color(1f, 0.85f, 0.4f), count: Mathf.RoundToInt(24 * strength),
                speed: 5f * strength, size: 0.2f, lifetime: 0.4f, gravity: 1.2f, streaks: true);
            ShakeCamera(0.08f * strength, 0.25f);
            StartCoroutine(Shake(target, 0.09f * strength, 0.3f));
            yield return Flash(target, HurtTint, 0.35f);
        }

        /// <summary>Tints every model part: a bright white flash first, then <paramref name="tint"/> fading back to normal.</summary>
        public IEnumerator Flash(Transform target, Color tint, float duration = 0.35f)
        {
            if (target == null) yield break;
            Renderer[] parts = target.GetComponentsInChildren<Renderer>();
            var block = new MaterialPropertyBlock();
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                Color color = t < 0.18f ? new Color(3f, 3f, 3f) : Color.Lerp(tint, Color.white, (t - 0.18f) / 0.82f);
                Tint(parts, block, color);
                yield return null;
            }
            foreach (Renderer part in parts)
            {
                if (part == null) continue;
                for (int i = 0; i < part.sharedMaterials.Length; i++) part.SetPropertyBlock(null, i);
                _tinted.Remove(part);
            }
        }

        /// <summary>A quick step toward a point and back (a lunge for a melee attack).</summary>
        public IEnumerator Lunge(Transform mover, Vector3 toward, float distance = 0.35f, float duration = 0.45f)
        {
            Vector3 direction = toward - mover.position;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
            yield return Offset(mover, duration, t =>
            {
                float amount = t < 0.3f ? EaseOut(t / 0.3f) : 1f - EaseInOut((t - 0.3f) / 0.7f);
                return direction * distance * amount;
            });
        }

        /// <summary>Wiggles an object around its spot, getting calmer over time.</summary>
        public IEnumerator Shake(Transform target, float amount, float duration)
        {
            yield return Offset(target, duration, t =>
            {
                Vector3 random = Random.insideUnitSphere * amount * (1f - t);
                random.y *= 0.3f;
                return random;
            });
        }

        /// <summary>Shakes the camera (runs alongside the game; stronger shakes win).</summary>
        public void ShakeCamera(float amount, float duration)
        {
            amount *= _shakeStrength;
            if (amount < _shakeAmount * (_shakeTimeLeft / Mathf.Max(0.01f, _shakeDuration))) return;
            _shakeAmount = amount;
            _shakeDuration = _shakeTimeLeft = duration;
        }

        // ---------- Projectiles ----------

        /// <summary>A spear flies in an arc and sticks into the target (e.g. a castle), which shakes.</summary>
        public IEnumerator SpearThrow(Vector3 from, Vector3 to, Transform target)
        {
            GameObject spear = _spearPrefab != null ? Instantiate(_spearPrefab, transform) : FallbackSpear();
            spear.transform.position = from;
            TrailRenderer trail = AddTrail(spear, new Color(1f, 0.95f, 0.8f), width: 0.18f, time: 0.22f);

            // Longer throws fly higher and take a little longer.
            float distance = Vector3.Distance(from, to);
            yield return Fly(spear.transform, from, to, arcHeight: 1f + distance * 0.18f,
                duration: Mathf.Clamp(0.3f + distance * 0.04f, 0.4f, 0.85f), faceForward: true);

            trail.emitting = false;
            Vector3 front = TowardCamera(to, 0.6f);
            Sparks(front, new Color(1f, 0.8f, 0.35f), count: 30, speed: 5f, size: 0.2f, lifetime: 0.45f, gravity: 1.5f, streaks: true);
            Smoke(front, 8, 0.6f, DustColor);
            ShakeCamera(0.12f, 0.3f);
            if (target != null)
            {
                StartCoroutine(Shake(target, 0.1f, 0.35f));
                StartCoroutine(Flash(target, HurtTint, 0.3f));
            }

            // The spear stays stuck for a moment, then shrinks away.
            yield return new WaitForSeconds(0.25f);
            StartCoroutine(ShrinkAndDestroy(spear.transform, 0.35f));
        }

        /// <summary>A glowing sword spins through the air to the target; sparks fly where it hits.</summary>
        public IEnumerator SwordThrow(Vector3 from, Vector3 to)
        {
            GameObject sword = _swordPrefab != null ? Instantiate(_swordPrefab, transform) : FallbackSpear();
            sword.transform.position = from;
            TrailRenderer trail = AddTrail(sword, SlashColor, width: 0.3f, time: 0.2f);

            float distance = Vector3.Distance(from, to);
            Transform body = sword.transform;
            yield return Fly(body, from, to, arcHeight: 0.6f + distance * 0.12f,
                duration: Mathf.Clamp(0.25f + distance * 0.04f, 0.35f, 0.8f), faceForward: true,
                spin: t => body.rotation *= Quaternion.Euler(t * 1440f, 0f, 0f)); // end over end

            trail.emitting = false;
            Sparks(TowardCamera(to, 0.6f), SlashColor, count: 26, speed: 5f, size: 0.2f, lifetime: 0.4f, gravity: 1.2f, streaks: true);
            ShakeCamera(0.1f, 0.25f);
            StartCoroutine(ShrinkAndDestroy(body, 0.2f));
        }

        /// <summary>A flaming ball flies from one point to another and explodes.</summary>
        public IEnumerator Fireball(Vector3 from, Vector3 to)
        {
            var ball = new GameObject("Fireball");
            ball.transform.SetParent(transform, false);
            ball.transform.position = from;

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(core.GetComponent<Collider>());
            core.transform.SetParent(ball.transform, false);
            core.transform.localScale = Vector3.one * 0.3f;
            var coreRenderer = core.GetComponent<Renderer>();
            coreRenderer.sharedMaterial = _solidMaterial;
            coreRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, new Color(1f, 0.85f, 0.4f)); // hot yellow middle; the flames around it are orange
            coreRenderer.SetPropertyBlock(block);

            ParticleSystem flames = Flames(ball.transform);
            TrailRenderer trail = AddTrail(ball, FireColor, width: 0.4f, time: 0.25f);

            yield return Fly(ball.transform, from, to, arcHeight: 1.3f, duration: 0.6f, faceForward: false,
                spin: t => core.transform.localScale = Vector3.one * (0.3f + Mathf.Sin(t * 40f) * 0.04f));

            // Let the flames and trail fade on their own while the ball itself is gone.
            flames.transform.SetParent(transform, true);
            flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(flames.gameObject, 1f);
            trail.emitting = false;
            Destroy(core);
            Destroy(ball, trail.time + 0.1f);

            Explosion(TowardCamera(to, 0.6f), FireColor);
        }

        /// <summary>A fiery explosion: a bright flash, sparks, embers, smoke and a ring on the ground.</summary>
        public void Explosion(Vector3 at, Color color, float scale = 1f)
        {
            ParticleSystem flash = Particles("Flash", at, 1f);
            ParticleSystem.MainModule main = flash.main;
            main.startLifetime = 0.22f;
            main.startSpeed = 0f;
            main.startSize = 2.2f * scale;
            main.startColor = new Color(1f, 0.9f, 0.55f, 0.9f);
            Burst(flash, 1);
            SizeOverLifetime(flash, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f));
            flash.Play();

            Sparks(at, new Color(1f, 0.8f, 0.3f), count: Mathf.RoundToInt(36 * scale), speed: 7f * scale, size: 0.22f,
                lifetime: 0.5f, gravity: 1.2f, streaks: true);
            Sparks(at, color, count: Mathf.RoundToInt(30 * scale), speed: 3f * scale, size: 0.4f, lifetime: 0.7f, gravity: 0.3f);
            Sparks(at, new Color(0.85f, 0.15f, 0.05f), count: Mathf.RoundToInt(16 * scale), speed: 2f * scale, size: 0.55f, lifetime: 0.6f, gravity: -0.2f);
            Smoke(at, Mathf.RoundToInt(12 * scale), 1.2f * scale);
            StartCoroutine(Ring(new Vector3(at.x, GroundY(at), at.z), color, 0.2f, 1.8f * scale, 0.45f, 0.25f));
            ShakeCamera(0.16f * scale, 0.35f);
        }

        // ---------- Magic ----------

        /// <summary>A magic circle at someone's feet with sparkles rising: someone casts a spell.</summary>
        public IEnumerator CastCircle(Vector3 feet, Color color)
        {
            StartCoroutine(Ring(feet + Vector3.up * 0.05f, color, 0.15f, 0.8f, 0.5f, 0.12f));
            StartCoroutine(Ring(feet + Vector3.up * 0.05f, Color.white, 0.1f, 0.55f, 0.45f, 0.06f));
            Rising(feet, color, count: 30, radius: 0.5f, emitSeconds: 0.35f);
            yield return new WaitForSeconds(0.35f);
        }

        /// <summary>The ground shakes under a castle: magic rune, rumbling, dust rings, flying rocks and a dust cloud.</summary>
        public IEnumerator Earthquake(Transform castle)
        {
            Vector3 at = castle.position;
            StartCoroutine(Ring(at + Vector3.up * 0.05f, MagicColor, 2f, 0.4f, 0.4f, 0.16f)); // the spell lands
            yield return new WaitForSeconds(0.35f);

            ShakeCamera(0.28f, 1.1f);
            StartCoroutine(Shake(castle, 0.16f, 1.1f));
            StartCoroutine(Flash(castle, new Color(1f, 0.55f, 0.4f), 1f));
            for (int wave = 0; wave < 3; wave++)
            {
                StartCoroutine(Ring(at + Vector3.up * 0.05f, DustColor, 0.4f, 2.6f, 0.7f, 0.35f));
                Sparks(at + Vector3.up * 0.2f, new Color(0.45f, 0.35f, 0.25f), count: 14, speed: 4.5f, size: 0.2f,
                    lifetime: 0.9f, gravity: 2.2f, upward: true, radius: 0.7f);
                Smoke(at, 6, 1.2f, DustColor);
                yield return new WaitForSeconds(0.28f);
            }
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>A golden pillar of light, a ring and rising sparkles: someone levelled up.</summary>
        public IEnumerator LevelUp(Transform hero, float feetBelow)
        {
            Vector3 feet = hero.position - Vector3.up * feetBelow;
            StartCoroutine(Beam(feet, LevelColor, 1f));
            StartCoroutine(Ring(feet + Vector3.up * 0.05f, LevelColor, 0.2f, 1.3f, 0.6f, 0.24f));
            StartCoroutine(Ring(feet + Vector3.up * 0.05f, Color.white, 0.1f, 0.9f, 0.5f, 0.1f));
            Rising(feet, LevelColor, count: 50, radius: 0.5f, emitSeconds: 0.6f, speed: 3f);
            Sparks(TowardCamera(hero.position + Vector3.up * 0.5f, 0.5f), new Color(1f, 0.95f, 0.6f), count: 20, speed: 3.5f, size: 0.18f,
                lifetime: 0.7f, gravity: -0.2f);
            yield return Pop(hero, 0.18f, 0.4f);
            yield return new WaitForSeconds(0.25f);
        }

        /// <summary>A soft glow ring and a few rising sparkles in one color: a pickup, healing, haste...</summary>
        public IEnumerator Aura(Transform hero, float feetBelow, Color color)
        {
            Vector3 feet = hero.position - Vector3.up * feetBelow;
            StartCoroutine(Ring(feet + Vector3.up * 0.05f, color, 0.15f, 0.9f, 0.45f, 0.18f));
            Rising(feet, color, count: 24, radius: 0.45f, emitSeconds: 0.3f);
            yield return Pop(hero, 0.1f, 0.3f);
        }

        /// <summary>A puff of smoke with a few stars (knock-outs, defeated monsters).</summary>
        public void Poof(Vector3 at)
        {
            at = TowardCamera(at, 0.4f);
            Smoke(at, 16, 1.1f, new Color(0.8f, 0.8f, 0.85f, 0.85f));
            Sparks(at, new Color(1f, 0.95f, 0.6f), count: 16, speed: 3f, size: 0.18f, lifetime: 0.5f, gravity: 0.3f);
        }

        // ---------- Animation helpers ----------

        /// <summary>Plays the first animation the model has from the given names. Returns its length in seconds (0 = none).</summary>
        public static float Animate(Transform model, params string[] clips)
        {
            return Animate(model, 1f, clips);
        }

        public static float Animate(Transform model, float speed, params string[] clips)
        {
            if (model == null) return 0f;
            ModelAnimator animator = model.GetComponentInChildren<ModelAnimator>();
            return animator != null ? animator.Play(speed, clips) : 0f;
        }

        public static void StopAnimation(Transform model)
        {
            ModelAnimator animator = model != null ? model.GetComponentInChildren<ModelAnimator>() : null;
            if (animator != null) animator.Stop();
        }

        /// <summary>Runs an effect alongside the game (it keeps going even if the caller moves on).</summary>
        public void Run(IEnumerator effect)
        {
            StartCoroutine(effect);
        }

        /// <summary>Turns a hero (around its vertical axis) so its model faces a point.</summary>
        public IEnumerator Face(Transform hero, Vector3 point, float duration = 0.12f)
        {
            ModelAnimator model = hero.GetComponentInChildren<ModelAnimator>();
            Vector3 facing = Quaternion.Inverse(hero.rotation) * (model != null ? model.transform.forward : hero.forward);
            Vector3 wanted = point - hero.position;
            facing.y = 0f;
            wanted.y = 0f;
            if (wanted.sqrMagnitude < 0.0001f) yield break;
            float yaw = Vector3.SignedAngle(facing, wanted, Vector3.up);
            yield return Turn(hero, Quaternion.Euler(0f, yaw, 0f), duration);
        }

        /// <summary>Turns a hero back to its normal pose (facing the camera).</summary>
        public IEnumerator FaceFront(Transform hero, float duration = 0.15f)
        {
            yield return Turn(hero, Quaternion.identity, duration);
        }

        /// <summary>Stops all running effects and removes their leftovers (e.g. when a new match starts).</summary>
        public void Clear()
        {
            StopAllCoroutines();
            foreach (Transform child in transform) Destroy(child.gameObject);
            _shakeTimeLeft = 0f;
            foreach (Renderer part in _tinted)
            {
                if (part == null) continue;
                for (int i = 0; i < part.sharedMaterials.Length; i++) part.SetPropertyBlock(null, i);
            }
            _tinted.Clear();
        }

        // ---------- Building blocks ----------

        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            camera.transform.position -= _cameraOffset;
            _cameraOffset = Vector3.zero;
            if (_shakeTimeLeft > 0f)
            {
                _shakeTimeLeft -= Time.deltaTime;
                float fade = Mathf.Clamp01(_shakeTimeLeft / _shakeDuration);
                Vector2 random = Random.insideUnitCircle * _shakeAmount * fade;
                _cameraOffset = camera.transform.right * random.x + camera.transform.up * random.y;
                camera.transform.position += _cameraOffset;
            }
        }

        /// <summary>A flat glowing circle on the ground that grows (or shrinks) and fades.</summary>
        private IEnumerator Ring(Vector3 center, Color color, float fromRadius, float toRadius, float duration, float width)
        {
            LineRenderer ring = NewLine("Ring", 48, loop: true);
            ring.alignment = LineAlignment.TransformZ;
            ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float radius = Mathf.Lerp(fromRadius, toRadius, EaseOut(t));
                for (int i = 0; i < ring.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / ring.positionCount;
                    ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
                }
                ring.widthMultiplier = width * (1f - t * 0.6f);
                SetLineColor(ring, color, 1f - t * t);
                yield return null;
            }
            Destroy(ring.gameObject);
        }

        /// <summary>A vertical column of light that flares up and fades.</summary>
        private IEnumerator Beam(Vector3 bottom, Color color, float duration)
        {
            LineRenderer beam = NewLine("Beam", 2, loop: false);
            beam.SetPosition(0, bottom);
            beam.SetPosition(1, bottom + Vector3.up * 3.2f);
            beam.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.45f));
            var gradient = new Gradient();
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float strength = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
                beam.widthMultiplier = 1.5f * strength;
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.35f), 0f), new GradientColorKey(color, 1f) },
                    new[] { new GradientAlphaKey(strength, 0f), new GradientAlphaKey(strength * 0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
                beam.colorGradient = gradient;
                yield return null;
            }
            Destroy(beam.gameObject);
        }

        /// <summary>Flies along an arc from one point to another.</summary>
        private static IEnumerator Fly(Transform body, Vector3 from, Vector3 to, float arcHeight, float duration, bool faceForward,
            Action<float> spin = null)
        {
            Vector3 previous = from;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                Vector3 position = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * arcHeight;
                body.position = position;
                if (faceForward && (position - previous).sqrMagnitude > 0.00001f)
                {
                    body.rotation = Quaternion.LookRotation(position - previous);
                }
                spin?.Invoke(t);
                previous = position;
                yield return null;
            }
            body.position = to;
        }

        /// <summary>A quick grow-and-back, so a hero looks like it "powers up".</summary>
        private static IEnumerator Pop(Transform target, float amount, float duration)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                if (target == null) yield break;
                target.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * amount);
                yield return null;
            }
            if (target != null) target.localScale = Vector3.one;
        }

        private static IEnumerator Turn(Transform target, Quaternion rotation, float duration)
        {
            Quaternion start = target.rotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                target.rotation = Quaternion.Slerp(start, rotation, EaseOut(t));
                yield return null;
            }
            target.rotation = rotation;
        }

        /// <summary>
        /// Moves an object by an extra offset over time and removes it at the end. Because only the
        /// offset is added, other movement (or another shake) can run at the same time.
        /// </summary>
        private static IEnumerator Offset(Transform target, float duration, Func<float, Vector3> offsetAt)
        {
            Vector3 applied = Vector3.zero;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                if (target == null) yield break;
                Vector3 offset = offsetAt(t);
                target.position += offset - applied;
                applied = offset;
                yield return null;
            }
            if (target != null) target.position -= applied;
        }

        private static IEnumerator ShrinkAndDestroy(Transform target, float duration)
        {
            Vector3 start = target.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                if (target == null) yield break;
                target.localScale = start * (1f - t);
                yield return null;
            }
            if (target != null) Destroy(target.gameObject);
        }

        private void Sparks(Vector3 at, Color color, int count, float speed, float size, float lifetime, float gravity,
            bool streaks = false, bool upward = false, float radius = 0.15f)
        {
            ParticleSystem sparks = Particles("Sparks", at, lifetime + 0.5f);
            ParticleSystem.MainModule main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.gravityModifier = gravity;
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.6f));
            Burst(sparks, count);

            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = upward ? ParticleSystemShapeType.Hemisphere : ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            if (upward) shape.rotation = new Vector3(-90f, 0f, 0f);

            SizeOverLifetime(sparks, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            if (streaks)
            {
                var renderer = sparks.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.06f;
                renderer.lengthScale = 1.5f;
            }
            sparks.Play();
        }

        private void Smoke(Vector3 at, int count, float size, Color? color = null)
        {
            ParticleSystem smoke = Particles("Smoke", at, 2f);
            ParticleSystem.MainModule main = smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.12f; // drifts up
            main.startColor = color ?? SmokeColor;
            Burst(smoke, count);

            ParticleSystem.ShapeModule shape = smoke.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystem.LimitVelocityOverLifetimeModule drag = smoke.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.5f;
            SizeOverLifetime(smoke, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.3f));
            smoke.Play();
        }

        /// <summary>Sparkles rising from a circle on the ground.</summary>
        private void Rising(Vector3 feet, Color color, int count, float radius, float emitSeconds, float speed = 2f)
        {
            ParticleSystem rising = Particles("Rising", feet, emitSeconds + 1.5f);
            ParticleSystem.MainModule main = rising.main;
            main.duration = emitSeconds;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
            ParticleSystem.EmissionModule emission = rising.emission;
            emission.rateOverTime = count / emitSeconds;

            ParticleSystem.ShapeModule shape = rising.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(90f, 0f, 0f); // flat on the ground

            ParticleSystem.VelocityOverLifetimeModule velocity = rising.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            SizeOverLifetime(rising, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            rising.Play();
        }

        /// <summary>Flames that trail behind a moving object (left behind in the world as it flies).</summary>
        private ParticleSystem Flames(Transform parent)
        {
            ParticleSystem flames = Particles("Flames", parent.position, float.PositiveInfinity);
            flames.transform.SetParent(parent, true);
            ParticleSystem.MainModule main = flames.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(FireColor, new Color(0.95f, 0.2f, 0.05f));
            ParticleSystem.EmissionModule emission = flames.emission;
            emission.rateOverTime = 90f;
            ParticleSystem.ShapeModule shape = flames.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            SizeOverLifetime(flames, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            flames.Play();
            return flames;
        }

        /// <summary>A stopped, empty particle system in world space, ready to be set up. Removed after <paramref name="lifetime"/>.</summary>
        private ParticleSystem Particles(string name, Vector3 at, float lifetime)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(transform, false);
            gameObject.transform.position = at;
            var particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;

            // Everything fades out at the end of its life.
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = gameObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _glowMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (!float.IsInfinity(lifetime)) Destroy(gameObject, lifetime);
            return particles;
        }

        private static void Burst(ParticleSystem particles, int count)
        {
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, count)) });
        }

        private static void SizeOverLifetime(ParticleSystem particles, AnimationCurve curve)
        {
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private LineRenderer NewLine(string name, int points, bool loop)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = LineMaterial;
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = points;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            for (int i = 0; i < points; i++) line.SetPosition(i, transform.position);
            return line;
        }

        private TrailRenderer AddTrail(GameObject body, Color color, float width, float time)
        {
            var trail = body.AddComponent<TrailRenderer>();
            trail.sharedMaterial = LineMaterial;
            trail.time = time;
            trail.widthMultiplier = width;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.5f), 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            return trail;
        }

        private static void SetLineColor(LineRenderer line, Color color, float alpha)
        {
            color.a *= Mathf.Clamp01(alpha);
            line.startColor = color;
            line.endColor = color;
        }

        /// <summary>The glow material with a soft-edged stripe texture, so lines look like glowing ribbons.</summary>
        private Material LineMaterial
        {
            get
            {
                if (_lineMaterial != null) return _lineMaterial;
                const int height = 32;
                var texture = new Texture2D(4, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < height; y++)
                {
                    float across = Mathf.Abs((y + 0.5f) / height * 2f - 1f); // 0 in the middle, 1 at the edges
                    float alpha = Mathf.Pow(1f - across, 1.8f);
                    for (int x = 0; x < 4; x++) texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                texture.Apply(false);
                _lineMaterial = new Material(_glowMaterial) { name = "FxLine" };
                _lineMaterial.SetTexture("_BaseMap", texture);
                return _lineMaterial;
            }
        }

        private GameObject FallbackSpear()
        {
            GameObject spear = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(spear.GetComponent<Collider>());
            spear.transform.SetParent(transform, false);
            spear.transform.localScale = new Vector3(0.06f, 0.5f, 0.06f);
            return spear;
        }

        private void Tint(Renderer[] parts, MaterialPropertyBlock block, Color tint)
        {
            foreach (Renderer part in parts)
            {
                if (part == null || part is ParticleSystemRenderer || part is LineRenderer || part is TrailRenderer) continue;
                _tinted.Add(part);
                Material[] materials = part.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !materials[i].HasProperty(BaseColorId)) continue;
                    Color original = materials[i].GetColor(BaseColorId);
                    part.GetPropertyBlock(block, i);
                    block.SetColor(BaseColorId, new Color(original.r * tint.r, original.g * tint.g, original.b * tint.b, original.a));
                    part.SetPropertyBlock(block, i);
                }
            }
        }

        /// <summary>A point moved toward the camera, so an effect is drawn in front of the model it hits.</summary>
        private static Vector3 TowardCamera(Vector3 point, float distance)
        {
            Camera camera = Camera.main;
            return camera != null ? point - camera.transform.forward * distance : point;
        }

        private static float GroundY(Vector3 at)
        {
            return Mathf.Min(at.y, 0.05f);
        }

        private static Vector3 Direction(Vector3 right, Vector3 up, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return right * Mathf.Cos(radians) + up * Mathf.Sin(radians);
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseIn(float t) => t * t;
        private static float EaseInOut(float t) => t * t * (3f - 2f * t);
    }
}
