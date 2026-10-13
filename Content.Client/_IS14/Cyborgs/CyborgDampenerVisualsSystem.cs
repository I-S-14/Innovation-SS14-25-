// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Linq;
using System.Numerics;
using Content.Shared._IS14.Cyborgs.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Cyborgs;

/// <summary>
/// Draws the hyperkinetic dampening field around a cyborg and the shimmer on projectiles it slowed,
/// like tgstation's dampener visuals: the border fades in and out over 1.5 seconds while wobbling,
/// and the segment a projectile passes through shudders.
/// </summary>
public sealed class CyborgDampenerVisualsSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string FieldEffect = "CyborgDampenerFieldEffect";
    private static readonly ResPath FieldRsi = new("/Textures/_IS14/Effects/dampener_field.rsi");
    private const string ShimmerState = "projectile_dampen_effect";
    private const string ShimmerLayer = "cyborg-dampened";

    // tg's ANIMATE_DAMPENER_TIME, the wobble on a hit and the time the wobble takes to settle.
    private const float FadeTime = 1.5f;
    private const float HitWobbleTime = 0.15f;
    private const float SettleTime = 0.25f;

    // Wobble strength in tiles and its speed in radians per second.
    private const float WobbleAmplitude = 1.5f / 32f;
    private const float WobbleSpeed = 14f;
    private const float WobbleScale = 0.06f;

    // Dampener tool -> its client side field effect.
    private readonly Dictionary<EntityUid, FieldVisuals> _fields = new();

    // Fields of switched off dampeners that are still fading out.
    private readonly List<FieldVisuals> _closing = new();

    private float _time;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgProjectileDampenerComponent, AfterAutoHandleStateEvent>(OnDampenerState);
        SubscribeLocalEvent<CyborgProjectileDampenerComponent, ComponentShutdown>(OnDampenerShutdown);
        SubscribeLocalEvent<CyborgDampenedProjectileComponent, ComponentStartup>(OnDampenedStartup);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        foreach (var field in _fields.Values.Concat(_closing))
        {
            if (!TerminatingOrDeleted(field.Uid))
                QueueDel(field.Uid);
        }

        _fields.Clear();
        _closing.Clear();
    }

    private void OnDampenerState(Entity<CyborgProjectileDampenerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var cyborg = CompOrNull<CyborgToolComponent>(ent)?.Cyborg;
        if (ent.Comp.Active && cyborg != null && Exists(cyborg))
        {
            if (!_fields.ContainsKey(ent.Owner))
                _fields[ent.Owner] = CreateField(cyborg.Value);
            return;
        }

        CloseField(ent.Owner);
    }

    private void OnDampenerShutdown(Entity<CyborgProjectileDampenerComponent> ent, ref ComponentShutdown args)
    {
        CloseField(ent.Owner);
    }

    private FieldVisuals CreateField(EntityUid cyborg)
    {
        var uid = Spawn(FieldEffect, new EntityCoordinates(cyborg, Vector2.Zero));
        var sprite = Comp<SpriteComponent>(uid);

        var offsets = sprite.AllLayers.Select(layer => ((SpriteComponent.Layer) layer).Offset).ToArray();
        var field = new FieldVisuals(uid, offsets)
        {
            Phases = offsets.Select(_ => _random.NextFloat(0f, MathF.Tau)).ToArray(),
            HitWobble = new float[offsets.Length],
        };

        _sprite.SetColor((uid, sprite), Color.White.WithAlpha(0f));
        return field;
    }

    private void CloseField(EntityUid tool)
    {
        if (!_fields.Remove(tool, out var field))
            return;

        field.Closing = true;
        _closing.Add(field);
    }

    private void OnDampenedStartup(Entity<CyborgDampenedProjectileComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite) &&
            !_sprite.LayerMapTryGet((ent.Owner, sprite), ShimmerLayer, out _, false))
        {
            var index = _sprite.AddLayer((ent.Owner, sprite), new SpriteSpecifier.Rsi(FieldRsi, ShimmerState));
            _sprite.LayerMapSet((ent.Owner, sprite), ShimmerLayer, index);
            sprite.LayerSetShader(index, "unshaded");
        }

        WobbleNearestSegment(ent.Owner);
    }

    /// <summary>
    /// tg's determine_wobble: the border segment where the projectile came in shudders briefly.
    /// </summary>
    private void WobbleNearestSegment(EntityUid projectile)
    {
        var position = _transform.GetWorldPosition(projectile);

        foreach (var field in _fields.Values)
        {
            if (TerminatingOrDeleted(field.Uid))
                continue;

            var local = position - _transform.GetWorldPosition(field.Uid);
            var best = -1;
            var bestDistance = 1.5f;
            for (var i = 0; i < field.Offsets.Length; i++)
            {
                var distance = (field.Offsets[i] - local).Length();
                if (distance >= bestDistance)
                    continue;

                best = i;
                bestDistance = distance;
            }

            if (best >= 0)
                field.HitWobble[best] = HitWobbleTime + SettleTime;
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _time += frameTime;

        foreach (var field in _fields.Values)
        {
            Animate(field, frameTime);
        }

        for (var i = _closing.Count - 1; i >= 0; i--)
        {
            var field = _closing[i];
            if (Animate(field, frameTime))
                continue;

            // The field goes with the cyborg if that got deleted first.
            if (!TerminatingOrDeleted(field.Uid))
                QueueDel(field.Uid);
            _closing.RemoveAt(i);
        }
    }

    /// <summary>
    /// Advances the fade and the wobble. False once a closing field has fully faded out.
    /// </summary>
    private bool Animate(FieldVisuals field, float frameTime)
    {
        if (!TryComp<SpriteComponent>(field.Uid, out var sprite))
            return false;

        field.Progress = Math.Clamp(field.Progress + (field.Closing ? -frameTime : frameTime) / FadeTime, 0f, 1f);
        if (field.Closing && field.Progress <= 0f)
            return false;

        var ent = (field.Uid, sprite);
        _sprite.SetColor(ent, Color.White.WithAlpha(field.Progress));

        // The whole border wobbles while it fades, then settles down.
        var fading = field.Closing || field.Progress < 1f;
        field.Settle = fading ? SettleTime : Math.Max(0f, field.Settle - frameTime);
        var fieldWobble = fading ? 1f : field.Settle / SettleTime;

        for (var i = 0; i < field.Offsets.Length; i++)
        {
            field.HitWobble[i] = Math.Max(0f, field.HitWobble[i] - frameTime);
            var hitWobble = Math.Min(1f, field.HitWobble[i] / SettleTime);
            var strength = Math.Max(fieldWobble, hitWobble);

            if (strength <= 0f)
            {
                _sprite.LayerSetOffset(ent, i, field.Offsets[i]);
                _sprite.LayerSetScale(ent, i, Vector2.One);
                continue;
            }

            var phase = _time * WobbleSpeed + field.Phases[i];
            var shake = new Vector2(MathF.Sin(phase), MathF.Cos(phase * 1.3f)) * WobbleAmplitude * strength;
            var pulse = 1f + MathF.Sin(phase * 0.7f) * WobbleScale * strength;
            _sprite.LayerSetOffset(ent, i, field.Offsets[i] + shake);
            _sprite.LayerSetScale(ent, i, new Vector2(pulse, 2f - pulse));
        }

        return true;
    }

    private sealed class FieldVisuals(EntityUid uid, Vector2[] offsets)
    {
        public readonly EntityUid Uid = uid;

        /// <summary>
        /// Resting offsets of the border segments.
        /// </summary>
        public readonly Vector2[] Offsets = offsets;

        public float[] Phases = [];

        /// <summary>
        /// Seconds of wobble left on each segment from projectiles passing through.
        /// </summary>
        public float[] HitWobble = [];

        /// <summary>
        /// 0 is invisible, 1 is fully faded in.
        /// </summary>
        public float Progress;

        public float Settle;

        public bool Closing;
    }
}
